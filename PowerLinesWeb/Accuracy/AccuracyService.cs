using Microsoft.EntityFrameworkCore;
using PowerLinesWeb.Data;
using PowerLinesWeb.Fixtures;

namespace PowerLinesWeb.Accuracy;

public class AccuracyService(ApplicationDbContext dbContext) : IAccuracyService
{
    readonly ApplicationDbContext dbContext = dbContext;

    public List<PowerLinesWeb.Models.Accuracy> Get()
    {
        var result = new List<PowerLinesWeb.Models.Accuracy>();

        var records = dbContext.Accuracy.AsNoTracking();

        foreach (var record in records)
        {
            var division = new Division(record.Division);
            result.Add(new PowerLinesWeb.Models.Accuracy
            {
                AccuracyId = record.AccuracyId,
                Country = division.Country,
                CountryRank = division.CountryRank,
                Division = division.Name,
                Tier = division.Tier,
                Matches = record.Matches,
                Recommended = record.Recommended,
                RecommendedAccuracy = record.RecommendedAccuracy,
                LowerRecommended = record.LowerRecommended,
                LowerRecommendedAccuracy = record.LowerRecommendedAccuracy,
                BaselineAccuracy = record.BaselineAccuracy,
                LogLoss = record.LogLoss,
                BrierScore = record.BrierScore,
                MarketLogLoss = record.MarketLogLoss,
                ValueBets = record.ValueBets,
                ValueWins = record.ValueWins,
                ValueRoi = record.ValueRoi,
                Calculated = record.Calculated
            });
        }

        return result.OrderBy(x => x.CountryRank).ThenBy(x => x.Tier).ToList();
    }
}
