namespace PowerLinesWeb.Analysis;

public class OddsCalculator(int id, GoalDistribution goalDistribution, MarketOdds marketOdds, ThresholdOptions thresholdOptions, ModelOptions modelOptions, BettingOptions bettingOptions, ExpectedGoals expectedGoals = null)
{
    readonly GoalDistribution goalDistribution = goalDistribution;
    readonly ThresholdOptions thresholdOptions = thresholdOptions;
    readonly ModelOptions modelOptions = modelOptions;
    readonly BettingOptions bettingOptions = bettingOptions;
    readonly ExpectedGoals expectedGoals = expectedGoals ?? new ExpectedGoals(0, 0);
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
        matchOdds.RawHomeProbability = raw.Home;
        matchOdds.RawDrawProbability = raw.Draw;
        matchOdds.RawAwayProbability = raw.Away;
        var total = raw.Home + raw.Draw + raw.Away;
        probabilities = total > 0 ? new(raw.Home / total, raw.Draw / total, raw.Away / total) : raw;
        matchOdds.HomeProbability = probabilities.Home;
        matchOdds.DrawProbability = probabilities.Draw;
        matchOdds.AwayProbability = probabilities.Away;
    }

    private decimal ConvertProbabilityToOdds(decimal probability)
    {
        // A zero probability is unbackable. Positive fair odds always use the exact reciprocal.
        if (probability <= 0)
        {
            return modelOptions.MaxOdds;
        }

        return 1 / probability;
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
        matchOdds.HomeXg = Math.Round((decimal)expectedGoals.Home, 2);
        matchOdds.AwayXg = Math.Round((decimal)expectedGoals.Away, 2);
        matchOdds.ExpectedGoals = matchOdds.HomeXg + matchOdds.AwayXg;
    }

    private void CalculateRecommendations()
    {
        var decision = RecommendationPolicy.Win(probabilities, thresholdOptions);
        matchOdds.Recommended = decision.Tier == "Strong" ? decision.Selection : "X";
        matchOdds.LowerRecommended = decision.Tier == "Broader" ? decision.Selection : "X";
    }

    private void CalculateValue()
    {
        var decision = RecommendationPolicy.Price(probabilities, marketOdds, bettingOptions, matchOdds.Calculated);
        matchOdds.ValueSelection = decision.Selection;
        matchOdds.ValueEdge = decision.MarketDisagreement;
        matchOdds.ValueExpectedReturn = decision.ExpectedReturn;
        matchOdds.ValueOdds = decision.Price;
        matchOdds.ValueStake = decision.Stake;
        matchOdds.PriceReason = decision.Reason;
    }
}
