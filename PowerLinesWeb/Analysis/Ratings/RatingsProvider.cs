using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PowerLinesWeb.Data;

namespace PowerLinesWeb.Analysis.Ratings;

// Scoped so a whole batch of fixtures shares one division query and one fit per as-of date, rather than
// re-reading and re-fitting the entire division for every match.
public class RatingsProvider(ApplicationDbContext dbContext, IOptions<ModelOptions> modelOptions) : IRatingsProvider
{
    readonly ApplicationDbContext dbContext = dbContext;
    readonly ModelOptions modelOptions = modelOptions.Value;
    readonly Dictionary<string, List<Result>> divisionResults = [];
    readonly Dictionary<(string Division, DateTime AsOf), TeamRatings> ratings = [];

    public TeamRatings Get(string division, DateTime asOf)
    {
        var key = (division, asOf.Date);

        if (!ratings.TryGetValue(key, out var teamRatings))
        {
            teamRatings = RatingsFitter.Fit(division, key.Item2, GetMatches(division, key.Item2), modelOptions);
            ratings.Add(key, teamRatings);
        }

        return teamRatings;
    }

    private List<Result> GetMatches(string division, DateTime asOf)
    {
        var startDate = asOf.AddYears(-modelOptions.YearsToAnalyse);

        // Strictly before the as-of date, otherwise a backtested result is trained on its own outcome.
        return GetDivisionResults(division)
            .Where(x => x.Date >= startDate && x.Date < asOf)
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
