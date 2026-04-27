namespace PowerLinesWeb.Extensions;

public static class DecimalExtensions
{
    public static decimal SafeDivide(decimal numerator, decimal denominator)
    {
        return denominator == 0 ? 0 : decimal.Divide(numerator, denominator);
    }
}
