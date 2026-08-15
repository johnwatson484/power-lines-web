using PowerLinesWeb.Analysis;
using PowerLinesWeb.Data;
using PowerLinesWeb.Extensions;

namespace PowerLinesWeb.Accuracy;

public static class AccuracyCalculator
{
    const int calibrationBuckets = 10;

    public static AccuracyRecord Calculate(string division, IReadOnlyList<Result> results)
    {
        var scored = results.Where(HasProbabilities).ToList();
        var priced = scored.Where(x => GetMarketProbabilities(x).HasProbabilities).ToList();
        var valueBets = results.Where(x => x.ResultMatchOdds.IsValue).ToList();

        return new AccuracyRecord
        {
            Division = division,
            Matches = results.Count,
            Recommended = results.Count(x => x.ResultMatchOdds.IsRecommended),
            RecommendedAccuracy = GetHitRate(results, x => x.ResultMatchOdds.Recommended),
            LowerRecommended = results.Count(x => x.ResultMatchOdds.IsLowerRecommended),
            LowerRecommendedAccuracy = GetHitRate(results, x => x.ResultMatchOdds.LowerRecommended),

            // Backing the home side every time is the bar any set of recommendations has to clear.
            BaselineAccuracy = Round(DecimalExtensions.SafeDivide(results.Count(x => x.FullTimeResult == "H"), results.Count)),

            ScoredMatches = scored.Count,
            LogLoss = Round(Average(scored, x => Scoring.GetLogLoss(GetModelProbabilities(x), GetResult(x)))),
            BrierScore = Round(Average(scored, x => Scoring.GetBrierScore(GetModelProbabilities(x), GetResult(x)))),

            // Only matches carrying a price, so this is a like for like comparison against the market
            // rather than against the whole division.
            PricedMatches = priced.Count,
            MarketLogLoss = Round(Average(priced, x => Scoring.GetLogLoss(GetMarketProbabilities(x), GetResult(x)))),

            ValueBets = valueBets.Count,
            ValueWins = valueBets.Count(x => x.ResultMatchOdds.ValueSelection == x.FullTimeResult),
            ValueRoi = GetReturnOnInvestment(valueBets),
            Calculated = DateTime.UtcNow
        };
    }

    public static List<AccuracyCalibrationRecord> CalculateCalibration(string division, IReadOnlyList<Result> results)
    {
        var calculated = DateTime.UtcNow;
        var predictions = results.Where(HasProbabilities)
            .SelectMany(x => new[] { 'H', 'D', 'A' }.Select(result => new
            {
                Probability = GetModelProbabilities(x).Get(result),
                Occurred = GetResult(x) == result
            }))
            .ToList();

        var records = new List<AccuracyCalibrationRecord>();

        for (var bucket = 0; bucket < calibrationBuckets; bucket++)
        {
            var lowerBound = (decimal)bucket / calibrationBuckets;
            var upperBound = (decimal)(bucket + 1) / calibrationBuckets;

            // The last bucket has to include a probability of exactly one.
            var inBucket = predictions
                .Where(x => x.Probability >= lowerBound && (x.Probability < upperBound || bucket == calibrationBuckets - 1))
                .ToList();

            records.Add(new AccuracyCalibrationRecord
            {
                Division = division,
                LowerBound = lowerBound,
                UpperBound = upperBound,
                Predicted = Round(inBucket.Count == 0 ? 0 : (double)inBucket.Average(x => x.Probability)),
                Observed = Round(DecimalExtensions.SafeDivide(inBucket.Count(x => x.Occurred), inBucket.Count)),
                Predictions = inBucket.Count,
                Calculated = calculated
            });
        }

        return records;
    }

    private static bool HasProbabilities(Result result)
    {
        return GetModelProbabilities(result).HasProbabilities;
    }

    private static MatchProbabilities GetModelProbabilities(Result result)
    {
        return new MatchProbabilities(
            result.ResultMatchOdds.HomeProbability,
            result.ResultMatchOdds.DrawProbability,
            result.ResultMatchOdds.AwayProbability);
    }

    private static MatchProbabilities GetMarketProbabilities(Result result)
    {
        return OddsConverter.RemoveMargin(new MarketOdds(result.HomeOddsAverage, result.DrawOddsAverage, result.AwayOddsAverage));
    }

    private static char GetResult(Result result)
    {
        return string.IsNullOrEmpty(result.FullTimeResult) ? 'X' : result.FullTimeResult[0];
    }

    private static decimal GetHitRate(IReadOnlyList<Result> results, Func<Result, string> selection)
    {
        var selected = results.Where(x => selection(x) != "X").ToList();
        return Round(DecimalExtensions.SafeDivide(selected.Count(x => selection(x) == x.FullTimeResult), selected.Count));
    }

    // Level stakes, so the return is not flattered by having staked more on the bets that happened to win.
    private static decimal GetReturnOnInvestment(IReadOnlyList<Result> valueBets)
    {
        var profit = valueBets.Sum(x => x.ResultMatchOdds.ValueSelection == x.FullTimeResult
            ? x.ResultMatchOdds.ValueOdds - 1
            : -1);

        return Round(DecimalExtensions.SafeDivide(profit, valueBets.Count));
    }

    private static double Average(IReadOnlyList<Result> results, Func<Result, double> score)
    {
        return results.Count == 0 ? 0 : results.Average(score);
    }

    private static decimal Round(double value)
    {
        return Round((decimal)value);
    }

    private static decimal Round(decimal value)
    {
        return Math.Round(value, 4);
    }
}
