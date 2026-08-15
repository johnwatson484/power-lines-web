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

            dbContext.Accuracy.Upsert(AccuracyCalculator.Calculate(division, testResults))
                .On(x => new { x.Division })
                .Run();

            dbContext.AccuracyCalibration.UpsertRange(AccuracyCalculator.CalculateCalibration(division, testResults))
                .On(x => new { x.Division, x.LowerBound })
                .Run();
        }
    }
}
