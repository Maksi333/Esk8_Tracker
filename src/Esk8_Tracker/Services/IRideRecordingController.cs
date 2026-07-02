namespace Esk8_Tracker.Services;

/// <summary>
/// Platform hook that keeps GPS fixes flowing to the RideRecorder while a ride
/// is active (a foreground service on Android). The RideRecorder itself is
/// started/stopped by the ViewModel in shared code.
/// </summary>
public interface IRideRecordingController
{
    bool IsSupported { get; }

    /// <summary>Android 12+ users can grant approximate-only location; the GPS provider needs precise.</summary>
    bool HasPreciseLocation { get; }

    void StartLocationService(int boardId);

    void StopLocationService();
}

public class UnsupportedRideRecordingController : IRideRecordingController
{
    public bool IsSupported => false;

    public bool HasPreciseLocation => false;

    public void StartLocationService(int boardId) { }

    public void StopLocationService() { }
}
