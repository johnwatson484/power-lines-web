using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RabbitMQ.Client;
using PowerLinesWeb.Data;

namespace PowerLinesWeb.Messaging;

public class MessageService(IOptions<MessageOptions> messageOptions, IServiceScopeFactory serviceScopeFactory, ILogger<MessageService> logger) : BackgroundService
{
    readonly MessageOptions messageOptions = messageOptions.Value;
    readonly IServiceScopeFactory serviceScopeFactory = serviceScopeFactory;
    readonly ILogger<MessageService> logger = logger;
    static readonly TimeSpan retryDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Connecting must not block host startup on .NET 8.
        await Task.Yield();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // This loop owns recovery, including initial connection and channel failures.
                // Do not also enable the client's automatic topology recovery.
                var factory = new ConnectionFactory
                {
                    HostName = messageOptions.Host, Port = messageOptions.Port,
                    UserName = messageOptions.Username, Password = messageOptions.Password,
                    DispatchConsumersAsync = true, ConsumerDispatchConcurrency = 1,
                    AutomaticRecoveryEnabled = false, TopologyRecoveryEnabled = false,
                    RequestedConnectionTimeout = TimeSpan.FromSeconds(10)
                };
                using var connection = factory.CreateConnection("power-lines-web");
                var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                connection.ConnectionShutdown += (_, _) => disconnected.TrySetResult();
                using var fixtures = Subscribe(connection, messageOptions.FixtureQueue, messageOptions.FixtureSubscription, ReceiveFixtureMessage, stoppingToken);
                using var results = Subscribe(connection, messageOptions.ResultQueue, messageOptions.ResultSubscription, ReceiveResultMessage, stoppingToken);
                using var imports = Subscribe(connection, "imports", "imports-web", ReceiveImportStatus, stoppingToken);
                logger.LogInformation("Consuming fixtures, results and import statuses with manual acknowledgements and prefetch 1");
                await Task.WhenAny(disconnected.Task, fixtures.Stopped, results.Stopped, imports.Stopped).WaitAsync(stoppingToken);
                logger.LogWarning("RabbitMQ connection or subscription closed; reconnecting after {Delay}", retryDelay);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "RabbitMQ consumer failed; reconnecting after {Delay}", retryDelay);
            }

            try { await Task.Delay(retryDelay, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    RabbitMqSubscription Subscribe(IConnection connection, string exchange, string queue, Action<string> receive, CancellationToken stoppingToken) =>
        new(connection, exchange, queue, (message, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            receive(message); // Ingestion returns only after its database transaction commits.
            return Task.CompletedTask;
        }, logger, retryDelay, stoppingToken);

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
