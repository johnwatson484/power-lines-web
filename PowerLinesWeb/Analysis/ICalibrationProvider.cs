namespace PowerLinesWeb.Analysis;

public interface ICalibrationProvider
{
    PowerLinesWeb.Forecasts.CalibrationArtifact GetArtifact(string modelVersion, DateTime cutoff) => null;
}
