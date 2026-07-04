namespace Esk8_Tracker.Core;

/// <summary>
/// Rough remaining-range estimate from the board's battery capacity.
/// Heuristic consumption from the design prototype: ~12 Wh per km.
/// Only shown when the board has battery specs — an estimate, hence the "~" in UI.
/// </summary>
public static class RangeEstimator
{
    public const double WhPerKm = 12.0;

    /// <summary>Remaining meters given battery Wh and meters already ridden; null if no specs.</summary>
    public static double? RemainingMeters(double? batteryWh, double riddenMeters)
    {
        if (batteryWh is not > 0) return null;
        var totalMeters = batteryWh.Value / WhPerKm * 1000.0;
        return Math.Max(0, totalMeters - riddenMeters);
    }
}
