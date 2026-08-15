using PowerLinesWeb.Analysis;

namespace PowerLinesWeb.Accuracy;

// Proper scoring rules. Unlike a hit rate these reward a forecast for being well calibrated rather
// than for being confident, so a model that says 60% and is right 60% of the time scores better than
// one that says 90% and is right 60% of the time.
public static class Scoring
{
    // Probabilities are floored before taking a log, so a result the model called impossible costs a
    // large but finite penalty instead of infinity.
    const decimal floor = 0.000001m;

    public static double GetLogLoss(MatchProbabilities probabilities, char result)
    {
        return -Math.Log((double)Math.Max(probabilities.Get(result), floor));
    }

    // Multiclass Brier score, the squared error summed over all three outcomes. Zero is perfect and
    // two is the worst possible.
    public static double GetBrierScore(MatchProbabilities probabilities, char result)
    {
        return GetSquaredError(probabilities.Home, result == 'H')
            + GetSquaredError(probabilities.Draw, result == 'D')
            + GetSquaredError(probabilities.Away, result == 'A');
    }

    private static double GetSquaredError(decimal probability, bool occurred)
    {
        var error = (double)probability - (occurred ? 1 : 0);
        return error * error;
    }
}
