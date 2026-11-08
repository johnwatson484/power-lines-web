using PowerLinesWeb.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PowerLinesWeb.Data;
using PowerLinesWeb.Messaging;

namespace PowerLinesWeb.Tests.Messaging;

[NonParallelizable]
public class IngestionConcurrencyTests : IntegrationFixture
{
    string database;
    string schema;
    DbContextOptions<ApplicationDbContext> options;

    [SetUp]
    public void SetUp()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        database = DatabaseConnection;
        var settings = new NpgsqlConnectionStringBuilder(database);
        if (!settings.Database.Contains("power_lines_test")) throw new InvalidOperationException("Integration DB must be named power_lines_test");
        schema = "ingestion_concurrency_" + Guid.NewGuid().ToString("N");
        using (var admin = new NpgsqlConnection(database))
        {
            admin.Open();
            using var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin);
            create.ExecuteNonQuery();
        }
        settings.SearchPath = schema;
        // Disable retries: concurrent ingestion should not rely on repeated serialization failures.
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(settings.ConnectionString,
            x => x.MigrationsHistoryTable("__EFMigrationsHistory", schema)).Options;
        using var db = new ApplicationDbContext(options);
        db.Database.Migrate();
    }

    [Test]
    public async Task Concurrent_distinct_results_and_fixtures_commit_with_their_quotes()
    {
        await Parallel(8, worker =>
        {
            for (var row = 0; row < 10; row++)
            {
                var number = worker * 10 + row;
                using var db = new ApplicationDbContext(options);
                var ingestion = new IngestionService(db, TimeProvider.System);
                ingestion.SaveResult(Result(number));
                ingestion.SaveFixture(Fixture(number));
            }
        });
        using var check = new ApplicationDbContext(options);
        Assert.That(check.Results.Count(), Is.EqualTo(80));
        Assert.That(check.Fixtures.Count(), Is.EqualTo(80));
        Assert.That(check.MarketQuotes.Count(), Is.EqualTo(320));
    }

    [Test]
    public void Failed_quote_write_rolls_back_the_result_and_releases_the_ingestion_lock()
    {
        using (var db = new ApplicationDbContext(options))
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE market_quotes ADD CONSTRAINT reject_test_quote CHECK (\"SourceFileHash\" <> 'reject')");
            var incoming = Result(1);
            incoming.SourceFileHash = "reject";
            Assert.Throws<DbUpdateException>(() => new IngestionService(db, TimeProvider.System).SaveResult(incoming));
        }
        using var check = new ApplicationDbContext(options);
        Assert.That(check.Results.Count(), Is.Zero);
        Assert.That(check.MarketQuotes.Count(), Is.Zero);
        new IngestionService(check, TimeProvider.System).SaveResult(Result(1));
        Assert.That(check.Results.Count(), Is.EqualTo(1));
        Assert.That(check.MarketQuotes.Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task Concurrent_duplicate_deliveries_keep_one_result_and_one_quote_revision()
    {
        await Parallel(8, _ =>
        {
            using var db = new ApplicationDbContext(options);
            new IngestionService(db, TimeProvider.System).SaveResult(Result(1));
        });
        using var check = new ApplicationDbContext(options);
        var saved = check.Results.Single();
        Assert.That(saved.Revision, Is.EqualTo(1));
        Assert.That(saved.ScoreRevision, Is.EqualTo(1));
        Assert.That(check.MarketQuotes.Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task Concurrent_score_corrections_preserve_every_revision()
    {
        using (var db = new ApplicationDbContext(options)) new IngestionService(db, TimeProvider.System).SaveResult(Result(1));
        await Parallel(8, number =>
        {
            using var db = new ApplicationDbContext(options);
            var incoming = Result(1);
            incoming.FullTimeHomeGoals = number + 2;
            incoming.SourceFileHash = $"correction-{number}";
            new IngestionService(db, TimeProvider.System).SaveResult(incoming);
        });
        using var check = new ApplicationDbContext(options);
        var saved = check.Results.Single();
        Assert.That(saved.Revision, Is.EqualTo(9));
        Assert.That(saved.ScoreRevision, Is.EqualTo(9));
        Assert.That(check.MarketQuotes.Count(), Is.EqualTo(18));
    }

    [Test]
    public async Task Concurrent_fixture_rescheduling_preserves_identity_and_quote_history()
    {
        using (var db = new ApplicationDbContext(options)) new IngestionService(db, TimeProvider.System).SaveFixture(Fixture(1));
        await Parallel(8, number =>
        {
            using var db = new ApplicationDbContext(options);
            var incoming = Fixture(1);
            incoming.Date = incoming.Date.AddDays(number + 1);
            incoming.SourceFileHash = $"reschedule-{number}";
            new IngestionService(db, TimeProvider.System).SaveFixture(incoming);
        });
        using var check = new ApplicationDbContext(options);
        Assert.That(check.Fixtures.Single().Revision, Is.EqualTo(9));
        Assert.That(check.MarketQuotes.Count(), Is.EqualTo(18));
    }

    [Test]
    public async Task Concurrent_ambiguous_renames_still_require_explicit_resolution()
    {
        var rejected = 0;
        await Parallel(2, number =>
        {
            using var db = new ApplicationDbContext(options);
            var incoming = Result(1);
            incoming.SourceMatchId = $"identity-{number}";
            incoming.AwayTeam = $"Away-{number}";
            try { new IngestionService(db, TimeProvider.System).SaveResult(incoming); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Ambiguous renamed result")) { Interlocked.Increment(ref rejected); }
        });
        using var check = new ApplicationDbContext(options);
        Assert.That(rejected, Is.EqualTo(1));
        Assert.That(check.Results.Count(), Is.EqualTo(1));
        Assert.That(check.MarketQuotes.Count(), Is.EqualTo(2));
    }

    static async Task Parallel(int count, Action<int> action)
    {
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var workers = Enumerable.Range(0, count).Select(number => Task.Run(async () => { await start.Task; action(number); })).ToArray();
        start.SetResult();
        await Task.WhenAll(workers).WaitAsync(TimeSpan.FromSeconds(30));
    }

    static Result Result(int number) => new()
    {
        Division = number % 2 == 0 ? "E0" : "D1", Date = new DateTime(2026, 8, 1).AddDays(number), HomeTeam = "Home", AwayTeam = "Away",
        FullTimeHomeGoals = 1, FullTimeAwayGoals = 0, FullTimeResult = "H", SourceMatchId = $"result-{number}",
        SourceFileHash = "hash", SourceUrl = "test", PriceKind = "AveragePreClosing",
        HomeOddsAverage = 2, DrawOddsAverage = 3, AwayOddsAverage = 4,
        SourcePrices = [new("Avg", "AveragePreClosing", 2, 3, 4)]
    };

    static Fixture Fixture(int number) => new()
    {
        Division = number % 2 == 0 ? "E0" : "D1", Date = new DateTime(2026, 11, 8).AddDays(number), HomeTeam = "Home", AwayTeam = "Away",
        SourceMatchId = $"fixture-{number}", SourceFileHash = "hash", SourceUrl = "test", PriceKind = "AveragePreClosing",
        HomeOddsAverage = 2, DrawOddsAverage = 3, AwayOddsAverage = 4,
        SourcePrices = [new("Avg", "AveragePreClosing", 2, 3, 4)]
    };

    [TearDown]
    public void TearDown()
    {
        if (schema == null) return;
        using var admin = new NpgsqlConnection(database);
        admin.Open();
        using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin);
        drop.ExecuteNonQuery();
    }
}
