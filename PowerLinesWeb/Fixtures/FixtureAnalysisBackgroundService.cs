using FlexLabs.EntityFrameworkCore.Upsert;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PowerLinesWeb.Analysis;
using PowerLinesWeb.Data;

namespace PowerLinesWeb.Fixtures;

public class FixtureAnalysisBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory serviceScopeFactory;
    private Timer timer;
    private readonly int frequencyInMinutes;

    public FixtureAnalysisBackgroundService(IServiceScopeFactory serviceScopeFactory, int frequencyInMinutes = 1)
    {
        this.serviceScopeFactory = serviceScopeFactory;
        this.frequencyInMinutes = frequencyInMinutes;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        timer = new Timer(GetMatchOdds, null, TimeSpan.Zero, TimeSpan.FromMinutes(frequencyInMinutes));
        return Task.CompletedTask;
    }

    protected void GetMatchOdds(object state)
    {
        var lastResultDate = GetLastResultDate();

        if (lastResultDate.HasValue)
        {
            CheckPendingFixtures(lastResultDate.Value);
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

    private void CheckPendingFixtures(DateTime lastResultDate)
    {
        List<Fixture> pendingFixtures;
        using (var scope = serviceScopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            pendingFixtures = dbContext.Fixtures.AsNoTracking()
                .Include(x => x.MatchOdds)
                .Where(x => x.Date >= DateTime.Today
                    && (x.MatchOdds == null || x.MatchOdds.Calculated < lastResultDate))
                .ToList();
        }

        if (pendingFixtures.Count > 0)
        {
            AnalyseFixtures(pendingFixtures);
        }
    }

    private void AnalyseFixtures(List<Fixture> fixtures)
    {
        foreach (var fixture in fixtures)
        {
            var analysisFixture = new AnalysisFixture
            {
                Id = fixture.FixtureId,
                Division = fixture.Division,
                Date = fixture.Date,
                HomeTeam = fixture.HomeTeam,
                AwayTeam = fixture.AwayTeam
            };

            using var scope = serviceScopeFactory.CreateScope();
            var analysisService = scope.ServiceProvider.GetRequiredService<IAnalysisService>();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var odds = analysisService.GetMatchOdds(analysisFixture);

            var matchOdds = new MatchOdds
            {
                FixtureId = odds.Id,
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

            dbContext.MatchOdds.Upsert(matchOdds)
                .On(x => new { x.FixtureId })
                .Run();
        }
    }
}
