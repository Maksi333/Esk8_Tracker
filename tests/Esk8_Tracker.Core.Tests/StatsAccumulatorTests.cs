using Esk8_Tracker.Core;

namespace Esk8_Tracker.Core.Tests;

public class StatsAccumulatorTests
{
    private static readonly DateTime T0 = new(2026, 7, 2, 10, 0, 0, DateTimeKind.Utc);

    private static GpsFix Fix(double lat, double lon, double secondsAfterT0, double? speed = null) =>
        new(T0.AddSeconds(secondsAfterT0), lat, lon, speed, 5, null);

    [Fact]
    public void FreshAccumulator_IsAllZero()
    {
        var acc = new StatsAccumulator();
        Assert.Equal(0, acc.DistanceMeters);
        Assert.Equal(0, acc.MovingSeconds);
        Assert.Equal(0, acc.MaxSpeedMps);
        Assert.Equal(0, acc.AvgSpeedMps);
        Assert.Equal(0, acc.CurrentSpeedMps);
        Assert.Null(acc.LastFixUtc);
    }

    [Fact]
    public void FirstFix_SetsCurrentSpeed_ButNoDistance()
    {
        var acc = new StatsAccumulator();
        acc.Add(Fix(55.0, 12.0, 0, speed: 6.0));
        Assert.Equal(0, acc.DistanceMeters);
        Assert.Equal(6.0, acc.CurrentSpeedMps);
        Assert.Equal(T0, acc.LastFixUtc);
    }

    [Fact]
    public void TwoFixes_AccumulateHaversineDistance()
    {
        var acc = new StatsAccumulator();
        acc.Add(Fix(55.0, 12.0, 0));
        acc.Add(Fix(55.0001, 12.0, 2)); // ~11.1 m
        Assert.InRange(acc.DistanceMeters, 10.5, 11.7);
    }

    [Fact]
    public void MovingTime_OnlyCountsAboveThreshold()
    {
        var acc = new StatsAccumulator();
        acc.Add(Fix(55.0, 12.0, 0));
        acc.Add(Fix(55.0001, 12.0, 2));      // ~5.6 m/s -> moving, +2 s
        acc.Add(Fix(55.0001, 12.0, 10));     // same spot -> 0 m/s -> not moving
        Assert.Equal(2.0, acc.MovingSeconds, 3);
    }

    [Fact]
    public void GapOver30Seconds_DoesNotAccumulateDistanceOrTime()
    {
        var acc = new StatsAccumulator();
        acc.Add(Fix(55.0, 12.0, 0));
        acc.Add(Fix(55.01, 12.0, 45)); // 45 s gap: segment break
        Assert.Equal(0, acc.DistanceMeters);
        Assert.Equal(0, acc.MovingSeconds);
        // but the fix still becomes the new anchor:
        acc.Add(Fix(55.0101, 12.0, 47)); // ~11.1 m in 2 s
        Assert.InRange(acc.DistanceMeters, 10.5, 11.7);
    }

    [Fact]
    public void ReportedSpeed_PreferredOverImpliedSpeed()
    {
        var acc = new StatsAccumulator();
        acc.Add(Fix(55.0, 12.0, 0));
        acc.Add(Fix(55.0001, 12.0, 2, speed: 7.7));
        Assert.Equal(7.7, acc.CurrentSpeedMps);
        Assert.Equal(7.7, acc.MaxSpeedMps);
    }

    [Fact]
    public void ImpliedSpeed_UsedWhenReportedMissing()
    {
        var acc = new StatsAccumulator();
        acc.Add(Fix(55.0, 12.0, 0));
        acc.Add(Fix(55.0001, 12.0, 2)); // ~11.1 m / 2 s ~ 5.56 m/s
        Assert.InRange(acc.CurrentSpeedMps, 5.2, 5.9);
    }

    [Fact]
    public void MaxSpeed_TracksHighestSeen()
    {
        var acc = new StatsAccumulator();
        acc.Add(Fix(55.0, 12.0, 0, speed: 3));
        acc.Add(Fix(55.0001, 12.0, 2, speed: 9));
        acc.Add(Fix(55.0002, 12.0, 4, speed: 5));
        Assert.Equal(9, acc.MaxSpeedMps);
    }

    [Fact]
    public void AvgSpeed_IsDistanceOverMovingTime()
    {
        var acc = new StatsAccumulator();
        acc.Add(Fix(55.0, 12.0, 0));
        acc.Add(Fix(55.0001, 12.0, 2));
        acc.Add(Fix(55.0002, 12.0, 4));
        Assert.Equal(acc.DistanceMeters / acc.MovingSeconds, acc.AvgSpeedMps, 6);
    }

    [Fact]
    public void BreakSegment_PreventsDistanceAcrossPause()
    {
        var acc = new StatsAccumulator();
        acc.Add(Fix(55.0, 12.0, 0));
        acc.BreakSegment(); // e.g. user paused and rode elsewhere
        acc.Add(Fix(55.01, 12.0, 10)); // would be ~1112 m if not broken
        Assert.Equal(0, acc.DistanceMeters);
    }
}
