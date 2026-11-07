namespace PowerLinesWeb.Analysis;

public interface ICalibrationProvider
{
    ProbabilityCalibrator Get(string division);
    PowerLinesWeb.Forecasts.CalibrationArtifact GetArtifact(string modelVersion, DateTime cutoff) => null;
}
