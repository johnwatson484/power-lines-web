using PowerLinesWeb.Analysis;
using PowerLinesWeb.Data;

namespace PowerLinesWeb.Tests.Analysis;

// A league with known attack, defence, home advantage and low score correlation, so a fitted model can
// be checked against the parameters the matches were actually generated from.
public static class SyntheticLeague
{
    public const double HomeAdvantage = 1.15;
    public const double Baseline = 1.35;

    public static IReadOnlyDictionary<string, (double Attack, double Defence)> Teams { get; } =
        new Dictionary<string, (double Attack, double Defence)>
        {
            ["Strong"] = (1.45, 0.70),
            ["Good"] = (1.20, 0.90),
            ["Average"] = (1.00, 1.00),
            ["Poor"] = (0.85, 1.15),
            ["Weak"] = (0.65, 1.40),
            ["Attacking"] = (1.35, 1.30),
            ["Defensive"] = (0.75, 0.65),
            ["Middling"] = (1.05, 0.95),
            ["Erratic"] = (0.95, 1.20),
            ["Dull"] = (0.75, 0.75)
        };

    public static double GetHomeGoals(string home, string away)
    {
        return Baseline * Teams[home].Attack * Teams[away].Defence * HomeAdvantage;
    }

    public static double GetAwayGoals(string home, string away)
    {
        return Baseline * Teams[away].Attack * Teams[home].Defence;
    }

    public static List<Result> Build(int seasons, double correlation = 0, int seed = 20260815)
    {
        var random = new Random(seed);
        var matches = new List<Result>();
        var date = new DateTime(1966, 8, 1);

        for (var season = 0; season < seasons; season++)
        {
            foreach (var home in Teams.Keys)
            {
                foreach (var away in Teams.Keys.Where(x => x != home))
                {
                    date = date.AddDays(1);
                    var expected = new ExpectedGoals(GetHomeGoals(home, away), GetAwayGoals(home, away));
                    var score = SampleScore(random, expected, correlation);

                    matches.Add(new Result
                    {
                        Division = "E0",
                        Date = date,
                        HomeTeam = home,
                        AwayTeam = away,
                        FullTimeHomeGoals = score.Home,
                        FullTimeAwayGoals = score.Away,
                        FullTimeResult = GetResult(score)
                    });
                }
            }
        }

        return matches;
    }

    private static string GetResult((int Home, int Away) score)
    {
        if (score.Home > score.Away)
        {
            return "H";
        }

        return score.Away > score.Home ? "A" : "D";
    }

    // Rejection sampling, so the sampled scores follow the corrected distribution exactly rather than
    // an approximation of it.
    private static (int Home, int Away) SampleScore(Random random, ExpectedGoals expectedGoals, double correlation)
    {
        var lowScorelines = new[] { (Home: 0, Away: 0), (Home: 0, Away: 1), (Home: 1, Away: 0), (Home: 1, Away: 1) };
        var ceiling = Math.Max(1, lowScorelines.Max(x => DixonColes.GetAdjustment(x.Home, x.Away, expectedGoals, correlation)));

        while (true)
        {
            var home = SamplePoisson(random, expectedGoals.Home);
            var away = SamplePoisson(random, expectedGoals.Away);

            if (random.NextDouble() * ceiling <= DixonColes.GetAdjustment(home, away, expectedGoals, correlation))
            {
                return (home, away);
            }
        }
    }

    private static int SamplePoisson(Random random, double expectedGoals)
    {
        var limit = Math.Exp(-expectedGoals);
        var goals = 0;
        var product = 1d;

        do
        {
            goals++;
            product *= random.NextDouble();
        }
        while (product > limit);

        return goals - 1;
    }
}
