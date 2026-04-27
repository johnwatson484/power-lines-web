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

    public void CalculateDistribution()
    {
        foreach (var homeGoalProbability in HomeGoalProbabilities)
        {
            foreach (var awayGoalProbability in AwayGoalProbabilities)
            {
                var scoreProbability = new ScoreProbability(homeGoalProbability, awayGoalProbability);
                scoreProbability.CalculateProbability();
                ScoreProbabilities.Add(scoreProbability);
            }
        }
    }
}
