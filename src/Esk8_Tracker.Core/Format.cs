namespace Esk8_Tracker.Core;

/// <summary>Display formatting (current culture): km, km/h, h:mm:ss.</summary>
public static class Format
{
    public static string SpeedKmh(double mps) => (mps * 3.6).ToString("0.0");

    public static string DistanceKm(double meters) => (meters / 1000.0).ToString("0.00");

    public static string Duration(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
    }
}
