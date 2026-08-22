using System.ComponentModel.DataAnnotations.Schema;

namespace PowerLinesWeb.Data;

[Table("result_match_odds")]
public class ResultMatchOdds
{
    [Column("matchOddsId")]
    public int ResultMatchOddsId { get; set; }

    [Column("resultId")]
    public int ResultId { get; set; }

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

    [Column("homeXg")]
    public decimal HomeXg { get; set; }

    [Column("awayXg")]
    public decimal AwayXg { get; set; }

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

    public bool IsRecommended => Recommended != "X";

    public bool IsLowerRecommended => LowerRecommended != "X";

    public bool IsValue => ValueSelection != "X";
}
