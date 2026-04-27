namespace PowerLinesWeb.Analysis;

public class Poisson
{
    const double euler = 2.71828;

    public double GetProbability(int randomVariable, double averageRateOfSuccess)
    {
        return Math.Pow(euler, -averageRateOfSuccess) * Math.Pow(averageRateOfSuccess, randomVariable) / Factorial(randomVariable);
    }

    private int Factorial(int n)
    {
        int value = 1;
        for (int i = 1; i <= n; i++)
        {
            value *= i;
        }
        return value;
    }
}
