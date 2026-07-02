using Esk8_Tracker.Core.Models;

namespace Esk8_Tracker.Core.Data;

/// <summary>Persistence seam used by RideRecorder; implemented by Esk8Database.</summary>
public interface IRideStore
{
    Task<int> CreateRideAsync(int boardId, DateTime startedAtUtc);

    Task SavePointsAsync(IReadOnlyList<TrackPoint> points);

    Task FinalizeRideAsync(int rideId, double distanceMeters, double movingSeconds,
        double avgSpeedMps, double maxSpeedMps, DateTime endedAtUtc, bool wasRecovered);
}
