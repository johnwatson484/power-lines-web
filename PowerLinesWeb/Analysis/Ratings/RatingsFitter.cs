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

        for (var iteration = 0; iteration < options.MaxIterations; iteration++)
        {
            if (Sweep(ratings, matches, weights, options) < options.Tolerance)
            {
                break;
            }
        }

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

    // Blocks are maximised one at a time, each using the values the previous block has just produced.
    // Updating attack and defence together instead leaves their product only marginally stable, which
    // resonates with the home advantage update and sends the fit to zero.
    private static double Sweep(TeamRatings ratings, IReadOnlyList<Result> matches, double[] weights, ModelOptions options)
    {
        var change = UpdateAttack(ratings, matches, weights, options);
        change = Math.Max(change, UpdateDefence(ratings, matches, weights, options));
        change = Math.Max(change, UpdateHomeAdvantage(ratings, matches, weights));

        Normalise(ratings);
        return change;
    }

    private static double UpdateAttack(TeamRatings ratings, IReadOnlyList<Result> matches, double[] weights, ModelOptions options)
    {
        var scored = new Dictionary<string, double>();
        var exposure = new Dictionary<string, double>();

        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var weight = weights[i];
            var home = ratings.Find(match.HomeTeam);
            var away = ratings.Find(match.AwayTeam);

            Add(scored, match.HomeTeam, weight * match.FullTimeHomeGoals);
            Add(scored, match.AwayTeam, weight * match.FullTimeAwayGoals);

            Add(exposure, match.HomeTeam, weight * ratings.AverageGoals * away.Defence * ratings.HomeAdvantage);
            Add(exposure, match.AwayTeam, weight * ratings.AverageGoals * home.Defence);
        }

        var prior = GetPrior(ratings, options);
        var change = 0d;

        foreach (var team in ratings.Teams)
        {
            var attack = (scored[team.Team] + prior) / (exposure[team.Team] + prior);
            change = Math.Max(change, Math.Abs(attack - team.Attack));
            team.Attack = attack;
        }

        return change;
    }

    private static double UpdateDefence(TeamRatings ratings, IReadOnlyList<Result> matches, double[] weights, ModelOptions options)
    {
        var conceded = new Dictionary<string, double>();
        var exposure = new Dictionary<string, double>();

        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var weight = weights[i];
            var home = ratings.Find(match.HomeTeam);
            var away = ratings.Find(match.AwayTeam);

            Add(conceded, match.HomeTeam, weight * match.FullTimeAwayGoals);
            Add(conceded, match.AwayTeam, weight * match.FullTimeHomeGoals);

            Add(exposure, match.HomeTeam, weight * ratings.AverageGoals * away.Attack);
            Add(exposure, match.AwayTeam, weight * ratings.AverageGoals * home.Attack * ratings.HomeAdvantage);
        }

        var prior = GetPrior(ratings, options);
        var change = 0d;

        foreach (var team in ratings.Teams)
        {
            var defence = (conceded[team.Team] + prior) / (exposure[team.Team] + prior);
            change = Math.Max(change, Math.Abs(defence - team.Defence));
            team.Defence = defence;
        }

        return change;
    }

    private static double UpdateHomeAdvantage(TeamRatings ratings, IReadOnlyList<Result> matches, double[] weights)
    {
        var homeGoals = 0d;
        var exposure = 0d;

        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var weight = weights[i];

            homeGoals += weight * match.FullTimeHomeGoals;
            exposure += weight * ratings.AverageGoals * ratings.Find(match.HomeTeam).Attack * ratings.Find(match.AwayTeam).Defence;
        }

        if (exposure <= 0)
        {
            return 0;
        }

        var homeAdvantage = homeGoals / exposure;
        var change = Math.Abs(homeAdvantage - ratings.HomeAdvantage);
        ratings.HomeAdvantage = homeAdvantage;

        return change;
    }

    // A gamma prior centred on average, so a team with little history regresses to the league rather
    // than to whatever its handful of matches happened to produce.
    private static double GetPrior(TeamRatings ratings, ModelOptions options)
    {
        return options.PriorMatches * ratings.AverageGoals;
    }

    // Attack and defence are only identified up to a common scale factor, so pin the mean attack to 1.
    private static void Normalise(TeamRatings ratings)
    {
        var mean = ratings.Teams.Average(x => x.Attack);

        if (mean <= 0)
        {
            return;
        }

        foreach (var team in ratings.Teams)
        {
            team.Attack /= mean;
            team.Defence *= mean;
        }
    }

    private static void Add(Dictionary<string, double> totals, string team, double value)
    {
        totals[team] = totals.GetValueOrDefault(team) + value;
    }
}
