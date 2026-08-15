using Microsoft.EntityFrameworkCore;
using PowerLinesWeb.Analysis;
using PowerLinesWeb.Data;

namespace PowerLinesWeb.Tests.Analysis;

// Manual diagnostic, not part of CI: recomputes value-bet hit rate and ROI for a grid of MinEdge
// thresholds directly from already-backtested data, so tuning MinEdge needs no model refit.
// Run against a dev database populated by ResultAnalysisBackgroundService, e.g.:
//   dotnet test --filter FullyQualifiedName~MinEdgeSweepTests
[TestFixture]
[Explicit("Requires a live Postgres with real backtested result_match_odds data.")]
public class MinEdgeSweepTests
{
    private static readonly decimal[] Candidates = [0.03m, 0.05m, 0.08m, 0.10m, 0.15m, 0.20m];

    [Test]
    public void Sweep_MinEdge_against_real_backtested_results()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Server=localhost;Port=6000;Database=power_lines_web;User Id=postgres;Password=postgres;")
            .Options;

        using var dbContext = new ApplicationDbContext(options);

        var rows = (from result in dbContext.Results.AsNoTracking()
                    join odds in dbContext.ResultMatchOdds.AsNoTracking() on result.ResultId equals odds.ResultId
                    where result.HomeOddsAverage > 0
                    select new
                    {
                        result.FullTimeResult,
                        result.HomeOddsAverage,
                        result.DrawOddsAverage,
                        result.AwayOddsAverage,
                        odds.HomeProbability,
                        odds.DrawProbability,
                        odds.AwayProbability
                    }).ToList();

        TestContext.Progress.WriteLine($"Priced, backtested matches: {rows.Count}");
        TestContext.Progress.WriteLine("MinEdge  Bets   Wins   HitRate  ROI");

        foreach (var minEdge in Candidates)
        {
            int bets = 0, wins = 0;
            decimal profit = 0;

            foreach (var row in rows)
            {
                var marketOdds = new MarketOdds(row.HomeOddsAverage, row.DrawOddsAverage, row.AwayOddsAverage);
                var marketProbabilities = OddsConverter.RemoveMargin(marketOdds);

                if (!marketProbabilities.HasProbabilities)
                {
                    continue;
                }

                var best = new[]
                    {
                        (Selection: 'H', Price: marketOdds.Home, Edge: row.HomeProbability - marketProbabilities.Home),
                        (Selection: 'D', Price: marketOdds.Draw, Edge: row.DrawProbability - marketProbabilities.Draw),
                        (Selection: 'A', Price: marketOdds.Away, Edge: row.AwayProbability - marketProbabilities.Away)
                    }
                    .Where(x => x.Price is >= 1.2m and <= 10m && x.Edge >= minEdge)
                    .OrderByDescending(x => x.Edge)
                    .Select(x => (x.Selection, x.Price))
                    .Cast<(char Selection, decimal Price)?>()
                    .FirstOrDefault();

                if (best == null)
                {
                    continue;
                }

                bets++;

                if (best.Value.Selection.ToString() == row.FullTimeResult)
                {
                    wins++;
                    profit += best.Value.Price - 1;
                }
                else
                {
                    profit -= 1;
                }
            }

            var hitRate = bets == 0 ? 0 : (decimal)wins / bets;
            var roi = bets == 0 ? 0 : profit / bets;
            TestContext.Progress.WriteLine($"{minEdge,6:p0} {bets,6} {wins,6} {hitRate,8:p1} {roi,6:p1}");
        }

        Assert.Pass();
    }
}
