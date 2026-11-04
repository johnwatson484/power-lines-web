namespace PowerLinesWeb.Data;

// Distinct source columns survive transport even when only the pre-closing average feeds the latest projection.
public record IncomingPrice(string Columns, string Kind, decimal Home, decimal Draw, decimal Away);
