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
    private Consumer importConsumer;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Connecting here (rather than in StartAsync) keeps a slow or unreachable broker from delaying the host itself.
        CreateConnection();
        CreateFixtureConsumer();
        CreateResultConsumer();
        importConsumer = connection.CreateConsumerChannel(new ConsumerOptions { Name = "imports", QueueName = "imports", SubscriptionQueueName = "imports-web", QueueType = QueueType.ExchangeFanout });
        importConsumer.Listen(new Action<string>(ReceiveImportStatus));
        fixtureConsumer.Listen(new Action<string>(ReceiveFixtureMessage));
        resultConsumer.Listen(new Action<string>(ReceiveResultMessage));
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        connection?.CloseConnection();
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

    private void ReceiveImportStatus(string message)
    {
        var status = JsonConvert.DeserializeObject<PowerLinesWeb.Forecasts.ImportStatus>(message) ?? throw new FormatException("Empty import status");
        if (string.IsNullOrWhiteSpace(status.Division) || string.IsNullOrWhiteSpace(status.SourceUrl) || status.CompletedAt == default || status.Status != "Completed")
            throw new FormatException("Invalid import status");
        using var scope = serviceScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (!db.ImportStatuses.Any(x => x.SourceUrl == status.SourceUrl && x.Division == status.Division && x.CompletedAt == status.CompletedAt))
        { db.ImportStatuses.Add(status); db.SaveChanges(); }
    }

    private void ReceiveFixtureMessage(string message)
    {
        using var scope = serviceScopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<IngestionService>().SaveFixture(JsonConvert.DeserializeObject<Fixture>(message) ?? throw new FormatException("Empty fixture"));
    }

    private void ReceiveResultMessage(string message)
    {
        using var scope = serviceScopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<IngestionService>().SaveResult(JsonConvert.DeserializeObject<Result>(message) ?? throw new FormatException("Empty result"));
    }
}
