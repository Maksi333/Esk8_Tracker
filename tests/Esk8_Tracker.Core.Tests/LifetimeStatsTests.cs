using Esk8_Tracker.Core;
using Esk8_Tracker.Core.Models;

namespace Esk8_Tracker.Core.Tests;

public class LifetimeStatsTests
{
    private static Ride Ride(string localDay, double distMeters = 5000, double movingSec = 900,
        double maxMps = 10, double gain = 20)
    {
        var local = DateTime.Parse(localDay + "T12:00:00");
        return new Ride
        {
            StartedAt = local.ToUniversalTime(),
            EndedAt = local.ToUniversalTime().AddSeconds(movingSec + 60),
            DistanceMeters = distMeters,
            MovingSeconds = movingSec,
            MaxSpeedMps = maxMps,
            ElevationGainMeters = gain,
        };
    }

    [Fact]
    public void Totals_SumAcrossRides()
    {
        var rides = new[] { Ride("2026-06-01"), Ride("2026-06-02", 3000, 600, 12, 5) };
        var t = LifetimeStats.Totals(rides);
        Assert.Equal(8000, t.DistanceMeters);
        Assert.Equal(2, t.RideCount);
        Assert.Equal(1500, t.MovingSeconds);
        Assert.Equal(25, t.ClimbMeters);
    }

    [Fact]
    public void EmptyRides_AllZero()
    {
        var t = LifetimeStats.Totals(Array.Empty<Ride>());
        var r = LifetimeStats.Records(Array.Empty<Ride>());
        Assert.Equal(0, t.DistanceMeters);
        Assert.Equal(0, r.TopSpeedMps);
        Assert.Equal(0, r.BestStreakDays);
    }

    [Fact]
    public void BestStreak_CountsConsecutiveLocalDays()
    {
        var rides = new[]
        {
            Ride("2026-06-01"), Ride("2026-06-02"), Ride("2026-06-02"), // duplicate day counts once
            Ride("2026-06-03"),
            Ride("2026-06-10"), Ride("2026-06-11"),
        };
        Assert.Equal(3, LifetimeStats.BestStreakDays(rides));
    }

    [Fact]
    public void CurrentStreak_DiesWhenYesterdayMissed()
    {
        var rides = new[] { Ride("2026-06-28"), Ride("2026-06-29"), Ride("2026-06-30") };
        Assert.Equal(3, LifetimeStats.CurrentStreakDays(rides, new DateOnly(2026, 7, 1)));
        Assert.Equal(0, LifetimeStats.CurrentStreakDays(rides, new DateOnly(2026, 7, 3)));
    }

    [Fact]
    public void WeeklyDistance_BucketsMondayWeeks()
    {
        // 2026-06-29 is a Monday; "today" = Wed 2026-07-01.
        var rides = new[]
        {
            Ride("2026-06-30", 4000),          // this week
            Ride("2026-06-29", 1000),          // this week
            Ride("2026-06-28", 2000),          // last week (Sunday)
            Ride("2026-01-01", 9000),          // far outside the 12-week window
        };
        var weeks = LifetimeStats.WeeklyDistanceMeters(rides, new DateOnly(2026, 7, 1), 12);
        Assert.Equal(12, weeks.Length);
        Assert.Equal(5000, weeks[^1]);
        Assert.Equal(2000, weeks[^2]);
        Assert.Equal(0, weeks.Take(10).Sum());
    }
}
