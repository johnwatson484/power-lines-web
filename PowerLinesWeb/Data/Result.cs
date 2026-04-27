using System.ComponentModel.DataAnnotations.Schema;

namespace PowerLinesWeb.Data;

[Table("results")]
public class Result
{
    [Column("resultId")]
    public int ResultId { get; set; }

    [Column("division")]
    public string Division { get; set; }

    [Column("date")]
    public DateTime Date { get; set; }

    [Column("homeTeam")]
    public string HomeTeam { get; set; }

    [Column("awayTeam")]
    public string AwayTeam { get; set; }

    [Column("fullTimeHomeGoals")]
    public int FullTimeHomeGoals { get; set; }

    [Column("fullTimeAwayGoals")]
    public int FullTimeAwayGoals { get; set; }

    [Column("fullTimeResult")]
    public string FullTimeResult { get; set; }

    [Column("halfTimeHomeGoals")]
    public int HalfTimeHomeGoals { get; set; }

    [Column("halfTimeAwayGoals")]
    public int HalfTimeAwayGoals { get; set; }

    [Column("halfTimeResult")]
    public string HalfTimeResult { get; set; }

    [Column("homeOddsAverage")]
    public decimal HomeOddsAverage { get; set; }

    [Column("drawOddsAverage")]
    public decimal DrawOddsAverage { get; set; }

    [Column("awayOddsAverage")]
    public decimal AwayOddsAverage { get; set; }

    [Column("created")]
    public DateTime Created { get; set; }

    public virtual ResultMatchOdds ResultMatchOdds { get; set; }

    public Result()
    {
        Created = DateTime.UtcNow;
    }
}
