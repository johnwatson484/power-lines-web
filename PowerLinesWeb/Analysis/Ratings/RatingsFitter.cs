using PowerLinesWeb.Data;

namespace PowerLinesWeb.Analysis.Ratings;

// Maximum likelihood fit of a Maher/Dixon-Coles goals model by coordinate ascent. The updates below are
// the closed-form stationary points of the weighted Poisson log-likelihood, so each sweep is an exact
// maximisation of one parameter block given the others.
public static class RatingsFitter
{
    public static TeamRatings Fit(string division, DateTime asOf, IReadOnlyList<Result> matches, ModelOptions options)
    {
        var weights = matches.Select(x => GetWeight(x.Date, asOf, options.HalfLifeDays)).ToArray();
        var totalWeight = weights.Sum();

        var averageGoals = totalWeight > 0
            ? matches.Select((x, i) => weights[i] * (x.FullTimeHomeGoals + x.FullTimeAwayGoals)).Sum() / (2 * totalWeight)
            : 0;

        var ratings = new TeamRatings(division, asOf, averageGoals);
        SeedTeams(ratings, matches);

        if (averageGoals <= 0)
        {
            return ratings;
        }

        FitGoalRates(ratings, matches, weights, options);

        // The attack, defence and home advantage updates above do not involve the low score correction,
        // so it only has to be fitted once the goal rates have settled.
        FitLowScoreCorrelation(ratings, matches, weights, options);

        return ratings;
    }

    private static void FitLowScoreCorrelation(TeamRatings ratings, IReadOnlyList<Result> matches, double[] weights, ModelOptions options)
    {
        var lower = double.MinValue;
        var upper = double.MaxValue;
        var lowScoring = new List<(double Weight, int HomeGoals, int AwayGoals, ExpectedGoals Expected)>();

        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var expected = ratings.GetExpectedGoals(match.HomeTeam, match.AwayTeam);

            lower = Math.Max(lower, DixonColes.GetLowerBound(expected));
            upper = Math.Min(upper, DixonColes.GetUpperBound(expected));

            // Only the four lowest scorelines are adjusted, so every other match is a constant.
            if (match.FullTimeHomeGoals <= 1 && match.FullTimeAwayGoals <= 1)
            {
                lowScoring.Add((weights[i], match.FullTimeHomeGoals, match.FullTimeAwayGoals, expected));
            }
        }

        // Back away from the bounds so that no adjustment can reach zero and take the log with it.
        lower *= 0.99;
        upper *= 0.99;

        if (lowScoring.Count == 0 || lower >= upper)
        {
            return;
        }

        ratings.LowScoreCorrelation = Maximise(
            correlation => lowScoring.Sum(x => x.Weight * Math.Log(DixonColes.GetAdjustment(x.HomeGoals, x.AwayGoals, x.Expected, correlation))),
            lower,
            upper,
            options.Tolerance);
    }

    // Golden section search. The correction has a single parameter, so a bracketed one dimensional
    // search is enough and cannot step outside the range where the adjustments stay positive.
    private static double Maximise(Func<double, double> objective, double lower, double upper, double tolerance)
    {
        var ratio = (Math.Sqrt(5) - 1) / 2;
        var left = upper - ratio * (upper - lower);
        var right = lower + ratio * (upper - lower);
        var leftValue = objective(left);
        var rightValue = objective(right);

        while (upper - lower > tolerance)
        {
            if (leftValue < rightValue)
            {
                lower = left;
                (left, leftValue) = (right, rightValue);
                right = lower + ratio * (upper - lower);
                rightValue = objective(right);
            }
            else
            {
                upper = right;
                (right, rightValue) = (left, leftValue);
                left = upper - ratio * (upper - lower);
                leftValue = objective(left);
            }
        }

        return (lower + upper) / 2;
    }

    // Exponential decay so recent form counts for more than a squad that has since been rebuilt.
    private static double GetWeight(DateTime matchDate, DateTime asOf, double halfLifeDays)
    {
        if (halfLifeDays <= 0)
        {
            return 1;
        }

        var age = (asOf - matchDate).TotalDays;
        return Math.Pow(0.5, age / halfLifeDays);
    }

    private static void SeedTeams(TeamRatings ratings, IReadOnlyList<Result> matches)
    {
        foreach (var match in matches)
        {
            ratings.GetOrAdd(match.HomeTeam).HomeMatches++;
            ratings.GetOrAdd(match.AwayTeam).AwayMatches++;
        }
    }

    // Indexed arrays avoid per-match dictionary allocation/lookups in every optimization sweep.
    // Block ordering and penalized stationary updates match the corrected incumbent exactly.
    private static void FitGoalRates(TeamRatings ratings, IReadOnlyList<Result> matches, double[] weights, ModelOptions options)
    {
        var teams = ratings.Teams.ToArray();
        var indices = teams.Select((t, i) => (t.Team, i)).ToDictionary(x => x.Team, x => x.i);
        var home = matches.Select(x => indices[x.HomeTeam]).ToArray();
        var away = matches.Select(x => indices[x.AwayTeam]).ToArray();
        var attack = teams.Select(x => x.Attack).ToArray();
        var defence = teams.Select(x => x.Defence).ToArray();
        var scored = new double[teams.Length]; var conceded = new double[teams.Length];
        var exposure = new double[teams.Length];
        var weightedHomeGoals = 0d;
        for (var m = 0; m < matches.Count; m++)
        {
            var hg = weights[m] * matches[m].FullTimeHomeGoals;
            var ag = weights[m] * matches[m].FullTimeAwayGoals;
            scored[home[m]] += hg; scored[away[m]] += ag;
            conceded[home[m]] += ag; conceded[away[m]] += hg;
            weightedHomeGoals += hg;
        }
        var prior = options.PriorMatches * ratings.AverageGoals;
        for (var iteration = 0; iteration < options.MaxIterations; iteration++)
        {
            var change = 0d;
            Array.Clear(exposure);
            for (var m = 0; m < matches.Count; m++)
            {
                var w = weights[m] * ratings.AverageGoals;
                exposure[home[m]] += w * defence[away[m]] * ratings.HomeAdvantage;
                exposure[away[m]] += w * defence[home[m]];
            }
            for (var t = 0; t < teams.Length; t++)
            {
                var next = (scored[t] + prior) / (exposure[t] + prior);
                change = Math.Max(change, Math.Abs(next - attack[t])); attack[t] = next;
            }
            Array.Clear(exposure);
            for (var m = 0; m < matches.Count; m++)
            {
                var w = weights[m] * ratings.AverageGoals;
                exposure[home[m]] += w * attack[away[m]];
                exposure[away[m]] += w * attack[home[m]] * ratings.HomeAdvantage;
            }
            for (var t = 0; t < teams.Length; t++)
            {
                var next = (conceded[t] + prior) / (exposure[t] + prior);
                change = Math.Max(change, Math.Abs(next - defence[t])); defence[t] = next;
            }
            var homeExposure = 0d;
            for (var m = 0; m < matches.Count; m++) homeExposure += weights[m] * ratings.AverageGoals * attack[home[m]] * defence[away[m]];
            if (homeExposure > 0)
            {
                var next = weightedHomeGoals / homeExposure;
                change = Math.Max(change, Math.Abs(next - ratings.HomeAdvantage)); ratings.HomeAdvantage = next;
            }
            var mean = attack.Average();
            if (mean > 0) for (var t = 0; t < teams.Length; t++) { attack[t] /= mean; defence[t] *= mean; }
            if (change < options.Tolerance) break;
        }
        for (var t = 0; t < teams.Length; t++) { teams[t].Attack = attack[t]; teams[t].Defence = defence[t]; }
    }
}
