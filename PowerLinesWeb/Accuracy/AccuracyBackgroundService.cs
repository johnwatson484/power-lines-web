using FlexLabs.EntityFrameworkCore.Upsert;
using Microsoft.EntityFrameworkCore;
using PowerLinesWeb.Data;
using PowerLinesWeb.Extensions;

namespace PowerLinesWeb.Accuracy;

public class AccuracyBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly int frequencyInMinutes;

    public AccuracyBackgroundService(IServiceScopeFactory serviceScopeFactory, int frequencyInMinutes = 5)
    {
        this.serviceScopeFactory = serviceScopeFactory;
        this.frequencyInMinutes = frequencyInMinutes;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // PeriodicTimer waits for each run to finish, so a slow cycle cannot overlap the next.
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(frequencyInMinutes));

        try
        {
            do
            {
                CalculateAccuracy();
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void CalculateAccuracy()
    {
        try
        {
            var lastOddsDate = GetLastOddsDate();

            if (lastOddsDate.HasValue)
            {
                CheckPendingAccuracy(lastOddsDate.Value);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("AccuracyBackgroundService error: {0}", ex.Message);
        }
    }

    private DateTime? GetLastOddsDate()
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return dbContext.ResultMatchOdds.AsNoTracking()
            .OrderByDescending(x => x.Calculated)
            .Select(x => (DateTime?)x.Calculated)
            .FirstOrDefault();
    }

    private void CheckPendingAccuracy(DateTime lastOddsDate)
    {
        var accuracyCalculatedDate = GetAccuracyCalculatedDate();
        CalculateAccuracyIfPending(lastOddsDate, accuracyCalculatedDate);
    }

    private DateTime? GetAccuracyCalculatedDate()
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return dbContext.Accuracy.AsNoTracking()
            .OrderByDescending(x => x.Calculated)
            .Select(x => (DateTime?)x.Calculated)
            .FirstOrDefault();
    }

    private void CalculateAccuracyIfPending(DateTime? lastOddsDate, DateTime? accuracyCalculatedDate)
    {
        if (!lastOddsDate.HasValue || lastOddsDate.Value > DateTime.UtcNow.AddMinutes(-5))
        {
            return;
        }
        if (!accuracyCalculatedDate.HasValue || accuracyCalculatedDate.Value < lastOddsDate.Value)
        {
            Calculate();
        }
    }

    private void Calculate()
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var divisions = dbContext.Results.AsNoTracking().Select(x => x.Division).Distinct().ToList();

        foreach (var division in divisions)
        {
            var testResults = dbContext.Results.AsNoTracking()
                .Include(x => x.ResultMatchOdds)
                .Where(x => x.Division == division && x.ResultMatchOdds != null)
                .ToList();

            var accuracy = new AccuracyRecord
            {
                Division = division,
                Matches = testResults.Count,
                Recommended = testResults.Count(x => x.ResultMatchOdds.IsRecommended),
                LowerRecommended = testResults.Count(x => x.ResultMatchOdds.IsLowerRecommended),
                Calculated = DateTime.UtcNow
            };

            var recommendedCount = accuracy.Recommended;
            var lowerRecommendedCount = accuracy.LowerRecommended;

            accuracy.RecommendedAccuracy = Math.Round(DecimalExtensions.SafeDivide(
                testResults.Count(x => x.ResultMatchOdds.Recommended == x.FullTimeResult), recommendedCount), 2);
            accuracy.LowerRecommendedAccuracy = Math.Round(DecimalExtensions.SafeDivide(
                testResults.Count(x => x.ResultMatchOdds.LowerRecommended == x.FullTimeResult), lowerRecommendedCount), 2);

            dbContext.Accuracy.Upsert(accuracy)
                .On(x => new { x.Division })
                .Run();
        }
    }
}
