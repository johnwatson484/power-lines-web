using PowerLinesWeb.Analysis;
using PowerLinesWeb.Analysis.Ratings;
using PowerLinesWeb.Data;

namespace PowerLinesWeb.Tests.Analysis.Ratings;

public class RatingsFitterTests
{
    // The league is sampled once and fitted once, because every recovery test asks the same question of it.
    static readonly Lazy<TeamRatings> syntheticFit = new(() =>
        RatingsFitter.Fit("E0", new DateTime(2026, 1, 1), SyntheticLeague.Build(seasons: 200), Options()));

    [Test]
    public void Fit_recovers_the_expected_goals_it_was_generated_from()
    {
        var ratings = syntheticFit.Value;
        var errors = new List<double>();

        foreach (var home in SyntheticLeague.Teams.Keys)
        {
            foreach (var away in SyntheticLeague.Teams.Keys.Where(x => x != home))
            {
                var expected = ratings.GetExpectedGoals(home, away);

                errors.Add(RelativeError(expected.Home, SyntheticLeague.GetHomeGoals(home, away)));
                errors.Add(RelativeError(expected.Away, SyntheticLeague.GetAwayGoals(home, away)));
            }
        }

        // Sampled goals carry their own noise, so the fit is judged on the spread of its errors rather
        // than on any single pairing.
        Assert.That(errors.Average(), Is.LessThan(0.02), "mean relative error");
        Assert.That(errors.Max(), Is.LessThan(0.06), "worst relative error");
    }

    [Test]
    public void Fit_recovers_the_home_advantage()
    {
        Assert.That(syntheticFit.Value.HomeAdvantage, Is.EqualTo(SyntheticLeague.HomeAdvantage).Within(2).Percent);
    }

    [Test]
    public void Fit_recovers_the_relative_attacking_strength_of_each_team()
    {
        foreach (var (team, expected) in SyntheticLeague.Teams)
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
    public void Fit_recovers_the_low_score_correlation_it_was_generated_from()
    {
        var matches = SyntheticLeague.Build(seasons: 200, correlation: -0.12);
        var ratings = RatingsFitter.Fit("E0", new DateTime(2026, 1, 1), matches, Options());

        Assert.That(ratings.LowScoreCorrelation, Is.EqualTo(-0.12).Within(0.03));
    }

    [Test]
    public void Fit_leaves_the_correlation_at_zero_for_independent_goals()
    {
        Assert.That(syntheticFit.Value.LowScoreCorrelation, Is.EqualTo(0).Within(0.02));
    }

    [Test]
    public void Fit_regresses_a_thin_sample_team_towards_the_league()
    {
        var matches = SyntheticLeague.Build(seasons: 20);
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
    public void Fit_favours_recent_form_when_a_half_life_is_set()
    {
        var matches = BuildDeclineLeague();
        var asOf = new DateTime(2026, 1, 1);

        var undecayed = RatingsFitter.Fit("E0", asOf, matches, Options()).Find("Faded").Attack;
        var decayed = RatingsFitter.Fit("E0", asOf, matches, Options(halfLifeDays: 365)).Find("Faded").Attack;

        // The team was strong five years ago and poor since, so decay should rate it lower.
        Assert.That(decayed, Is.LessThan(undecayed - 0.1));
    }

    [Test]
    public void Fit_returns_empty_ratings_when_there_is_no_history()
    {
        var ratings = RatingsFitter.Fit("E0", new DateTime(2026, 1, 1), [], Options());

        Assert.That(ratings.Teams, Is.Empty);
        Assert.That(ratings.AverageGoals, Is.EqualTo(0));
    }

    private static ModelOptions Options(double priorMatches = 5, double halfLifeDays = 0)
    {
        return new ModelOptions { PriorMatches = priorMatches, HalfLifeDays = halfLifeDays, MaxIterations = 200, Tolerance = 1e-10 };
    }

    private static double RelativeError(double actual, double expected)
    {
        return Math.Abs(actual - expected) / expected;
    }

    // A league where one team scores freely for a season and then dries up.
    private static List<Result> BuildDeclineLeague()
    {
        var matches = new List<Result>();
        var date = new DateTime(2021, 1, 1);

        for (var round = 0; round < 400; round++)
        {
            date = date.AddDays(4);
            var faded = round < 100;

            matches.Add(new Result
            {
                Division = "E0",
                Date = date,
                HomeTeam = "Faded",
                AwayTeam = round % 2 == 0 ? "Rival" : "Other",
                FullTimeHomeGoals = faded ? 4 : 0,
                FullTimeAwayGoals = faded ? 0 : 2
            });

            matches.Add(new Result
            {
                Division = "E0",
                Date = date,
                HomeTeam = "Rival",
                AwayTeam = "Other",
                FullTimeHomeGoals = 1,
                FullTimeAwayGoals = 1
            });
        }

        return matches;
    }
}
