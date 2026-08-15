using Microsoft.Extensions.Options;
using PowerLinesWeb.Analysis.Ratings;

namespace PowerLinesWeb.Analysis;

public class AnalysisService(IRatingsProvider ratingsProvider, IOptions<ThresholdOptions> thresholdOptions, IOptions<ModelOptions> modelOptions) : IAnalysisService
{
    readonly IRatingsProvider ratingsProvider = ratingsProvider;
    readonly ThresholdOptions thresholdOptions = thresholdOptions.Value;
    readonly ModelOptions modelOptions = modelOptions.Value;

    public AnalysisMatchOdds GetMatchOdds(AnalysisFixture fixture)
    {
        var ratings = ratingsProvider.Get(fixture.Division, fixture.Date);

        if (!ratings.CanRate(fixture.HomeTeam, fixture.AwayTeam, modelOptions.MinTeamMatches))
        {
            return null;
        }

        var expectedGoals = ratings.GetExpectedGoals(fixture.HomeTeam, fixture.AwayTeam);
        var goalDistribution = CalculateGoalDistribution(expectedGoals, new DixonColes(expectedGoals, ratings.LowScoreCorrelation));

        var oddsCalculator = new OddsCalculator(fixture.Id, goalDistribution, thresholdOptions, modelOptions);
        return oddsCalculator.GetMatchOdds();
    }

    private GoalDistribution CalculateGoalDistribution(ExpectedGoals expectedGoals, DixonColes lowScoreCorrection)
    {
        var goalDistribution = new GoalDistribution();

        for (var goals = 0; goals <= modelOptions.MaxGoals; goals++)
        {
            goalDistribution.HomeGoalProbabilities.Add(GetGoalProbability(goals, expectedGoals.Home));
            goalDistribution.AwayGoalProbabilities.Add(GetGoalProbability(goals, expectedGoals.Away));
        }

        goalDistribution.CalculateDistribution(lowScoreCorrection);
        return goalDistribution;
    }

    private static GoalProbability GetGoalProbability(int goals, double expectedGoals)
    {
        return new GoalProbability(goals, (decimal)Poisson.GetProbability(goals, expectedGoals));
    }
}
