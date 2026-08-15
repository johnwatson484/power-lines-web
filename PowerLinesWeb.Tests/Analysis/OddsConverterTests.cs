using PowerLinesWeb.Analysis;

namespace PowerLinesWeb.Tests.Analysis;

public class OddsConverterTests
{
    [Test]
    public void Removing_the_margin_leaves_probabilities_summing_to_one()
    {
        var probabilities = OddsConverter.RemoveMargin(new MarketOdds(2.10m, 3.40m, 3.60m));

        Assert.That(probabilities.Home + probabilities.Draw + probabilities.Away, Is.EqualTo(1m).Within(0.000001m));
    }

    [Test]
    public void Removing_the_margin_keeps_the_ranking_of_the_prices()
    {
        var probabilities = OddsConverter.RemoveMargin(new MarketOdds(2.10m, 3.40m, 3.60m));

        Assert.That(probabilities.Home, Is.GreaterThan(probabilities.Draw));
        Assert.That(probabilities.Draw, Is.GreaterThan(probabilities.Away));
    }

    [Test]
    public void Removing_the_margin_lowers_every_implied_probability()
    {
        var marketOdds = new MarketOdds(2.10m, 3.40m, 3.60m);
        var probabilities = OddsConverter.RemoveMargin(marketOdds);

        Assert.That(probabilities.Home, Is.LessThan(1 / marketOdds.Home));
        Assert.That(probabilities.Draw, Is.LessThan(1 / marketOdds.Draw));
        Assert.That(probabilities.Away, Is.LessThan(1 / marketOdds.Away));
    }

    [Test]
    public void A_fair_book_is_left_alone()
    {
        var probabilities = OddsConverter.RemoveMargin(new MarketOdds(2, 4, 4));

        Assert.That(probabilities.Home, Is.EqualTo(0.5m).Within(0.000001m));
        Assert.That(probabilities.Draw, Is.EqualTo(0.25m).Within(0.000001m));
        Assert.That(probabilities.Away, Is.EqualTo(0.25m).Within(0.000001m));
    }

    [TestCase(0, 0, 0)]
    [TestCase(2.10, 0, 3.60)]
    [TestCase(1, 3.40, 3.60)]
    [TestCase(2.10, 3.40, 1)]
    public void Missing_or_unpayable_prices_produce_no_probabilities(decimal home, decimal draw, decimal away)
    {
        var probabilities = OddsConverter.RemoveMargin(new MarketOdds(home, draw, away));

        Assert.That(probabilities.HasProbabilities, Is.False);
    }
}
