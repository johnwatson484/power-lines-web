using PowerLinesWeb.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using PowerLinesWeb.Data;
using PowerLinesWeb.Forecasts;

namespace PowerLinesWeb.Tests.Forecasts;

[NonParallelizable]
public class LegacyTableMigrationTests : IntegrationFixture
{
    const string previousMigration = "20261104203645_ProtectCompletedReports";
    static readonly string[] currentTables = ["fixtures", "results", "match_odds", "import_statuses",
        "market_quotes", "forecast_snapshots", "forecast_settlements", "calibration_artifacts", "evaluation_runs"];

    [Test]
    public void Upgrade_preserves_current_data_and_rollback_restores_only_the_legacy_schema()
    {
        var connection = DatabaseConnection;
        var settings = new NpgsqlConnectionStringBuilder(connection);
        if (!settings.Database.Contains("power_lines_test"))
            throw new InvalidOperationException("Integration DB must be named power_lines_test");

        // A private schema exercises the full migration chain without touching other test fixtures.
        // Npgsql reads this switch on first connection, before the context constructor runs.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        var schema = "legacy_migration_" + Guid.NewGuid().ToString("N");
        using var admin = new NpgsqlConnection(connection);
        admin.Open();
        using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin)) create.ExecuteNonQuery();
        try
        {
            settings.SearchPath = schema;
            using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(settings.ConnectionString, options => options.MigrationsHistoryTable("__EFMigrationsHistory", schema)).Options);
            var migrator = db.GetService<IMigrator>();
            migrator.Migrate(previousMigration);
            SeedCurrentData(db);
            SeedLegacyData(db);
            var before = currentTables.ToDictionary(table => table, table => ReadRows(db, table));
            var legacySchema = ReadLegacySchema(db);

            migrator.Migrate();
            Assert.That(ReadLegacySchema(db), Is.Empty);
            foreach (var table in currentTables)
                Assert.That(ReadRows(db, table), Is.EqualTo(before[table]), table);
            Assert.That(db.Database.GetPendingMigrations(), Is.Empty);
            Assert.That(db.Database.HasPendingModelChanges(), Is.False);

            migrator.Migrate(previousMigration);
            Assert.That(ReadLegacySchema(db), Is.EqualTo(legacySchema));
            foreach (var table in new[] { "accuracy", "accuracy_calibration", "result_match_odds" })
                Assert.That(ReadRows(db, table), Is.EqualTo("[]"), table);
            foreach (var table in currentTables)
                Assert.That(ReadRows(db, table), Is.EqualTo(before[table]), table);

            migrator.Migrate();
            Assert.That(ReadLegacySchema(db), Is.Empty);
        }
        finally
        {
            using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin);
            drop.ExecuteNonQuery();
        }
    }

#pragma warning disable EF1002 // Identifiers come only from the fixed table lists in this fixture.
    static string ReadRows(ApplicationDbContext db, string table) => db.Database.SqlQueryRaw<string>(
        $"SELECT COALESCE(jsonb_agg(to_jsonb(t) ORDER BY to_jsonb(t)::text), '[]'::jsonb)::text AS \"Value\" FROM \"{table}\" AS t").Single();
#pragma warning restore EF1002

    // Compare columns, indexes and foreign keys on rollback. Old diagnostic rows are not recoverable.
    static List<string> ReadLegacySchema(ApplicationDbContext db) => db.Database.SqlQueryRaw<string>("""
        SELECT concat(table_name, ':column:', column_name, ':', data_type, ':', is_nullable) AS "Value"
        FROM information_schema.columns
        WHERE table_schema = current_schema() AND table_name IN ('accuracy', 'accuracy_calibration', 'result_match_odds')
        UNION ALL
        SELECT concat(tablename, ':index:', indexdef) AS "Value" FROM pg_indexes
        WHERE schemaname = current_schema() AND tablename IN ('accuracy', 'accuracy_calibration', 'result_match_odds')
        UNION ALL
        SELECT concat(c.relname, ':foreign key:', pg_get_constraintdef(k.oid)) AS "Value"
        FROM pg_constraint k JOIN pg_class c ON c.oid = k.conrelid JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = current_schema() AND c.relname = 'result_match_odds' AND k.contype = 'f'
        ORDER BY "Value"
        """).ToList();

    static void SeedCurrentData(ApplicationDbContext db)
    {
        var day = new DateTime(2025, 8, 1);
        var fixture = new Fixture { Division = "E0", Date = day, HomeTeam = "Home", AwayTeam = "Away" };
        var result = new Result { Division = "E0", Date = day, HomeTeam = "Home", AwayTeam = "Away", FullTimeResult = "H", FullTimeHomeGoals = 1 };
        db.AddRange(fixture, result);
        db.SaveChanges();
        var snapshot = new ForecastSnapshot { RunId = "live:test", MatchIdentity = "fixture:1", FixtureId = fixture.FixtureId,
            MatchDate = day, ForecastAt = day.AddDays(-1), TrainingCutoff = day.AddDays(-1), FinalHome = .6m, FinalDraw = .3m, FinalAway = .1m };
        db.AddRange(snapshot,
            new MatchOdds { FixtureId = fixture.FixtureId, HomeProbability = .6m, DrawProbability = .3m, AwayProbability = .1m },
            new ImportStatus { SourceUrl = "test", Division = "E0", CompletedAt = day, Status = "Completed" },
            new MarketQuote { MatchIdentity = "fixture:1", Source = "test", Revision = "1", IngestedAt = day, Home = 2, Draw = 3, Away = 4 },
            new CalibrationArtifact { Version = "test", ModelVersion = "model", FitFrom = day.AddYears(-1), FitCutoff = day.AddDays(-1), Samples = 250 },
            new EvaluationRun { Id = "completed", Status = "Completed", TestFrom = day, TestThrough = day, CompletedAt = day, ReportJson = "{}" });
        db.SaveChanges();
        db.ForecastSettlements.Add(new() { ForecastSnapshotId = snapshot.Id, ResultId = result.ResultId, ResultRevision = 1, Outcome = "H", SettledAt = day });
        db.SaveChanges();
    }

    static void SeedLegacyData(ApplicationDbContext db) => db.Database.ExecuteSqlRaw("""
        INSERT INTO accuracy (division, matches, recommended, "lowerRecommended", calculated)
        VALUES ('E0', 1, 1, 0, '2025-08-01');
        INSERT INTO accuracy_calibration (division, "lowerBound", "upperBound", predicted, observed, predictions, calculated)
        VALUES ('E0', 0, 0.1, 0.05, 0, 1, '2025-08-01');
        INSERT INTO result_match_odds ("resultId", home, draw, away, "expectedHomeGoals", "expectedAwayGoals", "expectedGoals", calculated)
        SELECT "resultId", 2, 3, 4, 1, 0, 1, '2025-08-01' FROM results;
        """);
}
