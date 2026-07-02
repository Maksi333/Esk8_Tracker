namespace Esk8_Tracker.Core;

/// <summary>
/// Incremental ride statistics over an ordered stream of *accepted* fixes.
/// Filtering junk fixes is FixFilter's job; this class handles distance,
/// speed, moving time, and signal-gap segmentation.
/// </summary>
public class StatsAccumulator
{
    public const double GapSeconds = 30.0;
    public const double MovingThresholdMps = 1.0 / 3.6;

    private GpsFix? _prev;

    public double DistanceMeters { get; private set; }
    public double MovingSeconds { get; private set; }
    public double MaxSpeedMps { get; private set; }
    public double CurrentSpeedMps { get; private set; }
    public DateTime? LastFixUtc { get; private set; }

    public double AvgSpeedMps => MovingSeconds > 0 ? DistanceMeters / MovingSeconds : 0;

    public void Add(GpsFix fix)
    {
        LastFixUtc = fix.TimestampUtc;

        if (_prev is null)
        {
            CurrentSpeedMps = ClampNonNegative(fix.SpeedMps ?? 0);
            MaxSpeedMps = Math.Max(MaxSpeedMps, CurrentSpeedMps);
            _prev = fix;
            return;
        }

        var dt = (fix.TimestampUtc - _prev.TimestampUtc).TotalSeconds;
        if (dt <= 0 || dt > GapSeconds)
        {
            // Out-of-order or signal gap: restart the segment at this fix.
            CurrentSpeedMps = ClampNonNegative(fix.SpeedMps ?? 0);
            MaxSpeedMps = Math.Max(MaxSpeedMps, CurrentSpeedMps);
            _prev = fix;
            return;
        }

        var meters = GeoMath.HaversineMeters(_prev.Latitude, _prev.Longitude, fix.Latitude, fix.Longitude);
        var speed = ClampNonNegative(fix.SpeedMps ?? meters / dt);

        DistanceMeters += meters;
        if (speed > MovingThresholdMps)
            MovingSeconds += dt;

        CurrentSpeedMps = speed;
        MaxSpeedMps = Math.Max(MaxSpeedMps, speed);
        _prev = fix;
    }

    /// <summary>Forget the previous fix so nothing accumulates across a pause.</summary>
    public void BreakSegment() => _prev = null;

    private static double ClampNonNegative(double v) => v < 0 ? 0 : v;
}
