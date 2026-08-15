using Microsoft.Extensions.Options;
using PowerLinesWeb.Extensions;

namespace PowerLinesWeb.Analysis;

public class OddsCalculator(int id, GoalDistribution goalDistribution, MarketOdds marketOdds, ThresholdOptions thresholdOptions, ModelOptions modelOptions, BettingOptions bettingOptions, ProbabilityCalibrator calibrator = null)
{
    readonly GoalDistribution goalDistribution = goalDistribution;
    readonly ThresholdOptions thresholdOptions = thresholdOptions;
    readonly ModelOptions modelOptions = modelOptions;
    readonly BettingOptions bettingOptions = bettingOptions;
    readonly ProbabilityCalibrator calibrator = calibrator ?? ProbabilityCalibrator.None;
    readonly AnalysisMatchOdds matchOdds = new AnalysisMatchOdds(id);
    MatchProbabilities probabilities = MatchProbabilities.None;

    public AnalysisMatchOdds GetMatchOdds()
    {
        CalculateProbabilities();
        CalculateResultOdds();
        CalculateScoreOdds();
        CalculateRecommendations();
        CalculateValue();
        return matchOdds;
    }

    private void CalculateProbabilities()
    {
        var raw = new MatchProbabilities(GetRawProbability('H'), GetRawProbability('D'), GetRawProbability('A'));
        probabilities = Calibrate(raw);
        matchOdds.HomeProbability = probabilities.Home;
        matchOdds.DrawProbability = probabilities.Draw;
        matchOdds.AwayProbability = probabilities.Away;
    }

    // Calibrating each result independently no longer leaves them summing to one, so they are rescaled back.
    private MatchProbabilities Calibrate(MatchProbabilities raw)
    {
        var home = calibrator.Calibrate(raw.Home);
        var draw = calibrator.Calibrate(raw.Draw);
        var away = calibrator.Calibrate(raw.Away);
        var total = home + draw + away;

        if (total <= 0)
        {
            return raw;
        }

        return new MatchProbabilities(home / total, draw / total, away / total);
    }

    private decimal ConvertProbabilityToOdds(decimal probability)
    {
        // A vanishing probability is an unbackable price, not a price of zero.
        if (probability <= 1 / modelOptions.MaxOdds)
        {
            return modelOptions.MaxOdds;
        }

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
        return probabilities.Get(result);
    }

    private decimal GetRawProbability(char result)
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

    // A recommendation says which result is most likely. Value says whether the price on offer is
    // longer than that likelihood justifies, which is the only thing that makes a bet profitable.
    private void CalculateValue()
    {
        var market = OddsConverter.RemoveMargin(marketOdds);

        if (!market.HasProbabilities)
        {
            return;
        }

        foreach (var result in new[] { 'H', 'D', 'A' })
        {
            var price = marketOdds.Get(result);

            if (price < bettingOptions.MinOdds || price > bettingOptions.MaxOdds)
            {
                continue;
            }

            var edge = GetResultProbability(result) - market.Get(result);

            if (edge < bettingOptions.MinEdge || edge <= matchOdds.ValueEdge)
            {
                continue;
            }

            matchOdds.ValueSelection = char.ToString(result);
            matchOdds.ValueEdge = edge;
            matchOdds.ValueOdds = price;
            matchOdds.ValueStake = GetStake(GetResultProbability(result), price);
        }
    }

    // Fractional Kelly, because a full Kelly stake on a probability this uncertain is reckless.
    private decimal GetStake(decimal probability, decimal price)
    {
        var profit = price - 1;
        var stake = (probability * price - 1) / profit;

        return stake <= 0 ? 0 : Math.Round(stake * bettingOptions.KellyFraction, 4);
    }
}
