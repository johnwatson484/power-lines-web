namespace PowerLinesWeb.Analysis;

public record MatchProbabilities(decimal Home, decimal Draw, decimal Away)
{
    public static MatchProbabilities None { get; } = new(0, 0, 0);

    public bool HasProbabilities => Home > 0 && Draw > 0 && Away > 0;

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
