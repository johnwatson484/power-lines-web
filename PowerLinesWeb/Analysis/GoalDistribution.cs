namespace PowerLinesWeb.Analysis;

public class GoalDistribution
{
    public List<GoalProbability> HomeGoalProbabilities { get; private set; }

    public List<GoalProbability> AwayGoalProbabilities { get; private set; }

    public List<ScoreProbability> ScoreProbabilities { get; private set; }

    public GoalDistribution()
    {
        HomeGoalProbabilities = new List<GoalProbability>();
        AwayGoalProbabilities = new List<GoalProbability>();
        ScoreProbabilities = new List<ScoreProbability>();
    }

    public void CalculateDistribution(DixonColes lowScoreCorrection)
    {
        foreach (var homeGoalProbability in HomeGoalProbabilities)
        {
            foreach (var awayGoalProbability in AwayGoalProbabilities)
            {
                var scoreProbability = new ScoreProbability(homeGoalProbability, awayGoalProbability);
                scoreProbability.CalculateProbability();
                scoreProbability.Scale((decimal)lowScoreCorrection.GetAdjustment(homeGoalProbability.Goals, awayGoalProbability.Goals));
                ScoreProbabilities.Add(scoreProbability);
            }
        }

        Normalise();
    }

    // The grid is truncated at a maximum scoreline, so it must be rescaled back to a total of 1.
    private void Normalise()
    {
        var total = ScoreProbabilities.Sum(x => x.Probability);

        if (total <= 0)
        {
            return;
        }

        foreach (var scoreProbability in ScoreProbabilities)
        {
            scoreProbability.Scale(1 / total);
        }
    }
}
