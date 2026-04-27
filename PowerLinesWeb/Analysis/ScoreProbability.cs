namespace PowerLinesWeb.Analysis;

public class ScoreProbability(GoalProbability homeGoalProbability, GoalProbability awayGoalProbability)
{
    public GoalProbability HomeGoalProbability { get; private set; } = homeGoalProbability;
    public GoalProbability AwayGoalProbability { get; private set; } = awayGoalProbability;
    public decimal Probability { get; private set; }

    public char Result
    {
        get
        {
            if (HomeGoalProbability.Goals > AwayGoalProbability.Goals)
            {
                return 'H';
            }
            if (AwayGoalProbability.Goals > HomeGoalProbability.Goals)
            {
                return 'A';
            }
            return 'D';
        }
    }

    public void CalculateProbability()
    {
        Probability = HomeGoalProbability.Probability * AwayGoalProbability.Probability;
    }
}
