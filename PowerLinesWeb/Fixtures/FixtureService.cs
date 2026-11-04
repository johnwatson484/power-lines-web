using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PowerLinesWeb.Analysis;
using PowerLinesWeb.Data;
using PowerLinesWeb.Models;

namespace PowerLinesWeb.Fixtures;

public class FixtureService(ApplicationDbContext dbContext, TimeProvider clock, IOptions<ThresholdOptions> thresholds, IOptions<FeedOptions> feeds) : IFixtureService
{
    readonly ApplicationDbContext dbContext = dbContext;
    readonly TimeProvider clock = clock;
    readonly ThresholdOptions thresholds = thresholds.Value;
    readonly FeedOptions feeds = feeds.Value;

    public List<Models.Fixture> Get() => GetPage().Fixtures;

    public FixturePage GetPage()
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var resultDates = dbContext.Results.AsNoTracking().GroupBy(x => x.Division).Select(g => new { Division = g.Key, Date = g.Max(x => x.Date), Received = g.Max(x => x.Updated ?? x.Created) }).ToList();
        var fixtureDates = dbContext.Fixtures.AsNoTracking().GroupBy(x => x.Division).Select(g => new { Division = g.Key, Date = g.Max(x => x.Date), Received = g.Max(x => x.Updated ?? x.Created) }).ToList();
        var imports = dbContext.ImportStatuses.AsNoTracking().Where(x => x.Status == "Completed").GroupBy(x => x.Division).Select(g => new { Division = g.Key, Completed = g.Max(x => x.CompletedAt) }).ToList();
        var status = resultDates.Select(x => x.Division).Union(fixtureDates.Select(x => x.Division)).Order().Select(division =>
        {
            var lastResult = resultDates.Find(x => x.Division == division);
            var lastFixture = fixtureDates.Find(x => x.Division == division);
            var lastImport = imports.Find(x => x.Division == division);
            var stale = (lastImport != null && lastImport.Completed < now.AddHours(-feeds.MaxImportAgeHours)) ||
                (!feeds.SeasonBreakMonths.Contains(now.Month) && (lastResult == null || lastResult.Date < now.AddDays(-feeds.MaxResultAgeDaysInSeason)));
            return new DivisionFeedStatus { Division = division, LastResultDate = lastResult?.Date, LastFixtureDate = lastFixture?.Date, LastReceivedAt = new[] { lastResult?.Received, lastFixture?.Received }.Max(), LastSuccessfulImport = lastImport?.Completed,
                Status = stale ? "Feed not updated (local snapshot)" : lastFixture?.Date > now ? "Fixtures received" : feeds.SeasonBreakMonths.Contains(now.Month) ? "Season transition / no scheduled games in feed" : "No scheduled games in feed; import status " + (lastImport == null ? "unknown" : "current") };
        }).ToList();
        var fixtures = dbContext.Fixtures.AsNoTracking().Include(x => x.MatchOdds).Where(x => x.Date >= now.Date.AddDays(-1)).ToList();
        var fixtureIds = fixtures.Select(x => x.FixtureId).ToList();
        var latest = dbContext.ForecastSnapshots.AsNoTracking().Where(x => x.Origin == "LivePrematch" && x.FixtureId.HasValue && fixtureIds.Contains(x.FixtureId.Value))
            .GroupBy(x => x.FixtureId).Select(g => g.OrderByDescending(x => x.Id).First()).ToList().ToDictionary(x => x.FixtureId.Value);
        var rows = fixtures.Select(f =>
        {
            var division = new Division(f.Division); var odds = f.MatchOdds;
            var valid = odds != null && f.AnalysisStatus == "Available" && f.Date > now && f.Date.TimeOfDay != TimeSpan.Zero && f.LifecycleStatus == "Scheduled";
            var feed = status.Find(x => x.Division == f.Division);
            return new Models.Fixture
            {
                FixtureId = f.FixtureId, Country = division.Country, CountryRank = division.CountryRank, Division = division.Name, Tier = division.Tier,
                Date = f.Date, HomeTeam = f.HomeTeam, AwayTeam = f.AwayTeam, IsValid = valid,
                HomeOdds = valid ? odds.Home : null, DrawOdds = valid ? odds.Draw : null, AwayOdds = valid ? odds.Away : null,
                HomeProbability = valid ? odds.HomeProbability : null, DrawProbability = valid ? odds.DrawProbability : null, AwayProbability = valid ? odds.AwayProbability : null,
                HomeXg = valid ? odds.HomeXg : 0, AwayXg = valid ? odds.AwayXg : 0, ExpectedGoals = valid ? odds.ExpectedGoals : 0,
                Recommended = valid ? odds.Recommended : "X", LowerRecommended = valid ? odds.LowerRecommended : "X", ValueSelection = valid ? odds.ValueSelection : "X",
                ValueOdds = valid ? odds.ValueOdds : 0, ValueEdge = valid ? odds.ValueEdge : 0, ValueStake = valid ? odds.ValueStake : 0,
                Calculated = odds?.Calculated ?? default,
                ForecastOrigin = latest.GetValueOrDefault(f.FixtureId)?.Origin ?? "Legacy provenance unknown",
                Support = latest.GetValueOrDefault(f.FixtureId)?.Support ?? "Support unavailable",
                Status = f.LifecycleStatus != "Scheduled" ? f.LifecycleStatus : f.Date <= now ? "Kickoff passed" : f.Date.TimeOfDay == TimeSpan.Zero ? "Kickoff time unavailable" : f.AnalysisStatus ?? "Awaiting analysis",
                FeedStatus = feed?.Status ?? "Import status unknown", Source = f.SourceUrl ?? "Legacy source unknown", QuoteObservedAt = f.QuoteObservedAt,
                MarketHome = f.HomeOddsAverage > 1 ? f.HomeOddsAverage : null, MarketDraw = f.DrawOddsAverage > 1 ? f.DrawOddsAverage : null, MarketAway = f.AwayOddsAverage > 1 ? f.AwayOddsAverage : null,
                PriceKind = f.PriceKind ?? "Legacy average: mapping/timing unqualified"
            };
        }).OrderByDescending(x => Math.Max(x.HomeProbability ?? 0, x.AwayProbability ?? 0)).ThenBy(x => x.Date).ThenBy(x => x.FixtureId).ToList();
        return new FixturePage { Fixtures = rows, Feeds = status, Higher = thresholds.Higher, Lower = thresholds.Lower, AsOf = now };
    }
}
