using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PowerLinesWeb.Accuracy;
using PowerLinesWeb.Analysis;
using PowerLinesWeb.Analysis.Ratings;
using PowerLinesWeb.Data;
using PowerLinesWeb.Fixtures;
using PowerLinesWeb.Forecasts;
using PowerLinesWeb.Messaging;
using PowerLinesWeb.Tests.Analysis;

namespace PowerLinesWeb.Tests.Forecasts;

[NonParallelizable]
public class PersistenceTests
{
    ServiceProvider provider;
    readonly FixedClock clock = new(new DateTimeOffset(2025,8,1,12,0,0,TimeSpan.Zero));
    string connection;
    ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);

    [OneTimeSetUp]
    public void Initialize()
    {
        connection=Environment.GetEnvironmentVariable("POWERLINES_TEST_DB");
        if(string.IsNullOrEmpty(connection)) Assert.Ignore("Set POWERLINES_TEST_DB to an isolated disposable database to run persistence/job integration tests.");
        if(!connection.Contains("power_lines_test")) throw new InvalidOperationException("Integration DB must be named power_lines_test");
        using var db=Db(); db.Database.Migrate();
        var services=new ServiceCollection();services.AddDbContext<ApplicationDbContext>(x=>x.UseNpgsql(connection));
        services.AddSingleton<TimeProvider>(clock);services.AddMemoryCache(x=>x.SizeLimit=256);
        services.AddSingleton<IOptions<ModelOptions>>(Options.Create(new ModelOptions()));
        services.AddSingleton<IOptions<ThresholdOptions>>(Options.Create(new ThresholdOptions()));
        services.AddSingleton<IOptions<BettingOptions>>(Options.Create(new BettingOptions()));
        services.AddSingleton<IOptions<FeedOptions>>(Options.Create(new FeedOptions()));
        services.AddScoped<IRatingsProvider,RatingsProvider>();services.AddScoped<ICalibrationProvider,CalibrationProvider>();
        services.AddScoped<IAnalysisService,AnalysisService>();services.AddScoped<IngestionService>();services.AddScoped<FixtureProjectionJob>();
        provider=services.BuildServiceProvider();
    }

    [SetUp]
    public void Reset()
    {
        using var db=Db();
        db.Database.ExecuteSqlRaw("TRUNCATE TABLE forecast_settlements, forecast_snapshots, market_quotes, calibration_artifacts, evaluation_runs, import_statuses, match_odds, result_match_odds, fixtures, results RESTART IDENTITY CASCADE");
        var rows=SyntheticLeague.Build(3);
        for(var i=0;i<rows.Count;i++) {rows[i].Date=new DateTime(2024,8,1).AddDays(i);rows[i].Created=rows[i].Date.AddDays(1);}
        db.Results.AddRange(rows);db.SaveChanges();
        clock.Now=new DateTimeOffset(2025,8,1,12,0,0,TimeSpan.Zero);
    }

    void IngestFixture(decimal h=2) {using var scope=provider.CreateScope();scope.ServiceProvider.GetRequiredService<IngestionService>().SaveFixture(new() {Division="E0",Date=clock.Now.UtcDateTime.AddDays(1),HomeTeam="Strong",AwayTeam="Weak",HomeOddsAverage=h,DrawOddsAverage=4,AwayOddsAverage=4,SourceMatchId="stable-fixture",SourceUrl="source",SourceFileHash="hash",PriceKind="AveragePreClosing"});}
    void Run() {using var scope=provider.CreateScope();scope.ServiceProvider.GetRequiredService<FixtureProjectionJob>().Run();}

    [Test]
    public void Quote_corrections_refresh_decisions_and_keep_original_forecasts_immutable()
    {
        IngestFixture();Run();using var db=Db();var first=db.ForecastSnapshots.AsNoTracking().Single();var created=db.Fixtures.AsNoTracking().Single().Created;
        IngestFixture(3);Run();var after=db.ForecastSnapshots.AsNoTracking().OrderBy(x=>x.Id).ToList();
        Assert.That(after.Count,Is.EqualTo(2));Assert.That(after[0].RawHome,Is.EqualTo(after[1].RawHome));Assert.That(after[0].Id,Is.EqualTo(first.Id));
        Assert.That(db.Fixtures.AsNoTracking().Single().Created,Is.EqualTo(created));Assert.That(db.MarketQuotes.Count(),Is.EqualTo(2));
        var tracked=db.ForecastSnapshots.First();tracked.FinalHome=.1m;
        Assert.Throws<InvalidOperationException>(()=>db.SaveChanges());
        db.ChangeTracker.Clear();
        Assert.Throws<Npgsql.PostgresException>(()=>db.Database.ExecuteSqlRaw("UPDATE forecast_snapshots SET \"FinalHome\"=0.1"));
    }

    [Test]
    public void Corrected_results_change_inputs_but_identical_or_missing_price_reimports_are_noops()
    {
        IngestFixture();Run();long oldRevision;DateTime created;
        Result incoming;
        using(var db=Db()) {var r=db.Results.OrderBy(x=>x.ResultId).First();created=r.Created;oldRevision=r.ScoreRevision;incoming=new() {Division=r.Division,Date=r.Date,HomeTeam=r.HomeTeam,AwayTeam=r.AwayTeam,FullTimeHomeGoals=9,FullTimeAwayGoals=0,FullTimeResult="H"};}
        using(var scope=provider.CreateScope()) scope.ServiceProvider.GetRequiredService<IngestionService>().SaveResult(incoming);
        Run();
        using(var db=Db()) {var r=db.Results.Single(x=>x.Date==incoming.Date);Assert.That(r.Created,Is.EqualTo(created));Assert.That(r.ScoreRevision,Is.EqualTo(oldRevision+1));Assert.That(db.ForecastSnapshots.Select(x=>x.InputRevision).Distinct().Count(),Is.EqualTo(2));oldRevision=r.Revision;}
        using(var scope=provider.CreateScope()) scope.ServiceProvider.GetRequiredService<IngestionService>().SaveResult(incoming);
        using(var db=Db()) Assert.That(db.Results.Single(x=>x.Date==incoming.Date).Revision,Is.EqualTo(oldRevision));
    }

    [Test]
    public void Policy_and_calibration_changes_invalidate_latest_projection_without_mutating_archives()
    {
        IngestFixture();Run();
        var policy=provider.GetRequiredService<IOptions<ThresholdOptions>>().Value;policy.Version="policy-v2";Run();
        using var db=Db();Assert.That(db.ForecastSnapshots.Count(),Is.EqualTo(2));
        var model=provider.GetRequiredService<IOptions<ModelOptions>>().Value;model.EnableCalibration=true;
        db.CalibrationArtifacts.Add(new() {Version="t-v1",ModelVersion=ForecastVersions.Hash(model),FitFrom=new DateTime(2023,1,1),FitCutoff=clock.Now.UtcDateTime.AddDays(-2),Samples=300,Temperature=1.1,Activated=true});db.SaveChanges();
        Run();var current=db.ForecastSnapshots.AsNoTracking().OrderByDescending(x=>x.Id).First();
        Assert.That(current.CalibrationVersion,Is.EqualTo("t-v1"));Assert.That(current.RawHome,Is.Not.EqualTo(current.FinalHome));
        // A future artifact must never change this earlier forecast/calibration lookup.
        db.CalibrationArtifacts.Add(new() {Version="future",ModelVersion=ForecastVersions.Hash(model),FitFrom=new DateTime(2023,1,1),FitCutoff=clock.Now.UtcDateTime.AddDays(1),Samples=300,Temperature=2,Activated=true});db.SaveChanges();
        Run();Assert.That(db.ForecastSnapshots.Count(),Is.EqualTo(3));model.EnableCalibration=false;policy.Version="win-v1-conservative";
    }

    [Test]
    public void Missing_prices_cannot_erase_known_good_quotes_and_reimport_is_idempotent()
    {
        IngestFixture();using var scope=provider.CreateScope();var ingestion=scope.ServiceProvider.GetRequiredService<IngestionService>();
        var fixture=new Fixture {Division="E0",Date=clock.Now.UtcDateTime.AddDays(1),HomeTeam="Strong",AwayTeam="Weak",SourceMatchId="stable-fixture"};
        ingestion.SaveFixture(fixture);ingestion.SaveFixture(fixture);
        using var db=Db();Assert.That(db.Fixtures.Count(),Is.EqualTo(1));Assert.That(db.Fixtures.Single().HomeOddsAverage,Is.EqualTo(2));Assert.That(db.Fixtures.Single().Revision,Is.EqualTo(1));
    }

    [Test]
    public void Fixture_list_includes_unsupported_and_pending_rows_and_empty_reports_do_not_publish()
    {
        IngestFixture();using var db=Db();db.Fixtures.Add(new() {Division="E0",Date=clock.Now.UtcDateTime.AddDays(2),HomeTeam="New",AwayTeam="Unknown"});db.SaveChanges();
        var service=new FixtureService(db,clock,Options.Create(new ThresholdOptions {Lower=.60m}),Options.Create(new FeedOptions()));
        var page=service.GetPage();Assert.That(page.Fixtures.Count,Is.EqualTo(2));Assert.That(page.Fixtures.All(x=>x.HomeProbability==null),Is.True);Assert.That(page.Lower,Is.EqualTo(.60m));
        Run();Assert.That(service.GetPage().Fixtures.Any(x=>x.Status=="Insufficient history"),Is.True);
        db.EvaluationRuns.Add(new() {Id="unfinished",Status="Running"});db.SaveChanges();
        Assert.That(new AccuracyService(db).GetPage().RunId,Is.Null);
        var publisher=new EvaluationPublisher(db,clock);
        var json="{\"Status\":\"Running\"}";var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
        Assert.Throws<InvalidOperationException>(()=>publisher.Publish("bad",json,hash,"code"));
    }

    [Test]
    public void Settlement_appends_outcomes_and_never_rewrites_prematch_probabilities()
    {
        IngestFixture();Run();using var db=Db();var snapshot=db.ForecastSnapshots.AsNoTracking().Single();
        db.Results.Add(new() {Division="E0",Date=clock.Now.UtcDateTime.AddDays(1).Date,HomeTeam="Strong",AwayTeam="Weak",FullTimeHomeGoals=1,FullTimeAwayGoals=0,FullTimeResult="H",Revision=1});db.SaveChanges();
        clock.Now=clock.Now.AddDays(2);Run();
        Assert.That(db.ForecastSettlements.Single().Outcome,Is.EqualTo("H"));Assert.That(db.ForecastSnapshots.AsNoTracking().Single().FinalHome,Is.EqualTo(snapshot.FinalHome));
    }

    [Test]
    public void Stale_results_are_explicitly_unavailable_and_quote_only_updates_reuse_rating_fits()
    {
        IngestFixture();Run();
        TeamRatings first;
        using(var scope=provider.CreateScope()) first=scope.ServiceProvider.GetRequiredService<IRatingsProvider>().Get("E0",clock.Now.UtcDateTime);
        IngestFixture(3);Run();
        using(var scope=provider.CreateScope()) Assert.That(scope.ServiceProvider.GetRequiredService<IRatingsProvider>().Get("E0",clock.Now.UtcDateTime),Is.SameAs(first));
        clock.Now=new DateTimeOffset(2025,11,1,12,0,0,TimeSpan.Zero);IngestFixture();Run();
        using var db=Db();var last=db.ForecastSnapshots.AsNoTracking().OrderByDescending(x=>x.Id).First();
        Assert.That(last.Status,Is.EqualTo("Stale result feed"));Assert.That(last.FinalHome,Is.Null);
    }

    [OneTimeTearDown]
    public void Dispose()=>provider?.Dispose();
    public class FixedClock(DateTimeOffset now):TimeProvider {public DateTimeOffset Now {get;set;}=now;public override DateTimeOffset GetUtcNow()=>Now;}
}
