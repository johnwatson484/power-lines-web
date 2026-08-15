using PowerLinesWeb.Analysis;

namespace PowerLinesWeb.Tests.Analysis;

public class PoissonTests
{
    [TestCase(0, 1.5, 0.2231301601)]
    [TestCase(1, 1.5, 0.3346952402)]
    [TestCase(2, 2.5, 0.2565156207)]
    [TestCase(3, 0.8, 0.0383427383)]
    public void GetProbability_matches_the_poisson_mass_function(int goals, double expectedGoals, double expected)
    {
        var probability = Poisson.GetProbability(goals, expectedGoals);

        Assert.That(probability, Is.EqualTo(expected).Within(1e-9));
    }

    [Test]
    public void GetProbability_sums_to_one_over_the_full_support()
    {
        var total = Enumerable.Range(0, 40).Sum(goals => Poisson.GetProbability(goals, 2.5));

        Assert.That(total, Is.EqualTo(1).Within(1e-9));
    }

    [Test]
    public void GetProbability_stays_accurate_beyond_a_32_bit_factorial()
    {
        var probability = Poisson.GetProbability(15, 3);

        Assert.That(probability, Is.EqualTo(5.4630574e-7).Within(1e-14));
    }

    [Test]
    public void GetProbability_of_no_goals_is_certain_when_nothing_is_expected()
    {
        Assert.That(Poisson.GetProbability(0, 0), Is.EqualTo(1));
        Assert.That(Poisson.GetProbability(1, 0), Is.EqualTo(0));
    }
}
