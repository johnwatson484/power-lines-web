using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PowerLinesWeb.Data;

[Table("accuracy")]
public class AccuracyRecord
{
    [Key]
    [Column("accuracyId")]
    public int AccuracyId { get; set; }

    [Column("division")]
    public string Division { get; set; }

    [Column("matches")]
    public int Matches { get; set; }

    [Column("recommended")]
    public int Recommended { get; set; }

    [Column("recommendedAccuracy")]
    public decimal RecommendedAccuracy { get; set; }

    [Column("lowerRecommended")]
    public int LowerRecommended { get; set; }

    [Column("lowerRecommendedAccuracy")]
    public decimal LowerRecommendedAccuracy { get; set; }

    [Column("baselineAccuracy")]
    public decimal BaselineAccuracy { get; set; }

    [Column("scoredMatches")]
    public int ScoredMatches { get; set; }

    [Column("logLoss")]
    public decimal LogLoss { get; set; }

    [Column("brierScore")]
    public decimal BrierScore { get; set; }

    [Column("pricedMatches")]
    public int PricedMatches { get; set; }

    [Column("marketLogLoss")]
    public decimal MarketLogLoss { get; set; }

    [Column("valueBets")]
    public int ValueBets { get; set; }

    [Column("valueWins")]
    public int ValueWins { get; set; }

    [Column("valueRoi")]
    public decimal ValueRoi { get; set; }

    [Column("calculated")]
    public DateTime Calculated { get; set; }
}
