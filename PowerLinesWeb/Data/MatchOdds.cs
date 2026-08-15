using System.ComponentModel.DataAnnotations.Schema;

namespace PowerLinesWeb.Data;

[Table("match_odds")]
public class MatchOdds
{
    [Column("matchOddsId")]
    public int MatchOddsId { get; set; }

    [Column("fixtureId")]
    public int FixtureId { get; set; }

    [Column("home")]
    public decimal Home { get; set; }

    [Column("draw")]
    public decimal Draw { get; set; }

    [Column("away")]
    public decimal Away { get; set; }

    [Column("expectedHomeGoals")]
    public int HomeGoals { get; set; }

    [Column("expectedAwayGoals")]
    public int AwayGoals { get; set; }

    [Column("expectedGoals")]
    public decimal ExpectedGoals { get; set; }

    [Column("homeProbability")]
    public decimal HomeProbability { get; set; }

    [Column("drawProbability")]
    public decimal DrawProbability { get; set; }

    [Column("awayProbability")]
    public decimal AwayProbability { get; set; }

    [Column("recommended")]
    public string Recommended { get; set; }

    [Column("lowerRecommended")]
    public string LowerRecommended { get; set; }

    [Column("valueSelection")]
    public string ValueSelection { get; set; }

    [Column("valueEdge")]
    public decimal ValueEdge { get; set; }

    [Column("valueOdds")]
    public decimal ValueOdds { get; set; }

    [Column("valueStake")]
    public decimal ValueStake { get; set; }

    [Column("calculated")]
    public DateTime Calculated { get; set; }
}
