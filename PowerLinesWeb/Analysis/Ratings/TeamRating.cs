namespace PowerLinesWeb.Analysis.Ratings;

public class TeamRating(string team)
{
    public string Team { get; } = team;
    public double Attack { get; set; } = 1;
    public double Defence { get; set; } = 1;
    public int HomeMatches { get; set; }
    public int AwayMatches { get; set; }
}
