using Microsoft.Extensions.Options;
using PowerLinesWeb.Analysis.Ratings;
using PowerLinesWeb.Forecasts;

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
        var cutoff = fixture.InformationCutoff ?? (fixture.HistoricalReplay ? fixture.Date.Date : DateTime.UtcNow);
        if (cutoff > fixture.Date) cutoff = fixture.Date;
        var ratings = ratingsProvider.Get(fixture.Division, cutoff);

        var supported = ratings.CanRate(fixture.HomeTeam, fixture.AwayTeam, modelOptions.MinTeamMatches);
        var total = ratings.HasTotalHistory(fixture.HomeTeam, fixture.AwayTeam, modelOptions.MinTeamMatches);
        if (ratings.AverageGoals <= 0 || (modelOptions.HistoryStrategy == "LegacyVenue" && !supported) ||
            (modelOptions.HistoryStrategy == "TotalHistory" && !total))
        {
            return null;
        }

        var expectedGoals = ratings.GetExpectedGoals(fixture.HomeTeam, fixture.AwayTeam);
        var goalDistribution = CalculateGoalDistribution(expectedGoals, new DixonColes(expectedGoals, modelOptions.UseRho ? ratings.LowScoreCorrelation : 0));
        var odds = new OddsCalculator(fixture.Id, goalDistribution, fixture.MarketOdds, thresholdOptions, modelOptions, bettingOptions, ProbabilityCalibrator.None, expectedGoals).GetMatchOdds();
        var artifact = modelOptions.EnableCalibration ? calibrationProvider.GetArtifact(ForecastVersions.Hash(modelOptions), cutoff) : null;
        var final = TemperatureScaling.Apply(new(odds.RawHomeProbability, odds.RawDrawProbability, odds.RawAwayProbability), artifact?.Temperature ?? 1);
        odds.HomeProbability = final.Home; odds.DrawProbability = final.Draw; odds.AwayProbability = final.Away;
        odds.Home = 1 / final.Home; odds.Draw = 1 / final.Draw; odds.Away = 1 / final.Away;
        odds.Support = supported ? "LegacyVenue" : total ? "TotalHistory" : "LowSupportPrior";
        odds.CalibrationVersion = artifact?.Version ?? "identity"; odds.CalibrationCutoff = artifact?.FitCutoff;
        var win = RecommendationPolicy.Win(final, thresholdOptions, supported || total);
        odds.Recommended = win.Tier == "Strong" ? win.Selection : "X";
        odds.LowerRecommended = win.Tier == "Broader" ? win.Selection : "X";
        var price = RecommendationPolicy.Price(final, fixture.MarketOdds, bettingOptions, cutoff, supported || total);
        odds.ValueSelection = price.Selection; odds.ValueEdge = price.MarketDisagreement;
        odds.ValueExpectedReturn = price.ExpectedReturn; odds.ValueOdds = price.Price; odds.ValueStake = price.Stake;
        odds.PriceReason = price.Reason;
        return odds;
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
