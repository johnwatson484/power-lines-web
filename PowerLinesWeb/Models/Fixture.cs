using System;

namespace PowerLinesWeb.Models
{
    public class Fixture
    {
        public int FixtureId { get; set; }
        public string Country { get; set; }
        public int CountryRank { get; set; }
        public string Division { get; set; }
        public int Tier { get; set; }
        public DateTime Date { get; set; }
        public string HomeTeam { get; set; }
        public string AwayTeam { get; set; }
        public decimal HomeOdds { get; set; }
        public decimal DrawOdds { get; set; }
        public decimal AwayOdds { get; set; }
        public int HomeGoals { get; set; }
        public int AwayGoals { get; set; }
        public decimal ExpectedGoals { get; set; }
        public bool IsValid { get; set; }
        public string Recommended { get; set; }
        public string LowerRecommended { get; set; }
        public DateTime Calculated { get; set; }
    }
}
