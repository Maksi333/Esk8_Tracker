using Esk8_Tracker.Core.Data;
using Esk8_Tracker.Core.Models;

namespace Esk8_Tracker.Core;

public enum RecorderState { Idle, Recording, Paused }

public record RideLiveStats(
    double DistanceMeters,
    double CurrentSpeedMps,
    double AvgSpeedMps,
    double MaxSpeedMps,
    double MovingSeconds);

/// <summary>
/// Owns the active ride: filters fixes, accumulates stats, buffers points to the
/// store every 10 points / 10 seconds (crash safety), and raises UI events.
/// Single-threaded by design: platform fixes and UI calls both arrive on the main thread.
/// </summary>
public class RideRecorder(IRideStore store)
{
    public const int FlushEveryPoints = 10;
    public const double FlushEverySeconds = 10.0;
    public const double SignalLostSeconds = 5.0;

    private StatsAccumulator _acc = new();
    private GpsFix? _lastAccepted;
    private readonly List<TrackPoint> _buffer = new();
    private readonly List<(double Lat, double Lon)> _route = new();
    private DateTime? _lastFlushUtc;

    public RecorderState State { get; private set; } = RecorderState.Idle;
    public int? ActiveRideId { get; private set; }
    public DateTime? RideStartedUtc { get; private set; }
    public IReadOnlyList<(double Lat, double Lon)> RoutePoints => _route;

    public RideLiveStats CurrentStats => new(
        _acc.DistanceMeters, _acc.CurrentSpeedMps, _acc.AvgSpeedMps, _acc.MaxSpeedMps, _acc.MovingSeconds);

    public event Action? StateChanged;
    public event Action<RideLiveStats>? StatsUpdated;
    public event Action<GpsFix>? FixAccepted;

    /// <summary>Every fix as delivered, before filtering, in Recording *and* Paused —
    /// the auto-pause monitor needs to see movement while frozen.</summary>
    public event Action<GpsFix>? RawFix;

    public async Task<int> StartAsync(int boardId, DateTime nowUtc)
    {
        if (State != RecorderState.Idle)
            throw new InvalidOperationException($"Cannot start a ride while {State}.");

        _acc = new StatsAccumulator();
        _lastAccepted = null;
        _buffer.Clear();
        _route.Clear();
        _lastFlushUtc = null;

        ActiveRideId = await store.CreateRideAsync(boardId, nowUtc);
        RideStartedUtc = nowUtc;
        State = RecorderState.Recording;
        StateChanged?.Invoke();
        return ActiveRideId.Value;
    }

    public void Pause()
    {
        if (State != RecorderState.Recording) return;
        State = RecorderState.Paused;
        _acc.BreakSegment();
        _lastAccepted = null;
        StateChanged?.Invoke();
    }

    public void Resume()
    {
        if (State != RecorderState.Paused) return;
        State = RecorderState.Recording;
        StateChanged?.Invoke();
    }

    public async Task StopAsync(DateTime nowUtc)
    {
        if (State == RecorderState.Idle)
            throw new InvalidOperationException("No active ride to stop.");

        // Failures here propagate loudly and leave the ride active so Stop can be retried.
        var rideId = ActiveRideId!.Value;
        await FlushAsync();
        await store.FinalizeRideAsync(rideId, _acc.DistanceMeters, _acc.MovingSeconds,
            _acc.AvgSpeedMps, _acc.MaxSpeedMps, nowUtc, wasRecovered: false);

        State = RecorderState.Idle;
        ActiveRideId = null;
        RideStartedUtc = null;
        StateChanged?.Invoke();
    }

    /// <summary>
    /// Continue an interrupted ride found on relaunch ("Resume unsaved ride?").
    /// Rebuilds live stats from the persisted points and keeps appending to the same ride.
    /// </summary>
    public void ResumeRecovered(Ride unfinished, IReadOnlyList<TrackPoint> points)
    {
        if (State != RecorderState.Idle)
            throw new InvalidOperationException($"Cannot resume a recovered ride while {State}.");

        _acc = new StatsAccumulator();
        _buffer.Clear();
        _route.Clear();
        _lastFlushUtc = null;
        foreach (var p in points)
        {
            _acc.Add(new GpsFix(p.Timestamp, p.Latitude, p.Longitude,
                p.SpeedMps, p.AccuracyMeters, p.AltitudeMeters));
            _route.Add((p.Latitude, p.Longitude));
        }
        // The gap between the last stored point and "now" exceeds GapSeconds, so the
        // accumulator restarts its segment on the next fix instead of teleporting.
        _lastAccepted = null;

        ActiveRideId = unfinished.Id;
        RideStartedUtc = unfinished.StartedAt;
        State = RecorderState.Recording;
        StateChanged?.Invoke();
        StatsUpdated?.Invoke(CurrentStats);
    }

    public async Task OnFixAsync(GpsFix fix)
    {
        if (State != RecorderState.Idle) RawFix?.Invoke(fix);
        if (State != RecorderState.Recording) return;
        if (!FixFilter.ShouldAccept(_lastAccepted, fix)) return;

        _lastAccepted = fix;
        _acc.Add(fix);
        _route.Add((fix.Latitude, fix.Longitude));

        _buffer.Add(new TrackPoint
        {
            RideId = ActiveRideId!.Value,
            Timestamp = fix.TimestampUtc,
            Latitude = fix.Latitude,
            Longitude = fix.Longitude,
            SpeedMps = _acc.CurrentSpeedMps,
            AccuracyMeters = fix.AccuracyMeters,
            AltitudeMeters = fix.AltitudeMeters,
        });

        FixAccepted?.Invoke(fix);
        StatsUpdated?.Invoke(CurrentStats);

        _lastFlushUtc ??= fix.TimestampUtc;
        var due = _buffer.Count >= FlushEveryPoints
               || (fix.TimestampUtc - _lastFlushUtc.Value).TotalSeconds >= FlushEverySeconds;
        if (due)
        {
            _lastFlushUtc = fix.TimestampUtc;
            try
            {
                await FlushAsync();
            }
            catch (Exception ex)
            {
                // Transient store failure must not kill recording. FlushAsync kept the
                // points in the buffer, so the count trigger fires again on the next fix.
                System.Diagnostics.Debug.WriteLine($"Point flush failed; will retry: {ex}");
            }
        }
    }

    public bool IsSignalLost(DateTime nowUtc)
    {
        if (State != RecorderState.Recording) return false;
        var reference = _acc.LastFixUtc ?? RideStartedUtc;
        return reference is not null && (nowUtc - reference.Value).TotalSeconds > SignalLostSeconds;
    }

    private async Task FlushAsync()
    {
        if (_buffer.Count == 0) return;
        var batch = _buffer.ToList();
        _buffer.Clear();
        try
        {
            await store.SavePointsAsync(batch);
        }
        catch
        {
            // Crash safety: a failed save must not lose points. Put them back
            // (in front — new fixes may have arrived) for the next attempt.
            _buffer.InsertRange(0, batch);
            throw;
        }
    }
}
