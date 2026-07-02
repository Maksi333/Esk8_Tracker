namespace Esk8_Tracker.Core;

/// <summary>Display formatting (current culture): km, km/h, h:mm:ss.</summary>
public static class Format
{
    public static string SpeedKmh(double mps) => (mps * 3.6).ToString("0.0");

    public static string DistanceKm(double meters) => (meters / 1000.0).ToString("0.00");

    public static string Duration(double seconds) =>
        TimeSpan.FromSeconds(seconds).ToString(@"h\:mm\:ss");
}
