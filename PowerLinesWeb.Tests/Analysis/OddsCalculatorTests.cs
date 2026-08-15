using PowerLinesWeb.Analysis;

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

    private static AnalysisMatchOdds Calculate(decimal[] homeGoalProbabilities, decimal[] awayGoalProbabilities)
    {
        var distribution = new GoalDistribution();

        for (var goals = 0; goals < homeGoalProbabilities.Length; goals++)
        {
            distribution.HomeGoalProbabilities.Add(new GoalProbability(goals, homeGoalProbabilities[goals]));
            distribution.AwayGoalProbabilities.Add(new GoalProbability(goals, awayGoalProbabilities[goals]));
        }

        distribution.CalculateDistribution(DixonColes.None);

        var thresholdOptions = new ThresholdOptions { Higher = 0.7m, Lower = 0.65m };
        return new OddsCalculator(1, distribution, thresholdOptions, new ModelOptions()).GetMatchOdds();
    }
}
