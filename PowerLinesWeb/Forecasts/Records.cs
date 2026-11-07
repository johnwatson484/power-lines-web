using System.ComponentModel.DataAnnotations.Schema;

namespace PowerLinesWeb.Forecasts;

[Table("forecast_snapshots")]
public class ForecastSnapshot
{
    public long Id { get; set; }
    public string RunId { get; set; }
    public string MatchIdentity { get; set; }
    public int? FixtureId { get; set; }
    public string Division { get; set; }
    public DateTime MatchDate { get; set; }
    public string HomeTeam { get; set; }
    public string AwayTeam { get; set; }
    public string Origin { get; set; }
    public DateTime ForecastAt { get; set; }
    public DateTime TrainingCutoff { get; set; }
    public string ModelVersion { get; set; }
    public string InputRevision { get; set; }
    public string CalibrationVersion { get; set; }
    public DateTime? CalibrationCutoff { get; set; }
    public string PolicyVersion { get; set; }
    public string Support { get; set; }
    public string Status { get; set; }
    public decimal? RawHome { get; set; }
    public decimal? RawDraw { get; set; }
    public decimal? RawAway { get; set; }
    public decimal? FinalHome { get; set; }
    public decimal? FinalDraw { get; set; }
    public decimal? FinalAway { get; set; }
    public string DecisionJson { get; set; }
    public string QuoteRevision { get; set; }
}

[Table("calibration_artifacts")]
public class CalibrationArtifact
{
    public long Id { get; set; }
    public string Version { get; set; }
    public string ModelVersion { get; set; }
    public DateTime FitFrom { get; set; }
    public DateTime FitCutoff { get; set; }
    public int Samples { get; set; }
    public double Temperature { get; set; } = 1;
    public string TrainingFingerprint { get; set; }
    public string TrainingOrigin { get; set; } = "EarlierRawOutOfFold";
    public bool Activated { get; set; }
}

[Table("market_quotes")]
public class MarketQuote
{
    public long Id { get; set; }
    public string MatchIdentity { get; set; }
    public string Source { get; set; }
    public string SourceFileHash { get; set; }
    public string Revision { get; set; }
    public string Kind { get; set; } = "Unknown";
    public DateTime? ObservedAt { get; set; }
    public DateTime IngestedAt { get; set; }
    public bool Available { get; set; }
    public decimal Home { get; set; }
    public decimal Draw { get; set; }
    public decimal Away { get; set; }
    public decimal Commission { get; set; }
}

[Table("evaluation_runs")]
public class EvaluationRun
{
    public string Id { get; set; }
    public string DatasetHash { get; set; }
    public string ManifestHash { get; set; }
    public string CodeVersion { get; set; }
    public DateTime TestFrom { get; set; }
    public DateTime TestThrough { get; set; }
    public string Status { get; set; } = "Running";
    public DateTime? CompletedAt { get; set; }
    public string ReportJson { get; set; }
}

[Table("forecast_settlements")]
public class ForecastSettlement
{
    public long Id { get; set; }
    public long ForecastSnapshotId { get; set; }
    public int ResultId { get; set; }
    public long ResultRevision { get; set; }
    public string Outcome { get; set; }
    public DateTime SettledAt { get; set; }
}

[Table("import_statuses")]
public class ImportStatus
{
    public long Id { get; set; }
    public string SourceUrl { get; set; }
    public string Division { get; set; }
    public string SourceSeason { get; set; }
    public string SourceFileHash { get; set; }
    public int RowCount { get; set; }
    public DateTime CompletedAt { get; set; }
    public string Status { get; set; }
}
