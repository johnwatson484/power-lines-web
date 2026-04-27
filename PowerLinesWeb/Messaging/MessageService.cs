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
            dbContext.Fixtures.Add(fixture);
            dbContext.SaveChanges();
        }
        catch (DbUpdateException)
        {
            Console.WriteLine("{0} v {1} exists, skipping", fixture.HomeTeam, fixture.AwayTeam);
        }
    }

    private void ReceiveResultMessage(string message)
    {
        var result = JsonConvert.DeserializeObject<Result>(message);
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        try
        {
            dbContext.Results.Add(result);
            dbContext.SaveChanges();
        }
        catch (DbUpdateException)
        {
            Console.WriteLine("{0} v {1} {2} exists, skipping", result.HomeTeam, result.AwayTeam, result.Date.Year);
        }
    }
}
