using Microsoft.EntityFrameworkCore;
using PowerLinesWeb.Forecasts;

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

    public DbSet<ImportStatus> ImportStatuses { get; set; }
    public DbSet<ForecastSnapshot> ForecastSnapshots { get; set; }
    public DbSet<CalibrationArtifact> CalibrationArtifacts { get; set; }
    public DbSet<MarketQuote> MarketQuotes { get; set; }
    public DbSet<EvaluationRun> EvaluationRuns { get; set; }
    public DbSet<ForecastSettlement> ForecastSettlements { get; set; }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        GuardImmutableRecords();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        GuardImmutableRecords();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    void GuardImmutableRecords()
    {
        foreach (var entry in ChangeTracker.Entries())
            if (entry.State is EntityState.Modified or EntityState.Deleted &&
                (entry.Entity is ForecastSnapshot or CalibrationArtifact or MarketQuote or ForecastSettlement ||
                 entry.Entity is EvaluationRun && entry.OriginalValues.GetValue<string>("Status") == "Completed"))
                throw new InvalidOperationException("Published evidence is append-only; create a new revision/run.");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ImportStatus>().HasIndex(x => new { x.SourceUrl, x.Division, x.CompletedAt }).IsUnique();
        modelBuilder.Entity<ForecastSnapshot>().HasIndex(x => new { x.RunId, x.MatchIdentity }).IsUnique();
        modelBuilder.Entity<CalibrationArtifact>().HasIndex(x => x.Version).IsUnique();
        modelBuilder.Entity<CalibrationArtifact>().HasIndex(x => new { x.ModelVersion, x.FitCutoff });
        modelBuilder.Entity<MarketQuote>().HasIndex(x => new { x.MatchIdentity, x.Source, x.Revision }).IsUnique();
        modelBuilder.Entity<ForecastSettlement>().HasIndex(x => new { x.ForecastSnapshotId, x.ResultRevision }).IsUnique();
        modelBuilder.Entity<Fixture>().HasIndex(x => x.SourceMatchId).IsUnique();
        modelBuilder.Entity<Result>().HasIndex(x => x.SourceMatchId).IsUnique();
        modelBuilder.Entity<Fixture>()
            .HasIndex(x => new { x.Date, x.HomeTeam, x.AwayTeam }).IsUnique();

        modelBuilder.Entity<Result>()
            .HasIndex(x => new { x.Date, x.HomeTeam, x.AwayTeam }).IsUnique();
    }
}
