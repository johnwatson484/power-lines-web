using PowerLinesWeb.Analysis;

namespace PowerLinesWeb.Tests.Analysis;

public class GoalDistributionTests
{
    [Test]
    public void CalculateDistribution_normalises_a_truncated_grid_to_one()
    {
        var distribution = Build(maxGoals: 5, homeExpectedGoals: 2.5, awayExpectedGoals: 1.8);

        Assert.That(distribution.ScoreProbabilities.Sum(x => x.Probability), Is.EqualTo(1m).Within(0.0000001m));
    }

    [Test]
    public void The_default_goal_cap_tracks_a_complete_grid()
    {
        var capped = Build(maxGoals: new ModelOptions().MaxGoals, homeExpectedGoals: 2.5, awayExpectedGoals: 1.8);
        var complete = Build(maxGoals: 25, homeExpectedGoals: 2.5, awayExpectedGoals: 1.8);

        Assert.That(ResultProbability(capped, 'H'), Is.EqualTo(ResultProbability(complete, 'H')).Within(0.001m));
        Assert.That(ResultProbability(capped, 'D'), Is.EqualTo(ResultProbability(complete, 'D')).Within(0.001m));
        Assert.That(ResultProbability(capped, 'A'), Is.EqualTo(ResultProbability(complete, 'A')).Within(0.001m));
    }

    [Test]
    public void Truncating_at_five_goals_biases_against_the_stronger_team()
    {
        var truncated = Build(maxGoals: 5, homeExpectedGoals: 2.5, awayExpectedGoals: 1.8);
        var complete = Build(maxGoals: 25, homeExpectedGoals: 2.5, awayExpectedGoals: 1.8);

        // Normalising cannot recover the tail, so the old cap understated the favourite by over a point.
        Assert.That(ResultProbability(complete, 'H') - ResultProbability(truncated, 'H'), Is.GreaterThan(0.01m));
    }

    [Test]
    public void CalculateDistribution_classifies_every_scoreline()
    {
        var distribution = Build(maxGoals: 3, homeExpectedGoals: 1.4, awayExpectedGoals: 1.1);

        var home = distribution.ScoreProbabilities.Count(x => x.Result == 'H');
        var draw = distribution.ScoreProbabilities.Count(x => x.Result == 'D');
        var away = distribution.ScoreProbabilities.Count(x => x.Result == 'A');

        Assert.That(distribution.ScoreProbabilities, Has.Count.EqualTo(16));
        Assert.That(home, Is.EqualTo(6));
        Assert.That(draw, Is.EqualTo(4));
        Assert.That(away, Is.EqualTo(6));
    }

    private static GoalDistribution Build(int maxGoals, double homeExpectedGoals, double awayExpectedGoals)
    {
        var distribution = new GoalDistribution();

        for (var goals = 0; goals <= maxGoals; goals++)
        {
            distribution.HomeGoalProbabilities.Add(new GoalProbability(goals, (decimal)Poisson.GetProbability(goals, homeExpectedGoals)));
            distribution.AwayGoalProbabilities.Add(new GoalProbability(goals, (decimal)Poisson.GetProbability(goals, awayExpectedGoals)));
        }

        distribution.CalculateDistribution();
        return distribution;
    }

    private static decimal ResultProbability(GoalDistribution distribution, char result)
    {
        return distribution.ScoreProbabilities.Where(x => x.Result == result).Sum(x => x.Probability);
    }
}
