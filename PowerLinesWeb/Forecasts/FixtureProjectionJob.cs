using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PowerLinesWeb.Analysis;
using PowerLinesWeb.Data;
using PowerLinesWeb.Fixtures;

namespace PowerLinesWeb.Forecasts;

public class FixtureProjectionJob(ApplicationDbContext db, IAnalysisService analysis, ICalibrationProvider calibration, IOptions<ModelOptions> model, IOptions<ThresholdOptions> policy, IOptions<BettingOptions> betting, IOptions<FeedOptions> feed, TimeProvider clock)
{
    readonly ApplicationDbContext db = db;
    readonly IAnalysisService analysis = analysis;
    readonly ICalibrationProvider calibration = calibration;
    readonly ModelOptions model = model.Value;
    readonly ThresholdOptions policy = policy.Value;
    readonly BettingOptions betting = betting.Value;
    readonly TimeProvider clock = clock;
    readonly FeedOptions feed = feed.Value;

    public void Run() => db.Database.CreateExecutionStrategy().Execute(() =>
    {
        db.ChangeTracker.Clear();
        RunOnce();
    });

    void RunOnce()
    {
        using var transaction = db.Database.BeginTransaction(System.Data.IsolationLevel.RepeatableRead);
        var now = clock.GetUtcNow().UtcDateTime;
        var modelVersion = ForecastVersions.Hash(model);
        var policyVersion = ForecastVersions.Hash(new { policy, betting, feed });
        // Only score revisions affect rating inputs. Price-only corrections leave rating fits reusable.
        var inputVersion = ForecastVersions.Hash(db.Results.AsNoTracking().Where(x => x.Date < now.Date && x.Created <= now && (!x.Updated.HasValue || x.Updated <= now))
            .GroupBy(x => x.Division).Select(g => new { Division = g.Key, Count = g.Count(), Revision = g.Sum(x => x.ScoreRevision) }).OrderBy(x => x.Division).ToList());
        var resultDates = db.Results.AsNoTracking().Where(x => x.Date < now.Date && x.Created <= now).GroupBy(x => x.Division).Select(g => new { Division = g.Key, Last = g.Max(x => x.Date) }).ToDictionary(x => x.Division, x => x.Last);
        var artifact = model.EnableCalibration ? calibration.GetArtifact(modelVersion, now) : null;
        var calibrationVersion = artifact?.Version ?? "identity";
        foreach (var fixtureId in db.Fixtures.AsNoTracking().Where(x => x.Date >= now.Date.AddDays(-1) && x.LifecycleStatus == "Scheduled" && (!x.Updated.HasValue || x.Updated <= now) && (!x.Created.HasValue || x.Created <= now)).OrderBy(x => x.FixtureId).Select(x => x.FixtureId).ToList())
        {
            var fixture = db.Fixtures.Include(x => x.MatchOdds).Single(x => x.FixtureId == fixtureId);
            var kickoffUtc = FixtureTimePolicy.KickoffUtc(fixture.Date, feed.FixtureTimeZoneId);
            if (kickoffUtc.HasValue && kickoffUtc <= now) continue;
            var storedQuote = db.MarketQuotes.AsNoTracking().Where(x => x.MatchIdentity == $"fixture:{fixtureId}" && x.IngestedAt <= now).OrderByDescending(x => x.Id).FirstOrDefault();
            var quoteFresh = storedQuote != null && storedQuote.Available && storedQuote.ObservedAt.HasValue && storedQuote.ObservedAt <= now && storedQuote.ObservedAt >= now.AddMinutes(-betting.MaxQuoteAgeMinutes);
            var quoteId = storedQuote?.Id;
            var key = ForecastVersions.Hash(new { inputVersion, fixture.Revision, quoteId, quoteFresh, modelVersion, policyVersion, calibrationVersion, Day = now.Date });
            if (fixture.AnalysisKey == key) continue;
            try
            {
                var quote = storedQuote == null ? new MarketOdds(fixture.HomeOddsAverage, fixture.DrawOddsAverage, fixture.AwayOddsAverage)
                    : new MarketOdds(storedQuote.Home, storedQuote.Draw, storedQuote.Away)
                    { Source = storedQuote.Source, Kind = storedQuote.Kind, ObservedAt = storedQuote.ObservedAt, Revision = storedQuote.Revision, IsAvailable = storedQuote.Available, Commission = storedQuote.Commission };
                var stale = !feed.SeasonBreakMonths.Contains(now.Month) && (!resultDates.TryGetValue(fixture.Division, out var latestResult) || latestResult < now.Date.AddDays(-feed.MaxResultAgeDaysInSeason));
                var missingKickoff = !kickoffUtc.HasValue;
                var odds = stale || missingKickoff ? null : analysis.GetMatchOdds(new AnalysisFixture { Id = fixture.FixtureId, Division = fixture.Division, Date = kickoffUtc.Value, InformationCutoff = now, HomeTeam = fixture.HomeTeam, AwayTeam = fixture.AwayTeam, MarketOdds = quote });
                var status = missingKickoff ? "Kickoff time or timezone unverified" : stale ? "Stale result feed" : odds == null ? "Insufficient history" : "Available";
                db.ForecastSnapshots.Add(new ForecastSnapshot
                {
                    RunId = $"live:{key}", MatchIdentity = $"fixture:{fixture.FixtureId}", FixtureId = fixture.FixtureId, Division = fixture.Division, MatchDate = fixture.Date,
                    HomeTeam = fixture.HomeTeam, AwayTeam = fixture.AwayTeam, Origin = kickoffUtc.HasValue ? "LivePrematch" : "LiveUnavailable", ForecastAt = now, TrainingCutoff = now.Date, ModelVersion = modelVersion, InputRevision = inputVersion,
                    CalibrationVersion = odds?.CalibrationVersion ?? calibrationVersion, CalibrationCutoff = odds?.CalibrationCutoff,
                    PolicyVersion = policyVersion, Support = odds?.Support ?? "Unavailable", Status = status,
                    RawHome = odds?.RawHomeProbability, RawDraw = odds?.RawDrawProbability, RawAway = odds?.RawAwayProbability,
                    FinalHome = odds?.HomeProbability, FinalDraw = odds?.DrawProbability, FinalAway = odds?.AwayProbability,
                    QuoteRevision = quote.Revision, DecisionJson = JsonSerializer.Serialize(new { Win = odds?.Recommended, Broader = odds?.LowerRecommended, Price = odds?.ValueSelection, KickoffUtc = kickoffUtc, QuoteId = storedQuote?.Id, QuoteSource = storedQuote?.Source, QuoteKind = storedQuote?.Kind, QuoteTime = storedQuote?.ObservedAt, odds?.PriceReason, odds?.ValueExpectedReturn })
                });
                fixture.AnalysisStatus = status; fixture.AnalysisKey = key;
                if (odds != null)
                {
                    var projection = fixture.MatchOdds ?? new MatchOdds { FixtureId = fixture.FixtureId };
                    projection.Home = odds.Home; projection.Draw = odds.Draw; projection.Away = odds.Away;
                    projection.HomeProbability = odds.HomeProbability; projection.DrawProbability = odds.DrawProbability; projection.AwayProbability = odds.AwayProbability;
                    projection.HomeXg = odds.HomeXg; projection.AwayXg = odds.AwayXg; projection.ExpectedGoals = odds.ExpectedGoals;
                    projection.HomeGoals = odds.HomeGoals; projection.AwayGoals = odds.AwayGoals;
                    projection.Recommended = odds.Recommended; projection.LowerRecommended = odds.LowerRecommended;
                    projection.ValueSelection = odds.ValueSelection; projection.ValueEdge = odds.ValueEdge; projection.ValueOdds = odds.ValueOdds; projection.ValueStake = odds.ValueStake;
                    projection.Calculated = now;
                    if (fixture.MatchOdds == null) db.MatchOdds.Add(projection);
                }
                db.SaveChanges();
            }
            catch (Exception ex) when (ex is not DbUpdateException && ex is not Npgsql.NpgsqlException && ex is not TimeoutException)
            {
                // Roll back all tracked projection changes so a failed partial forecast is never published.
                db.ChangeTracker.Clear();
                var failed = db.Fixtures.Single(x => x.FixtureId == fixture.FixtureId);
                failed.AnalysisStatus = "Analysis failed";
                db.SaveChanges();
                Console.Error.WriteLine($"Fixture {fixture.FixtureId}: {ex.Message}");
            }
        }
        Settle(now);
        transaction.Commit();
    }

    void Settle(DateTime now)
    {
        var unresolved = db.ForecastSnapshots.AsNoTracking().Where(x => x.Origin == "LivePrematch" && x.Status == "Available" && x.MatchDate < now).ToList();
        foreach (var snapshot in unresolved)
        {
            var result = db.Results.AsNoTracking().SingleOrDefault(x => x.Division == snapshot.Division && x.Date.Date == snapshot.MatchDate.Date && x.HomeTeam == snapshot.HomeTeam && x.AwayTeam == snapshot.AwayTeam);
            if (result == null || db.ForecastSettlements.Any(x => x.ForecastSnapshotId == snapshot.Id && x.ResultRevision == result.Revision)) continue;
            db.ForecastSettlements.Add(new ForecastSettlement { ForecastSnapshotId = snapshot.Id, ResultId = result.ResultId, ResultRevision = result.Revision, Outcome = result.FullTimeResult, SettledAt = now });
        }
        db.SaveChanges();
    }
}
