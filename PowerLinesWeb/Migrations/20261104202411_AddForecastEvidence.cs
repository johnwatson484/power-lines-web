using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PowerLinesWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddForecastEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "halfTimeHomeGoals",
                table: "results",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "halfTimeAwayGoals",
                table: "results",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "PriceCollection",
                table: "results",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceKind",
                table: "results",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "QuoteObservedAt",
                table: "results",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Revision",
                table: "results",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "ScoreRevision",
                table: "results",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "SourceFileHash",
                table: "results",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceMatchId",
                table: "results",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceRowId",
                table: "results",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceSeason",
                table: "results",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                table: "results",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Updated",
                table: "results",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnalysisKey",
                table: "fixtures",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnalysisStatus",
                table: "fixtures",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Created",
                table: "fixtures",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LifecycleStatus",
                table: "fixtures",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceCollection",
                table: "fixtures",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceKind",
                table: "fixtures",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "QuoteObservedAt",
                table: "fixtures",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Revision",
                table: "fixtures",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "SourceFileHash",
                table: "fixtures",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceMatchId",
                table: "fixtures",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceRowId",
                table: "fixtures",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceSeason",
                table: "fixtures",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                table: "fixtures",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Updated",
                table: "fixtures",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "calibration_artifacts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Version = table.Column<string>(type: "text", nullable: true),
                    ModelVersion = table.Column<string>(type: "text", nullable: true),
                    FitFrom = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FitCutoff = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Samples = table.Column<int>(type: "integer", nullable: false),
                    Temperature = table.Column<double>(type: "double precision", nullable: false),
                    TrainingFingerprint = table.Column<string>(type: "text", nullable: true),
                    TrainingOrigin = table.Column<string>(type: "text", nullable: true),
                    Activated = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calibration_artifacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_runs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    DatasetHash = table.Column<string>(type: "text", nullable: true),
                    ManifestHash = table.Column<string>(type: "text", nullable: true),
                    CodeVersion = table.Column<string>(type: "text", nullable: true),
                    TestFrom = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TestThrough = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReportJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "forecast_settlements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ForecastSnapshotId = table.Column<long>(type: "bigint", nullable: false),
                    ResultId = table.Column<int>(type: "integer", nullable: false),
                    ResultRevision = table.Column<long>(type: "bigint", nullable: false),
                    Outcome = table.Column<string>(type: "text", nullable: true),
                    SettledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_forecast_settlements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "forecast_snapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunId = table.Column<string>(type: "text", nullable: true),
                    MatchIdentity = table.Column<string>(type: "text", nullable: true),
                    FixtureId = table.Column<int>(type: "integer", nullable: true),
                    Division = table.Column<string>(type: "text", nullable: true),
                    MatchDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    HomeTeam = table.Column<string>(type: "text", nullable: true),
                    AwayTeam = table.Column<string>(type: "text", nullable: true),
                    Origin = table.Column<string>(type: "text", nullable: true),
                    ForecastAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TrainingCutoff = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModelVersion = table.Column<string>(type: "text", nullable: true),
                    InputRevision = table.Column<string>(type: "text", nullable: true),
                    CalibrationVersion = table.Column<string>(type: "text", nullable: true),
                    CalibrationCutoff = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PolicyVersion = table.Column<string>(type: "text", nullable: true),
                    Support = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: true),
                    RawHome = table.Column<decimal>(type: "numeric", nullable: true),
                    RawDraw = table.Column<decimal>(type: "numeric", nullable: true),
                    RawAway = table.Column<decimal>(type: "numeric", nullable: true),
                    FinalHome = table.Column<decimal>(type: "numeric", nullable: true),
                    FinalDraw = table.Column<decimal>(type: "numeric", nullable: true),
                    FinalAway = table.Column<decimal>(type: "numeric", nullable: true),
                    DecisionJson = table.Column<string>(type: "text", nullable: true),
                    QuoteRevision = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_forecast_snapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "import_statuses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceUrl = table.Column<string>(type: "text", nullable: true),
                    Division = table.Column<string>(type: "text", nullable: true),
                    SourceSeason = table.Column<string>(type: "text", nullable: true),
                    SourceFileHash = table.Column<string>(type: "text", nullable: true),
                    RowCount = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_statuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "market_quotes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MatchIdentity = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<string>(type: "text", nullable: true),
                    SourceFileHash = table.Column<string>(type: "text", nullable: true),
                    Revision = table.Column<string>(type: "text", nullable: true),
                    Kind = table.Column<string>(type: "text", nullable: true),
                    ObservedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IngestedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Available = table.Column<bool>(type: "boolean", nullable: false),
                    Home = table.Column<decimal>(type: "numeric", nullable: false),
                    Draw = table.Column<decimal>(type: "numeric", nullable: false),
                    Away = table.Column<decimal>(type: "numeric", nullable: false),
                    Commission = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_market_quotes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_results_SourceMatchId",
                table: "results",
                column: "SourceMatchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fixtures_SourceMatchId",
                table: "fixtures",
                column: "SourceMatchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_calibration_artifacts_ModelVersion_FitCutoff",
                table: "calibration_artifacts",
                columns: new[] { "ModelVersion", "FitCutoff" });

            migrationBuilder.CreateIndex(
                name: "IX_calibration_artifacts_Version",
                table: "calibration_artifacts",
                column: "Version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_forecast_settlements_ForecastSnapshotId_ResultRevision",
                table: "forecast_settlements",
                columns: new[] { "ForecastSnapshotId", "ResultRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_forecast_snapshots_RunId_MatchIdentity",
                table: "forecast_snapshots",
                columns: new[] { "RunId", "MatchIdentity" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_import_statuses_SourceUrl_Division_CompletedAt",
                table: "import_statuses",
                columns: new[] { "SourceUrl", "Division", "CompletedAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_market_quotes_MatchIdentity_Source_Revision",
                table: "market_quotes",
                columns: new[] { "MatchIdentity", "Source", "Revision" },
                unique: true);
            migrationBuilder.Sql("""
                UPDATE fixtures SET "LifecycleStatus" = 'Scheduled', "AnalysisStatus" = 'Awaiting analysis';
                CREATE FUNCTION reject_evidence_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'Evidence is append-only; create a new revision/run'; END; $$;
                CREATE TRIGGER forecast_snapshots_immutable BEFORE UPDATE OR DELETE ON forecast_snapshots FOR EACH ROW EXECUTE PROCEDURE reject_evidence_mutation();
                CREATE TRIGGER market_quotes_immutable BEFORE UPDATE OR DELETE ON market_quotes FOR EACH ROW EXECUTE PROCEDURE reject_evidence_mutation();
                CREATE TRIGGER calibration_artifacts_immutable BEFORE UPDATE OR DELETE ON calibration_artifacts FOR EACH ROW EXECUTE PROCEDURE reject_evidence_mutation();
                CREATE TRIGGER forecast_settlements_immutable BEFORE UPDATE OR DELETE ON forecast_settlements FOR EACH ROW EXECUTE PROCEDURE reject_evidence_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS reject_evidence_mutation() CASCADE;");
            migrationBuilder.DropTable(
                name: "calibration_artifacts");

            migrationBuilder.DropTable(
                name: "evaluation_runs");

            migrationBuilder.DropTable(
                name: "forecast_settlements");

            migrationBuilder.DropTable(
                name: "forecast_snapshots");

            migrationBuilder.DropTable(
                name: "import_statuses");

            migrationBuilder.DropTable(
                name: "market_quotes");

            migrationBuilder.DropIndex(
                name: "IX_results_SourceMatchId",
                table: "results");

            migrationBuilder.DropIndex(
                name: "IX_fixtures_SourceMatchId",
                table: "fixtures");

            migrationBuilder.DropColumn(
                name: "PriceCollection",
                table: "results");

            migrationBuilder.DropColumn(
                name: "PriceKind",
                table: "results");

            migrationBuilder.DropColumn(
                name: "QuoteObservedAt",
                table: "results");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "results");

            migrationBuilder.DropColumn(
                name: "ScoreRevision",
                table: "results");

            migrationBuilder.DropColumn(
                name: "SourceFileHash",
                table: "results");

            migrationBuilder.DropColumn(
                name: "SourceMatchId",
                table: "results");

            migrationBuilder.DropColumn(
                name: "SourceRowId",
                table: "results");

            migrationBuilder.DropColumn(
                name: "SourceSeason",
                table: "results");

            migrationBuilder.DropColumn(
                name: "SourceUrl",
                table: "results");

            migrationBuilder.DropColumn(
                name: "Updated",
                table: "results");

            migrationBuilder.DropColumn(
                name: "AnalysisKey",
                table: "fixtures");

            migrationBuilder.DropColumn(
                name: "AnalysisStatus",
                table: "fixtures");

            migrationBuilder.DropColumn(
                name: "Created",
                table: "fixtures");

            migrationBuilder.DropColumn(
                name: "LifecycleStatus",
                table: "fixtures");

            migrationBuilder.DropColumn(
                name: "PriceCollection",
                table: "fixtures");

            migrationBuilder.DropColumn(
                name: "PriceKind",
                table: "fixtures");

            migrationBuilder.DropColumn(
                name: "QuoteObservedAt",
                table: "fixtures");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "fixtures");

            migrationBuilder.DropColumn(
                name: "SourceFileHash",
                table: "fixtures");

            migrationBuilder.DropColumn(
                name: "SourceMatchId",
                table: "fixtures");

            migrationBuilder.DropColumn(
                name: "SourceRowId",
                table: "fixtures");

            migrationBuilder.DropColumn(
                name: "SourceSeason",
                table: "fixtures");

            migrationBuilder.DropColumn(
                name: "SourceUrl",
                table: "fixtures");

            migrationBuilder.DropColumn(
                name: "Updated",
                table: "fixtures");

            migrationBuilder.AlterColumn<int>(
                name: "halfTimeHomeGoals",
                table: "results",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "halfTimeAwayGoals",
                table: "results",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
