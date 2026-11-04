using PowerLinesWeb.Analysis;
using PowerLinesWeb.Data;
using PowerLinesWeb.Extensions;

namespace PowerLinesWeb.Accuracy;

public static class AccuracyCalculator
{
    const int calibrationBuckets = 10;

    public static AccuracyRecord Calculate(string division, IReadOnlyList<Result> results)
    {
        var scored = results.Where(x => HasProbabilities(x) && x.FullTimeResult is "H" or "D" or "A").ToList();
        var strong = results.Where(x => x.ResultMatchOdds?.IsRecommended == true).ToList();
        var broader = results.Where(x => x.ResultMatchOdds?.IsLowerRecommended == true && !x.ResultMatchOdds.IsRecommended).ToList();
        var selected = strong.Concat(broader).ToList();
        var priced = scored.Where(x => GetMarketProbabilities(x).HasProbabilities).ToList();
        var valueBets = results.Where(x => x.ResultMatchOdds?.IsValue == true).ToList();

        return new AccuracyRecord
        {
            Division = division,
            Matches = results.Count,
            Recommended = strong.Count,
            RecommendedAccuracy = GetHitRate(strong, x => x.ResultMatchOdds.Recommended),
            LowerRecommended = broader.Count,
            LowerRecommendedAccuracy = GetHitRate(broader, x => x.ResultMatchOdds.LowerRecommended),

            // All-home baseline on exactly the selected fixtures.
            BaselineAccuracy = selected.Count == 0 ? null : Round((decimal)selected.Count(x => x.FullTimeResult == "H") / selected.Count),

            ScoredMatches = scored.Count,
            LogLoss = Round(Average(scored, x => Scoring.GetLogLoss(GetModelProbabilities(x), GetResult(x)))),
            BrierScore = Round(Average(scored, x => Scoring.GetBrierScore(GetModelProbabilities(x), GetResult(x)))),

            // Only matches carrying a price, so this is a like for like comparison against the market
            // rather than against the whole division.
            PricedMatches = priced.Count,
            PairedModelLogLoss = Round(Average(priced, x => Scoring.GetLogLoss(GetModelProbabilities(x), GetResult(x)))),
            PairedModelBrier = Round(Average(priced, x => Scoring.GetBrierScore(GetModelProbabilities(x), GetResult(x)))),
            InvalidRows = results.Count(x => x.ResultMatchOdds != null && (x.FullTimeResult is not ("H" or "D" or "A") || !HasProbabilities(x))),
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
        if (result.ResultMatchOdds == null) return MatchProbabilities.None;
        return new MatchProbabilities(
            result.ResultMatchOdds.HomeProbability,
            result.ResultMatchOdds.DrawProbability,
            result.ResultMatchOdds.AwayProbability);
    }

    private static MatchProbabilities GetMarketProbabilities(Result result)
    {
        if (result.PriceKind != "AveragePreClosing" || result.SourceFileHash == null) return MatchProbabilities.None;
        return OddsConverter.RemoveMargin(new MarketOdds(result.HomeOddsAverage, result.DrawOddsAverage, result.AwayOddsAverage));
    }

    private static char GetResult(Result result)
    {
        return string.IsNullOrEmpty(result.FullTimeResult) ? 'X' : result.FullTimeResult[0];
    }

    private static decimal? GetHitRate(IReadOnlyList<Result> results, Func<Result, string> selection)
    {
        var selected = results.Where(x => selection(x) is "H" or "A").ToList();
        return selected.Count == 0 ? null : Round((decimal)selected.Count(x => selection(x) == x.FullTimeResult) / selected.Count);
    }

    // Level stakes, so the return is not flattered by having staked more on the bets that happened to win.
    private static decimal? GetReturnOnInvestment(IReadOnlyList<Result> valueBets)
    {
        if (valueBets.Count == 0) return null;
        var profit = valueBets.Sum(x => x.ResultMatchOdds.ValueSelection == x.FullTimeResult
            ? x.ResultMatchOdds.ValueOdds - 1
            : -1);

        return Round(DecimalExtensions.SafeDivide(profit, valueBets.Count));
    }

    private static double? Average(IReadOnlyList<Result> results, Func<Result, double> score)
    {
        return results.Count == 0 ? null : results.Average(score);
    }

    private static decimal? Round(double? value) => value.HasValue ? Round((decimal)value.Value) : null;
    private static decimal Round(double value) => Round((decimal)value);

    private static decimal Round(decimal value)
    {
        return Math.Round(value, 4);
    }
}
