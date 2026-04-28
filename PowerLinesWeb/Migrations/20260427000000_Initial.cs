using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using PowerLinesWeb.Data;

#nullable disable

namespace PowerLinesWeb.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Microsoft.EntityFrameworkCore.Migrations.Migration("20260427000000_Initial")]
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accuracy",
                columns: table => new
                {
                    accuracyId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    division = table.Column<string>(type: "text", nullable: true),
                    matches = table.Column<int>(type: "integer", nullable: false),
                    recommended = table.Column<int>(type: "integer", nullable: false),
                    recommendedAccuracy = table.Column<decimal>(type: "numeric", nullable: false),
                    lowerRecommended = table.Column<int>(type: "integer", nullable: false),
                    lowerRecommendedAccuracy = table.Column<decimal>(type: "numeric", nullable: false),
                    calculated = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accuracy", x => x.accuracyId);
                });

            migrationBuilder.CreateTable(
                name: "fixtures",
                columns: table => new
                {
                    fixtureId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    division = table.Column<string>(type: "text", nullable: true),
                    date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    homeTeam = table.Column<string>(type: "text", nullable: true),
                    awayTeam = table.Column<string>(type: "text", nullable: true),
                    homeOddsAverage = table.Column<decimal>(type: "numeric", nullable: false),
                    drawOddsAverage = table.Column<decimal>(type: "numeric", nullable: false),
                    awayOddsAverage = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fixtures", x => x.fixtureId);
                });

            migrationBuilder.CreateTable(
                name: "results",
                columns: table => new
                {
                    resultId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    division = table.Column<string>(type: "text", nullable: true),
                    date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    homeTeam = table.Column<string>(type: "text", nullable: true),
                    awayTeam = table.Column<string>(type: "text", nullable: true),
                    fullTimeHomeGoals = table.Column<int>(type: "integer", nullable: false),
                    fullTimeAwayGoals = table.Column<int>(type: "integer", nullable: false),
                    fullTimeResult = table.Column<string>(type: "text", nullable: true),
                    halfTimeHomeGoals = table.Column<int>(type: "integer", nullable: false),
                    halfTimeAwayGoals = table.Column<int>(type: "integer", nullable: false),
                    halfTimeResult = table.Column<string>(type: "text", nullable: true),
                    homeOddsAverage = table.Column<decimal>(type: "numeric", nullable: false),
                    drawOddsAverage = table.Column<decimal>(type: "numeric", nullable: false),
                    awayOddsAverage = table.Column<decimal>(type: "numeric", nullable: false),
                    created = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_results", x => x.resultId);
                });

            migrationBuilder.CreateTable(
                name: "match_odds",
                columns: table => new
                {
                    matchOddsId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fixtureId = table.Column<int>(type: "integer", nullable: false),
                    home = table.Column<decimal>(type: "numeric", nullable: false),
                    draw = table.Column<decimal>(type: "numeric", nullable: false),
                    away = table.Column<decimal>(type: "numeric", nullable: false),
                    expectedHomeGoals = table.Column<int>(type: "integer", nullable: false),
                    expectedAwayGoals = table.Column<int>(type: "integer", nullable: false),
                    expectedGoals = table.Column<decimal>(type: "numeric", nullable: false),
                    recommended = table.Column<string>(type: "text", nullable: true),
                    lowerRecommended = table.Column<string>(type: "text", nullable: true),
                    calculated = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_match_odds", x => x.matchOddsId);
                    table.ForeignKey(
                        name: "FK_match_odds_fixtures_fixtureId",
                        column: x => x.fixtureId,
                        principalTable: "fixtures",
                        principalColumn: "fixtureId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "result_match_odds",
                columns: table => new
                {
                    matchOddsId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    resultId = table.Column<int>(type: "integer", nullable: false),
                    home = table.Column<decimal>(type: "numeric", nullable: false),
                    draw = table.Column<decimal>(type: "numeric", nullable: false),
                    away = table.Column<decimal>(type: "numeric", nullable: false),
                    expectedHomeGoals = table.Column<int>(type: "integer", nullable: false),
                    expectedAwayGoals = table.Column<int>(type: "integer", nullable: false),
                    expectedGoals = table.Column<decimal>(type: "numeric", nullable: false),
                    recommended = table.Column<string>(type: "text", nullable: true),
                    lowerRecommended = table.Column<string>(type: "text", nullable: true),
                    calculated = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
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
                name: "IX_fixtures_date_homeTeam_awayTeam",
                table: "fixtures",
                columns: new[] { "date", "homeTeam", "awayTeam" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_match_odds_fixtureId",
                table: "match_odds",
                column: "fixtureId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_result_match_odds_resultId",
                table: "result_match_odds",
                column: "resultId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_results_date_homeTeam_awayTeam",
                table: "results",
                columns: new[] { "date", "homeTeam", "awayTeam" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "accuracy");
            migrationBuilder.DropTable(name: "match_odds");
            migrationBuilder.DropTable(name: "result_match_odds");
            migrationBuilder.DropTable(name: "fixtures");
            migrationBuilder.DropTable(name: "results");
        }
    }
}
