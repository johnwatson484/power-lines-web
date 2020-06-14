using System;
using System.ComponentModel.DataAnnotations;

namespace PowerLinesWeb.Models
{
    public class Accuracy
    {
        public int AccuracyId { get; set; }

        public string Division { get; set; }

        [Display(Name = "Analysed")]
        public int Matches { get; set; }

        public int Recommended { get; set; }

        [Display(Name = "Accuracy")]
        public decimal RecommendedAccuracy { get; set; }

        [Display(Name = "Low Recommended")]
        public int LowerRecommended { get; set; }

        [Display(Name = "Low Accuracy")]
        public decimal LowerRecommendedAccuracy { get; set; }

        public DateTime Calculated { get; set; }
    }
}
