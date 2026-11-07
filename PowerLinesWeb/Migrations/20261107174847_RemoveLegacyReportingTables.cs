using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PowerLinesWeb.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyReportingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accuracy");

            migrationBuilder.DropTable(
                name: "accuracy_calibration");

            migrationBuilder.DropTable(
                name: "result_match_odds");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accuracy",
                columns: table => new
                {
                    accuracyId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    baselineAccuracy = table.Column<decimal>(type: "numeric", nullable: true),
                    brierScore = table.Column<decimal>(type: "numeric", nullable: true),
                    calculated = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    division = table.Column<string>(type: "text", nullable: true),
                    InvalidRows = table.Column<int>(type: "integer", nullable: false),
                    logLoss = table.Column<decimal>(type: "numeric", nullable: true),
                    lowerRecommended = table.Column<int>(type: "integer", nullable: false),
                    lowerRecommendedAccuracy = table.Column<decimal>(type: "numeric", nullable: true),
                    marketLogLoss = table.Column<decimal>(type: "numeric", nullable: true),
                    matches = table.Column<int>(type: "integer", nullable: false),
                    PairedModelBrier = table.Column<decimal>(type: "numeric", nullable: true),
                    PairedModelLogLoss = table.Column<decimal>(type: "numeric", nullable: true),
                    pricedMatches = table.Column<int>(type: "integer", nullable: false),
                    recommended = table.Column<int>(type: "integer", nullable: false),
                    recommendedAccuracy = table.Column<decimal>(type: "numeric", nullable: true),
                    scoredMatches = table.Column<int>(type: "integer", nullable: false),
                    valueBets = table.Column<int>(type: "integer", nullable: false),
                    valueRoi = table.Column<decimal>(type: "numeric", nullable: true),
                    valueWins = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accuracy", x => x.accuracyId);
                });

            migrationBuilder.CreateTable(
                name: "accuracy_calibration",
                columns: table => new
                {
                    accuracyCalibrationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    calculated = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    division = table.Column<string>(type: "text", nullable: true),
                    lowerBound = table.Column<decimal>(type: "numeric", nullable: false),
                    observed = table.Column<decimal>(type: "numeric", nullable: false),
                    predicted = table.Column<decimal>(type: "numeric", nullable: false),
                    predictions = table.Column<int>(type: "integer", nullable: false),
                    upperBound = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accuracy_calibration", x => x.accuracyCalibrationId);
                });

            migrationBuilder.CreateTable(
                name: "result_match_odds",
                columns: table => new
                {
                    matchOddsId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    away = table.Column<decimal>(type: "numeric", nullable: false),
                    expectedAwayGoals = table.Column<int>(type: "integer", nullable: false),
                    awayProbability = table.Column<decimal>(type: "numeric", nullable: false),
                    awayXg = table.Column<decimal>(type: "numeric", nullable: false),
                    calculated = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    draw = table.Column<decimal>(type: "numeric", nullable: false),
                    drawProbability = table.Column<decimal>(type: "numeric", nullable: false),
                    expectedGoals = table.Column<decimal>(type: "numeric", nullable: false),
                    home = table.Column<decimal>(type: "numeric", nullable: false),
                    expectedHomeGoals = table.Column<int>(type: "integer", nullable: false),
                    homeProbability = table.Column<decimal>(type: "numeric", nullable: false),
                    homeXg = table.Column<decimal>(type: "numeric", nullable: false),
                    lowerRecommended = table.Column<string>(type: "text", nullable: true),
                    recommended = table.Column<string>(type: "text", nullable: true),
                    resultId = table.Column<int>(type: "integer", nullable: false),
                    valueEdge = table.Column<decimal>(type: "numeric", nullable: false),
                    valueOdds = table.Column<decimal>(type: "numeric", nullable: false),
                    valueSelection = table.Column<string>(type: "text", nullable: true),
                    valueStake = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_result_match_odds", x => x.matchOddsId);
                    table.ForeignKey(
                        name: "FK_result_match_odds_results_resultId",
                        column: x => x.resultId,
                        principalTable: "results",
                        principalColumn: "resultId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accuracy_division",
                table: "accuracy",
                column: "division",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accuracy_calibration_division_lowerBound",
                table: "accuracy_calibration",
                columns: new[] { "division", "lowerBound" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_result_match_odds_resultId",
                table: "result_match_odds",
                column: "resultId",
                unique: true);
        }
    }
}
