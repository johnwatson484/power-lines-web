namespace PowerLinesWeb.Analysis;

public record MatchProbabilities(decimal Home, decimal Draw, decimal Away)
{
    public static MatchProbabilities None { get; } = new(0, 0, 0);

    public bool IsValid => Home >= 0 && Draw >= 0 && Away >= 0 && Home <= 1 && Draw <= 1 && Away <= 1
        && Math.Abs(Home + Draw + Away - 1) <= 0.00000001m;
    public bool HasProbabilities => IsValid;

    public decimal Get(char result)
    {
        return result switch
        {
            'H' => Home,
            'D' => Draw,
            'A' => Away,
            _ => 0
        };
    }
}
