using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PowerLinesWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddExpectedGoalsPerSide : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "awayXg",
                table: "result_match_odds",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "homeXg",
                table: "result_match_odds",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "awayXg",
                table: "match_odds",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "homeXg",
                table: "match_odds",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "awayXg",
                table: "result_match_odds");

            migrationBuilder.DropColumn(
                name: "homeXg",
                table: "result_match_odds");

            migrationBuilder.DropColumn(
                name: "awayXg",
                table: "match_odds");

            migrationBuilder.DropColumn(
                name: "homeXg",
                table: "match_odds");
        }
    }
}
