using PowerLinesWeb.Accuracy;
using PowerLinesWeb.Analysis;

namespace PowerLinesWeb.Tests.Accuracy;

public class ScoringTests
{
    [Test]
    public void A_certain_and_correct_forecast_scores_perfectly()
    {
        var probabilities = new MatchProbabilities(1, 0, 0);

        Assert.That(Scoring.GetLogLoss(probabilities, 'H'), Is.EqualTo(0).Within(1e-9));
        Assert.That(Scoring.GetBrierScore(probabilities, 'H'), Is.EqualTo(0).Within(1e-9));
    }

    [Test]
    public void A_certain_and_wrong_forecast_is_penalised_but_not_infinitely()
    {
        var probabilities = new MatchProbabilities(1, 0, 0);

        Assert.That(Scoring.GetLogLoss(probabilities, 'A'), Is.GreaterThan(10));
        Assert.That(double.IsFinite(Scoring.GetLogLoss(probabilities, 'A')), Is.True);
        Assert.That(Scoring.GetBrierScore(probabilities, 'A'), Is.EqualTo(2).Within(1e-9));
    }

    [Test]
    public void Log_loss_is_the_negative_log_of_the_probability_given_to_the_result()
    {
        var probabilities = new MatchProbabilities(0.5m, 0.3m, 0.2m);

        Assert.That(Scoring.GetLogLoss(probabilities, 'D'), Is.EqualTo(-Math.Log(0.3)).Within(1e-9));
    }

    [Test]
    public void Brier_sums_the_squared_error_across_all_three_outcomes()
    {
        var probabilities = new MatchProbabilities(0.5m, 0.3m, 0.2m);
        var expected = Math.Pow(0.5 - 1, 2) + Math.Pow(0.3, 2) + Math.Pow(0.2, 2);

        Assert.That(Scoring.GetBrierScore(probabilities, 'H'), Is.EqualTo(expected).Within(1e-9));
    }

    [Test]
    public void An_overconfident_forecast_scores_worse_than_a_calibrated_one()
    {
        var calibrated = new MatchProbabilities(0.6m, 0.25m, 0.15m);
        var overconfident = new MatchProbabilities(0.9m, 0.05m, 0.05m);

        // Both call the same winner, so a hit rate cannot tell them apart.
        Assert.That(Average(calibrated), Is.LessThan(Average(overconfident)));
    }

    // Log loss over a run of matches where home wins six times in ten.
    private static double Average(MatchProbabilities probabilities)
    {
        var results = new[] { 'H', 'H', 'H', 'H', 'H', 'H', 'D', 'D', 'A', 'A' };
        return results.Average(x => Scoring.GetLogLoss(probabilities, x));
    }
}
