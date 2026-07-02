namespace Esk8_Tracker.Core;

/// <summary>
/// Decides whether a raw GPS fix is trustworthy enough to store and feed into stats.
/// Junk fixes must never pollute the database or the top-speed record.
/// </summary>
public static class FixFilter
{
    public const double MaxAccuracyMeters = 30.0;
    public const double MaxPlausibleSpeedMps = 120.0 / 3.6;

    public static bool ShouldAccept(GpsFix? previousAccepted, GpsFix candidate)
    {
        if (candidate.AccuracyMeters > MaxAccuracyMeters)
            return false;

        if (candidate.SpeedMps is > MaxPlausibleSpeedMps)
            return false;

        if (previousAccepted is not null)
        {
            var dt = (candidate.TimestampUtc - previousAccepted.TimestampUtc).TotalSeconds;
            if (dt > 0 && dt <= StatsAccumulator.GapSeconds)
            {
                var meters = GeoMath.HaversineMeters(
                    previousAccepted.Latitude, previousAccepted.Longitude,
                    candidate.Latitude, candidate.Longitude);
                if (meters / dt > MaxPlausibleSpeedMps)
                    return false;
            }
        }

        return true;
    }
}
