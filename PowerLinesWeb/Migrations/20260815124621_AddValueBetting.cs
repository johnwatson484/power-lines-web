using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PowerLinesWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddValueBetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "awayProbability",
                table: "result_match_odds",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "drawProbability",
                table: "result_match_odds",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "homeProbability",
                table: "result_match_odds",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "valueEdge",
                table: "result_match_odds",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "valueOdds",
                table: "result_match_odds",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "valueSelection",
                table: "result_match_odds",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "valueStake",
                table: "result_match_odds",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "awayProbability",
                table: "match_odds",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "drawProbability",
                table: "match_odds",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "homeProbability",
                table: "match_odds",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "valueEdge",
                table: "match_odds",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "valueOdds",
                table: "match_odds",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "valueSelection",
                table: "match_odds",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "valueStake",
                table: "match_odds",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "awayProbability",
                table: "result_match_odds");

            migrationBuilder.DropColumn(
                name: "drawProbability",
                table: "result_match_odds");

            migrationBuilder.DropColumn(
                name: "homeProbability",
                table: "result_match_odds");

            migrationBuilder.DropColumn(
                name: "valueEdge",
                table: "result_match_odds");

            migrationBuilder.DropColumn(
                name: "valueOdds",
                table: "result_match_odds");

            migrationBuilder.DropColumn(
                name: "valueSelection",
                table: "result_match_odds");

            migrationBuilder.DropColumn(
                name: "valueStake",
                table: "result_match_odds");

            migrationBuilder.DropColumn(
                name: "awayProbability",
                table: "match_odds");

            migrationBuilder.DropColumn(
                name: "drawProbability",
                table: "match_odds");

            migrationBuilder.DropColumn(
                name: "homeProbability",
                table: "match_odds");

            migrationBuilder.DropColumn(
                name: "valueEdge",
                table: "match_odds");

            migrationBuilder.DropColumn(
                name: "valueOdds",
                table: "match_odds");

            migrationBuilder.DropColumn(
                name: "valueSelection",
                table: "match_odds");

            migrationBuilder.DropColumn(
                name: "valueStake",
                table: "match_odds");
        }
    }
}
