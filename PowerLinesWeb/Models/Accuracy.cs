using System;
using System.ComponentModel.DataAnnotations;

namespace PowerLinesWeb.Models
{
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
        public DateTime Calculated { get; set; }
    }
}
