using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PowerLinesWeb.Data;
using PowerLinesWeb.Forecasts;

namespace PowerLinesWeb.Analysis;

public class CalibrationProvider(ApplicationDbContext dbContext) : ICalibrationProvider
{
    readonly ApplicationDbContext dbContext = dbContext;
    // Legacy reporting buckets have unknown chronology and are never training evidence.
    public ProbabilityCalibrator Get(string division) => ProbabilityCalibrator.None;
    public CalibrationArtifact GetArtifact(string modelVersion, DateTime cutoff) => dbContext.CalibrationArtifacts.AsNoTracking()
        .Where(x => x.Activated && x.ModelVersion == modelVersion && x.FitCutoff <= cutoff.Date && x.TrainingOrigin == "EarlierRawOutOfFold")
        .OrderByDescending(x => x.FitCutoff).FirstOrDefault();
}
