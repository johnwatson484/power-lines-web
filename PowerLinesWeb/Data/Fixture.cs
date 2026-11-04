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

    public long Revision { get; set; }
    public DateTime? Updated { get; set; }
    public string SourceUrl { get; set; }
    public string SourceSeason { get; set; }
    public string SourceFileHash { get; set; }
    public string SourceRowId { get; set; }
    public string SourceMatchId { get; set; }
    public string PriceKind { get; set; }
    public string PriceCollection { get; set; }
    public DateTime? QuoteObservedAt { get; set; }

    public DateTime? Created { get; set; }
    public string AnalysisKey { get; set; }
    public string AnalysisStatus { get; set; } = "Awaiting analysis";
    public string LifecycleStatus { get; set; } = "Scheduled";

    public virtual MatchOdds MatchOdds { get; set; }
}
