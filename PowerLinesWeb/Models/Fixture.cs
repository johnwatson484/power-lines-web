using System.ComponentModel.DataAnnotations;

namespace PowerLinesWeb.Models;

public class Fixture
{
    public int FixtureId { get; set; }
    public string Country { get; set; }
    public int CountryRank { get; set; }
    public string Division { get; set; }
    public int Tier { get; set; }

    [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm}")]
    public DateTime Date { get; set; }

    [Display(Name = "Home")]
    public string HomeTeam { get; set; }

    [Display(Name = "Away")]
    public string AwayTeam { get; set; }

    [Display(Name = @"1")]
    [DisplayFormat(DataFormatString = "{0:n2}")]
    public decimal HomeOdds { get; set; }

    [Display(Name = "X")]
    [DisplayFormat(DataFormatString = "{0:n2}")]
    public decimal DrawOdds { get; set; }

    [Display(Name = @"2")]
    [DisplayFormat(DataFormatString = "{0:n2}")]
    public decimal AwayOdds { get; set; }

    public int HomeGoals { get; set; }
    public int AwayGoals { get; set; }

    [Display(Name = "Prediction")]
    public string ExpectedScore
    {
        get
        {
            return string.Format("{0} : {1}", HomeGoals, AwayGoals);
        }
    }

    [DisplayFormat(DataFormatString = "{0:n2}")]
    public decimal ExpectedGoals { get; set; }
    public bool IsValid { get; set; }
    public string Recommended { get; set; }
    public string LowerRecommended { get; set; }
    public DateTime Calculated { get; set; }
}

