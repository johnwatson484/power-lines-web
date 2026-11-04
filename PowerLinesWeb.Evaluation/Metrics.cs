using PowerLinesWeb.Accuracy;
using PowerLinesWeb.Analysis;

namespace PowerLinesWeb.Evaluation;

public record Interval(double Lower, double Upper);
public record Scores(int Eligible, int Forecasts, double? Per100, double? LogLoss, double? Brier);
public record SelectionMetrics(int Count, int Home, int Away, double? HitRate, double? HomeHitRate, double? AwayHitRate, double? MeanProbability, double? CalibrationGap, Interval? GapInterval, double? Per100, double? PerWeek, double? AllHomeBaseline, double? MarketFavouriteBaseline, int MarketBaselineCount, double? FairOddsMedian, double? MarketOddsMedian, Interval? HitRateInterval = null, Interval? HomeHitRateInterval = null, Interval? AwayHitRateInterval = null);
public record Comparison(int Count, double? DeltaLogLoss, Interval? DeltaLogLossInterval, double? DeltaBrier, double? CandidateLogLoss = null, double? BaselineLogLoss = null, double? CandidateBrier = null, double? BaselineBrier = null);
public record PolicyComparison(int CandidateSelections, int BaselineSelections, double? VolumeRatio, double? HitRateDelta, Interval? HitRateDeltaInterval, SelectionMetrics Incremental);

public static class Metrics
{
    public static Scores Score(IEnumerable<Prediction> input)
    {
        var rows = input.ToList(); var valid = rows.Where(x => x.Probabilities?.IsValid == true).ToList();
        return new(rows.Count, valid.Count, rows.Count == 0 ? null : 100d * valid.Count / rows.Count,
            valid.Count == 0 ? null : valid.Average(x => Scoring.GetLogLoss(x.Probabilities, x.Input.Outcome)),
            valid.Count == 0 ? null : valid.Average(x => Scoring.GetBrierScore(x.Probabilities, x.Input.Outcome)));
    }

    public static Comparison Compare(IEnumerable<Prediction> candidate, IEnumerable<Prediction> baseline, int seed, int repeats)
    {
        var b = baseline.Where(x => x.Probabilities?.IsValid == true).ToDictionary(x => x.Input.Id);
        var paired = candidate.Where(x => x.Probabilities?.IsValid == true && b.ContainsKey(x.Input.Id)).ToList();
        if (paired.Count == 0) return new(0, null, null, null);
        var blocks = paired.GroupBy(x => Week(x.Input.Date)).Select(g => new List<(int N, double Sum)> { (g.Count(), g.Sum(x => Scoring.GetLogLoss(x.Probabilities, x.Input.Outcome) - Scoring.GetLogLoss(b[x.Input.Id].Probabilities, x.Input.Outcome))) }).ToList();
        double Delta(IEnumerable<(int N, double Sum)> data) { var list = data.ToList(); return list.Sum(x => x.Sum) / list.Sum(x => x.N); }
        return new(paired.Count, Delta(blocks.SelectMany(x => x)), Bootstrap(blocks, Delta, seed, repeats), paired.Average(x => Scoring.GetBrierScore(x.Probabilities, x.Input.Outcome) - Scoring.GetBrierScore(b[x.Input.Id].Probabilities, x.Input.Outcome)),
            paired.Average(x => Scoring.GetLogLoss(x.Probabilities, x.Input.Outcome)), paired.Average(x => Scoring.GetLogLoss(b[x.Input.Id].Probabilities, x.Input.Outcome)),
            paired.Average(x => Scoring.GetBrierScore(x.Probabilities, x.Input.Outcome)), paired.Average(x => Scoring.GetBrierScore(b[x.Input.Id].Probabilities, x.Input.Outcome)));
    }

    public static SelectionMetrics Select(IEnumerable<Prediction> input, decimal threshold, IReadOnlyDictionary<int, QualifiedQuote> quotes, int seed, int repeats, bool legacy = false, Func<Prediction, bool> segment = null)
    {
        var universe = input.ToList();
        var rows = universe.Where(x => x.Probabilities?.IsValid == true && (segment == null || segment(x)))
            .Select(x => (Row: x, Side: legacy ? Replay.Legacy(x.Probabilities) : RecommendationPolicy.Win(x.Probabilities, new ThresholdOptions { Lower = threshold }, x.Support != "LowSupportPrior").Selection))
            .Where(x => x.Side != "X").ToList();
        if (rows.Count == 0) return new(0, 0, 0, null, null, null, null, null, null, universe.Count == 0 ? null : 0, 0, null, null, 0, null, null);
        var selected = rows.Select(x => x.Row).ToList(); var sides = rows.ToDictionary(x => x.Row.Input.Id, x => x.Side[0]);
        double Gap(IEnumerable<Prediction> data) => data.Average(x => (double)x.Probabilities.Get(sides[x.Input.Id]) - (x.Input.Outcome == sides[x.Input.Id] ? 1 : 0));
        var home = rows.Where(x => x.Side == "H").ToList(); var away = rows.Where(x => x.Side == "A").ToList();
        var priced = rows.Where(x => quotes.ContainsKey(x.Row.Input.Id)).ToList();
        var weeks = Math.Max(1, (universe.Max(x => x.Input.Date) - universe.Min(x => x.Input.Date)).TotalDays / 7);
        var fair = rows.Select(x => (double)(1 / x.Row.Probabilities.Get(x.Side[0]))).Order().ToList();
        var marketPrices = priced.Select(x => { var q = quotes[x.Row.Input.Id]; return (double)(x.Side == "H" ? q.Home : q.Away); }).Order().ToList();
        Interval? HitInterval(List<Prediction> subset) => Bootstrap(subset.GroupBy(x => Week(x.Input.Date)).Select(g => new List<(int N, int Hits)> { (g.Count(), g.Count(x => x.Input.Outcome == sides[x.Input.Id])) }).ToList(), sampled => { var list = sampled.ToList(); return (double)list.Sum(x => x.Hits) / list.Sum(x => x.N); }, seed, repeats);
        return new(rows.Count, home.Count, away.Count, rows.Average(x => x.Row.Input.Outcome == x.Side[0] ? 1d : 0),
            home.Count == 0 ? null : home.Average(x => x.Row.Input.Outcome == 'H' ? 1d : 0), away.Count == 0 ? null : away.Average(x => x.Row.Input.Outcome == 'A' ? 1d : 0),
            rows.Average(x => (double)x.Row.Probabilities.Get(x.Side[0])), Gap(selected), Bootstrap(selected.GroupBy(x => Week(x.Input.Date)).Select(g => new List<(int N, double Gap)> { (g.Count(), g.Sum(x => (double)x.Probabilities.Get(sides[x.Input.Id]) - (x.Input.Outcome == sides[x.Input.Id] ? 1 : 0))) }).ToList(), blocks => { var list = blocks.ToList(); return list.Sum(x => x.Gap) / list.Sum(x => x.N); }, seed, repeats),
            100d * rows.Count / universe.Count, rows.Count / weeks, rows.Average(x => x.Row.Input.Outcome == 'H' ? 1d : 0),
            priced.Count == 0 ? null : priced.Average(x => { var q = quotes[x.Row.Input.Id]; var favourite = q.Home < q.Draw && q.Home < q.Away ? 'H' : q.Draw < q.Home && q.Draw < q.Away ? 'D' : q.Away < q.Home && q.Away < q.Draw ? 'A' : 'X'; return x.Row.Input.Outcome == favourite ? 1d : 0; }), priced.Count,
            fair[fair.Count / 2], marketPrices.Count == 0 ? null : marketPrices[marketPrices.Count / 2], HitInterval(selected), HitInterval(home.Select(x => x.Row).ToList()), HitInterval(away.Select(x => x.Row).ToList()));
    }

    public static PolicyComparison ComparePolicy(List<Prediction> candidate, List<Prediction> baseline, decimal threshold, IReadOnlyDictionary<int, QualifiedQuote> quotes, int seed, int repeats)
    {
        var b = baseline.ToDictionary(x => x.Input.Id);
        var paired = candidate.Where(x => b.ContainsKey(x.Input.Id)).ToList();
        bool IsCandidate(Prediction x) => x.Probabilities?.IsValid == true && RecommendationPolicy.Win(x.Probabilities, new ThresholdOptions { Lower = threshold }, x.Support != "LowSupportPrior").Selection != "X";
        bool IsBaseline(Prediction x) => b[x.Input.Id].Probabilities?.IsValid == true && Replay.Legacy(b[x.Input.Id].Probabilities) != "X";
        var blockStatistics = paired.GroupBy(x => Week(x.Input.Date)).Select(g =>
        {
            var cs = g.Where(IsCandidate).ToList(); var bs = g.Where(IsBaseline).ToList();
            return new List<(int C, int CHits, int B, int BHits)> { (cs.Count, cs.Count(x => x.Input.Outcome == RecommendationPolicy.Win(x.Probabilities, new ThresholdOptions { Lower = threshold }, x.Support != "LowSupportPrior").Selection[0]), bs.Count, bs.Count(x => x.Input.Outcome == Replay.Legacy(b[x.Input.Id].Probabilities)[0])) };
        }).ToList();
        double Difference(IEnumerable<(int C, int CHits, int B, int BHits)> data)
        {
            var list = data.ToList(); var cn = list.Sum(x => x.C); var bn = list.Sum(x => x.B);
            return cn == 0 || bn == 0 ? double.NaN : (double)list.Sum(x => x.CHits) / cn - (double)list.Sum(x => x.BHits) / bn;
        }
        var ccount = paired.Count(IsCandidate); var bcount = paired.Count(IsBaseline);
        return new(ccount, bcount, bcount == 0 ? null : (double)ccount / bcount, ccount == 0 || bcount == 0 ? null : Difference(blockStatistics.SelectMany(x => x)),
            ccount == 0 || bcount == 0 ? null : Bootstrap(blockStatistics, Difference, seed, repeats),
            Select(candidate, threshold, quotes, seed, repeats, segment: x => !IsBaseline(x)));
    }

    public static object Reliability(IEnumerable<Prediction> data)
    {
        var rows = data.Where(x => x.Probabilities?.IsValid == true).ToList();
        return new[] { 'H', 'D', 'A' }.SelectMany(side => Enumerable.Range(0, 10).Select(bin =>
        {
            var cell = rows.Where(x => (int)Math.Min(9, Math.Floor(x.Probabilities.Get(side) * 10)) == bin).ToList();
            var observed = cell.Count == 0 ? 0 : cell.Average(x => x.Input.Outcome == side ? 1d : 0);
            var n = cell.Count; var z2 = 1.96 * 1.96;
            var centre = n == 0 ? 0 : (observed + z2 / (2 * n)) / (1 + z2 / n);
            var margin = n == 0 ? 0 : 1.96 * Math.Sqrt(observed * (1 - observed) / n + z2 / (4 * n * n)) / (1 + z2 / n);
            return new { Outcome = side.ToString(), ObservedInterval = n == 0 ? null : new Interval(centre - margin, centre + margin), IntervalMethod = "Descriptive Wilson; promotion comparisons use paired weekly blocks", Lower = bin / 10d, Upper = (bin + 1) / 10d, Count = cell.Count, Predicted = cell.Count == 0 ? (double?)null : cell.Average(x => (double)x.Probabilities.Get(side)), Observed = cell.Count == 0 ? (double?)null : cell.Average(x => x.Input.Outcome == side ? 1d : 0) };
        })).ToList();
    }

    public static Interval? Bootstrap<T>(List<List<T>> blocks, Func<IEnumerable<T>, double> metric, int seed, int repeats)
    {
        if (blocks.Count == 0 || repeats <= 0) return null;
        var random = new Random(seed); var values = new List<double>(repeats);
        for (var n = 0; n < repeats; n++)
        {
            var sample = Enumerable.Range(0, blocks.Count).SelectMany(_ => blocks[random.Next(blocks.Count)]);
            var value = metric(sample); if (double.IsFinite(value)) values.Add(value);
        }
        if (values.Count == 0) return null;
        values.Sort(); return new(values[(int)(.025 * (values.Count - 1))], values[(int)(.975 * (values.Count - 1))]);
    }
    public static DateTime Week(DateTime date) => date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
}
