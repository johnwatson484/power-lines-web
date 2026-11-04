using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;
using PowerLinesWeb.Forecasts;
using PowerLinesWeb.Data;

namespace PowerLinesWeb.Analysis.Ratings;

// Scoped so a whole batch of fixtures shares one division query and one fit per as-of date, rather than
// re-reading and re-fitting the entire division for every match.
public class RatingsProvider(ApplicationDbContext dbContext, IOptions<ModelOptions> modelOptions, IMemoryCache cache) : IRatingsProvider
{
    readonly ApplicationDbContext dbContext = dbContext;
    readonly ModelOptions modelOptions = modelOptions.Value;
    readonly Dictionary<string, List<Result>> divisionResults = [];
    readonly Dictionary<(string Division, DateTime AsOf), TeamRatings> ratings = [];

    DateTime? requestCutoff;
    public TeamRatings Get(string division, DateTime asOf)
    {
        if (requestCutoff != asOf)
        {
            // A retried transaction gets a fresh snapshot. Never reuse a previous attempt's row cache.
            divisionResults.Clear(); ratings.Clear(); requestCutoff = asOf;
        }
        var key = (division, asOf);

        if (!ratings.TryGetValue(key, out var teamRatings))
        {
            var matches = GetMatches(division, asOf);
            var version = ForecastVersions.Hash(new { Count = matches.Count, Revision = matches.Sum(x => x.ScoreRevision), Model = modelOptions });
            teamRatings = cache.GetOrCreate((division, asOf.Date, version), entry =>
            {
                entry.SetSize(1).SetSlidingExpiration(TimeSpan.FromMinutes(30));
                return RatingsFitter.Fit(division, asOf.Date, matches, modelOptions);
            });
            ratings.Add(key, teamRatings);
        }

        return teamRatings;
    }

    private List<Result> GetMatches(string division, DateTime asOf)
    {
        var startDate = asOf.Date.AddYears(-modelOptions.YearsToAnalyse);

        // Strictly before the as-of date, otherwise a backtested result is trained on its own outcome.
        return GetDivisionResults(division)
            .Where(x => x.Date >= startDate && x.Date < asOf.Date && x.Created <= asOf && (!x.Updated.HasValue || x.Updated <= asOf))
            .ToList();
    }

    private List<Result> GetDivisionResults(string division)
    {
        if (!divisionResults.TryGetValue(division, out var results))
        {
            results = dbContext.Results.AsNoTracking()
                .Where(x => x.Division == division)
                .OrderBy(x => x.Date)
                .ToList();

            divisionResults.Add(division, results);
        }

        return results;
    }
}
