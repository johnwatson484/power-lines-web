using System.Globalization;
using System.Text;
using System.Text.Json;
using PowerLinesWeb.Accuracy;
using PowerLinesWeb.Analysis;
using PowerLinesWeb.Data;
using PowerLinesWeb.Forecasts;

namespace PowerLinesWeb.Evaluation;

public record FrozenSelection(string ManifestHash, string Recipe, bool Calibrated, double Temperature, DateTime CalibrationCutoff, decimal BlendWeight, decimal ShortlistThreshold, string DevelopmentRule, string[] DevelopmentIds, string[] FinalIds);

public class Tournament(EvaluationManifest manifest, IReadOnlyDictionary<int, QualifiedQuote> quotes, string manifestHash)
{
    readonly EvaluationManifest manifest = manifest;
    readonly IReadOnlyDictionary<int, QualifiedQuote> quotes = quotes;
    readonly string manifestHash = manifestHash;

    public List<Prediction> Candidate(List<RawPrediction> rows, string recipe, bool calibrated, bool total = false, bool sparse = false, FrozenSelection frozen = null, bool rhoZero = false)
    {
        var selected = rows.Where(x => x.Recipe == recipe).ToList();
        var temperatures = new Dictionary<DateTime, double>();
        foreach (var cutoff in manifest.DevelopmentBlocks)
            temperatures[cutoff] = TemperatureScaling.Fit(selected.Select(x => new RawCalibrationExample(x.Date, recipe, rhoZero ? x.Independent : x.Raw, x.Outcome)), recipe, cutoff, manifest.Temperatures);
        return selected.Where(x => x.Date >= manifest.DevelopmentFrom).Select(x =>
        {
            var cutoff = frozen?.CalibrationCutoff ?? manifest.DevelopmentBlocks.Where(d => d <= x.Date).Max();
            var temperature = calibrated ? frozen?.Temperature ?? temperatures[cutoff] : 1;
            var supported = total ? x.HomeCount >= 10 && x.AwayCount >= 10 : x.LegacyEligible;
            var p = supported || sparse ? TemperatureScaling.Apply(rhoZero ? x.Independent : x.Raw, temperature) : null;
            return new Prediction(x, recipe + (rhoZero ? "-rho0" : "") + (calibrated ? "-temperature" : "-raw") + (sparse ? "-sparse" : total ? "-total10" : ""), p,
                supported ? total ? "TotalHistory" : "LegacyVenue" : "LowSupportPrior", p == null ? "Insufficient history" : "", calibrated ? $"temperature:{temperature:R}" : "identity", cutoff, RawOverride: rhoZero ? x.Independent : x.Raw);
        }).ToList();
    }

    public FrozenSelection Freeze(List<RawPrediction> raw, string output)
    {
        if (raw.Any(x => x.Date >= manifest.HoldoutFrom)) throw new InvalidOperationException("Holdout outcomes cannot participate in selection");
        var candidates = manifest.Recipes.SelectMany(recipe => new[] { Candidate(raw, recipe.Id, false), Candidate(raw, recipe.Id, true) }).ToList();
        candidates.Add(Candidate(raw, "goals365p5", false, rhoZero: true));
        var selected = candidates.OrderBy(x => Metrics.Score(x).LogLoss ?? double.MaxValue).ThenBy(x => x[0].Candidate, StringComparer.Ordinal).First();
        var recipeId = selected[0].Input.Recipe;
        // Experimental blend fitted on chronological development predictions only.
        var alpha = manifest.BlendWeights.OrderBy(a => selected.Where(x => x.Probabilities != null && quotes.ContainsKey(x.Input.Id))
            .Select(x => Scoring.GetLogLoss(Replay.Blend(x.Probabilities, Market(x.Input.Id), a), x.Input.Outcome)).DefaultIfEmpty(double.MaxValue).Average()).ThenByDescending(x => x).First();
        var incumbent = Candidate(raw, "goals365p5", false);
        var baselineMetrics = Metrics.Select(incumbent, .65m, quotes, manifest.Seed, 0, true);
        var policies = manifest.ShortlistThresholds.Select(threshold => new { Threshold = threshold, Metrics = Metrics.Select(selected, threshold, quotes, manifest.Seed, 0) }).ToList();
        var qualifying = policies.Where(x => x.Metrics.Count >= manifest.MinSelections && x.Metrics.Count >= baselineMetrics.Count * manifest.MinVolumeRatio &&
            x.Metrics.HitRate >= baselineMetrics.HitRate - manifest.MaxHitRateLoss && Math.Abs(x.Metrics.CalibrationGap ?? 1) <= manifest.MaxCalibrationGap).OrderByDescending(x => x.Metrics.Count).ThenByDescending(x => x.Threshold).FirstOrDefault();
        // If no policy meets the development objective, retain the conservative threshold for the test.
        var threshold = qualifying?.Threshold ?? .65m;
        var calibrated = selected[0].Candidate.Contains("temperature");
        var finalTemperature = calibrated ? TemperatureScaling.Fit(raw.Where(x => x.Recipe == recipeId).Select(x => new RawCalibrationExample(x.Date, recipeId, x.Raw, x.Outcome)), recipeId, manifest.HoldoutFrom, manifest.Temperatures) : 1;
        var frozen = new FrozenSelection(manifestHash, recipeId + (selected[0].Candidate.Contains("rho0") ? "-rho0" : ""), calibrated, finalTemperature, manifest.HoldoutFrom, alpha, threshold,
            "Lowest common-cohort development log loss; choose maximum-volume threshold meeting declared development precision/calibration constraints, otherwise .65. Final scores cannot change this artifact.",
            selected.Select(x => x.Input.Id.ToString(CultureInfo.InvariantCulture)).ToArray(), []);
        WriteJson(Path.Combine(output, "development-comparison.json"), new { Candidates = candidates.Select(x => new { Candidate = x[0].Candidate, Scores = Metrics.Score(x) }), Policies = policies, Baseline = baselineMetrics, QualifiedMarketPairs = selected.Count(x => x.Probabilities != null && quotes.ContainsKey(x.Input.Id)), Frozen = frozen });
        WriteJson(Path.Combine(output, "frozen-selection.json"), frozen);
        var trainingRows = raw.Where(x => x.Recipe == recipeId && x.Date < manifest.HoldoutFrom).ToList();
        WriteJson(Path.Combine(output, "calibration-artifact.json"), new { Version = ForecastVersions.Hash(new { recipeId, finalTemperature, manifest.HoldoutFrom, manifest.SnapshotSha256 }), ModelRecipe = recipeId, FitFrom = manifest.RawFrom, FitCutoff = manifest.HoldoutFrom, Samples = trainingRows.Count, Temperature = finalTemperature, TrainingOrigin = "EarlierRawOutOfFold", TrainingFingerprint = ForecastVersions.Hash(trainingRows), Activated = false });
        return frozen;
    }

    public object Report(List<RawPrediction> raw, FrozenSelection frozen, string output, IReadOnlyList<Result> source)
    {
        var baseline = Candidate(raw, "goals365p5", false, frozen: frozen);
        var candidate = Candidate(raw, frozen.Recipe.Replace("-rho0", ""), frozen.Calibrated, frozen: frozen, rhoZero: frozen.Recipe.EndsWith("-rho0"));
        var total = Candidate(raw, frozen.Recipe.Replace("-rho0", ""), frozen.Calibrated, total: true, frozen: frozen, rhoZero: frozen.Recipe.EndsWith("-rho0"));
        var sparse = Candidate(raw, "goals365p20", false, total: true, sparse: true, frozen: frozen);
        var rho0 = Candidate(raw, "goals365p5", false, frozen: frozen, rhoZero: true);
        var market = baseline.Select(x => x with { Candidate = "market-only", Probabilities = Market(x.Input.Id), RawOverride = Market(x.Input.Id), Support = "MarketOnly", Exclusion = Market(x.Input.Id) == null ? "Unqualified/missing quote" : "" }).ToList();
        var blend = candidate.Select(x => x with { Candidate = "experimental-blend", Probabilities = x.Probabilities == null ? null : Replay.Blend(x.Probabilities, Market(x.Input.Id), frozen.BlendWeight), Support = Market(x.Input.Id) == null ? "GoalsOnlyNoMarketFallback" : "MarketAssisted", BlendWeight = frozen.BlendWeight }).ToList();
        var league = baseline.Select(x => x with { Candidate = "smoothed-league", Probabilities = x.Input.League, RawOverride = x.Input.League, Support = "LowSupportPrior", Exclusion = "" }).ToList();
        var total5 = candidate.Select(x => x with { Candidate = x.Candidate + "-total5", Probabilities = x.Input.HomeCount >= 5 && x.Input.AwayCount >= 5 ? TemperatureScaling.Apply(frozen.Recipe.EndsWith("-rho0") ? x.Input.Independent : x.Input.Raw, frozen.Temperature) : null, Support = "TotalHistory5", Exclusion = x.Input.HomeCount >= 5 && x.Input.AwayCount >= 5 ? "" : "Insufficient total history" }).ToList();
        var marketFallback = sparse.Select(x => x.Support == "LowSupportPrior" && Market(x.Input.Id) is { } mp
            ? x with { Candidate = "experimental-sparse-market-fallback", Probabilities = mp, RawOverride = mp, Support = "MarketOnly", Exclusion = "" }
            : x with { Candidate = "experimental-sparse-market-fallback" }).ToList();
        var all = new[] { baseline, candidate, total, total5, sparse, marketFallback, rho0, market, blend, league }.GroupBy(x => x[0].Candidate).Select(g => g.First()).ToArray();
        WritePredictions(Path.Combine(output, "predictions.csv"), all.SelectMany(x => x));
        WriteJson(Path.Combine(output, "calibration.json"), all.Select(x => new { Candidate = x[0].Candidate, Reliability = Metrics.Reliability(x) }));
        var test = candidate.Where(x => x.Input.Date >= manifest.HoldoutFrom && x.Input.Date <= manifest.HoldoutThrough).ToList();
        var control = baseline.Where(x => x.Input.Date >= manifest.HoldoutFrom && x.Input.Date <= manifest.HoldoutThrough).ToList();
        var full = total.Where(x => x.Input.Date >= manifest.HoldoutFrom && x.Input.Date <= manifest.HoldoutThrough).ToList();
        var reps = manifest.BootstrapResamples;
        var odds = Metrics.Compare(test, control, manifest.Seed, reps);
        var policy = Metrics.ComparePolicy(test, control, frozen.ShortlistThreshold, quotes, manifest.Seed, reps);
        var fullPolicy = Metrics.ComparePolicy(full, control, frozen.ShortlistThreshold, quotes, manifest.Seed, reps);
        var shortlist = Metrics.Select(test, frozen.ShortlistThreshold, quotes, manifest.Seed, reps);
        bool reliable = shortlist.Count >= manifest.MinSelections && shortlist.GapInterval?.Lower >= -manifest.MaxCalibrationGap && shortlist.GapInterval?.Upper <= manifest.MaxCalibrationGap;
        var oddsPassed = odds.DeltaLogLossInterval?.Upper < 0 && odds.DeltaBrier <= manifest.BrierTolerance;
        var policyPassed = policy.VolumeRatio >= manifest.MinVolumeRatio && fullPolicy.VolumeRatio >= manifest.MinVolumeRatio && policy.HitRateDeltaInterval?.Lower >= -manifest.MaxHitRateLoss && fullPolicy.HitRateDeltaInterval?.Lower >= -manifest.MaxHitRateLoss && reliable;
        var report = new
        {
            Status = "Completed", ManifestHash = manifestHash, DatasetHash = manifest.SnapshotSha256, FrozenSelection = frozen,
            TestFrom = manifest.HoldoutFrom, TestThrough = manifest.HoldoutThrough, AllEligible = control.Count,
            SourceRowsInRange = source.Count(x => x.Date >= manifest.HoldoutFrom && x.Date <= manifest.HoldoutThrough),
            InvalidRowsExcluded = source.Count(x => x.Date >= manifest.HoldoutFrom && x.Date <= manifest.HoldoutThrough && !Replay.Valid(x)),
            ProbabilityConvention = "Multiclass Brier: squared errors summed over H/D/A. Empty cohorts unavailable. Weekly block bootstrap, fixed seed.",
            QuoteConvention = manifest.QuoteConvention, Limitation = manifest.ResearchLimitation,
            ReliabilityCurves = Metrics.Reliability(test),
            OddsComparison = odds, PolicyComparisonCommon = policy, PolicyComparisonFull = fullPolicy, Shortlist = shortlist,
            Strong = Metrics.Select(test, .70m, quotes, manifest.Seed, reps),
            Broader = Metrics.Select(test, frozen.ShortlistThreshold, quotes, manifest.Seed, reps, segment: x => Math.Max(x.Probabilities.Home, x.Probabilities.Away) < .70m),
            Newcomer = Metrics.Select(full, frozen.ShortlistThreshold, quotes, manifest.Seed, reps, segment: x => !x.Input.LegacyEligible),
            NewcomerProbabilityScores = Metrics.Score(full.Where(x => !x.Input.LegacyEligible)),
            SparseUnknownScores = Metrics.Score(sparse.Where(x => x.Input.Date >= manifest.HoldoutFrom && x.Input.Date <= manifest.HoldoutThrough && (x.Input.HomeCount == 0 || x.Input.AwayCount == 0))),
            ModelMarketComparison = Metrics.Compare(test, market.Where(x => x.Input.Date >= manifest.HoldoutFrom && x.Input.Date <= manifest.HoldoutThrough), manifest.Seed, reps),
            Promotion = new { FairOddsModel = oddsPassed, WiderShortlist = policyPassed, MarketAssistedDefault = false, ProductionAction = "Retain corrected incumbent and conservative .65/.70 policy unless both statistical and provenance gates pass; candidates remain experimental." },
            Candidates = all.Select(rows => new { Candidate = rows[0].Candidate, Holdout = Metrics.Score(rows.Where(x => x.Input.Date >= manifest.HoldoutFrom && x.Input.Date <= manifest.HoldoutThrough)), Diagnostic = Metrics.Score(rows.Where(x => x.Input.Date > manifest.HoldoutThrough)) }),
            Divisions = all.SelectMany(rows => rows.Where(x => x.Input.Date >= manifest.HoldoutFrom && x.Input.Date <= manifest.HoldoutThrough).GroupBy(x => x.Input.Division).Select(g => new { Candidate = rows[0].Candidate, Division = g.Key, Scores = Metrics.Score(g), Selections = Metrics.Select(g, frozen.ShortlistThreshold, quotes, manifest.Seed, 0) })),
            Periods = all.SelectMany(rows => rows.GroupBy(x => x.Input.Date.ToString("yyyy-MM", CultureInfo.InvariantCulture)).Select(g => new { Candidate = rows[0].Candidate, Period = g.Key, Scores = Metrics.Score(g) })),
            Exclusions = all.SelectMany(rows => rows.GroupBy(x => new { Forecast = x.Exclusion.Length == 0 ? "Forecast available" : x.Exclusion, Win = x.Probabilities == null ? x.Exclusion : RecommendationPolicy.Win(x.Probabilities, new ThresholdOptions { Lower = frozen.ShortlistThreshold }, x.Support != "LowSupportPrior").Reason, Price = "No verified decision-time available offers" }).Select(g => new { Candidate = rows[0].Candidate, Reasons = g.Key, Count = g.Count() })),
            ActionablePriceSelections = 0, IndicativeRoi = "Unavailable: no claim of executable-price returns; source averages are comparison data only."
        };
        WriteJson(Path.Combine(output, "report.json"), report);
        System.IO.File.WriteAllText(Path.Combine(output, "report.md"), $"# Fixed-snapshot chronological evaluation\n\nHoldout {manifest.HoldoutFrom:yyyy-MM-dd}–{manifest.HoldoutThrough:yyyy-MM-dd}. Eligible: {control.Count}. Forecasts: {Metrics.Score(test).Forecasts}.\n\nSelected recipe: {frozen.Recipe}; temperature {frozen.Temperature}; shortlist threshold {frozen.ShortlistThreshold}. Frozen before final replay.\n\nLog-loss delta: {odds.DeltaLogLoss:G6}; paired 95% interval [{odds.DeltaLogLossInterval?.Lower:G6}, {odds.DeltaLogLossInterval?.Upper:G6}]. Brier delta: {odds.DeltaBrier:G6}.\n\nUnique win selections: {policy.CandidateSelections} vs legacy {policy.BaselineSelections}; ratio {policy.VolumeRatio:G4}. Hit-rate delta: {policy.HitRateDelta:G4}; interval [{policy.HitRateDeltaInterval?.Lower:G4}, {policy.HitRateDeltaInterval?.Upper:G4}].\n\nFair-odds promotion: {oddsPassed}. Wider-policy promotion: {policyPassed}. Market-assisted promotion: false (timing unavailable). See report.json for denominators, reliability, home/away, incremental and newcomer cohorts.\n\n{manifest.QuoteConvention}\n\n{manifest.ResearchLimitation}\n");
        return report;
    }

    MatchProbabilities? Market(int id) => quotes.TryGetValue(id, out var q) ? OddsConverter.RemoveMargin(new(q.Home, q.Draw, q.Away)) : null;
    public static void WriteJson(string path, object value) => System.IO.File.WriteAllText(path, JsonSerializer.Serialize(value, new JsonSerializerOptions(Replay.Json) { WriteIndented = true }) + "\n");

    void WritePredictions(string path, IEnumerable<Prediction> rows)
    {
        static string Csv(object? value) => "\"" + Convert.ToString(value, CultureInfo.InvariantCulture)?.Replace("\"", "\"\"") + "\"";
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        writer.WriteLine("id,division,date,candidate,origin,trainingCutoff,calibrationCutoff,calibrationVersion,support,exclusion,rawH,rawD,rawA,finalH,finalD,finalA,outcome,legacyEligible,homeCount,awayCount,homeWeight,awayWeight,homeEffectiveN,awayEffectiveN,homeAgeDays,awayAgeDays,winSelection,tier,fairOdds,marketH,marketD,marketA,marketSource,priceKind,quoteTime,priceExclusion");
        var frozenThreshold = JsonSerializer.Deserialize<FrozenSelection>(System.IO.File.ReadAllText(Path.Combine(Path.GetDirectoryName(path), "frozen-selection.json")), Replay.Json).ShortlistThreshold;
        foreach (var row in rows.OrderBy(x => x.Input.Date).ThenBy(x => x.Input.Id).ThenBy(x => x.Candidate, StringComparer.Ordinal))
        {
            var x = row.Input; var win = row.Probabilities == null ? null : RecommendationPolicy.Win(row.Probabilities, new ThresholdOptions { Lower = frozenThreshold }, row.Support != "LowSupportPrior");
            quotes.TryGetValue(x.Id, out var quote);
            var raw = row.RawOverride ?? x.Raw;
            writer.WriteLine(string.Join(",", new object?[] { x.Id, x.Division, x.Date.ToString("yyyy-MM-dd"), row.Candidate, "HistoricalReplay", x.Date.ToString("yyyy-MM-dd"), row.CalibrationCutoff.ToString("yyyy-MM-dd"), row.CalibrationVersion, row.Support, row.Exclusion, raw.Home, raw.Draw, raw.Away, row.Probabilities?.Home, row.Probabilities?.Draw, row.Probabilities?.Away, x.Outcome, x.LegacyEligible, x.HomeCount, x.AwayCount, x.HomeWeight, x.AwayWeight, x.HomeEffectiveN, x.AwayEffectiveN, x.HomeAgeDays, x.AwayAgeDays, win?.Selection, win?.Tier, win?.FairOdds, quote?.Home, quote?.Draw, quote?.Away, quote?.SourceUrl, quote?.PriceKind, null, "No verified available offer" }.Select(Csv)));
        }
    }
}
