using System.Text.Json;
using PowerLinesWeb.Data;

namespace PowerLinesWeb.Forecasts;

public class EvaluationPublisher(ApplicationDbContext db, TimeProvider clock)
{
    readonly ApplicationDbContext db = db;
    readonly TimeProvider clock = clock;
    public void Publish(string id, string reportJson, string expectedReportHash, string codeVersion)
    {
        var actual = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(reportJson))).ToLowerInvariant();
        if (actual != expectedReportHash) throw new InvalidOperationException("Completion fingerprint mismatch");
        using var document = JsonDocument.Parse(reportJson); var report = document.RootElement;
        if (report.GetProperty("Status").GetString() != "Completed") throw new InvalidOperationException("Only completed runs may be published");
        if (db.EvaluationRuns.Any(x => x.Id == id)) throw new InvalidOperationException("Run already exists; publication is immutable");
        db.EvaluationRuns.Add(new EvaluationRun { Id = id, Status = "Completed", CompletedAt = clock.GetUtcNow().UtcDateTime,
            DatasetHash = report.GetProperty("DatasetHash").GetString(), ManifestHash = report.GetProperty("ManifestHash").GetString(), CodeVersion = codeVersion,
            TestFrom = report.GetProperty("TestFrom").GetDateTime(), TestThrough = report.GetProperty("TestThrough").GetDateTime(), ReportJson = reportJson });
        db.SaveChanges();
    }
}
