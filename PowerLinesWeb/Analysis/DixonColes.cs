namespace PowerLinesWeb.Analysis;

// Dixon and Coles (1997) low score correction. Independent Poisson understates draws and overstates
// the 1-0 and 0-1 scorelines, so the four lowest scoring cells are rescaled before the grid is
// normalised. A negative correlation is the usual fit, and it moves probability into 0-0 and 1-1.
public class DixonColes(ExpectedGoals expectedGoals, double correlation)
{
    // Leaves an independent Poisson grid untouched.
    public static DixonColes None { get; } = new(new ExpectedGoals(0, 0), 0);

    public double Correlation { get; } = correlation;

    public double GetAdjustment(int homeGoals, int awayGoals)
    {
        return GetAdjustment(homeGoals, awayGoals, expectedGoals, Correlation);
    }

    public static double GetAdjustment(int homeGoals, int awayGoals, ExpectedGoals expectedGoals, double correlation)
    {
        return (homeGoals, awayGoals) switch
        {
            (0, 0) => 1 - expectedGoals.Home * expectedGoals.Away * correlation,
            (0, 1) => 1 + expectedGoals.Home * correlation,
            (1, 0) => 1 + expectedGoals.Away * correlation,
            (1, 1) => 1 - correlation,
            _ => 1
        };
    }

    // Outside these bounds an adjustment turns negative, which would price a scoreline below zero.
    public static double GetLowerBound(ExpectedGoals expectedGoals)
    {
        return -Math.Min(SafeInverse(expectedGoals.Home), SafeInverse(expectedGoals.Away));
    }

    public static double GetUpperBound(ExpectedGoals expectedGoals)
    {
        return Math.Min(1, SafeInverse(expectedGoals.Home * expectedGoals.Away));
    }

    private static double SafeInverse(double value)
    {
        return value > 0 ? 1 / value : double.MaxValue;
    }
}
