using System.Text.Json;
using PowerLinesWeb.Accuracy;
using PowerLinesWeb.Analysis;
using PowerLinesWeb.Data;
using PowerLinesWeb.Evaluation;
using PowerLinesWeb.Tests.Analysis;

namespace PowerLinesWeb.Tests.Forecasts;

public class PolicyAndReplayTests
{
    static readonly DateTime day = new(2025, 8, 1);
    static readonly ThresholdOptions thresholds = new();
    static MarketOdds Offer(decimal h = 2, decimal d = 4, decimal a = 4) => new(h, d, a) { Source = "Bookmaker", Kind = "AvailableOffer", Revision = "1", ObservedAt = day.AddMinutes(-1), IsAvailable = true };

    [TestCase(.70, .20, .10, "H", "Strong")]
    [TestCase(.65, .20, .15, "H", "Broader")]
    [TestCase(.15, .20, .65, "A", "Broader")]
    [TestCase(.30, .40, .30, "X", "None")]
    [TestCase(.50, 0, .50, "X", "None")]
    public void Tiers_are_disjoint_and_draw_probability_is_preserved(decimal h, decimal d, decimal a, string side, string tier)
    {
        var p = new MatchProbabilities(h, d, a); var decision = RecommendationPolicy.Win(p, thresholds);
        Assert.That(decision.Selection, Is.EqualTo(side)); Assert.That(decision.Tier, Is.EqualTo(tier));
        if (side != "X") { Assert.That(decision.Probability, Is.EqualTo(p.Get(side[0]))); Assert.That(decision.FairOdds, Is.EqualTo(1 / p.Get(side[0]))); }
    }

    [Test]
    public void No_history_cannot_be_a_strong_recommendation()
    {
        Assert.That(RecommendationPolicy.Win(new(.9m, .05m, .05m), thresholds, false).Selection, Is.EqualTo("X"));
    }

    [Test]
    public void Negative_and_zero_ev_are_rejected_even_with_large_market_disagreement()
    {
        var p = new MatchProbabilities(.60m, .35m, .05m);
        Assert.That(p.Home - OddsConverter.RemoveMargin(new(1.6m, 3, 3)).Home, Is.GreaterThan(.08m));
        Assert.That(RecommendationPolicy.Price(p, Offer(1.6m, 3, 3), new(), day).Selection, Is.EqualTo("X"));
        Assert.That(RecommendationPolicy.Price(new(.5m, .25m, .25m), Offer(), new(), day).Selection, Is.EqualTo("X"));
    }

    [Test]
    public void Quotes_need_provenance_availability_and_a_valid_observation_time()
    {
        var p = new MatchProbabilities(.8m, .1m, .1m); var quote = Offer();
        foreach (var invalid in new[] { new MarketOdds(2,4,4), quote with { Source = null }, quote with { Revision = null }, quote with { ObservedAt = null }, quote with { ObservedAt = day.AddMinutes(1) }, quote with { ObservedAt = day.AddHours(-2) }, quote with { IsAvailable = false }, quote with { Kind = "AveragePreClosing" }, quote with { Commission = 1 } })
            Assert.That(RecommendationPolicy.Price(p, invalid, new(), day).Selection, Is.EqualTo("X"));
        Assert.That(RecommendationPolicy.Price(p, quote, new(), day).Stake, Is.InRange(.000001m, .05m));
    }

    [Test]
    public void Commission_and_ev_floor_are_applied_before_selection()
    {
        var p = new MatchProbabilities(.6m, .2m, .2m);
        var decision = RecommendationPolicy.Price(p, Offer() with { Commission = .10m }, new(), day);
        Assert.That(decision.ExpectedReturn, Is.EqualTo(.14m));
        Assert.That(RecommendationPolicy.Price(p, Offer() with { Commission = .10m }, new() { MinExpectedReturn = .14m }, day).Selection, Is.EqualTo("X"));
        Assert.Throws<ArgumentOutOfRangeException>(() => RecommendationPolicy.Price(p, Offer(), new() { MinExpectedReturn = -.1m }, day));
    }

    [TestCase(.001, 30, -1)]
    [TestCase(40, .01, -2)]
    [TestCase(10, 12, 2)]
    public void Extreme_future_pairings_have_nonnegative_normalized_probabilities(double h, double a, double rho)
    {
        var p = Replay.Goals(new(h, a), rho);
        Assert.That(p.IsValid, Is.True);
        var correction = new DixonColes(new(h, a), rho);
        for (var x=0;x<=10;x++) for(var y=0;y<=10;y++) Assert.That(correction.GetAdjustment(x,y), Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void Same_day_and_future_results_cannot_change_an_earlier_replay()
    {
        var history = SyntheticLeague.Build(2); var target = history[^10].Date;
        var options = new EvaluationManifest { Divisions = ["E0"] };
        var recipes = new[] { new Recipe("goals365p5", 365, 5) };
        var before = Replay.Generate(history, options, target, target, recipes).Single();
        foreach(var row in history.Where(x=>x.Date>=target)) { row.FullTimeHomeGoals=9;row.FullTimeAwayGoals=0;row.FullTimeResult="H"; }
        var after = Replay.Generate(history, options, target, target, recipes).Single();
        Assert.That(after.Raw, Is.EqualTo(before.Raw));
        Assert.That(after.Independent, Is.EqualTo(before.Independent));
        Assert.That(after.HomeCount, Is.EqualTo(before.HomeCount));
    }

    [Test]
    public void Calibration_only_uses_earlier_raw_examples_of_the_same_recipe()
    {
        var raw = Enumerable.Range(0, 250).Select(i => new RawCalibrationExample(day.AddDays(-1), "a", new(.8m,.1m,.1m), i%2==0?'H':'A')).ToList();
        var temperature = TemperatureScaling.Fit(raw, "a", day, [.7,1,2]);
        raw.AddRange(Enumerable.Range(0,500).Select(i => new RawCalibrationExample(day, "a", new(.99m,.005m,.005m),'H')));
        raw.AddRange(Enumerable.Range(0,500).Select(i => new RawCalibrationExample(day.AddDays(-2), "b", new(.99m,.005m,.005m),'H')));
        Assert.That(TemperatureScaling.Fit(raw, "a", day, [.7,1,2]), Is.EqualTo(temperature));
        Assert.That(TemperatureScaling.Apply(new(.6m,.3m,.1m), temperature).IsValid, Is.True);
    }

    static Prediction Prediction(int id, bool priced=true) => new(new(id,"E0",day,"a",new(.6m,.3m,.1m),new(.6m,.3m,.1m),new(.4m,.3m,.3m),true,20,20,10,10,15,15,7,7,'H'),"a",new(.6m,.3m,.1m),"LegacyVenue","","identity",day.AddDays(-1));

    [Test]
    public void Paired_metrics_keep_identical_ids_and_empty_cohorts_are_unavailable()
    {
        var candidate = new List<Prediction> { Prediction(1) }; var market = new List<Prediction> { Prediction(1) with { Probabilities = new(.5m,.3m,.2m) } };
        var before = Metrics.Compare(candidate,market,484,100);
        candidate.Add(Prediction(2) with { Probabilities=new(.01m,.01m,.98m) });
        Assert.That(Metrics.Compare(candidate,market,484,100),Is.EqualTo(before));
        Assert.That(Metrics.Score([]).LogLoss, Is.Null); Assert.That(Metrics.Compare([],[],484,100).DeltaLogLoss, Is.Null);
        Assert.That(new MatchProbabilities(-.1m,.5m,.6m).IsValid,Is.False);
        Assert.Throws<ArgumentException>(() => Scoring.GetLogLoss(new(.6m,.3m,.1m), 'X'));
        Assert.Throws<ArgumentException>(() => Scoring.GetBrierScore(new(-.1m,.5m,.6m), 'H'));
    }

    [Test]
    public void Repeated_fixed_replays_are_identical_and_holdout_cannot_select_parameters()
    {
        var history = SyntheticLeague.Build(1); var start=history[^3].Date;
        var m = new EvaluationManifest { Divisions=["E0"],HoldoutFrom=start };
        var r = new[] {new Recipe("goals365p5",365,5)};
        var a=Replay.Generate(history,m,start,history[^1].Date,r);var b=Replay.Generate(history,m,start,history[^1].Date,r);
        Assert.That(JsonSerializer.Serialize(a),Is.EqualTo(JsonSerializer.Serialize(b)));
        Assert.Throws<InvalidOperationException>(()=>new Tournament(m,new Dictionary<int,QualifiedQuote>(),"hash").Freeze(a,Path.GetTempPath()));
    }
}
