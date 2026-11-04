using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;
using PowerLinesWeb.Data;
using PowerLinesWeb.Forecasts;

namespace PowerLinesWeb.Evaluation;

public static class PublishCommand
{
    public static void Run(string directory)
    {
        var connection = Environment.GetEnvironmentVariable("POWERLINES_PUBLISH_DB") ?? throw new ArgumentException("Set POWERLINES_PUBLISH_DB explicitly. Publication never applies migrations or launches workers.");
        var report = System.IO.File.ReadAllText(Path.Combine(directory,"report.json"));
        var completion = System.IO.File.ReadAllText(Path.Combine(directory,"COMPLETE")).Trim();
        using var run = JsonDocument.Parse(System.IO.File.ReadAllText(Path.Combine(directory,"run.json")));
        if (run.RootElement.GetProperty("Status").GetString() != "Completed") throw new InvalidOperationException("Incomplete evaluation");
        string Sha(string path) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.IO.File.ReadAllBytes(path))).ToLowerInvariant();
        var checksumsPath = Path.Combine(directory,"checksums.json");
        if (Sha(checksumsPath) != run.RootElement.GetProperty("ArtifactChecksumsHash").GetString()) throw new InvalidOperationException("Artifact manifest changed");
        foreach(var pair in JsonSerializer.Deserialize<Dictionary<string,string>>(System.IO.File.ReadAllText(checksumsPath)))
            if (Path.GetFileName(pair.Key) != pair.Key || Sha(Path.Combine(directory,pair.Key)) != pair.Value) throw new InvalidOperationException("Artifact fingerprint mismatch: " + pair.Key);
        var runId = run.RootElement.GetProperty("Id").GetString();
        var created = run.RootElement.GetProperty("CompletedAt").GetDateTime();
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        using var transaction = db.Database.BeginTransaction();
        // All records and the completed report become visible in the same commit.
        new EvaluationPublisher(db,TimeProvider.System).Publish(runId,report,completion,run.RootElement.GetProperty("CodeSha").GetString());
        var path=Path.Combine(directory,"predictions.csv");
        using var file = System.IO.File.OpenRead(System.IO.File.Exists(path) ? path : path+".gz");
        using Stream stream = System.IO.File.Exists(path) ? file : new GZipStream(file,CompressionMode.Decompress);
        using var csv = new TextFieldParser(stream) { HasFieldsEnclosedInQuotes=true };
        csv.SetDelimiters(",");var names=csv.ReadFields();var index=names.Select((x,i)=>(x,i)).ToDictionary(x=>x.x,x=>x.i);
        var batchCount = 0;
        while(!csv.EndOfData)
        {
            var row=csv.ReadFields();string Get(string name)=>row[index[name]];
            decimal? Number(string name)=>decimal.TryParse(Get(name),NumberStyles.Float,CultureInfo.InvariantCulture,out var n)?n:null;
            if(Get("origin")!="HistoricalReplay")throw new FormatException("Replay publication cannot relabel provenance");
            db.ForecastSnapshots.Add(new ForecastSnapshot { RunId=runId+":"+Get("candidate"),MatchIdentity="result:"+Get("id"), Division=Get("division"), MatchDate=DateTime.ParseExact(Get("date"),"yyyy-MM-dd",CultureInfo.InvariantCulture),
                Origin="HistoricalReplay",ForecastAt=created,TrainingCutoff=DateTime.ParseExact(Get("trainingCutoff"),"yyyy-MM-dd",CultureInfo.InvariantCulture),
                ModelVersion=Get("candidate"),InputRevision=run.RootElement.GetProperty("DatasetHash").GetString(),CalibrationVersion=Get("calibrationVersion"),CalibrationCutoff=DateTime.ParseExact(Get("calibrationCutoff"),"yyyy-MM-dd",CultureInfo.InvariantCulture),PolicyVersion=run.RootElement.GetProperty("ManifestHash").GetString(),Support=Get("support"),Status=Get("exclusion").Length==0?"Available":Get("exclusion"),
                RawHome=Number("rawH"),RawDraw=Number("rawD"),RawAway=Number("rawA"),FinalHome=Number("finalH"),FinalDraw=Number("finalD"),FinalAway=Number("finalA"),DecisionJson=JsonSerializer.Serialize(new { Selection=Get("winSelection"),Tier=Get("tier"),FairOdds=Number("fairOdds"),PriceExclusion=Get("priceExclusion") }) });
            if(++batchCount % 1000 == 0){db.SaveChanges();db.ChangeTracker.Clear();}
        }
        db.SaveChanges();transaction.Commit();Console.WriteLine($"Published completed run {runId}; replay origin preserved.");
    }
}
