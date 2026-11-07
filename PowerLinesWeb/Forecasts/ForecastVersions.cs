using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PowerLinesWeb.Forecasts;

public static class ForecastVersions
{
    public static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))).ToLowerInvariant();
}
