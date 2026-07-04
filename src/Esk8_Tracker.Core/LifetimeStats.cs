using Esk8_Tracker.Core.Models;

namespace Esk8_Tracker.Core;

public record LifetimeTotals(
    double DistanceMeters,
    int RideCount,
    double MovingSeconds,
    double ClimbMeters);

public record RideRecords(
    double TopSpeedMps,
    double LongestRideMeters,
    double LongestSessionSeconds,
    int BestStreakDays);

/// <summary>
/// Lifetime aggregates over saved rides. Everything derives from real data;
/// a fresh install genuinely reads zero.
/// </summary>
public static class LifetimeStats
{
    public static LifetimeTotals Totals(IReadOnlyList<Ride> rides) => new(
        rides.Sum(r => r.DistanceMeters),
        rides.Count,
        rides.Sum(r => r.MovingSeconds),
        rides.Sum(r => r.ElevationGainMeters));

    public static RideRecords Records(IReadOnlyList<Ride> rides) => new(
        rides.Count > 0 ? rides.Max(r => r.MaxSpeedMps) : 0,
        rides.Count > 0 ? rides.Max(r => r.DistanceMeters) : 0,
        rides.Count > 0 ? rides.Max(r => r.MovingSeconds) : 0,
        BestStreakDays(rides));

    /// <summary>Longest run of consecutive local calendar days with at least one ride.</summary>
    public static int BestStreakDays(IReadOnlyList<Ride> rides)
    {
        if (rides.Count == 0) return 0;
        var days = rides.Select(r => DateOnly.FromDateTime(r.StartedAt.ToLocalTime()))
            .Distinct().OrderBy(d => d).ToList();
        int best = 1, run = 1;
        for (var i = 1; i < days.Count; i++)
        {
            run = days[i].DayNumber - days[i - 1].DayNumber == 1 ? run + 1 : 1;
            best = Math.Max(best, run);
        }
        return best;
    }

    /// <summary>Current streak ending today/yesterday (for streak-achievement progress).</summary>
    public static int CurrentStreakDays(IReadOnlyList<Ride> rides, DateOnly todayLocal)
    {
        if (rides.Count == 0) return 0;
        var days = rides.Select(r => DateOnly.FromDateTime(r.StartedAt.ToLocalTime()))
            .Distinct().OrderByDescending(d => d).ToList();
        if (todayLocal.DayNumber - days[0].DayNumber > 1) return 0;
        var run = 1;
        for (var i = 1; i < days.Count; i++)
        {
            if (days[i - 1].DayNumber - days[i].DayNumber != 1) break;
            run++;
        }
        return run;
    }

    /// <summary>
    /// Distance per week for the trailing <paramref name="weeks"/> weeks (oldest → newest,
    /// weeks start Monday, last entry = the week containing <paramref name="todayLocal"/>).
    /// </summary>
    public static double[] WeeklyDistanceMeters(IReadOnlyList<Ride> rides, DateOnly todayLocal, int weeks = 12)
    {
        var result = new double[weeks];
        var thisMonday = todayLocal.AddDays(-(((int)todayLocal.DayOfWeek + 6) % 7));
        var firstMonday = thisMonday.AddDays(-7 * (weeks - 1));
        foreach (var r in rides)
        {
            var day = DateOnly.FromDateTime(r.StartedAt.ToLocalTime());
            var idx = (day.DayNumber - firstMonday.DayNumber) / 7;
            if (idx >= 0 && idx < weeks)
                result[idx] += r.DistanceMeters;
        }
        return result;
    }
}
