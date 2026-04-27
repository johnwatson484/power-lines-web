using System.ComponentModel.DataAnnotations.Schema;

namespace PowerLinesWeb.Data;

[Table("fixtures")]
public class Fixture
{
    [Column("fixtureId")]
    public int FixtureId { get; set; }

    [Column("division")]
    public string Division { get; set; }

    [Column("date")]
    public DateTime Date { get; set; }

    [Column("homeTeam")]
    public string HomeTeam { get; set; }

    [Column("awayTeam")]
    public string AwayTeam { get; set; }

    [Column("homeOddsAverage")]
    public decimal HomeOddsAverage { get; set; }

    [Column("drawOddsAverage")]
    public decimal DrawOddsAverage { get; set; }

    [Column("awayOddsAverage")]
    public decimal AwayOddsAverage { get; set; }

    public virtual MatchOdds MatchOdds { get; set; }
}
