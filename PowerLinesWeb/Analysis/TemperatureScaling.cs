using PowerLinesWeb.Accuracy;

namespace PowerLinesWeb.Analysis;

public record RawCalibrationExample(DateTime Date, string ModelVersion, MatchProbabilities Raw, char Outcome);

public static class TemperatureScaling
{
    public static MatchProbabilities Apply(MatchProbabilities p, double temperature)
    {
        if (!p.IsValid || !double.IsFinite(temperature) || temperature <= 0) throw new ArgumentOutOfRangeException(nameof(temperature));
        if (temperature == 1) return p;
        var logits = new[] { p.Home, p.Draw, p.Away }.Select(x => Math.Log((double)Math.Max(x, .000000000001m)) / temperature).ToArray();
        var max = logits.Max();
        var weights = logits.Select(x => Math.Exp(x - max)).ToArray();
        return new((decimal)(weights[0] / weights.Sum()), (decimal)(weights[1] / weights.Sum()), (decimal)(weights[2] / weights.Sum()));
    }

    public static double Fit(IEnumerable<RawCalibrationExample> examples, string modelVersion, DateTime cutoff, IEnumerable<double> grid, int minimum = 200)
    {
        var earlier = examples.Where(x => x.ModelVersion == modelVersion && x.Date < cutoff && x.Raw.IsValid && "HDA".Contains(x.Outcome)).ToList();
        if (earlier.Count < minimum) return 1;
        return grid.OrderBy(x => earlier.Average(row => Scoring.GetLogLoss(Apply(row.Raw, x), row.Outcome))).ThenBy(x => Math.Abs(x - 1)).First();
    }
}
