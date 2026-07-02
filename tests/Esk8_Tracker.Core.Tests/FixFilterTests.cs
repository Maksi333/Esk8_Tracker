using Esk8_Tracker.Core;

namespace Esk8_Tracker.Core.Tests;

public class FixFilterTests
{
    private static readonly DateTime T0 = new(2026, 7, 2, 10, 0, 0, DateTimeKind.Utc);

    private static GpsFix Fix(double lat, double lon, double secondsAfterT0,
        double accuracy = 5, double? speed = null) =>
        new(T0.AddSeconds(secondsAfterT0), lat, lon, speed, accuracy, null);

    [Fact]
    public void FirstFix_WithGoodAccuracy_IsAccepted()
    {
        Assert.True(FixFilter.ShouldAccept(null, Fix(55.0, 12.0, 0)));
    }

    [Fact]
    public void PoorAccuracy_IsRejected()
    {
        Assert.False(FixFilter.ShouldAccept(null, Fix(55.0, 12.0, 0, accuracy: 31)));
    }

    [Fact]
    public void AccuracyExactlyAtThreshold_IsAccepted()
    {
        Assert.True(FixFilter.ShouldAccept(null, Fix(55.0, 12.0, 0, accuracy: 30)));
    }

    [Fact]
    public void ImpliedSpeedAbove120Kmh_IsRejected()
    {
        var prev = Fix(55.0, 12.0, 0);
        // 0.01 deg latitude = ~1112 m in 10 s = ~400 km/h
        var spike = Fix(55.01, 12.0, 10);
        Assert.False(FixFilter.ShouldAccept(prev, spike));
    }

    [Fact]
    public void ReportedSpeedAbove120Kmh_IsRejected()
    {
        var prev = Fix(55.0, 12.0, 0);
        var next = Fix(55.00001, 12.0, 1, speed: 40.0); // 144 km/h claimed
        Assert.False(FixFilter.ShouldAccept(prev, next));
    }

    [Fact]
    public void NormalRidingFix_IsAccepted()
    {
        var prev = Fix(55.0, 12.0, 0);
        // ~11 m in 2 s = ~20 km/h
        var next = Fix(55.0001, 12.0, 2, speed: 5.5);
        Assert.True(FixFilter.ShouldAccept(prev, next));
    }

    [Fact]
    public void ImpliedSpeed_NotChecked_AcrossLargeTimeGaps()
    {
        // After a 60 s signal gap the rider may legitimately be far away;
        // gap handling is StatsAccumulator's job, not the filter's.
        var prev = Fix(55.0, 12.0, 0);
        var afterGap = Fix(55.01, 12.0, 60); // ~1112 m in 60 s = ~67 km/h, fine
        Assert.True(FixFilter.ShouldAccept(prev, afterGap));
    }
}
