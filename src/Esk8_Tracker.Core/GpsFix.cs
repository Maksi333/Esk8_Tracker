namespace Esk8_Tracker.Core;

/// <summary>A single GPS fix as delivered by a platform location source.</summary>
public record GpsFix(
    DateTime TimestampUtc,
    double Latitude,
    double Longitude,
    double? SpeedMps,
    double AccuracyMeters,
    double? AltitudeMeters);
