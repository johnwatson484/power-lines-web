namespace PowerLinesWeb.Analysis;

public class ThresholdOptions
{
    public decimal Higher { get; set; } = .70m;
    public decimal Lower { get; set; } = .65m;
    public string Version { get; set; } = "win-v1-conservative";
    public void Validate()
    {
        if (Lower < .5m || Lower > Higher || Higher > 1) throw new ArgumentOutOfRangeException(nameof(Lower));
    }
}
