namespace PowerLinesWeb.Analysis;

public class ModelOptions
{
    public int MaxGoals { get; set; } = 10;
    public int MinTeamMatches { get; set; } = 10;
    public int YearsToAnalyse { get; set; } = 6;
    public int BacktestYears { get; set; } = 3;
    public decimal MaxOdds { get; set; } = 1000;
    public double HalfLifeDays { get; set; }
    public double PriorMatches { get; set; } = 5;
    public int MaxIterations { get; set; } = 100;
    public double Tolerance { get; set; } = 0.000001;
}
