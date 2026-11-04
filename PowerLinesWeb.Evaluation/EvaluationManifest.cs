namespace PowerLinesWeb.Evaluation;

public record Recipe(string Id, double HalfLifeDays, double PriorMatches);
public class EvaluationManifest
{
    public string SnapshotSha256 { get; set; }
    public string QuotesSha256 { get; set; }
    public string[] Divisions { get; set; }
    public DateTime RawFrom { get; set; }
    public DateTime DevelopmentFrom { get; set; }
    public DateTime HoldoutFrom { get; set; }
    public DateTime HoldoutThrough { get; set; }
    public DateTime DiagnosticThrough { get; set; }
    public DateTime[] DevelopmentBlocks { get; set; }
    public Recipe[] Recipes { get; set; }
    public double[] Temperatures { get; set; }
    public decimal[] BlendWeights { get; set; }
    public decimal[] ShortlistThresholds { get; set; }
    public int Seed { get; set; }
    public int BootstrapResamples { get; set; }
    public double MaxHitRateLoss { get; set; }
    public double MinVolumeRatio { get; set; }
    public double MaxCalibrationGap { get; set; }
    public double BrierTolerance { get; set; }
    public int MinSelections { get; set; }
    public string UpdateRule { get; set; }
    public string QuoteConvention { get; set; }
    public string ResearchLimitation { get; set; }
}
