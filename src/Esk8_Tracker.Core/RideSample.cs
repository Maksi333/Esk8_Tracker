namespace Esk8_Tracker.Core;

/// <summary>
/// One analyzed point of a ride, ready for rendering. X/Y are meters in a local
/// equirectangular projection (route shape only — not for navigation), so route
/// views can scale-to-fit without knowing about geodesy.
/// </summary>
public record RideSample(
    double TimeSeconds,
    double DistanceMeters,
    double SpeedMps,
    double X,
    double Y,
    double? ElevationMeters);
