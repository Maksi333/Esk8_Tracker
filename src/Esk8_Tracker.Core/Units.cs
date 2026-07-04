using System.Globalization;

namespace Esk8_Tracker.Core;

public enum UnitSystem { Metric, Imperial }

/// <summary>
/// All unit conversion + display formatting. Invariant culture: the design's
/// instrument numerals always use "." decimals.
/// </summary>
public static class Units
{
    public const double MetersPerMile = 1609.344;
    public const double KmPerMile = 1.609344;

    public static double DistanceValue(double meters, UnitSystem u) =>
        u == UnitSystem.Imperial ? meters / MetersPerMile : meters / 1000.0;

    public static double SpeedValue(double mps, UnitSystem u) =>
        u == UnitSystem.Imperial ? mps * 3.6 / KmPerMile : mps * 3.6;

    public static string DistanceUnit(UnitSystem u) => u == UnitSystem.Imperial ? "mi" : "km";

    public static string SpeedUnit(UnitSystem u) => u == UnitSystem.Imperial ? "mph" : "km/h";

    /// <summary>Split length in meters: per-km metric, per-mile imperial.</summary>
    public static double SplitMeters(UnitSystem u) => u == UnitSystem.Imperial ? MetersPerMile : 1000.0;

    // ---- display strings (invariant, tabular-friendly) ----

    public static string Distance(double meters, UnitSystem u, int decimals = 1) =>
        DistanceValue(meters, u).ToString("F" + decimals, CultureInfo.InvariantCulture);

    /// <summary>Whole-number distance with thousands separator (lifetime odometer).</summary>
    public static string DistanceWhole(double meters, UnitSystem u) =>
        Math.Round(DistanceValue(meters, u)).ToString("N0", CultureInfo.InvariantCulture);

    public static string Speed(double mps, UnitSystem u) =>
        Math.Round(SpeedValue(mps, u)).ToString(CultureInfo.InvariantCulture);

    public static string SpeedF1(double mps, UnitSystem u) =>
        SpeedValue(mps, u).ToString("F1", CultureInfo.InvariantCulture);

    /// <summary>m:ss under an hour, h:mm:ss above (matches the prototype timer format).</summary>
    public static string Duration(double seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}"
            : $"{t.Minutes}:{t.Seconds:D2}";
    }

    /// <summary>Compact duration for stat rows: "1:52" (h:mm) or "34m".</summary>
    public static string DurationCompact(double seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:D2}" : $"{t.Minutes}m";
    }

    public static string Elevation(double meters, UnitSystem u) =>
        u == UnitSystem.Imperial
            ? Math.Round(meters * 3.28084).ToString(CultureInfo.InvariantCulture)
            : Math.Round(meters).ToString(CultureInfo.InvariantCulture);

    public static string ElevationUnit(UnitSystem u) => u == UnitSystem.Imperial ? "ft" : "m";
}
