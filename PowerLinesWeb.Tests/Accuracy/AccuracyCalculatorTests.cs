using PowerLinesWeb.Accuracy;
using PowerLinesWeb.Data;

namespace PowerLinesWeb.Tests.Accuracy;

public class AccuracyCalculatorTests
{
    [Test]
    public void The_baseline_is_the_share_of_home_wins()
    {
        var results = new List<Result>
        {
            Build("H", "X", "X"),
            Build("H", "X", "X"),
            Build("D", "X", "X"),
            Build("A", "X", "X")
        };

        Assert.That(AccuracyCalculator.Calculate("E0", results).BaselineAccuracy, Is.EqualTo(0.5m));
    }

    [Test]
    public void Hit_rates_count_only_the_matches_that_were_recommended()
    {
        var results = new List<Result>
        {
            Build("H", "H", "H"),
            Build("A", "H", "H"),
            Build("D", "X", "D"),
            Build("H", "X", "X")
        };

        var accuracy = AccuracyCalculator.Calculate("E0", results);

        Assert.That(accuracy.Recommended, Is.EqualTo(2));
        Assert.That(accuracy.RecommendedAccuracy, Is.EqualTo(0.5m));
        Assert.That(accuracy.LowerRecommended, Is.EqualTo(3));
        Assert.That(accuracy.LowerRecommendedAccuracy, Is.EqualTo(0.6667m));
    }

    [Test]
    public void Matches_without_probabilities_are_left_out_of_the_scoring()
    {
        var results = new List<Result>
        {
            Build("H", "X", "X", homeProbability: 0.6m, drawProbability: 0.25m, awayProbability: 0.15m),
            Build("H", "X", "X")
        };

        var accuracy = AccuracyCalculator.Calculate("E0", results);

        Assert.That(accuracy.Matches, Is.EqualTo(2));
        Assert.That(accuracy.ScoredMatches, Is.EqualTo(1));
        Assert.That(accuracy.LogLoss, Is.EqualTo(Math.Round((decimal)-Math.Log(0.6), 4)));
    }

    [Test]
    public void The_market_is_scored_on_the_matches_that_carry_a_price()
    {
        var results = new List<Result>
        {
            Build("H", "X", "X", homeProbability: 0.6m, drawProbability: 0.25m, awayProbability: 0.15m, homeOdds: 2m, drawOdds: 4m, awayOdds: 4m),
            Build("H", "X", "X", homeProbability: 0.6m, drawProbability: 0.25m, awayProbability: 0.15m)
        };

        var accuracy = AccuracyCalculator.Calculate("E0", results);

        Assert.That(accuracy.ScoredMatches, Is.EqualTo(2));
        Assert.That(accuracy.PricedMatches, Is.EqualTo(1));
        Assert.That(accuracy.MarketLogLoss, Is.EqualTo(Math.Round((decimal)-Math.Log(0.5), 4)));
    }

    [Test]
    public void Value_returns_are_measured_at_level_stakes()
    {
        var results = new List<Result>
        {
            Value("H", selection: "H", odds: 3m),
            Value("A", selection: "H", odds: 3m),
            Value("A", selection: "H", odds: 3m)
        };

        var accuracy = AccuracyCalculator.Calculate("E0", results);

        Assert.That(accuracy.ValueBets, Is.EqualTo(3));
        Assert.That(accuracy.ValueWins, Is.EqualTo(1));
        Assert.That(accuracy.ValueRoi, Is.EqualTo(0m));
    }

    [Test]
    public void A_losing_run_of_value_bets_shows_a_negative_return()
    {
        var results = new List<Result> { Value("A", selection: "H", odds: 3m) };

        Assert.That(AccuracyCalculator.Calculate("E0", results).ValueRoi, Is.EqualTo(-1m));
    }

    [Test]
    public void Divisions_with_no_analysed_matches_do_not_divide_by_zero()
    {
        var accuracy = AccuracyCalculator.Calculate("E0", []);

        Assert.That(accuracy.Matches, Is.EqualTo(0));
        Assert.That(accuracy.RecommendedAccuracy, Is.EqualTo(0m));
        Assert.That(accuracy.BaselineAccuracy, Is.EqualTo(0m));
        Assert.That(accuracy.LogLoss, Is.EqualTo(0m));
        Assert.That(accuracy.ValueRoi, Is.EqualTo(0m));
    }

    [Test]
    public void Calibration_reports_the_observed_rate_in_each_band()
    {
        var results = Enumerable.Range(0, 10)
            .Select(x => Build(x < 6 ? "H" : "A", "X", "X", homeProbability: 0.65m, drawProbability: 0.2m, awayProbability: 0.15m))
            .ToList();

        var calibration = AccuracyCalculator.CalculateCalibration("E0", results);
        var band = calibration.Single(x => x.LowerBound == 0.6m);

        Assert.That(band.Predictions, Is.EqualTo(10));
        Assert.That(band.Predicted, Is.EqualTo(0.65m));
        Assert.That(band.Observed, Is.EqualTo(0.6m));
    }

    [Test]
    public void Calibration_covers_the_whole_probability_range()
    {
        var calibration = AccuracyCalculator.CalculateCalibration("E0", []);

        Assert.That(calibration, Has.Count.EqualTo(10));
        Assert.That(calibration.First().LowerBound, Is.EqualTo(0m));
        Assert.That(calibration.Last().UpperBound, Is.EqualTo(1m));
    }

    private static Result Build(string fullTimeResult, string recommended, string lowerRecommended,
        decimal homeProbability = 0, decimal drawProbability = 0, decimal awayProbability = 0,
        decimal homeOdds = 0, decimal drawOdds = 0, decimal awayOdds = 0)
    {
        return new Result
        {
            Division = "E0",
            FullTimeResult = fullTimeResult,
            HomeOddsAverage = homeOdds,
            DrawOddsAverage = drawOdds,
            AwayOddsAverage = awayOdds,
            ResultMatchOdds = new ResultMatchOdds
            {
                Recommended = recommended,
                LowerRecommended = lowerRecommended,
                ValueSelection = "X",
                HomeProbability = homeProbability,
                DrawProbability = drawProbability,
                AwayProbability = awayProbability
            }
        };
    }

    private static Result Value(string fullTimeResult, string selection, decimal odds)
    {
        var result = Build(fullTimeResult, "X", "X");
        result.ResultMatchOdds.ValueSelection = selection;
        result.ResultMatchOdds.ValueOdds = odds;
        return result;
    }
}
