using FlexLabs.EntityFrameworkCore.Upsert;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using PowerLinesMessaging;
using PowerLinesWeb.Data;

namespace PowerLinesWeb.Messaging;

public class MessageService(IOptions<MessageOptions> messageOptions, IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    private readonly MessageOptions messageOptions = messageOptions.Value;
    private readonly IServiceScopeFactory serviceScopeFactory = serviceScopeFactory;
    private Connection connection;
    private Consumer fixtureConsumer;
    private Consumer resultConsumer;

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        CreateConnection();
        CreateFixtureConsumer();
        CreateResultConsumer();
        return base.StartAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        fixtureConsumer.Listen(new Action<string>(ReceiveFixtureMessage));
        resultConsumer.Listen(new Action<string>(ReceiveResultMessage));
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        connection.CloseConnection();
    }

    protected void CreateConnection()
    {
        var options = new ConnectionOptions
        {
            Host = messageOptions.Host,
            Port = messageOptions.Port,
            Username = messageOptions.Username,
            Password = messageOptions.Password
        };
        connection = new Connection(options);
    }

    protected void CreateFixtureConsumer()
    {
        var options = new ConsumerOptions
        {
            Name = messageOptions.FixtureQueue,
            QueueName = messageOptions.FixtureQueue,
            SubscriptionQueueName = messageOptions.FixtureSubscription,
            QueueType = QueueType.ExchangeFanout
        };
        fixtureConsumer = connection.CreateConsumerChannel(options);
    }

    protected void CreateResultConsumer()
    {
        var options = new ConsumerOptions
        {
            Name = messageOptions.ResultQueue,
            QueueName = messageOptions.ResultQueue,
            SubscriptionQueueName = messageOptions.ResultSubscription,
            QueueType = QueueType.ExchangeFanout
        };
        resultConsumer = connection.CreateConsumerChannel(options);
    }

    private void ReceiveFixtureMessage(string message)
    {
        var fixture = JsonConvert.DeserializeObject<Fixture>(message);
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        try
        {
            // Upsert so a corrected re-import (e.g. updated odds) reaches fixtures already seen, not just new ones.
            dbContext.Fixtures.Upsert(fixture)
                .On(x => new { x.Date, x.HomeTeam, x.AwayTeam })
                .WhenMatched(x => new Fixture
                {
                    Division = fixture.Division,
                    HomeOddsAverage = fixture.HomeOddsAverage,
                    DrawOddsAverage = fixture.DrawOddsAverage,
                    AwayOddsAverage = fixture.AwayOddsAverage
                })
                .Run();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error saving fixture {0} v {1}: {2}", fixture.HomeTeam, fixture.AwayTeam, ex);
        }
    }

    private void ReceiveResultMessage(string message)
    {
        var result = JsonConvert.DeserializeObject<Result>(message);
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        try
        {
            // Upsert so a corrected re-import (e.g. updated odds) reaches results already seen, not just new ones.
            // Created is deliberately left out of WhenMatched so a re-import cannot reset it and stall the debounce
            // in ResultAnalysisBackgroundService, which waits for Created to stop changing before it backtests.
            dbContext.Results.Upsert(result)
                .On(x => new { x.Date, x.HomeTeam, x.AwayTeam })
                .WhenMatched(x => new Result
                {
                    Division = result.Division,
                    FullTimeHomeGoals = result.FullTimeHomeGoals,
                    FullTimeAwayGoals = result.FullTimeAwayGoals,
                    FullTimeResult = result.FullTimeResult,
                    HalfTimeHomeGoals = result.HalfTimeHomeGoals,
                    HalfTimeAwayGoals = result.HalfTimeAwayGoals,
                    HalfTimeResult = result.HalfTimeResult,
                    HomeOddsAverage = result.HomeOddsAverage,
                    DrawOddsAverage = result.DrawOddsAverage,
                    AwayOddsAverage = result.AwayOddsAverage
                })
                .Run();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error saving result {0} v {1} {2}: {3}", result.HomeTeam, result.AwayTeam, result.Date.Year, ex);
        }
    }
}
