using PowerLinesWeb.Analysis;

namespace PowerLinesWeb.Tests.Analysis;

public class DixonColesTests
{
    static readonly ExpectedGoals expectedGoals = new(1.6, 1.1);

    [Test]
    public void No_correction_leaves_every_scoreline_alone()
    {
        for (var home = 0; home <= 3; home++)
        {
            for (var away = 0; away <= 3; away++)
            {
                Assert.That(DixonColes.None.GetAdjustment(home, away), Is.EqualTo(1));
            }
        }
    }

    [TestCase(0, 0, 1.2112)]
    [TestCase(0, 1, 0.808)]
    [TestCase(1, 0, 0.868)]
    [TestCase(1, 1, 1.12)]
    public void The_four_lowest_scorelines_are_adjusted(int homeGoals, int awayGoals, double expected)
    {
        var dixonColes = new DixonColes(expectedGoals, -0.12);

        Assert.That(dixonColes.GetAdjustment(homeGoals, awayGoals), Is.EqualTo(expected).Within(1e-9));
    }

    [TestCase(0, 2)]
    [TestCase(2, 0)]
    [TestCase(1, 2)]
    [TestCase(2, 1)]
    [TestCase(3, 3)]
    public void Scorelines_above_one_goal_are_untouched(int homeGoals, int awayGoals)
    {
        var dixonColes = new DixonColes(expectedGoals, -0.12);

        Assert.That(dixonColes.GetAdjustment(homeGoals, awayGoals), Is.EqualTo(1));
    }

    [Test]
    public void The_bounds_keep_every_adjustment_positive()
    {
        var lower = DixonColes.GetLowerBound(expectedGoals);
        var upper = DixonColes.GetUpperBound(expectedGoals);

        foreach (var correlation in new[] { lower + 1e-9, upper - 1e-9, 0 })
        {
            var dixonColes = new DixonColes(expectedGoals, correlation);

            for (var home = 0; home <= 1; home++)
            {
                for (var away = 0; away <= 1; away++)
                {
                    Assert.That(dixonColes.GetAdjustment(home, away), Is.GreaterThan(0), $"{correlation} at {home}-{away}");
                }
            }
        }
    }

    [Test]
    public void A_negative_correlation_moves_probability_into_draws()
    {
        var independent = GetDrawProbability(DixonColes.None);
        var corrected = GetDrawProbability(new DixonColes(expectedGoals, -0.12));

        Assert.That(corrected, Is.GreaterThan(independent + 0.005m));
    }

    private static decimal GetDrawProbability(DixonColes lowScoreCorrection)
    {
        var distribution = new GoalDistribution();

        for (var goals = 0; goals <= 10; goals++)
        {
            distribution.HomeGoalProbabilities.Add(new GoalProbability(goals, (decimal)Poisson.GetProbability(goals, expectedGoals.Home)));
            distribution.AwayGoalProbabilities.Add(new GoalProbability(goals, (decimal)Poisson.GetProbability(goals, expectedGoals.Away)));
        }

        distribution.CalculateDistribution(lowScoreCorrection);
        return distribution.ScoreProbabilities.Where(x => x.Result == 'D').Sum(x => x.Probability);
    }
}
