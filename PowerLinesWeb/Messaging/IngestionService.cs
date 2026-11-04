using Microsoft.EntityFrameworkCore;
using PowerLinesWeb.Data;
using PowerLinesWeb.Forecasts;

namespace PowerLinesWeb.Messaging;

// Called with a fresh DbContext per delivery. Identical re-imports are no-ops.
public class IngestionService(ApplicationDbContext db, TimeProvider clock)
{
    readonly ApplicationDbContext db = db;
    readonly TimeProvider clock = clock;

    public void SaveFixture(Fixture incoming)
    {
        Validate(incoming.Division, incoming.HomeTeam, incoming.AwayTeam, incoming.Date);
        using var transaction = db.Database.BeginTransaction(System.Data.IsolationLevel.Serializable);
        var existing = db.Fixtures.SingleOrDefault(x => (incoming.SourceMatchId != null && x.SourceMatchId == incoming.SourceMatchId) ||
            (x.Division == incoming.Division && x.Date == incoming.Date && x.HomeTeam == incoming.HomeTeam && x.AwayTeam == incoming.AwayTeam));
        var now = clock.GetUtcNow().UtcDateTime;
        if (existing == null)
        {
            // A rename without a source identity/explicit alias must be resolved, never silently duplicated.
            if (db.Fixtures.Any(x => x.Division == incoming.Division && x.Date == incoming.Date && (x.HomeTeam == incoming.HomeTeam || x.AwayTeam == incoming.AwayTeam)))
                throw new InvalidOperationException("Ambiguous renamed fixture; reconcile source identity/alias first.");
            incoming.Created = now; incoming.Updated = now; incoming.Revision = 1;
            incoming.AnalysisStatus = "Awaiting analysis"; db.Fixtures.Add(incoming); existing = incoming;
        }
        else
        {
            var before = ForecastVersions.Hash(new { existing.Division, Date = existing.Date.Ticks, existing.HomeTeam, existing.AwayTeam, existing.HomeOddsAverage, existing.DrawOddsAverage, existing.AwayOddsAverage, existing.SourceFileHash, existing.PriceKind, QuoteObservedAt = existing.QuoteObservedAt.HasValue ? existing.QuoteObservedAt.Value.Ticks : (long?)null, existing.LifecycleStatus });
            existing.Division = incoming.Division; existing.Date = incoming.Date;
            existing.HomeTeam = incoming.HomeTeam; existing.AwayTeam = incoming.AwayTeam;
            if (ValidPrices(incoming.HomeOddsAverage, incoming.DrawOddsAverage, incoming.AwayOddsAverage))
            { existing.HomeOddsAverage = incoming.HomeOddsAverage; existing.DrawOddsAverage = incoming.DrawOddsAverage; existing.AwayOddsAverage = incoming.AwayOddsAverage; }
            if (incoming.SourceFileHash != null)
            {
                existing.SourceUrl = incoming.SourceUrl; existing.SourceSeason = incoming.SourceSeason; existing.SourceFileHash = incoming.SourceFileHash;
                existing.SourceRowId = incoming.SourceRowId; existing.SourceMatchId = incoming.SourceMatchId;
                if (ValidPrices(incoming.HomeOddsAverage, incoming.DrawOddsAverage, incoming.AwayOddsAverage))
                { existing.PriceKind = incoming.PriceKind; existing.PriceCollection = incoming.PriceCollection; existing.QuoteObservedAt = incoming.QuoteObservedAt; }
            }
            existing.LifecycleStatus = incoming.LifecycleStatus ?? "Scheduled";
            var after = ForecastVersions.Hash(new { existing.Division, Date = existing.Date.Ticks, existing.HomeTeam, existing.AwayTeam, existing.HomeOddsAverage, existing.DrawOddsAverage, existing.AwayOddsAverage, existing.SourceFileHash, existing.PriceKind, QuoteObservedAt = existing.QuoteObservedAt.HasValue ? existing.QuoteObservedAt.Value.Ticks : (long?)null, existing.LifecycleStatus });
            if (before != after) { existing.Revision++; existing.Updated = now; existing.AnalysisKey = null; existing.AnalysisStatus = "Awaiting analysis"; }
        }
        db.SaveChanges(); // Assign the stable database identity before archiving quotes.
        ArchiveQuote($"fixture:{existing.FixtureId}:{existing.SourceMatchId}", existing.SourceUrl, existing.SourceFileHash, existing.Revision, existing.PriceKind, existing.QuoteObservedAt, existing.HomeOddsAverage, existing.DrawOddsAverage, existing.AwayOddsAverage, now);
        db.SaveChanges(); transaction.Commit();
    }

    public void SaveResult(Result incoming)
    {
        Validate(incoming.Division, incoming.HomeTeam, incoming.AwayTeam, incoming.Date);
        if (incoming.FullTimeHomeGoals < 0 || incoming.FullTimeAwayGoals < 0 || incoming.FullTimeResult != (incoming.FullTimeHomeGoals > incoming.FullTimeAwayGoals ? "H" : incoming.FullTimeHomeGoals < incoming.FullTimeAwayGoals ? "A" : "D"))
            throw new FormatException("Invalid score/outcome");
        using var transaction = db.Database.BeginTransaction(System.Data.IsolationLevel.Serializable);
        var existing = db.Results.SingleOrDefault(x => (incoming.SourceMatchId != null && x.SourceMatchId == incoming.SourceMatchId) ||
            (x.Division == incoming.Division && x.Date == incoming.Date && x.HomeTeam == incoming.HomeTeam && x.AwayTeam == incoming.AwayTeam));
        var now = clock.GetUtcNow().UtcDateTime;
        if (existing == null)
        {
            if (db.Results.Any(x => x.Division == incoming.Division && x.Date == incoming.Date && (x.HomeTeam == incoming.HomeTeam || x.AwayTeam == incoming.AwayTeam)))
                throw new InvalidOperationException("Ambiguous renamed result; reconcile source identity/alias first.");
            incoming.Created = now; incoming.Updated = now; incoming.Revision = 1; incoming.ScoreRevision = 1;
            db.Results.Add(incoming); existing = incoming;
        }
        else
        {
            if (existing.Date != incoming.Date || existing.Division != incoming.Division || existing.HomeTeam != incoming.HomeTeam || existing.AwayTeam != incoming.AwayTeam)
                throw new InvalidOperationException("Result identity correction requires staged reconciliation.");
            var scoreChanged = existing.FullTimeHomeGoals != incoming.FullTimeHomeGoals || existing.FullTimeAwayGoals != incoming.FullTimeAwayGoals || existing.FullTimeResult != incoming.FullTimeResult;
            var changed = scoreChanged || (incoming.HalfTimeHomeGoals.HasValue && existing.HalfTimeHomeGoals != incoming.HalfTimeHomeGoals) || (incoming.HalfTimeAwayGoals.HasValue && existing.HalfTimeAwayGoals != incoming.HalfTimeAwayGoals) || (incoming.HalfTimeResult != null && existing.HalfTimeResult != incoming.HalfTimeResult);
            existing.FullTimeHomeGoals = incoming.FullTimeHomeGoals; existing.FullTimeAwayGoals = incoming.FullTimeAwayGoals; existing.FullTimeResult = incoming.FullTimeResult;
            // Missing source fields never erase known historical observations.
            if (incoming.HalfTimeHomeGoals.HasValue) existing.HalfTimeHomeGoals = incoming.HalfTimeHomeGoals;
            if (incoming.HalfTimeAwayGoals.HasValue) existing.HalfTimeAwayGoals = incoming.HalfTimeAwayGoals;
            if (incoming.HalfTimeResult != null) existing.HalfTimeResult = incoming.HalfTimeResult;
            if (ValidPrices(incoming.HomeOddsAverage, incoming.DrawOddsAverage, incoming.AwayOddsAverage))
            {
                changed |= existing.HomeOddsAverage != incoming.HomeOddsAverage || existing.DrawOddsAverage != incoming.DrawOddsAverage || existing.AwayOddsAverage != incoming.AwayOddsAverage || existing.PriceKind != incoming.PriceKind;
                existing.HomeOddsAverage = incoming.HomeOddsAverage; existing.DrawOddsAverage = incoming.DrawOddsAverage; existing.AwayOddsAverage = incoming.AwayOddsAverage;
                existing.PriceKind = incoming.PriceKind; existing.PriceCollection = incoming.PriceCollection; existing.QuoteObservedAt = incoming.QuoteObservedAt;
            }
            if (incoming.SourceFileHash != null)
            {
                changed |= existing.SourceFileHash != incoming.SourceFileHash;
                existing.SourceUrl = incoming.SourceUrl; existing.SourceSeason = incoming.SourceSeason; existing.SourceFileHash = incoming.SourceFileHash;
                existing.SourceRowId = incoming.SourceRowId; existing.SourceMatchId = incoming.SourceMatchId;
            }
            if (scoreChanged) existing.ScoreRevision++;
            if (changed) { existing.Revision++; existing.Updated = now; }
        }
        db.SaveChanges();
        ArchiveQuote($"result:{existing.ResultId}:{existing.SourceMatchId}", existing.SourceUrl, existing.SourceFileHash, existing.Revision, existing.PriceKind, existing.QuoteObservedAt, existing.HomeOddsAverage, existing.DrawOddsAverage, existing.AwayOddsAverage, now);
        db.SaveChanges(); transaction.Commit();
    }

    void ArchiveQuote(string match, string source, string hash, long revision, string kind, DateTime? observedAt, decimal home, decimal draw, decimal away, DateTime now)
    {
        if (!ValidPrices(home, draw, away)) return;
        source ??= "LegacyUnknown";
        var version = revision.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (db.MarketQuotes.Any(x => x.MatchIdentity == match && x.Source == source && x.Revision == version)) return;
        db.MarketQuotes.Add(new MarketQuote { MatchIdentity = match, Source = source, SourceFileHash = hash, Revision = version, Kind = kind ?? "LegacyAverageUnknown", ObservedAt = observedAt, IngestedAt = now, Available = false, Home = home, Draw = draw, Away = away });
    }

    static bool ValidPrices(decimal h, decimal d, decimal a) => h > 1 && d > 1 && a > 1;
    static void Validate(string division, string home, string away, DateTime date)
    {
        if (string.IsNullOrWhiteSpace(division) || string.IsNullOrWhiteSpace(home) || string.IsNullOrWhiteSpace(away) || home == away || date.Year < 1990)
            throw new FormatException("Invalid match identity");
    }
}
