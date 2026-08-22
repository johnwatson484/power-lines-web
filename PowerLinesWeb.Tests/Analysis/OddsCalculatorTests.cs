using PowerLinesWeb.Analysis;
using PowerLinesWeb.Data;

namespace PowerLinesWeb.Tests.Analysis;

public class OddsCalculatorTests
{
    [Test]
    public void An_impossible_outcome_is_priced_as_unbackable_rather_than_zero()
    {
        var odds = Calculate(homeGoalProbabilities: [0.5m, 0.5m], awayGoalProbabilities: [1m, 0m]);

        Assert.That(odds.Away, Is.EqualTo(1000m));
        Assert.That(odds.Home, Is.EqualTo(2m));
        Assert.That(odds.Draw, Is.EqualTo(2m));
    }

    [Test]
    public void Odds_are_the_reciprocal_of_the_result_probability()
    {
        var odds = Calculate(homeGoalProbabilities: [0.2m, 0.8m], awayGoalProbabilities: [1m, 0m]);

        Assert.That(odds.Home, Is.EqualTo(1.25m));
        Assert.That(odds.Draw, Is.EqualTo(5m));
    }

    [Test]
    public void A_confident_prediction_is_recommended_at_both_thresholds()
    {
        var odds = Calculate(homeGoalProbabilities: [0.1m, 0.9m], awayGoalProbabilities: [1m, 0m]);

        Assert.That(odds.Recommended, Is.EqualTo("H"));
        Assert.That(odds.LowerRecommended, Is.EqualTo("H"));
    }

    [Test]
    public void A_marginal_prediction_is_recommended_only_at_the_lower_threshold()
    {
        var odds = Calculate(homeGoalProbabilities: [0.33m, 0.67m], awayGoalProbabilities: [1m, 0m]);

        Assert.That(odds.Recommended, Is.EqualTo("X"));
        Assert.That(odds.LowerRecommended, Is.EqualTo("H"));
    }

    [Test]
    public void An_unconfident_prediction_is_not_recommended()
    {
        var odds = Calculate(homeGoalProbabilities: [0.5m, 0.5m], awayGoalProbabilities: [1m, 0m]);

        Assert.That(odds.Recommended, Is.EqualTo("X"));
        Assert.That(odds.LowerRecommended, Is.EqualTo("X"));
    }

    [Test]
    public void The_most_likely_scoreline_is_reported()
    {
        var odds = Calculate(homeGoalProbabilities: [0.3m, 0.7m], awayGoalProbabilities: [0.8m, 0.2m]);

        Assert.That(odds.HomeGoals, Is.EqualTo(1));
        Assert.That(odds.AwayGoals, Is.EqualTo(0));
    }

    [Test]
    public void Result_probabilities_are_reported_alongside_the_odds()
    {
        var odds = Calculate(homeGoalProbabilities: [0.2m, 0.8m], awayGoalProbabilities: [1m, 0m]);

        Assert.That(odds.HomeProbability, Is.EqualTo(0.8m));
        Assert.That(odds.DrawProbability, Is.EqualTo(0.2m));
        Assert.That(odds.AwayProbability, Is.EqualTo(0m));
    }

    [Test]
    public void No_value_is_found_without_market_prices()
    {
        var odds = Calculate(homeGoalProbabilities: [0.2m, 0.8m], awayGoalProbabilities: [1m, 0m]);

        Assert.That(odds.ValueSelection, Is.EqualTo("X"));
        Assert.That(odds.ValueStake, Is.EqualTo(0m));
    }

    [Test]
    public void A_price_longer_than_the_model_believes_is_flagged_as_value()
    {
        // The model makes home an 80% shot, the market prices it around 45%.
        var odds = Calculate([0.2m, 0.8m], [1m, 0m], new MarketOdds(2.20m, 3.40m, 3.40m), new BettingOptions());

        Assert.That(odds.ValueSelection, Is.EqualTo("H"));
        Assert.That(odds.ValueOdds, Is.EqualTo(2.20m));
        Assert.That(odds.ValueEdge, Is.GreaterThan(0.3m));
        Assert.That(odds.ValueStake, Is.GreaterThan(0m));
    }

    [Test]
    public void A_price_that_matches_the_model_is_not_value()
    {
        var odds = Calculate([0.2m, 0.8m], [1m, 0m], new MarketOdds(1.25m, 5m, 100m), new BettingOptions { MinOdds = 1m, MaxOdds = 1000m });

        Assert.That(odds.ValueSelection, Is.EqualTo("X"));
    }

    [Test]
    public void An_edge_below_the_minimum_is_ignored()
    {
        var marketOdds = new MarketOdds(2.20m, 3.40m, 3.40m);
        var odds = Calculate([0.2m, 0.8m], [1m, 0m], marketOdds, new BettingOptions { MinEdge = 0.9m });

        Assert.That(odds.ValueSelection, Is.EqualTo("X"));
    }

    [Test]
    public void Prices_outside_the_backable_range_are_ignored()
    {
        var marketOdds = new MarketOdds(2.20m, 3.40m, 3.40m);
        var odds = Calculate([0.2m, 0.8m], [1m, 0m], marketOdds, new BettingOptions { MinOdds = 3m, MaxOdds = 10m });

        Assert.That(odds.ValueSelection, Is.EqualTo("X"));
    }

    [Test]
    public void The_stake_scales_with_the_kelly_fraction()
    {
        var marketOdds = new MarketOdds(2.20m, 3.40m, 3.40m);
        var full = Calculate([0.2m, 0.8m], [1m, 0m], marketOdds, new BettingOptions { KellyFraction = 1m });
        var quarter = Calculate([0.2m, 0.8m], [1m, 0m], marketOdds, new BettingOptions { KellyFraction = 0.25m });

        Assert.That(quarter.ValueStake, Is.EqualTo(full.ValueStake / 4).Within(0.0001m));
    }

    [Test]
    public void A_draw_can_be_value_even_though_it_is_never_the_most_likely_result()
    {
        var odds = Calculate([0.5m, 0.5m], [0.5m, 0.5m], new MarketOdds(4m, 4m, 4m), new BettingOptions());

        Assert.That(odds.Recommended, Is.EqualTo("X"));
        Assert.That(odds.ValueSelection, Is.EqualTo("D"));
    }

    [Test]
    public void Calibrated_probabilities_still_sum_to_one()
    {
        var buckets = new List<AccuracyCalibrationRecord>
        {
            new() { LowerBound = 0.0m, UpperBound = 0.5m, Predicted = 0.2m, Observed = 0.4m, Predictions = 100 },
            new() { LowerBound = 0.5m, UpperBound = 1.0m, Predicted = 0.8m, Observed = 0.6m, Predictions = 100 }
        };
        var calibrator = ProbabilityCalibrator.Build(buckets, minPredictions: 30);

        var odds = Calculate([0.2m, 0.8m], [1m, 0m], MarketOdds.None, new BettingOptions(), calibrator);

        Assert.That(odds.HomeProbability + odds.DrawProbability + odds.AwayProbability, Is.EqualTo(1m).Within(0.0001m));
    }

    private static AnalysisMatchOdds Calculate(decimal[] homeGoalProbabilities, decimal[] awayGoalProbabilities, ExpectedGoals expectedGoals = null)
    {
        return Calculate(homeGoalProbabilities, awayGoalProbabilities, MarketOdds.None, new BettingOptions(), ProbabilityCalibrator.None, expectedGoals);
    }

    private static AnalysisMatchOdds Calculate(decimal[] homeGoalProbabilities, decimal[] awayGoalProbabilities, MarketOdds marketOdds, BettingOptions bettingOptions)
    {
        return Calculate(homeGoalProbabilities, awayGoalProbabilities, marketOdds, bettingOptions, ProbabilityCalibrator.None);
    }

    private static AnalysisMatchOdds Calculate(decimal[] homeGoalProbabilities, decimal[] awayGoalProbabilities, MarketOdds marketOdds, BettingOptions bettingOptions, ProbabilityCalibrator calibrator, ExpectedGoals expectedGoals = null)
    {
        var distribution = new GoalDistribution();

        for (var goals = 0; goals < homeGoalProbabilities.Length; goals++)
        {
            distribution.HomeGoalProbabilities.Add(new GoalProbability(goals, homeGoalProbabilities[goals]));
            distribution.AwayGoalProbabilities.Add(new GoalProbability(goals, awayGoalProbabilities[goals]));
        }

        distribution.CalculateDistribution(DixonColes.None);

        var thresholdOptions = new ThresholdOptions { Higher = 0.7m, Lower = 0.65m };
        return new OddsCalculator(1, distribution, marketOdds, thresholdOptions, new ModelOptions(), bettingOptions, calibrator, expectedGoals).GetMatchOdds();
    }
}
