using SQLite;

namespace Esk8_Tracker.Core.Models;

public class TrackPoint
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int RideId { get; set; }

    public DateTime Timestamp { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public double SpeedMps { get; set; }

    public double AccuracyMeters { get; set; }

    /// <summary>Stored for future use (elevation charts); unused in v1 UI.</summary>
    public double? AltitudeMeters { get; set; }
}
