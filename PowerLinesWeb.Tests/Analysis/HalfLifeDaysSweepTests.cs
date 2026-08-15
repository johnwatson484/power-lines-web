using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PowerLinesWeb.Accuracy;
using PowerLinesWeb.Analysis;
using PowerLinesWeb.Analysis.Ratings;
using PowerLinesWeb.Data;

namespace PowerLinesWeb.Tests.Analysis;

// Manual diagnostic, not part of CI: re-fits and re-scores the walk-forward backtest for a candidate
// HalfLifeDays, against real historical results. Calibration is deliberately bypassed so the comparison
// isolates the effect of decay tuning rather than being confounded by a calibration curve fitted under
// a different HalfLifeDays. Each candidate takes roughly 15-20 minutes against the full real dataset.
// Run with: dotnet test --filter FullyQualifiedName~HalfLifeDaysSweepTests
[TestFixture]
[Explicit("Requires a live Postgres with real historical results; ~15-20 minutes per candidate.")]
public class HalfLifeDaysSweepTests
{
    [TestCase(0.0)]
    [TestCase(180.0)]
    [TestCase(365.0)]
    [TestCase(730.0)]
    public void Sweep_HalfLifeDays_against_real_walk_forward_backtest(double halfLifeDays)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Server=localhost;Port=6000;Database=power_lines_web;User Id=postgres;Password=postgres;")
            .Options;

        using var dbContext = new ApplicationDbContext(options);

        var modelOptions = new ModelOptions { HalfLifeDays = halfLifeDays };
        var ratingsProvider = new RatingsProvider(dbContext, Options.Create(modelOptions));
        var analysisService = new AnalysisService(
            ratingsProvider,
            new NoCalibrationProvider(),
            Options.Create(new ThresholdOptions { Higher = 0.7m, Lower = 0.65m }),
            Options.Create(modelOptions),
            Options.Create(new BettingOptions()));

        var startDate = DateTime.UtcNow.Date.AddYears(-modelOptions.BacktestYears);
        var divisions = dbContext.Results.AsNoTracking().Select(x => x.Division).Distinct().ToList();

        var totalLogLoss = 0.0;
        var scored = 0;

        foreach (var division in divisions)
        {
            var results = dbContext.Results.AsNoTracking()
                .Where(x => x.Division == division && x.Date >= startDate && x.FullTimeResult != null)
                .OrderBy(x => x.Date)
                .ToList();

            foreach (var result in results)
            {
                var odds = analysisService.GetMatchOdds(new AnalysisFixture
                {
                    Id = result.ResultId,
                    Division = result.Division,
                    Date = result.Date,
                    HomeTeam = result.HomeTeam,
                    AwayTeam = result.AwayTeam
                });

                if (odds == null)
                {
                    continue;
                }

                var probabilities = new MatchProbabilities(odds.HomeProbability, odds.DrawProbability, odds.AwayProbability);
                totalLogLoss += Scoring.GetLogLoss(probabilities, result.FullTimeResult[0]);
                scored++;
            }
        }

        var averageLogLoss = scored == 0 ? 0 : totalLogLoss / scored;
        TestContext.Progress.WriteLine($"HalfLifeDays={halfLifeDays,-6} scored={scored,-6} logLoss={averageLogLoss:n4}");

        Assert.Pass();
    }

    private class NoCalibrationProvider : ICalibrationProvider
    {
        public ProbabilityCalibrator Get(string division) => ProbabilityCalibrator.None;
    }
}
