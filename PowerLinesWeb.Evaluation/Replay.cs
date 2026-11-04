using System.IO.Compression;
using System.Text.Json;
using PowerLinesWeb.Analysis;
using PowerLinesWeb.Analysis.Ratings;
using PowerLinesWeb.Data;
using PowerLinesWeb.Forecasts;

namespace PowerLinesWeb.Evaluation;

public record QualifiedQuote(int Id, decimal Home, decimal Draw, decimal Away, string SourceUrl, string SourceFileHash, string PriceKind, DateTime? ObservedAt, bool StoredAverageMatched);
public record RawPrediction(int Id, string Division, DateTime Date, string Recipe, MatchProbabilities Raw, MatchProbabilities Independent,
    MatchProbabilities League, bool LegacyEligible, int HomeCount, int AwayCount, double HomeWeight, double AwayWeight,
    double HomeEffectiveN, double AwayEffectiveN, double? HomeAgeDays, double? AwayAgeDays, char Outcome);
public record Prediction(RawPrediction Input, string Candidate, MatchProbabilities? Probabilities, string Support, string Exclusion,
    string CalibrationVersion, DateTime CalibrationCutoff, decimal? BlendWeight = null, MatchProbabilities? RawOverride = null);

public static class Replay
{
    public static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = false };
    public static List<Result> ReadSnapshot(string path)
    {
        using var file = System.IO.File.OpenRead(path);
        using Stream data = path.EndsWith(".gz") ? new GZipStream(file, CompressionMode.Decompress) : file;
        using var reader = new StreamReader(data);
        var rows = new List<Result>();
        while (reader.ReadLine() is { } line)
        {
            using var row = JsonDocument.Parse(line);
            if (row.RootElement.GetProperty("Type").GetString() == "Result")
                rows.Add(row.RootElement.GetProperty("Data").Deserialize<Result>(Json));
        }
        return rows.OrderBy(x => x.Date).ThenBy(x => x.Division, StringComparer.Ordinal).ThenBy(x => x.ResultId).ToList();
    }

    public static List<RawPrediction> Generate(IReadOnlyList<Result> data, EvaluationManifest manifest, DateTime from, DateTime through, IReadOnlyList<Recipe> recipes)
    {
        var result = new System.Collections.Concurrent.ConcurrentBag<RawPrediction>();
        Parallel.ForEach(manifest.Divisions, new ParallelOptions { MaxDegreeOfParallelism = 4 }, division =>
        {
            var history = data.Where(x => x.Division == division && Valid(x)).ToList();
            foreach (var day in history.Where(x => x.Date >= from && x.Date <= through).GroupBy(x => x.Date.Date))
            {
                var train = history.Where(x => x.Date < day.Key && x.Date >= day.Key.AddYears(-6)).ToList();
                var league = new MatchProbabilities((train.Count(x => x.FullTimeResult == "H") + 1m) / (train.Count + 3), (train.Count(x => x.FullTimeResult == "D") + 1m) / (train.Count + 3), (train.Count(x => x.FullTimeResult == "A") + 1m) / (train.Count + 3));
                foreach (var recipe in recipes)
                {
                    var options = new ModelOptions { HalfLifeDays = recipe.HalfLifeDays, PriorMatches = recipe.PriorMatches };
                    var ratings = RatingsFitter.Fit(division, day.Key, train, options);
                    var support = train.SelectMany(x => new[] { (Team: x.HomeTeam, Date: x.Date), (Team: x.AwayTeam, Date: x.Date) }).GroupBy(x => x.Team).ToDictionary(g => g.Key, g =>
                    {
                        var weights = g.Select(x => Math.Pow(.5, (day.Key - x.Date).TotalDays / recipe.HalfLifeDays)).ToList();
                        return (Weight: weights.Sum(), Effective: weights.Sum() * weights.Sum() / weights.Sum(x => x * x), Age: (double?)(day.Key - g.Max(x => x.Date)).TotalDays);
                    });
                    foreach (var match in day)
                    {
                        var goals = ratings.GetExpectedGoals(match.HomeTeam, match.AwayTeam);
                        var raw = ratings.AverageGoals > 0 ? Goals(goals, ratings.LowScoreCorrelation) : league;
                        var independent = ratings.AverageGoals > 0 ? Goals(goals, 0) : league;
                        var h = ratings.Find(match.HomeTeam); var a = ratings.Find(match.AwayTeam);
                        var hs = support.GetValueOrDefault(match.HomeTeam); var ass = support.GetValueOrDefault(match.AwayTeam);
                        result.Add(new(match.ResultId, division, day.Key, recipe.Id, raw, independent, league, ratings.CanRate(match.HomeTeam, match.AwayTeam, 10),
                            (h?.HomeMatches ?? 0) + (h?.AwayMatches ?? 0), (a?.HomeMatches ?? 0) + (a?.AwayMatches ?? 0), hs.Weight, ass.Weight, hs.Effective, ass.Effective, hs.Age, ass.Age, match.FullTimeResult[0]));
                    }
                }
            }
            Console.WriteLine($"Raw forecasts completed: {division}, {from:yyyy-MM-dd}..{through:yyyy-MM-dd}");
        });
        return result.OrderBy(x => x.Date).ThenBy(x => x.Division, StringComparer.Ordinal).ThenBy(x => x.Id).ThenBy(x => x.Recipe, StringComparer.Ordinal).ToList();
    }

    public static bool Valid(Result r) => r.FullTimeHomeGoals >= 0 && r.FullTimeAwayGoals >= 0 && r.HomeTeam != r.AwayTeam &&
        r.FullTimeResult == (r.FullTimeHomeGoals > r.FullTimeAwayGoals ? "H" : r.FullTimeHomeGoals < r.FullTimeAwayGoals ? "A" : "D");

    public static MatchProbabilities Goals(ExpectedGoals goals, double rho)
    {
        if (!double.IsFinite(goals.Home) || !double.IsFinite(goals.Away) || goals.Home < 0 || goals.Away < 0) throw new InvalidOperationException("Invalid goal rates");
        var distribution = new GoalDistribution();
        for (var n = 0; n <= 10; n++)
        {
            distribution.HomeGoalProbabilities.Add(new(n, (decimal)Poisson.GetProbability(n, goals.Home)));
            distribution.AwayGoalProbabilities.Add(new(n, (decimal)Poisson.GetProbability(n, goals.Away)));
        }
        distribution.CalculateDistribution(new DixonColes(goals, rho));
        var p = new MatchProbabilities(distribution.ScoreProbabilities.Where(x => x.Result == 'H').Sum(x => x.Probability), distribution.ScoreProbabilities.Where(x => x.Result == 'D').Sum(x => x.Probability), distribution.ScoreProbabilities.Where(x => x.Result == 'A').Sum(x => x.Probability));
        if (!p.IsValid) throw new InvalidOperationException("Invalid H/D/A distribution");
        return p;
    }

    public static MatchProbabilities Blend(MatchProbabilities goals, MatchProbabilities? market, decimal alpha) => market == null ? goals : new(alpha * goals.Home + (1 - alpha) * market.Home, alpha * goals.Draw + (1 - alpha) * market.Draw, alpha * goals.Away + (1 - alpha) * market.Away);
    public static string Legacy(MatchProbabilities p) => p.Home > .65m && p.Home > p.Draw && p.Home > p.Away ? "H" : p.Away > .65m && p.Away > p.Draw && p.Away > p.Home ? "A" : "X";

    public static void WriteLines<T>(string path, IEnumerable<T> rows)
    {
        using var file = System.IO.File.Create(path); using var gzip = new GZipStream(file, CompressionLevel.SmallestSize); using var writer = new StreamWriter(gzip);
        foreach (var row in rows) writer.WriteLine(JsonSerializer.Serialize(row, Json));
    }
    public static List<T> ReadLines<T>(string path)
    {
        using var file = System.IO.File.OpenRead(path); using var gzip = new GZipStream(file, CompressionMode.Decompress); using var reader = new StreamReader(gzip);
        var rows = new List<T>(); while (reader.ReadLine() is { } line) rows.Add(JsonSerializer.Deserialize<T>(line, Json)); return rows;
    }
}
