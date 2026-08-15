using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PowerLinesWeb.Data;

namespace PowerLinesWeb.Analysis;

// Scoped so a whole batch of fixtures shares one calibration query per division rather than re-reading
// the calibration table for every match.
public class CalibrationProvider(ApplicationDbContext dbContext, IOptions<ModelOptions> modelOptions) : ICalibrationProvider
{
    readonly ApplicationDbContext dbContext = dbContext;
    readonly ModelOptions modelOptions = modelOptions.Value;
    readonly Dictionary<string, ProbabilityCalibrator> calibrators = [];

    public ProbabilityCalibrator Get(string division)
    {
        if (!calibrators.TryGetValue(division, out var calibrator))
        {
            var buckets = dbContext.AccuracyCalibration.AsNoTracking()
                .Where(x => x.Division == division)
                .OrderBy(x => x.LowerBound)
                .ToList();

            calibrator = ProbabilityCalibrator.Build(buckets, modelOptions.MinCalibrationPredictions);
            calibrators.Add(division, calibrator);
        }

        return calibrator;
    }
}
