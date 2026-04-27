using Microsoft.Extensions.Options;
using PowerLinesWeb.Extensions;

namespace PowerLinesWeb.Analysis;

public class OddsCalculator(int id, GoalDistribution goalDistribution, ThresholdOptions thresholdOptions)
{
    readonly GoalDistribution goalDistribution = goalDistribution;
    readonly ThresholdOptions thresholdOptions = thresholdOptions;
    readonly AnalysisMatchOdds matchOdds = new AnalysisMatchOdds(id);

    public AnalysisMatchOdds GetMatchOdds()
    {
        CalculateResultOdds();
        CalculateScoreOdds();
        CalculateRecommendations();
        return matchOdds;
    }

    private decimal ConvertProbabilityToOdds(decimal probability)
    {
        return Math.Round(DecimalExtensions.SafeDivide(1, probability), 2);
    }

    private void CalculateResultOdds()
    {
        matchOdds.Home = ConvertProbabilityToOdds(GetResultProbability('H'));
        matchOdds.Draw = ConvertProbabilityToOdds(GetResultProbability('D'));
        matchOdds.Away = ConvertProbabilityToOdds(GetResultProbability('A'));
    }

    private decimal GetResultProbability(char result)
    {
        return goalDistribution.ScoreProbabilities.Where(x => x.Result == result).Sum(x => x.Probability);
    }

    private void CalculateScoreOdds()
    {
        matchOdds.HomeGoals = goalDistribution.HomeGoalProbabilities.OrderByDescending(x => x.Probability).First().Goals;
        matchOdds.AwayGoals = goalDistribution.AwayGoalProbabilities.OrderByDescending(x => x.Probability).First().Goals;
        matchOdds.ExpectedGoals = ConvertProbabilityToOdds(GetExpectedGoalsProbability());
    }

    private decimal GetExpectedGoalsProbability()
    {
        return goalDistribution.ScoreProbabilities.First(x => x.HomeGoalProbability.Goals == matchOdds.HomeGoals
            && x.AwayGoalProbability.Goals == matchOdds.AwayGoals).Probability;
    }

    private void CalculateRecommendations()
    {
        var prediction = CalculatePrediction();
        var predictionProbability = GetResultProbability(prediction);
        if (predictionProbability > thresholdOptions.Higher)
        {
            matchOdds.Recommended = Char.ToString(prediction);
        }
        if (predictionProbability > thresholdOptions.Lower)
        {
            matchOdds.LowerRecommended = Char.ToString(prediction);
        }
    }

    private char CalculatePrediction()
    {
        var homeProbability = GetResultProbability('H');
        var drawProbability = GetResultProbability('D');
        var awayProbability = GetResultProbability('A');

        if (homeProbability > drawProbability && homeProbability > awayProbability)
        {
            return 'H';
        }
        if (drawProbability > homeProbability && drawProbability > awayProbability)
        {
            return 'D';
        }
        if (awayProbability > homeProbability && awayProbability > drawProbability)
        {
            return 'A';
        }
        return 'X';
    }
}
