using System.ComponentModel.DataAnnotations;

namespace PowerLinesWeb.Models;

public class Accuracy
{
    public int AccuracyId { get; set; }
    public string Country { get; set; }
    public int CountryRank { get; set; }
    public string Division { get; set; }
    public int Tier { get; set; }
    [Display(Name = "Analysed")]
    public int Matches { get; set; }
    public int Recommended { get; set; }
    [Display(Name = "Accuracy")]
    [DisplayFormat(DataFormatString = "{0:P0}")]
    public decimal RecommendedAccuracy { get; set; }
    [Display(Name = "Low Recommended")]
    public int LowerRecommended { get; set; }
    [Display(Name = "Low Accuracy")]
    [DisplayFormat(DataFormatString = "{0:P0}")]
    public decimal LowerRecommendedAccuracy { get; set; }
    [Display(Name = "Always Home")]
    [DisplayFormat(DataFormatString = "{0:P0}")]
    public decimal BaselineAccuracy { get; set; }
    [Display(Name = "Log Loss")]
    [DisplayFormat(DataFormatString = "{0:n4}")]
    public decimal LogLoss { get; set; }
    [Display(Name = "Brier")]
    [DisplayFormat(DataFormatString = "{0:n4}")]
    public decimal BrierScore { get; set; }
    [Display(Name = "Market Log Loss")]
    [DisplayFormat(DataFormatString = "{0:n4}")]
    public decimal MarketLogLoss { get; set; }
    [Display(Name = "Value Bets")]
    public int ValueBets { get; set; }
    [Display(Name = "Value Wins")]
    public int ValueWins { get; set; }
    [Display(Name = "Value ROI")]
    [DisplayFormat(DataFormatString = "{0:P1}")]
    public decimal ValueRoi { get; set; }

    // Only meaningful where the market log loss was measured on the same matches.
    public bool BeatsMarket => MarketLogLoss > 0 && LogLoss > 0 && LogLoss < MarketLogLoss;

    public DateTime Calculated { get; set; }
}
