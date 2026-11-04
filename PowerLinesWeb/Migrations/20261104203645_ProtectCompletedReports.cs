using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PowerLinesWeb.Migrations
{
    /// <inheritdoc />
    public partial class ProtectCompletedReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION protect_completed_evaluation() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF OLD."Status" = 'Completed' THEN RAISE EXCEPTION 'Completed evaluation runs are immutable'; END IF;
                    IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
                    RETURN NEW;
                END; $$;
                CREATE TRIGGER evaluation_runs_immutable BEFORE UPDATE OR DELETE ON evaluation_runs FOR EACH ROW EXECUTE PROCEDURE protect_completed_evaluation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS protect_completed_evaluation() CASCADE;");
        }
    }
}
