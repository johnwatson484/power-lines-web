using PowerLinesWeb.Analysis;
using PowerLinesWeb.Data;

namespace PowerLinesWeb.Tests.Analysis;

public class ProbabilityCalibratorTests
{
    [Test]
    public void With_no_calibration_data_the_probability_passes_through_unchanged()
    {
        Assert.That(ProbabilityCalibrator.None.Calibrate(0.42m), Is.EqualTo(0.42m));
    }

    [Test]
    public void With_a_single_usable_bucket_the_probability_passes_through_unchanged()
    {
        var buckets = new List<AccuracyCalibrationRecord>
        {
            Bucket(predicted: 0.5m, observed: 0.3m, predictions: 100)
        };

        var calibrator = ProbabilityCalibrator.Build(buckets, minPredictions: 30);

        Assert.That(calibrator.Calibrate(0.5m), Is.EqualTo(0.5m));
    }

    [Test]
    public void Buckets_below_the_sample_size_gate_are_excluded()
    {
        var buckets = new List<AccuracyCalibrationRecord>
        {
            Bucket(predicted: 0.2m, observed: 0.4m, predictions: 5),
            Bucket(predicted: 0.8m, observed: 0.6m, predictions: 5)
        };

        var calibrator = ProbabilityCalibrator.Build(buckets, minPredictions: 30);

        Assert.That(calibrator.Calibrate(0.5m), Is.EqualTo(0.5m), "both buckets were too thin to use");
    }

    [Test]
    public void A_perfectly_calibrated_model_is_left_unchanged()
    {
        var buckets = new List<AccuracyCalibrationRecord>
        {
            Bucket(predicted: 0.1m, observed: 0.1m, predictions: 100),
            Bucket(predicted: 0.5m, observed: 0.5m, predictions: 100),
            Bucket(predicted: 0.9m, observed: 0.9m, predictions: 100)
        };

        var calibrator = ProbabilityCalibrator.Build(buckets, minPredictions: 30);

        Assert.That(calibrator.Calibrate(0.3m), Is.EqualTo(0.3m).Within(0.0001m));
    }

    [Test]
    public void An_overconfident_model_is_pulled_towards_what_actually_happened()
    {
        var buckets = new List<AccuracyCalibrationRecord>
        {
            Bucket(predicted: 0.2m, observed: 0.25m, predictions: 100),
            Bucket(predicted: 0.9m, observed: 0.6m, predictions: 100)
        };

        var calibrator = ProbabilityCalibrator.Build(buckets, minPredictions: 30);

        Assert.That(calibrator.Calibrate(0.9m), Is.EqualTo(0.6m));
    }

    [Test]
    public void Probabilities_between_buckets_are_interpolated()
    {
        var buckets = new List<AccuracyCalibrationRecord>
        {
            Bucket(predicted: 0.2m, observed: 0.1m, predictions: 100),
            Bucket(predicted: 0.6m, observed: 0.5m, predictions: 100)
        };

        var calibrator = ProbabilityCalibrator.Build(buckets, minPredictions: 30);

        Assert.That(calibrator.Calibrate(0.4m), Is.EqualTo(0.3m).Within(0.0001m));
    }

    [Test]
    public void Probabilities_beyond_the_observed_range_hold_at_the_nearest_edge()
    {
        var buckets = new List<AccuracyCalibrationRecord>
        {
            Bucket(predicted: 0.3m, observed: 0.2m, predictions: 100),
            Bucket(predicted: 0.7m, observed: 0.6m, predictions: 100)
        };

        var calibrator = ProbabilityCalibrator.Build(buckets, minPredictions: 30);

        Assert.That(calibrator.Calibrate(0.05m), Is.EqualTo(0.2m));
        Assert.That(calibrator.Calibrate(0.95m), Is.EqualTo(0.6m));
    }

    [Test]
    public void A_dip_in_observed_frequency_is_smoothed_so_the_curve_never_falls()
    {
        // The middle bucket is noise: predicted rises but observed dips before recovering.
        var buckets = new List<AccuracyCalibrationRecord>
        {
            Bucket(predicted: 0.2m, observed: 0.2m, predictions: 100),
            Bucket(predicted: 0.5m, observed: 0.35m, predictions: 100),
            Bucket(predicted: 0.8m, observed: 0.6m, predictions: 100)
        };

        var calibrator = ProbabilityCalibrator.Build(buckets, minPredictions: 30);

        var low = calibrator.Calibrate(0.2m);
        var middle = calibrator.Calibrate(0.5m);
        var high = calibrator.Calibrate(0.8m);

        Assert.That(low, Is.LessThanOrEqualTo(middle));
        Assert.That(middle, Is.LessThanOrEqualTo(high));
    }

    private static AccuracyCalibrationRecord Bucket(decimal predicted, decimal observed, int predictions)
    {
        return new AccuracyCalibrationRecord { Predicted = predicted, Observed = observed, Predictions = predictions };
    }
}
