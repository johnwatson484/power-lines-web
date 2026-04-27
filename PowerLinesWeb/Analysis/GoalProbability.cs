namespace PowerLinesWeb.Analysis;

public class GoalProbability(int goals, decimal probability)
{
    public int Goals { get; private set; } = goals;
    public decimal Probability { get; private set; } = probability;
}
