using SQLite;

namespace Esk8_Tracker.Core.Models;

public class Ride
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int BoardId { get; set; }

    public DateTime StartedAt { get; set; }

    /// <summary>Null while the ride is in progress; set when finalized.</summary>
    public DateTime? EndedAt { get; set; }

    public double DistanceMeters { get; set; }

    public double MovingSeconds { get; set; }

    public double AvgSpeedMps { get; set; }

    public double MaxSpeedMps { get; set; }

    public bool WasRecovered { get; set; }
}
