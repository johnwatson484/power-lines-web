using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PowerLinesWeb.Analysis;
using PowerLinesWeb.Data;
using PowerLinesWeb.Models;

namespace PowerLinesWeb.Fixtures;

public class FixtureService(ApplicationDbContext dbContext, TimeProvider clock, IOptions<ThresholdOptions> thresholds, IOptions<FeedOptions> feeds, IOptions<BettingOptions> betting) : IFixtureService
{
    readonly ApplicationDbContext dbContext = dbContext;
    readonly TimeProvider clock = clock;
    readonly ThresholdOptions thresholds = thresholds.Value;
    readonly FeedOptions feeds = feeds.Value;
    readonly BettingOptions betting = betting.Value;

    public List<Models.Fixture> Get() => GetPage().Fixtures;

    public FixturePage GetPage()
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var fixtures = dbContext.Fixtures.AsNoTracking().Include(x => x.MatchOdds).Where(x => x.Date >= now.Date.AddDays(-1)).ToList();
        var fixtureIds = fixtures.Select(x => x.FixtureId).ToList();
        var latest = dbContext.ForecastSnapshots.AsNoTracking().Where(x => (x.Origin == "LivePrematch" || x.Origin == "LiveUnavailable") && x.FixtureId.HasValue && fixtureIds.Contains(x.FixtureId.Value))
            .GroupBy(x => x.FixtureId).Select(g => g.OrderByDescending(x => x.Id).First()).ToList().ToDictionary(x => x.FixtureId.Value);
        var quoteKeys = fixtureIds.Select(x => $"fixture:{x}").ToList();
        var quotes = dbContext.MarketQuotes.AsNoTracking().Where(x => quoteKeys.Contains(x.MatchIdentity) && x.IngestedAt <= now)
            .GroupBy(x => x.MatchIdentity).Select(g => g.OrderByDescending(x => x.Id).First()).ToList().ToDictionary(x => x.MatchIdentity);
        var rows = fixtures.Select(f =>
        {
            var division = new Division(f.Division); var odds = f.MatchOdds;
            var quote = quotes.GetValueOrDefault($"fixture:{f.FixtureId}");
            var offer = quote != null && quote.Available && quote.Kind == "AvailableOffer" && quote.Home > 1 && quote.Draw > 1 && quote.Away > 1 && quote.Commission >= 0 && quote.Commission < 1 && !string.IsNullOrWhiteSpace(quote.Revision) && !string.IsNullOrWhiteSpace(quote.Source) && quote.ObservedAt.HasValue && quote.ObservedAt <= now && quote.ObservedAt >= now.AddMinutes(-betting.MaxQuoteAgeMinutes);
            var currentPriceDecision = offer && odds != null && odds.Calculated >= quote.IngestedAt && SnapshotQuoteId(latest.GetValueOrDefault(f.FixtureId)?.DecisionJson) == quote.Id;
            var kickoffUtc = FixtureTimePolicy.KickoffUtc(f.Date, feeds.FixtureTimeZoneId);
            var valid = kickoffUtc.HasValue && kickoffUtc > now && odds != null && f.AnalysisStatus == "Available" && f.LifecycleStatus == "Scheduled";
            return new Models.Fixture
            {
                FixtureId = f.FixtureId, Country = division.Country, CountryRank = division.CountryRank, Division = division.Name, Tier = division.Tier,
                Date = f.Date, HomeTeam = f.HomeTeam, AwayTeam = f.AwayTeam, IsValid = valid,
                HomeOdds = valid ? odds.Home : null, DrawOdds = valid ? odds.Draw : null, AwayOdds = valid ? odds.Away : null,
                HomeProbability = valid ? odds.HomeProbability : null, DrawProbability = valid ? odds.DrawProbability : null, AwayProbability = valid ? odds.AwayProbability : null,
                HomeXg = valid ? odds.HomeXg : 0, AwayXg = valid ? odds.AwayXg : 0, ExpectedGoals = valid ? odds.ExpectedGoals : 0,
                Recommended = valid ? odds.Recommended : "X", LowerRecommended = valid ? odds.LowerRecommended : "X", ValueSelection = valid && currentPriceDecision ? odds.ValueSelection : "X",
                ValueOdds = valid ? odds.ValueOdds : 0, ValueEdge = valid ? odds.ValueEdge : 0, ValueStake = valid ? odds.ValueStake : 0,
                Calculated = odds?.Calculated ?? default,
                ForecastOrigin = latest.GetValueOrDefault(f.FixtureId)?.Origin ?? "Legacy provenance unknown",
                Support = latest.GetValueOrDefault(f.FixtureId)?.Support ?? "Support unavailable",
                Status = f.LifecycleStatus != "Scheduled" ? f.LifecycleStatus : !kickoffUtc.HasValue ? "Kickoff time or timezone unverified" : kickoffUtc <= now ? "Kickoff passed" : f.AnalysisStatus ?? "Awaiting analysis",
                Source = quote?.Source ?? "Legacy price source unknown", QuoteObservedAt = quote?.ObservedAt, AvailableOffer = offer,
                MarketHome = quote?.Home ?? (f.HomeOddsAverage > 1 ? f.HomeOddsAverage : null), MarketDraw = quote?.Draw ?? (f.DrawOddsAverage > 1 ? f.DrawOddsAverage : null), MarketAway = quote?.Away ?? (f.AwayOddsAverage > 1 ? f.AwayOddsAverage : null),
                PriceKind = quote?.Kind ?? "Legacy average: mapping/timing unqualified"
            };
        }).OrderByDescending(x => Math.Max(x.HomeProbability ?? 0, x.AwayProbability ?? 0)).ThenBy(x => x.Date).ThenBy(x => x.FixtureId).ToList();
        return new FixturePage { Fixtures = rows, Higher = thresholds.Higher, Lower = thresholds.Lower, Today = FixtureTimePolicy.FeedDate(now, feeds.FixtureTimeZoneId) };
    }
    static long? SnapshotQuoteId(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("QuoteId", out var quote) && quote.ValueKind == JsonValueKind.Number && quote.TryGetInt64(out var id) ? id : null;
    }
}
