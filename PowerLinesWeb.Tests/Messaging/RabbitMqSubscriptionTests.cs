using PowerLinesWeb.Tests.Infrastructure;
using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Npgsql;
using PowerLinesWeb.Data;
using PowerLinesWeb.Forecasts;
using PowerLinesWeb.Messaging;
using RabbitMQ.Client;

namespace PowerLinesWeb.Tests.Messaging;

[NonParallelizable]
public class RabbitMqSubscriptionTests : IntegrationFixture
{
    protected override bool NeedsRabbit => true;

    ConnectionFactory factory;
    IConnection admin;
    IModel publisher;
    ServiceProvider provider;
    string database;
    string scopedConnection;
    string schema;
    string exchange;
    string queue;
    readonly List<IConnection> connections = [];
    readonly List<RabbitMqSubscription> subscriptions = [];
    CancellationTokenSource stopping;

    [SetUp]
    public void SetUp()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        database = DatabaseConnection;
        var rabbit = RabbitConnection;
        var settings = new NpgsqlConnectionStringBuilder(database);
        if (!settings.Database.Contains("power_lines_test")) throw new InvalidOperationException("Integration DB must be named power_lines_test");

        schema = "ack_test_" + Guid.NewGuid().ToString("N");
        using (var db = new NpgsqlConnection(database))
        {
            db.Open();
            using var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", db);
            create.ExecuteNonQuery();
        }
        settings.SearchPath = schema;
        scopedConnection = settings.ConnectionString;
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(x => x.UseNpgsql(settings.ConnectionString,
            options => { options.EnableRetryOnFailure(); options.MigrationsHistoryTable("__EFMigrationsHistory", schema); }));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IngestionService>();
        provider = services.BuildServiceProvider();
        using (var scope = provider.CreateScope()) scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();

        factory = new ConnectionFactory { Uri = new Uri(rabbit), DispatchConsumersAsync = true, AutomaticRecoveryEnabled = false };
        admin = factory.CreateConnection();
        publisher = admin.CreateModel();
        publisher.ConfirmSelect();
        exchange = "ack-test-" + Guid.NewGuid().ToString("N");
        queue = exchange + "-web";
        stopping = new CancellationTokenSource();
    }

    [Test]
    public async Task Interrupted_before_save_retains_the_backlog_and_redelivers_every_result()
    {
        using var firstStop = new CancellationTokenSource();
        var started = Signal();
        var first = Subscribe(async (_, token) => { started.TrySetResult(); await Task.Delay(Timeout.Infinite, token); }, firstStop.Token);
        for (var i = 0; i < 20; i++) Publish(Result(i));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.That(publisher.MessageCount(queue), Is.EqualTo(19), "Only one delivery may leave the broker before a commit");
        Assert.That(ResultCount(), Is.Zero);

        firstStop.Cancel();
        connections[0].Abort();
        await first.Stopped.WaitAsync(TimeSpan.FromSeconds(10));
        await Until(() => publisher.MessageCount(queue) == 20);
        Subscribe(SaveResult);
        await Until(() => ResultCount() == 20 && publisher.MessageCount(queue) == 0);
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.That(db.Results.Select(x => x.SourceMatchId).Distinct().Count(), Is.EqualTo(20));
    }

    [Test]
    public async Task Interrupted_after_commit_before_ack_redelivers_without_duplicate_results_or_quotes()
    {
        using var firstStop = new CancellationTokenSource();
        var committed = Signal();
        Subscribe(async (message, token) =>
        {
            await SaveResult(message, token);
            committed.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
        }, firstStop.Token);
        Publish(Result(1));
        await committed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        DateTime created;
        using (var scope = provider.CreateScope()) created = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Results.Single().Created;
        firstStop.Cancel();
        connections[0].Abort();
        await Until(() => publisher.MessageCount(queue) == 1);
        var replayed = Signal();
        Subscribe(async (message, token) => { await SaveResult(message, token); replayed.TrySetResult(); });
        await replayed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        using var check = provider.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = db.Results.Single();
        Assert.That(saved.Created, Is.EqualTo(created));
        Assert.That(saved.Revision, Is.EqualTo(1));
        Assert.That(saved.ScoreRevision, Is.EqualTo(1));
        Assert.That(db.MarketQuotes.Count(), Is.EqualTo(2), "Primary and source-column quotes must not be duplicated");
    }

    [Test]
    public async Task Failed_database_handler_requeues_after_delay_and_can_commit_on_retry()
    {
        var attempts = 0;
        var firstFailure = Signal();
        var watch = new Stopwatch();
        Subscribe(async (message, token) =>
        {
            if (Interlocked.Increment(ref attempts) == 1)
            {
                watch.Start();
                firstFailure.TrySetResult();
                throw new InvalidOperationException("Simulated database failure");
            }
            await SaveResult(message, token);
        });
        Publish(Result(1));
        await firstFailure.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.That(ResultCount(), Is.Zero);
        await Until(() => ResultCount() == 1);
        Assert.That(attempts, Is.EqualTo(2));
        Assert.That(watch.Elapsed, Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(200)));
    }

    [Test]
    public async Task Invalid_payload_remains_recoverable_after_repeated_failures_and_shutdown()
    {
        var attempts = 0;
        Subscribe((message, token) => { Interlocked.Increment(ref attempts); return SaveResult(message, token); });
        Publish("{invalid json");
        await Until(() => Volatile.Read(ref attempts) >= 2);
        stopping.Cancel();
        connections[0].Abort();
        await Until(() => publisher.MessageCount(queue) == 1);
        Assert.That(ResultCount(), Is.Zero);
        var recovered = publisher.BasicGet(queue, autoAck: false);
        Assert.That(recovered, Is.Not.Null);
        Assert.That(recovered.Redelivered, Is.True);
        Assert.That(Encoding.UTF8.GetString(recovered.Body.Span), Is.EqualTo("{invalid json"));
        publisher.BasicNack(recovered.DeliveryTag, multiple: false, requeue: true);
    }

    [Test]
    public async Task Hosted_service_consumes_existing_durable_topology_and_all_three_message_types()
    {
        // Declare exactly the topology used by PowerLinesMessaging before starting the replacement.
        foreach (var (source, subscription) in new[] { (exchange, queue), (exchange + "-fixtures", queue + "-fixtures"), ("imports", "imports-web") })
        {
            publisher.ExchangeDeclare(source, ExchangeType.Fanout, durable: true, autoDelete: false);
            publisher.QueueDeclare(subscription, durable: true, exclusive: false, autoDelete: false);
            publisher.QueueBind(subscription, source, "");
        }
        var options = Options.Create(new MessageOptions
        {
            Host = factory.HostName, Port = factory.Port, Username = factory.UserName, Password = factory.Password,
            ResultQueue = exchange, ResultSubscription = queue, FixtureQueue = exchange + "-fixtures", FixtureSubscription = queue + "-fixtures"
        });
        const int records = 30;
        for (var row = 0; row < records; row++)
        {
            Publish(Result(row));
            Publish(new Fixture
            {
                Division = "E0", Date = new DateTime(2026, 11, 8).AddDays(row), HomeTeam = "Home", AwayTeam = "Away", SourceMatchId = $"fixture-{row}",
                SourceFileHash = "fixture-hash", SourceUrl = "fixture-source", HomeOddsAverage = 2, DrawOddsAverage = 3, AwayOddsAverage = 4,
                SourcePrices = [new("Avg", "AveragePreClosing", 2, 3, 4)]
            }, exchange + "-fixtures");
        }
        var status = new ImportStatus { SourceUrl = "test", Division = "E0", RowCount = 1, CompletedAt = DateTime.UtcNow, Status = "Completed" };
        Publish(status, "imports");
        var logger = new RecordingLogger();
        // Surface serialization errors rather than hiding them with database retries.
        using var hostProvider = new ServiceCollection()
            .AddDbContext<ApplicationDbContext>(x => x.UseNpgsql(scopedConnection))
            .AddSingleton(TimeProvider.System).AddScoped<IngestionService>().BuildServiceProvider();
        using var service = new MessageService(options, hostProvider.GetRequiredService<IServiceScopeFactory>(), logger);
        try
        {
            await service.StartAsync(CancellationToken.None);
            await Until(() =>
            {
                using var scope = provider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                return db.Results.Count() == records && db.Fixtures.Count() == records && db.ImportStatuses.Count() == 1;
            });
            // A repeat completion delivery must remain idempotent too.
            Publish(status, "imports");
            Publish(new ImportStatus { SourceUrl = "barrier", Division = "E0", RowCount = 1, CompletedAt = DateTime.UtcNow, Status = "Completed" }, "imports");
            await Until(() =>
            {
                using var scope = provider.CreateScope();
                return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().ImportStatuses.Count() == 2;
            });
            // Server cancellation must also restart subscriptions, without an app restart.
            publisher.QueueDelete(queue);
            publisher.QueueDeclare(queue, durable: true, exclusive: false, autoDelete: false);
            publisher.QueueBind(queue, exchange, "");
            Publish(Result(records));
            await Until(() => ResultCount() == records + 1);
            await service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
            using var check = provider.CreateScope();
            Assert.That(check.ServiceProvider.GetRequiredService<ApplicationDbContext>().ImportStatuses.Count(x => x.SourceUrl == "test"), Is.EqualTo(1));
            Assert.That(logger.Errors, Is.Empty);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
            publisher.QueueDelete("imports-web");
            publisher.ExchangeDelete("imports");
            publisher.QueueDelete(queue + "-fixtures");
            publisher.ExchangeDelete(exchange + "-fixtures");
        }
    }

    sealed class RecordingLogger : ILogger<MessageService>
    {
        public System.Collections.Concurrent.ConcurrentQueue<Exception> Errors { get; } = new();
        public IDisposable BeginScope<TState>(TState state) => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (level >= LogLevel.Error) Errors.Enqueue(exception);
        }
    }

    RabbitMqSubscription Subscribe(Func<string, CancellationToken, Task> process, CancellationToken? token = null)
    {
        var connection = factory.CreateConnection();
        connections.Add(connection);
        var subscription = new RabbitMqSubscription(connection, exchange, queue, process, NullLogger.Instance,
            TimeSpan.FromMilliseconds(200), token ?? stopping.Token);
        subscriptions.Add(subscription);
        return subscription;
    }

    Task SaveResult(string message, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IngestionService>().SaveResult(JsonConvert.DeserializeObject<Result>(message));
        return Task.CompletedTask;
    }

    int ResultCount()
    {
        using var scope = provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Results.Count();
    }

    static Result Result(int number) => new()
    {
        Division = "E0", Date = new DateTime(2026, 8, 1).AddDays(number), HomeTeam = "Home", AwayTeam = "Away",
        FullTimeHomeGoals = 1, FullTimeAwayGoals = 0, FullTimeResult = "H", SourceMatchId = $"match-{number}",
        SourceFileHash = "hash", SourceUrl = "test", PriceKind = "AveragePreClosing",
        HomeOddsAverage = 2, DrawOddsAverage = 3, AwayOddsAverage = 4,
        SourcePrices = [new("Avg", "AveragePreClosing", 2, 3, 4)]
    };

    void Publish(object message, string target = null)
    {
        var properties = publisher.CreateBasicProperties();
        properties.Persistent = true;
        publisher.BasicPublish(target ?? exchange, "", properties, Encoding.UTF8.GetBytes(message is string text ? text : JsonConvert.SerializeObject(message)));
        publisher.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5));
    }

    static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    static async Task Until(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!predicate()) await Task.Delay(25, timeout.Token);
    }

    [TearDown]
    public void TearDown()
    {
        stopping?.Cancel();
        foreach (var subscription in subscriptions) subscription.Dispose();
        subscriptions.Clear();
        foreach (var connection in connections) connection.Dispose();
        connections.Clear();
        if (publisher?.IsOpen == true) { publisher.QueueDelete(queue); publisher.ExchangeDelete(exchange); }
        publisher?.Dispose();
        admin?.Dispose();
        provider?.Dispose();
        stopping?.Dispose();
        if (schema != null)
        {
            using var db = new NpgsqlConnection(database);
            db.Open();
            using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", db);
            drop.ExecuteNonQuery();
        }
    }
}
