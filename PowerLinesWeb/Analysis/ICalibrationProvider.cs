namespace PowerLinesWeb.Analysis;

public interface ICalibrationProvider
{
    ProbabilityCalibrator Get(string division);
}
