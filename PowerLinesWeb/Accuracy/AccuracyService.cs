using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PowerLinesWeb.Data;
using PowerLinesWeb.Models;

namespace PowerLinesWeb.Accuracy;

public class AccuracyService(ApplicationDbContext dbContext) : IAccuracyService
{
    readonly ApplicationDbContext dbContext = dbContext;
    // Legacy reports are not evidence of a completed, reproducible evaluation.
    public List<Models.Accuracy> Get() => [];
    public AccuracyPage GetPage()
    {
        var run = dbContext.EvaluationRuns.AsNoTracking().Where(x => x.Status == "Completed" && x.CompletedAt != null)
            .OrderByDescending(x => x.CompletedAt).FirstOrDefault();
        if (run == null) return new();
        using var report = JsonDocument.Parse(run.ReportJson);
        var root = report.RootElement;
        var page = new AccuracyPage { RunId = run.Id, TestFrom = run.TestFrom, TestThrough = run.TestThrough, Eligible = root.GetProperty("AllEligible").GetInt32(), ReportJson = run.ReportJson };
        foreach (var row in root.GetProperty("Candidates").EnumerateArray())
        {
            var scores = row.GetProperty("Holdout");
            double? Number(string name) => scores.GetProperty(name).ValueKind == JsonValueKind.Null ? null : scores.GetProperty(name).GetDouble();
            page.Candidates.Add(new() { Candidate = row.GetProperty("Candidate").GetString(), Forecasts = scores.GetProperty("Forecasts").GetInt32(), Coverage = Number("Per100"), LogLoss = Number("LogLoss"), Brier = Number("Brier") });
        }
        return page;
    }
}
