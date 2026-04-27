using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PowerLinesWeb.Data;
using PowerLinesWeb.Extensions;

namespace PowerLinesWeb.Analysis;

public class AnalysisService : IAnalysisService
{
    readonly ApplicationDbContext dbContext;
    readonly ThresholdOptions thresholdOptions;
    const int yearsToAnalyse = 6;
    const int maxGoalsPerGame = 5;
    DateTime startDate;
    List<Result> matches;
    decimal homeExpectedGoals;
    decimal awayExpectedGoals;
    readonly GoalDistribution goalDistribution;
    readonly Poisson poisson;

    public AnalysisService(ApplicationDbContext dbContext, IOptions<ThresholdOptions> thresholdOptions)
    {
        this.dbContext = dbContext;
        this.thresholdOptions = thresholdOptions.Value;
        goalDistribution = new GoalDistribution();
        poisson = new Poisson();
    }

    public AnalysisMatchOdds GetMatchOdds(AnalysisFixture fixture)
    {
        SetStartDate(fixture.Date);
        SetAnalysisMatches(fixture.Division);

        CalculateExpectedGoals(fixture);
        CalculateGoalDistribution();

        var oddsCalculator = new OddsCalculator(fixture.Id, goalDistribution, thresholdOptions);
        return oddsCalculator.GetMatchOdds();
    }

    private void SetStartDate(DateTime fixtureDate)
    {
        startDate = fixtureDate.AddYears(-yearsToAnalyse).Date;
    }

    private void SetAnalysisMatches(string division)
    {
        matches = dbContext.Results.AsNoTracking().Where(x => x.Division == division && x.Date >= startDate).ToList();
    }

    private void CalculateExpectedGoals(AnalysisFixture fixture)
    {
        var totalAverageHomeGoals = GetTotalAverageHomeGoals();
        var totalAverageAwayGoals = GetTotalAverageAwayGoals();
        var totalAverageHomeConceded = totalAverageAwayGoals;
        var totalAverageAwayConceded = totalAverageHomeGoals;

        var averageHomeGoals = GetAverageHomeGoals(fixture.HomeTeam);
        var homeAttackStrength = GetAttackStrength(averageHomeGoals, totalAverageHomeGoals);
        var averageAwayConceded = GetAverageAwayConceded(fixture.AwayTeam);
        var awayDefenceStrength = GetDefenceStrength(averageAwayConceded, totalAverageAwayConceded);
        homeExpectedGoals = GetExpectedGoals(homeAttackStrength, awayDefenceStrength, totalAverageHomeGoals);

        var averageAwayGoals = GetAverageAwayGoals(fixture.AwayTeam);
        var awayAttackStrength = GetAttackStrength(averageAwayGoals, totalAverageAwayGoals);
        var averageHomeConceded = GetAverageHomeConceded(fixture.HomeTeam);
        var homeDefenceStrength = GetDefenceStrength(averageHomeConceded, totalAverageHomeConceded);
        awayExpectedGoals = GetExpectedGoals(awayAttackStrength, homeDefenceStrength, totalAverageAwayGoals);
    }

    private decimal GetTotalAverageHomeGoals()
    {
        return DecimalExtensions.SafeDivide(matches.Sum(x => x.FullTimeHomeGoals), matches.Count);
    }

    private decimal GetTotalAverageAwayGoals()
    {
        return DecimalExtensions.SafeDivide(matches.Sum(x => x.FullTimeAwayGoals), matches.Count);
    }

    private decimal GetAverageHomeGoals(string homeTeam)
    {
        var homeMatches = matches.Where(x => x.HomeTeam == homeTeam).ToList();
        return DecimalExtensions.SafeDivide(homeMatches.Sum(x => x.FullTimeHomeGoals), homeMatches.Count);
    }

    private decimal GetAverageAwayGoals(string awayTeam)
    {
        var awayMatches = matches.Where(x => x.AwayTeam == awayTeam).ToList();
        return DecimalExtensions.SafeDivide(awayMatches.Sum(x => x.FullTimeAwayGoals), awayMatches.Count);
    }

    private decimal GetAverageHomeConceded(string homeTeam)
    {
        var homeMatches = matches.Where(x => x.HomeTeam == homeTeam).ToList();
        return DecimalExtensions.SafeDivide(homeMatches.Sum(x => x.FullTimeAwayGoals), homeMatches.Count);
    }

    private decimal GetAverageAwayConceded(string awayTeam)
    {
        var awayMatches = matches.Where(x => x.AwayTeam == awayTeam).ToList();
        return DecimalExtensions.SafeDivide(awayMatches.Sum(x => x.FullTimeHomeGoals), awayMatches.Count);
    }

    private decimal GetAttackStrength(decimal averageGoals, decimal totalAverageGoals)
    {
        return DecimalExtensions.SafeDivide(averageGoals, totalAverageGoals);
    }

    private decimal GetDefenceStrength(decimal averageConceded, decimal totalAverageConceded)
    {
        return DecimalExtensions.SafeDivide(averageConceded, totalAverageConceded);
    }

    private decimal GetExpectedGoals(decimal teamAttackStrength, decimal oppositionDefenceStrength, decimal totalAverageGoals)
    {
        return teamAttackStrength * oppositionDefenceStrength * totalAverageGoals;
    }

    private void CalculateGoalDistribution()
    {
        for (int goals = 0; goals <= maxGoalsPerGame; goals++)
        {
            goalDistribution.HomeGoalProbabilities.Add(GetGoalProbability(goals, homeExpectedGoals));
            goalDistribution.AwayGoalProbabilities.Add(GetGoalProbability(goals, awayExpectedGoals));
        }

        goalDistribution.CalculateDistribution();
    }

    private GoalProbability GetGoalProbability(int goals, decimal expectedGoals)
    {
        return new GoalProbability(goals, (decimal)poisson.GetProbability(goals, (double)expectedGoals));
    }
}
