namespace Esk8_Tracker.Core.Data;

public record MonthlyStat(int Year, int Month, double DistanceMeters, int RideCount);

public record BoardStat(string BoardName, double DistanceMeters, int RideCount);

public class DashboardStats
{
    public double TotalDistanceMeters { get; init; }
    public int RideCount { get; init; }
    public double TotalMovingSeconds { get; init; }
    public double TopSpeedMps { get; init; }
    public double LongestRideMeters { get; init; }
    public List<MonthlyStat> Monthly { get; init; } = new();
    public List<BoardStat> PerBoard { get; init; } = new();
}
