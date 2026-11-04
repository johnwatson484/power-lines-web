namespace PowerLinesWeb.Models;

public class AccuracyPage
{
    public string RunId { get; set; }
    public DateTime? TestFrom { get; set; }
    public DateTime? TestThrough { get; set; }
    public int Eligible { get; set; }
    public List<EvaluationCandidateSummary> Candidates { get; set; } = [];
    public string ReportJson { get; set; }
}

public class EvaluationCandidateSummary
{
    public string Candidate { get; set; }
    public int Forecasts { get; set; }
    public double? Coverage { get; set; }
    public double? LogLoss { get; set; }
    public double? Brier { get; set; }
}
