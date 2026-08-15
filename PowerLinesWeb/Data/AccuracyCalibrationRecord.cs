using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PowerLinesWeb.Data;

// One row per probability band, so the model can be checked for saying 70% only when it happens 70%
// of the time rather than just for picking winners.
[Table("accuracy_calibration")]
public class AccuracyCalibrationRecord
{
    [Key]
    [Column("accuracyCalibrationId")]
    public int AccuracyCalibrationId { get; set; }

    [Column("division")]
    public string Division { get; set; }

    [Column("lowerBound")]
    public decimal LowerBound { get; set; }

    [Column("upperBound")]
    public decimal UpperBound { get; set; }

    [Column("predicted")]
    public decimal Predicted { get; set; }

    [Column("observed")]
    public decimal Observed { get; set; }

    [Column("predictions")]
    public int Predictions { get; set; }

    [Column("calculated")]
    public DateTime Calculated { get; set; }
}
