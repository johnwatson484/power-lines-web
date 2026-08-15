using PowerLinesWeb.Data;

namespace PowerLinesWeb.Analysis;

// Maps a raw model probability to what has actually been observed at that probability, using
// backtested calibration buckets, so a division whose 70% calls only come in 55% of the time is
// pulled back down rather than trusted at face value.
public class ProbabilityCalibrator(IReadOnlyList<CalibrationPoint> points)
{
    readonly IReadOnlyList<CalibrationPoint> points = points;

    public static ProbabilityCalibrator None { get; } = new([]);

    public static ProbabilityCalibrator Build(IReadOnlyList<AccuracyCalibrationRecord> buckets, int minPredictions)
    {
        var usable = buckets
            .Where(x => x.Predictions >= minPredictions)
            .Select(x => new WeightedPoint(x.Predicted, x.Observed, x.Predictions))
            .OrderBy(x => x.Predicted)
            .ToList();

        if (usable.Count < 2)
        {
            return None;
        }

        return new ProbabilityCalibrator(Isotonic(usable));
    }

    public decimal Calibrate(decimal probability)
    {
        if (points.Count < 2)
        {
            return probability;
        }

        // Beyond the observed range there is nothing to interpolate between, so hold at the nearest edge.
        if (probability <= points[0].Predicted)
        {
            return points[0].Observed;
        }

        if (probability >= points[^1].Predicted)
        {
            return points[^1].Observed;
        }

        for (var i = 0; i < points.Count - 1; i++)
        {
            var lower = points[i];
            var upper = points[i + 1];

            if (probability >= lower.Predicted && probability <= upper.Predicted)
            {
                if (upper.Predicted == lower.Predicted)
                {
                    return lower.Observed;
                }

                var fraction = (probability - lower.Predicted) / (upper.Predicted - lower.Predicted);
                return lower.Observed + fraction * (upper.Observed - lower.Observed);
            }
        }

        return probability;
    }

    // Pool-adjacent-violators: merges any buckets where observed frequency dips below an earlier,
    // lower-probability bucket, so the calibration curve can only ever rise, never fall, with probability.
    private static List<CalibrationPoint> Isotonic(IReadOnlyList<WeightedPoint> points)
    {
        var blocks = new List<WeightedPoint>();

        foreach (var point in points)
        {
            var merged = point;

            while (blocks.Count > 0 && blocks[^1].Observed > merged.Observed)
            {
                var previous = blocks[^1];
                blocks.RemoveAt(blocks.Count - 1);
                merged = previous.Merge(merged);
            }

            blocks.Add(merged);
        }

        return points
            .Select(point => new CalibrationPoint(point.Predicted, Containing(blocks, point).Observed))
            .ToList();
    }

    private static WeightedPoint Containing(IReadOnlyList<WeightedPoint> blocks, WeightedPoint point)
    {
        return blocks.First(block => point.Predicted >= block.LowerPredicted && point.Predicted <= block.UpperPredicted);
    }

    private readonly record struct WeightedPoint(decimal Predicted, decimal Observed, int Weight, decimal LowerPredicted, decimal UpperPredicted)
    {
        public WeightedPoint(decimal predicted, decimal observed, int weight) : this(predicted, observed, weight, predicted, predicted)
        {
        }

        public WeightedPoint Merge(WeightedPoint other)
        {
            var weight = Weight + other.Weight;
            var observed = (Observed * Weight + other.Observed * other.Weight) / weight;
            return new WeightedPoint(other.Predicted, observed, weight, LowerPredicted, other.UpperPredicted);
        }
    }
}

public record CalibrationPoint(decimal Predicted, decimal Observed);
