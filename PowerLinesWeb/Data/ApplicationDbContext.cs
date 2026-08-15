using Microsoft.EntityFrameworkCore;

namespace PowerLinesWeb.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    public DbSet<Fixture> Fixtures { get; set; }
    public DbSet<MatchOdds> MatchOdds { get; set; }
    public DbSet<Result> Results { get; set; }
    public DbSet<ResultMatchOdds> ResultMatchOdds { get; set; }
    public DbSet<AccuracyRecord> Accuracy { get; set; }
    public DbSet<AccuracyCalibrationRecord> AccuracyCalibration { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Fixture>()
            .HasIndex(x => new { x.Date, x.HomeTeam, x.AwayTeam }).IsUnique();

        modelBuilder.Entity<Result>()
            .HasIndex(x => new { x.Date, x.HomeTeam, x.AwayTeam }).IsUnique();

        modelBuilder.Entity<AccuracyRecord>()
            .HasIndex(x => x.Division).IsUnique();

        modelBuilder.Entity<AccuracyCalibrationRecord>()
            .HasIndex(x => new { x.Division, x.LowerBound }).IsUnique();
    }
}
