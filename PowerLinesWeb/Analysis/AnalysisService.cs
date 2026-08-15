using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PowerLinesWeb.Data;
using PowerLinesWeb.Extensions;

namespace PowerLinesWeb.Analysis;

public class AnalysisService(ApplicationDbContext dbContext, IOptions<ThresholdOptions> thresholdOptions, IOptions<ModelOptions> modelOptions) : IAnalysisService
{
    readonly ApplicationDbContext dbContext = dbContext;
    readonly ThresholdOptions thresholdOptions = thresholdOptions.Value;
    readonly ModelOptions modelOptions = modelOptions.Value;

    public AnalysisMatchOdds GetMatchOdds(AnalysisFixture fixture)
    {
        var matches = GetAnalysisMatches(fixture);

        if (!HasSufficientHistory(matches, fixture))
        {
            return null;
        }

        var expectedGoals = CalculateExpectedGoals(matches, fixture);
        var goalDistribution = CalculateGoalDistribution(expectedGoals);

        var oddsCalculator = new OddsCalculator(fixture.Id, goalDistribution, thresholdOptions, modelOptions);
        return oddsCalculator.GetMatchOdds();
    }

    private List<Result> GetAnalysisMatches(AnalysisFixture fixture)
    {
        // Strictly before the fixture, otherwise a backtested result is trained on its own outcome.
        var startDate = fixture.Date.AddYears(-modelOptions.YearsToAnalyse).Date;
        var endDate = fixture.Date.Date;

        return dbContext.Results.AsNoTracking()
            .Where(x => x.Division == fixture.Division && x.Date >= startDate && x.Date < endDate)
            .ToList();
    }

    private bool HasSufficientHistory(List<Result> matches, AnalysisFixture fixture)
    {
        var homeMatches = matches.Count(x => x.HomeTeam == fixture.HomeTeam);
        var awayMatches = matches.Count(x => x.AwayTeam == fixture.AwayTeam);

        return homeMatches >= modelOptions.MinTeamMatches && awayMatches >= modelOptions.MinTeamMatches;
    }

    private static ExpectedGoals CalculateExpectedGoals(List<Result> matches, AnalysisFixture fixture)
    {
        var totalAverageHomeGoals = DecimalExtensions.SafeDivide(matches.Sum(x => x.FullTimeHomeGoals), matches.Count);
        var totalAverageAwayGoals = DecimalExtensions.SafeDivide(matches.Sum(x => x.FullTimeAwayGoals), matches.Count);

        var homeMatches = matches.Where(x => x.HomeTeam == fixture.HomeTeam).ToList();
        var awayMatches = matches.Where(x => x.AwayTeam == fixture.AwayTeam).ToList();

        var homeAttackStrength = GetStrength(homeMatches.Sum(x => x.FullTimeHomeGoals), homeMatches.Count, totalAverageHomeGoals);
        var homeDefenceStrength = GetStrength(homeMatches.Sum(x => x.FullTimeAwayGoals), homeMatches.Count, totalAverageAwayGoals);
        var awayAttackStrength = GetStrength(awayMatches.Sum(x => x.FullTimeAwayGoals), awayMatches.Count, totalAverageAwayGoals);
        var awayDefenceStrength = GetStrength(awayMatches.Sum(x => x.FullTimeHomeGoals), awayMatches.Count, totalAverageHomeGoals);

        return new ExpectedGoals(
            homeAttackStrength * awayDefenceStrength * totalAverageHomeGoals,
            awayAttackStrength * homeDefenceStrength * totalAverageAwayGoals);
    }

    private static decimal GetStrength(int goals, int matchCount, decimal leagueAverage)
    {
        return DecimalExtensions.SafeDivide(DecimalExtensions.SafeDivide(goals, matchCount), leagueAverage);
    }

    private GoalDistribution CalculateGoalDistribution(ExpectedGoals expectedGoals)
    {
        var goalDistribution = new GoalDistribution();

        for (var goals = 0; goals <= modelOptions.MaxGoals; goals++)
        {
            goalDistribution.HomeGoalProbabilities.Add(GetGoalProbability(goals, expectedGoals.Home));
            goalDistribution.AwayGoalProbabilities.Add(GetGoalProbability(goals, expectedGoals.Away));
        }

        goalDistribution.CalculateDistribution();
        return goalDistribution;
    }

    private static GoalProbability GetGoalProbability(int goals, decimal expectedGoals)
    {
        return new GoalProbability(goals, (decimal)Poisson.GetProbability(goals, (double)expectedGoals));
    }
}
