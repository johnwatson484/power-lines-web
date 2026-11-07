using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PowerLinesWeb.Data;
using PowerLinesWeb.Forecasts;

namespace PowerLinesWeb.Analysis;

public class CalibrationProvider(ApplicationDbContext dbContext) : ICalibrationProvider
{
    readonly ApplicationDbContext dbContext = dbContext;
    public CalibrationArtifact GetArtifact(string modelVersion, DateTime cutoff) => dbContext.CalibrationArtifacts.AsNoTracking()
        .Where(x => x.Activated && x.ModelVersion == modelVersion && x.FitCutoff <= cutoff.Date && x.TrainingOrigin == "EarlierRawOutOfFold")
        .OrderByDescending(x => x.FitCutoff).FirstOrDefault();
}
