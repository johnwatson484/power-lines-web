namespace PowerLinesWeb.Analysis.Ratings;

public interface IRatingsProvider
{
    TeamRatings Get(string division, DateTime asOf);
}
