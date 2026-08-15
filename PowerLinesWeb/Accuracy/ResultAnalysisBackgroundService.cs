using FlexLabs.EntityFrameworkCore.Upsert;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PowerLinesWeb.Analysis;
using PowerLinesWeb.Data;

namespace PowerLinesWeb.Accuracy;

public class ResultAnalysisBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly int frequencyInMinutes;

    public ResultAnalysisBackgroundService(IServiceScopeFactory serviceScopeFactory, int frequencyInMinutes = 60)
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
                GetMatchOdds();
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
        }
    }

    protected void GetMatchOdds()
    {
        try
        {
            var lastResultDate = GetLastResultDate();

            if (lastResultDate == null || lastResultDate.Value > DateTime.UtcNow.AddMinutes(-10))
            {
                return;
            }

            CheckPendingResults(lastResultDate.Value);
        }
        catch (Exception ex)
        {
            Console.WriteLine("ResultAnalysisBackgroundService error: {0}", ex.Message);
        }
    }

    private DateTime? GetLastResultDate()
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return dbContext.Results.AsNoTracking()
            .OrderByDescending(x => x.Created)
            .Select(x => (DateTime?)x.Created)
            .FirstOrDefault();
    }

    private void CheckPendingResults(DateTime lastResultDate)
    {
        List<Result> pendingResults;
        using (var scope = serviceScopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var modelOptions = scope.ServiceProvider.GetRequiredService<IOptions<ModelOptions>>().Value;
            var startDate = DateTime.UtcNow.Date.AddYears(-modelOptions.BacktestYears);

            pendingResults = dbContext.Results.AsNoTracking()
                .Include(x => x.ResultMatchOdds)
                .Where(x => x.Date >= startDate
                    && (x.ResultMatchOdds == null || x.ResultMatchOdds.Calculated < lastResultDate))
                .ToList();
        }

        if (pendingResults.Count > 0)
        {
            AnalyseResults(pendingResults);
        }
    }

    private void AnalyseResults(List<Result> results)
    {
        foreach (var result in results)
        {
            var analysisFixture = new AnalysisFixture
            {
                Id = result.ResultId,
                Division = result.Division,
                Date = result.Date,
                HomeTeam = result.HomeTeam,
                AwayTeam = result.AwayTeam
            };

            using var scope = serviceScopeFactory.CreateScope();
            var analysisService = scope.ServiceProvider.GetRequiredService<IAnalysisService>();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var odds = analysisService.GetMatchOdds(analysisFixture);

            if (odds == null)
            {
                continue;
            }

            var resultMatchOdds = new ResultMatchOdds
            {
                ResultId = odds.Id,
                Home = odds.Home,
                Draw = odds.Draw,
                Away = odds.Away,
                HomeGoals = odds.HomeGoals,
                AwayGoals = odds.AwayGoals,
                ExpectedGoals = odds.ExpectedGoals,
                Recommended = odds.Recommended,
                LowerRecommended = odds.LowerRecommended,
                Calculated = odds.Calculated
            };

            dbContext.ResultMatchOdds.Upsert(resultMatchOdds)
                .On(x => new { x.ResultId })
                .Run();
        }
    }
}
