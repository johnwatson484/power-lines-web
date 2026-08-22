using Microsoft.Extensions.Options;
using PowerLinesWeb.Analysis.Ratings;

namespace PowerLinesWeb.Analysis;

public class AnalysisService(IRatingsProvider ratingsProvider, ICalibrationProvider calibrationProvider, IOptions<ThresholdOptions> thresholdOptions, IOptions<ModelOptions> modelOptions, IOptions<BettingOptions> bettingOptions) : IAnalysisService
{
    readonly IRatingsProvider ratingsProvider = ratingsProvider;
    readonly ICalibrationProvider calibrationProvider = calibrationProvider;
    readonly ThresholdOptions thresholdOptions = thresholdOptions.Value;
    readonly ModelOptions modelOptions = modelOptions.Value;
    readonly BettingOptions bettingOptions = bettingOptions.Value;

    public AnalysisMatchOdds GetMatchOdds(AnalysisFixture fixture)
    {
        var ratings = ratingsProvider.Get(fixture.Division, fixture.Date);

        if (!ratings.CanRate(fixture.HomeTeam, fixture.AwayTeam, modelOptions.MinTeamMatches))
        {
            return null;
        }

        var expectedGoals = ratings.GetExpectedGoals(fixture.HomeTeam, fixture.AwayTeam);
        var goalDistribution = CalculateGoalDistribution(expectedGoals, new DixonColes(expectedGoals, ratings.LowScoreCorrelation));
        var calibrator = calibrationProvider.Get(fixture.Division);

        var oddsCalculator = new OddsCalculator(fixture.Id, goalDistribution, fixture.MarketOdds, thresholdOptions, modelOptions, bettingOptions, calibrator, expectedGoals);
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
