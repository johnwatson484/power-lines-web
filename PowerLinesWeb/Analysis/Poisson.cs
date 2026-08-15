namespace PowerLinesWeb.Analysis;

public static class Poisson
{
    public static double GetProbability(int goals, double expectedGoals)
    {
        // Built up by recurrence rather than lambda^k / k! so the factorial cannot overflow.
        var probability = Math.Exp(-expectedGoals);

        for (var i = 1; i <= goals; i++)
        {
            probability *= expectedGoals / i;
        }

        return probability;
    }
}
