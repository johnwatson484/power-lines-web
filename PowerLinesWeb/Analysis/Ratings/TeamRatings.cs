namespace PowerLinesWeb.Analysis.Ratings;

public class TeamRatings(string division, DateTime asOf, double averageGoals)
{
    private readonly Dictionary<string, TeamRating> teams = [];

    public string Division { get; } = division;
    public DateTime AsOf { get; } = asOf;
    public double AverageGoals { get; } = averageGoals;
    public double HomeAdvantage { get; set; } = 1;

    // Dixon-Coles rho, fitted across the whole division rather than per team.
    public double LowScoreCorrelation { get; set; }

    public IReadOnlyCollection<TeamRating> Teams => teams.Values;

    public TeamRating GetOrAdd(string team)
    {
        if (!teams.TryGetValue(team, out var rating))
        {
            rating = new TeamRating(team);
            teams.Add(team, rating);
        }

        return rating;
    }

    public TeamRating Find(string team)
    {
        return teams.GetValueOrDefault(team);
    }

    public bool CanRate(string homeTeam, string awayTeam, int minimumMatches)
    {
        var home = Find(homeTeam);
        var away = Find(awayTeam);

        return home != null && away != null
            && home.HomeMatches >= minimumMatches
            && away.AwayMatches >= minimumMatches;
    }

    public ExpectedGoals GetExpectedGoals(string homeTeam, string awayTeam)
    {
        var home = Find(homeTeam);
        var away = Find(awayTeam);

        return new ExpectedGoals(
            AverageGoals * home.Attack * away.Defence * HomeAdvantage,
            AverageGoals * away.Attack * home.Defence);
    }
}
