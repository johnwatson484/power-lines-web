using Microsoft.Extensions.Options;
using PowerLinesWeb.Accuracy;
using PowerLinesWeb.Analysis;
using PowerLinesWeb.Analysis.Ratings;
using PowerLinesWeb.Data;

namespace PowerLinesWeb.Tests.Analysis;

// Runs the whole pipeline, from fitted ratings through to priced probabilities, over matches the model
// was never trained on. This is the only test that answers the question the service exists to answer:
// are the published probabilities any good?
public class BacktestTests
{
    static readonly Lazy<Backtest> backtest = new(Run);

    [Test]
    public void The_model_forecasts_better_than_guessing()
    {
        // A uniform forecast scores ln(3).
        Assert.That(backtest.Value.LogLoss, Is.LessThan(Math.Log(3)));
    }

    [Test]
    public void The_model_forecasts_better_than_the_long_run_base_rates()
    {
        // Knowing only how often home, draw and away come up is the honest do nothing forecast.
        Assert.That(backtest.Value.LogLoss, Is.LessThan(backtest.Value.BaseRateLogLoss));
    }

    [Test]
    public void The_model_comes_close_to_the_forecast_that_generated_the_matches()
    {
        // No model can beat the true parameters, so this is the ceiling rather than a target.
        Assert.That(backtest.Value.LogLoss, Is.GreaterThan(backtest.Value.OracleLogLoss - 0.001));
        Assert.That(backtest.Value.LogLoss, Is.LessThan(backtest.Value.OracleLogLoss + 0.02));
    }

    [Test]
    public void The_published_probabilities_are_a_distribution()
    {
        foreach (var probabilities in backtest.Value.Probabilities)
        {
            Assert.That(probabilities.Home + probabilities.Draw + probabilities.Away, Is.EqualTo(1m).Within(0.0001m));
        }
    }

    private static Backtest Run()
    {
        var matches = SyntheticLeague.Build(seasons: 40, correlation: -0.12);
        var split = matches.Count * 4 / 5;
        var training = matches.Take(split).ToList();
        var holdout = matches.Skip(split).ToList();

        var asOf = holdout[0].Date;
        var ratings = RatingsFitter.Fit("E0", asOf, training, new ModelOptions { HalfLifeDays = 0 });
        var analysisService = BuildAnalysisService(ratings);

        var probabilities = new List<MatchProbabilities>();
        var logLoss = new List<double>();
        var oracleLogLoss = new List<double>();

        foreach (var match in holdout)
        {
            var odds = analysisService.GetMatchOdds(new AnalysisFixture
            {
                Id = 1,
                Division = match.Division,
                Date = match.Date,
                HomeTeam = match.HomeTeam,
                AwayTeam = match.AwayTeam
            });

            var forecast = new MatchProbabilities(odds.HomeProbability, odds.DrawProbability, odds.AwayProbability);
            probabilities.Add(forecast);
            logLoss.Add(Scoring.GetLogLoss(forecast, match.FullTimeResult[0]));
            oracleLogLoss.Add(Scoring.GetLogLoss(GetOracleProbabilities(match), match.FullTimeResult[0]));
        }

        var result = new Backtest(probabilities, logLoss.Average(), oracleLogLoss.Average(), GetBaseRateLogLoss(training, holdout));

        TestContext.Progress.WriteLine(
            "Backtest over {0} held out matches: log loss {1:n4}, base rates {2:n4}, true parameters {3:n4}",
            holdout.Count, result.LogLoss, result.BaseRateLogLoss, result.OracleLogLoss);

        return result;
    }

    private static AnalysisService BuildAnalysisService(TeamRatings ratings)
    {
        return new AnalysisService(
            new FixedRatingsProvider(ratings),
            new NoCalibrationProvider(),
            Options.Create(new ThresholdOptions { Higher = 0.7m, Lower = 0.65m }),
            Options.Create(new ModelOptions()),
            Options.Create(new BettingOptions()));
    }

    private static MatchProbabilities GetOracleProbabilities(Result match)
    {
        var expectedGoals = new ExpectedGoals(
            SyntheticLeague.GetHomeGoals(match.HomeTeam, match.AwayTeam),
            SyntheticLeague.GetAwayGoals(match.HomeTeam, match.AwayTeam));

        var distribution = new GoalDistribution();

        for (var goals = 0; goals <= 10; goals++)
        {
            distribution.HomeGoalProbabilities.Add(new GoalProbability(goals, (decimal)Poisson.GetProbability(goals, expectedGoals.Home)));
            distribution.AwayGoalProbabilities.Add(new GoalProbability(goals, (decimal)Poisson.GetProbability(goals, expectedGoals.Away)));
        }

        distribution.CalculateDistribution(new DixonColes(expectedGoals, -0.12));

        return new MatchProbabilities(
            GetProbability(distribution, 'H'),
            GetProbability(distribution, 'D'),
            GetProbability(distribution, 'A'));
    }

    private static decimal GetProbability(GoalDistribution distribution, char result)
    {
        return distribution.ScoreProbabilities.Where(x => x.Result == result).Sum(x => x.Probability);
    }

    private static double GetBaseRateLogLoss(List<Result> training, List<Result> holdout)
    {
        var baseRates = new MatchProbabilities(
            GetShare(training, "H"),
            GetShare(training, "D"),
            GetShare(training, "A"));

        return holdout.Average(x => Scoring.GetLogLoss(baseRates, x.FullTimeResult[0]));
    }

    private static decimal GetShare(List<Result> results, string result)
    {
        return (decimal)results.Count(x => x.FullTimeResult == result) / results.Count;
    }

    private record Backtest(List<MatchProbabilities> Probabilities, double LogLoss, double OracleLogLoss, double BaseRateLogLoss);

    private class FixedRatingsProvider(TeamRatings ratings) : IRatingsProvider
    {
        public TeamRatings Get(string division, DateTime asOf) => ratings;
    }

    private class NoCalibrationProvider : ICalibrationProvider
    {
        public ProbabilityCalibrator Get(string division) => ProbabilityCalibrator.None;
    }
}
