using Esk8_Tracker.Core.Models;

namespace Esk8_Tracker.Core;

public enum AchievementState { Locked, InProgress, Earned }

public record AchievementDef(string Id, string IconKey, string Title, string Short, string Criteria, string Category);

public record AchievementStatus(AchievementDef Def, AchievementState State, double Progress, DateTime? EarnedAtUtc);

/// <summary>
/// Evaluates the badge set against real saved rides. A fresh install earns nothing;
/// there is no persisted achievement state to get out of sync — everything is
/// recomputed from ride history on demand.
/// </summary>
public static class Achievements
{
    public static readonly IReadOnlyList<AchievementDef> Defs = new AchievementDef[]
    {
        new("first_ride", "looks_one", "First ride", "First ride", "Complete and save your very first ride.", "Distance"),
        new("century", "straighten", "Century club", "100 km", "Ride 100 km total across all rides.", "Distance"),
        new("thousand", "public", "1000 km", "1000 km", "Ride 1,000 km total. You are officially addicted.", "Distance"),
        new("speed_40", "bolt", "Speed demon", "40 km/h", "Break 40 km/h on any ride.", "Speed"),
        new("speed_50", "rocket_launch", "Terminal velocity", "50 km/h", "Hit 50 km/h. Ride within your limits.", "Speed"),
        new("streak_7", "local_fire_department", "On a roll", "7-day streak", "Ride at least once a day for 7 days straight.", "Streak"),
        new("streak_30", "calendar_month", "Iron month", "30-day streak", "Ride every day for a full month.", "Streak"),
        new("night_owl", "nightlight", "Night owl", "Night ride", "Complete a ride that starts after 9 PM.", "Session"),
        new("climber", "terrain", "Climber", "500 m climb", "Climb 500 m of elevation in one ride.", "Session"),
        new("marathon", "route", "Marathon", "42 km ride", "Ride 42.2 km in a single session.", "Session"),
        new("explorer", "explore", "Explorer", "5 areas", "Log rides in 5 distinct areas.", "Explore"),
        new("collector", "garage", "Collector", "3 boards", "Ride on 3 different boards.", "Collection"),
        new("early_bird", "wb_sunny", "Sunrise rider", "Dawn ride", "Start a ride before 6 AM.", "Session"),
        new("fifty_rides", "star", "Fifty club", "50 rides", "Save 50 rides. A true regular.", "Distance"),
        new("endurance", "timer", "Endurance", "2h session", "Ride for 2 hours of moving time in one session.", "Session"),
        new("squad", "groups", "Squad", "Group ride", "Log a ride tagged as a group session.", "Session"),
    };

    /// <summary>~2 km grid cell of the ride start; distinct cells = distinct areas.</summary>
    private static (int, int)? AreaCell(Ride r) =>
        r is { StartLatitude: { } lat, StartLongitude: { } lon }
            ? ((int)Math.Floor(lat / 0.02), (int)Math.Floor(lon / 0.02))
            : null;

    public static IReadOnlyList<AchievementStatus> Evaluate(IReadOnlyList<Ride> rides, DateOnly todayLocal)
    {
        var ordered = rides.OrderBy(r => r.StartedAt).ToList();
        var result = new List<AchievementStatus>(Defs.Count);
        foreach (var def in Defs)
            result.Add(EvaluateOne(def, ordered, todayLocal));
        return result;
    }

    private static AchievementStatus EvaluateOne(AchievementDef def, List<Ride> rides, DateOnly todayLocal)
    {
        return def.Id switch
        {
            "first_ride" => Cumulative(def, rides, r => 1, 1),
            "century" => Cumulative(def, rides, r => r.DistanceMeters, 100_000),
            "thousand" => Cumulative(def, rides, r => r.DistanceMeters, 1_000_000),
            "fifty_rides" => Cumulative(def, rides, r => 1, 50),
            "speed_40" => Best(def, rides, r => r.MaxSpeedMps * 3.6, 40),
            "speed_50" => Best(def, rides, r => r.MaxSpeedMps * 3.6, 50),
            "climber" => Best(def, rides, r => r.ElevationGainMeters, 500),
            "marathon" => Best(def, rides, r => r.DistanceMeters, 42_200),
            "endurance" => Best(def, rides, r => r.MovingSeconds, 7200),
            "streak_7" => Streak(def, rides, 7, todayLocal),
            "streak_30" => Streak(def, rides, 30, todayLocal),
            "night_owl" => FirstMatch(def, rides, r => r.StartedAt.ToLocalTime().Hour >= 21),
            "early_bird" => FirstMatch(def, rides, r => r.StartedAt.ToLocalTime().Hour < 6),
            "squad" => FirstMatch(def, rides, r => r.TagList.Contains("Group", StringComparer.OrdinalIgnoreCase)),
            "explorer" => DistinctCount(def, rides, AreaCell, 5),
            "collector" => DistinctCount(def, rides, r => (object?)(r.BoardId == 0 ? null : r.BoardId), 3),
            _ => new AchievementStatus(def, AchievementState.Locked, 0, null),
        };
    }

    private static AchievementStatus Cumulative(AchievementDef def, List<Ride> rides,
        Func<Ride, double> value, double target)
    {
        double sum = 0;
        foreach (var r in rides)
        {
            sum += value(r);
            if (sum >= target)
                return new(def, AchievementState.Earned, 1, r.EndedAt ?? r.StartedAt);
        }
        return Partial(def, sum / target);
    }

    private static AchievementStatus Best(AchievementDef def, List<Ride> rides,
        Func<Ride, double> value, double target)
    {
        double best = 0;
        foreach (var r in rides)
        {
            best = Math.Max(best, value(r));
            if (best >= target)
                return new(def, AchievementState.Earned, 1, r.EndedAt ?? r.StartedAt);
        }
        return Partial(def, best / target);
    }

    private static AchievementStatus FirstMatch(AchievementDef def, List<Ride> rides, Func<Ride, bool> pred)
    {
        var hit = rides.FirstOrDefault(pred);
        return hit is null
            ? new(def, AchievementState.Locked, 0, null)
            : new(def, AchievementState.Earned, 1, hit.EndedAt ?? hit.StartedAt);
    }

    private static AchievementStatus DistinctCount<T>(AchievementDef def, List<Ride> rides,
        Func<Ride, T?> key, int target)
    {
        var seen = new HashSet<T>();
        foreach (var r in rides)
        {
            var k = key(r);
            if (k is null) continue;
            seen.Add(k);
            if (seen.Count >= target)
                return new(def, AchievementState.Earned, 1, r.EndedAt ?? r.StartedAt);
        }
        return Partial(def, seen.Count / (double)target);
    }

    private static AchievementStatus Streak(AchievementDef def, List<Ride> rides, int target, DateOnly todayLocal)
    {
        // Earned when the best-ever streak reached the target; walk days to find when.
        var days = rides.Select(r => (Day: DateOnly.FromDateTime(r.StartedAt.ToLocalTime()), Ride: r))
            .GroupBy(x => x.Day).OrderBy(g => g.Key).ToList();
        var run = 0;
        DateOnly? prev = null;
        foreach (var g in days)
        {
            run = prev is { } p && g.Key.DayNumber - p.DayNumber == 1 ? run + 1 : 1;
            prev = g.Key;
            if (run >= target)
            {
                var r = g.First().Ride;
                return new(def, AchievementState.Earned, 1, r.EndedAt ?? r.StartedAt);
            }
        }
        // In-progress = the streak still alive today counts toward the goal.
        var current = LifetimeStats.CurrentStreakDays(rides, todayLocal);
        return Partial(def, current / (double)target);
    }

    private static AchievementStatus Partial(AchievementDef def, double progress)
    {
        progress = Math.Clamp(progress, 0, 0.999);
        return progress > 0
            ? new(def, AchievementState.InProgress, progress, null)
            : new(def, AchievementState.Locked, 0, null);
    }
}
