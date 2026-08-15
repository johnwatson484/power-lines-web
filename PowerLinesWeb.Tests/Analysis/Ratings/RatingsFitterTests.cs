using PowerLinesWeb.Analysis;
using PowerLinesWeb.Analysis.Ratings;
using PowerLinesWeb.Data;

namespace PowerLinesWeb.Tests.Analysis.Ratings;

public class RatingsFitterTests
{
    const double homeAdvantage = 1.15;
    const double baseline = 1.35;

    static readonly Dictionary<string, (double Attack, double Defence)> trueRatings = new()
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

    // The league is sampled once and fitted once, because every recovery test asks the same question of it.
    static readonly Lazy<TeamRatings> syntheticFit = new(() =>
        RatingsFitter.Fit("E0", new DateTime(2026, 1, 1), BuildSyntheticLeague(seasons: 200), Options()));

    [Test]
    public void Fit_recovers_the_expected_goals_it_was_generated_from()
    {
        var ratings = syntheticFit.Value;
        var errors = new List<double>();

        foreach (var home in trueRatings.Keys)
        {
            foreach (var away in trueRatings.Keys.Where(x => x != home))
            {
                var expected = ratings.GetExpectedGoals(home, away);

                errors.Add(RelativeError(expected.Home, TrueHomeGoals(home, away)));
                errors.Add(RelativeError(expected.Away, TrueAwayGoals(home, away)));
            }
        }

        // Sampled goals carry their own noise, so the fit is judged on the spread of its errors rather
        // than on any single pairing.
        Assert.That(errors.Average(), Is.LessThan(0.02), "mean relative error");
        Assert.That(errors.Max(), Is.LessThan(0.06), "worst relative error");
    }

    private static double RelativeError(double actual, double expected)
    {
        return Math.Abs(actual - expected) / expected;
    }

    [Test]
    public void Fit_recovers_the_home_advantage()
    {
        Assert.That(syntheticFit.Value.HomeAdvantage, Is.EqualTo(homeAdvantage).Within(2).Percent);
    }

    [Test]
    public void Fit_recovers_the_relative_attacking_strength_of_each_team()
    {
        foreach (var (team, expected) in trueRatings)
        {
            Assert.That(syntheticFit.Value.Find(team).Attack, Is.EqualTo(expected.Attack).Within(3).Percent, team);
        }
    }

    [Test]
    public void Fit_normalises_attack_to_an_average_of_one()
    {
        Assert.That(syntheticFit.Value.Teams.Average(x => x.Attack), Is.EqualTo(1).Within(0.0001));
    }

    [Test]
    public void Fit_regresses_a_thin_sample_team_towards_the_league()
    {
        var matches = BuildSyntheticLeague(seasons: 20);
        var newcomer = matches.Take(4).Select(x => new Result
        {
            Division = x.Division,
            Date = x.Date,
            HomeTeam = "Newcomer",
            AwayTeam = x.AwayTeam,
            FullTimeHomeGoals = 3,
            FullTimeAwayGoals = 0
        });

        List<Result> withNewcomer = [.. matches, .. newcomer];

        var shrunk = RatingsFitter.Fit("E0", new DateTime(2026, 1, 1), withNewcomer, Options()).Find("Newcomer").Attack;
        var unshrunk = RatingsFitter.Fit("E0", new DateTime(2026, 1, 1), withNewcomer, Options(priorMatches: 0)).Find("Newcomer").Attack;

        // Four wins is not enough evidence to take the raw maximum likelihood estimate at face value.
        Assert.That(unshrunk, Is.GreaterThan(1.5));
        Assert.That(shrunk, Is.GreaterThan(1).And.LessThan(unshrunk));
    }

    [Test]
    public void Fit_returns_empty_ratings_when_there_is_no_history()
    {
        var ratings = RatingsFitter.Fit("E0", new DateTime(2026, 1, 1), [], Options());

        Assert.That(ratings.Teams, Is.Empty);
        Assert.That(ratings.AverageGoals, Is.EqualTo(0));
    }

    private static ModelOptions Options(double priorMatches = 5)
    {
        return new ModelOptions { PriorMatches = priorMatches, HalfLifeDays = 0, MaxIterations = 200, Tolerance = 1e-10 };
    }

    private static double TrueHomeGoals(string home, string away)
    {
        return baseline * trueRatings[home].Attack * trueRatings[away].Defence * homeAdvantage;
    }

    private static double TrueAwayGoals(string home, string away)
    {
        return baseline * trueRatings[away].Attack * trueRatings[home].Defence;
    }

    private static List<Result> BuildSyntheticLeague(int seasons)
    {
        var random = new Random(20260815);
        var matches = new List<Result>();
        var date = new DateTime(1966, 8, 1);

        for (var season = 0; season < seasons; season++)
        {
            foreach (var home in trueRatings.Keys)
            {
                foreach (var away in trueRatings.Keys.Where(x => x != home))
                {
                    date = date.AddDays(1);
                    matches.Add(new Result
                    {
                        Division = "E0",
                        Date = date,
                        HomeTeam = home,
                        AwayTeam = away,
                        FullTimeHomeGoals = SamplePoisson(random, TrueHomeGoals(home, away)),
                        FullTimeAwayGoals = SamplePoisson(random, TrueAwayGoals(home, away))
                    });
                }
            }
        }

        return matches;
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
