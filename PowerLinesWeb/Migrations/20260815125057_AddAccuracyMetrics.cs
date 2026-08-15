using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PowerLinesWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddAccuracyMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "baselineAccuracy",
                table: "accuracy",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "brierScore",
                table: "accuracy",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "logLoss",
                table: "accuracy",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "marketLogLoss",
                table: "accuracy",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "pricedMatches",
                table: "accuracy",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "scoredMatches",
                table: "accuracy",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "valueBets",
                table: "accuracy",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "valueRoi",
                table: "accuracy",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "valueWins",
                table: "accuracy",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "accuracy_calibration",
                columns: table => new
                {
                    accuracyCalibrationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    division = table.Column<string>(type: "text", nullable: true),
                    lowerBound = table.Column<decimal>(type: "numeric", nullable: false),
                    upperBound = table.Column<decimal>(type: "numeric", nullable: false),
                    predicted = table.Column<decimal>(type: "numeric", nullable: false),
                    observed = table.Column<decimal>(type: "numeric", nullable: false),
                    predictions = table.Column<int>(type: "integer", nullable: false),
                    calculated = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accuracy_calibration", x => x.accuracyCalibrationId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accuracy_calibration_division_lowerBound",
                table: "accuracy_calibration",
                columns: new[] { "division", "lowerBound" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accuracy_calibration");

            migrationBuilder.DropColumn(
                name: "baselineAccuracy",
                table: "accuracy");

            migrationBuilder.DropColumn(
                name: "brierScore",
                table: "accuracy");

            migrationBuilder.DropColumn(
                name: "logLoss",
                table: "accuracy");

            migrationBuilder.DropColumn(
                name: "marketLogLoss",
                table: "accuracy");

            migrationBuilder.DropColumn(
                name: "pricedMatches",
                table: "accuracy");

            migrationBuilder.DropColumn(
                name: "scoredMatches",
                table: "accuracy");

            migrationBuilder.DropColumn(
                name: "valueBets",
                table: "accuracy");

            migrationBuilder.DropColumn(
                name: "valueRoi",
                table: "accuracy");

            migrationBuilder.DropColumn(
                name: "valueWins",
                table: "accuracy");
        }
    }
}
