using PowerLinesWeb.Forecasts;

namespace PowerLinesWeb.Fixtures;

public class FixtureAnalysisBackgroundService(IServiceScopeFactory scopes) : BackgroundService
{
    readonly IServiceScopeFactory scopes = scopes;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        try
        {
            do
            {
                try { using var scope = scopes.CreateScope(); scope.ServiceProvider.GetRequiredService<FixtureProjectionJob>().Run(); }
                catch (Exception ex) { Console.Error.WriteLine($"Fixture analysis: {ex.Message}"); }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
