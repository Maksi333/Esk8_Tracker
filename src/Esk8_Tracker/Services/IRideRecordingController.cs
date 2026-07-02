namespace Esk8_Tracker.Services;

/// <summary>
/// Platform hook that keeps GPS fixes flowing to the RideRecorder while a ride
/// is active (a foreground service on Android). The RideRecorder itself is
/// started/stopped by the ViewModel in shared code.
/// </summary>
public interface IRideRecordingController
{
    bool IsSupported { get; }

    void StartLocationService(int boardId);

    void StopLocationService();
}

public class UnsupportedRideRecordingController : IRideRecordingController
{
    public bool IsSupported => false;

    public void StartLocationService(int boardId) { }

    public void StopLocationService() { }
}
