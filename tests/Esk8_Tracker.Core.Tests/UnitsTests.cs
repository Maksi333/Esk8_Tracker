using Esk8_Tracker.Core;

namespace Esk8_Tracker.Core.Tests;

public class UnitsTests
{
    [Fact]
    public void Distance_MetricAndImperial()
    {
        Assert.Equal("12.4", Units.Distance(12_400, UnitSystem.Metric));
        Assert.Equal("7.7", Units.Distance(12_400, UnitSystem.Imperial));
        Assert.Equal("1,284", Units.DistanceWhole(1_284_000, UnitSystem.Metric));
    }

    [Fact]
    public void Speed_RoundsWholeForDisplay()
    {
        Assert.Equal("28", Units.Speed(7.8, UnitSystem.Metric));    // 28.08 km/h
        Assert.Equal("17", Units.Speed(7.8, UnitSystem.Imperial));  // 17.45 mph
    }

    [Fact]
    public void Duration_MatchesInstrumentFormat()
    {
        Assert.Equal("0:05", Units.Duration(5));
        Assert.Equal("34:02", Units.Duration(2042));
        Assert.Equal("1:52:03", Units.Duration(6723));
        Assert.Equal("34m", Units.DurationCompact(2042));
        Assert.Equal("1:52", Units.DurationCompact(6723));
    }

    [Fact]
    public void SplitMeters_PerKmOrPerMile()
    {
        Assert.Equal(1000, Units.SplitMeters(UnitSystem.Metric));
        Assert.Equal(1609.344, Units.SplitMeters(UnitSystem.Imperial), 3);
    }

    [Fact]
    public void Labels_FollowUnitSystem()
    {
        Assert.Equal("km", Units.DistanceUnit(UnitSystem.Metric));
        Assert.Equal("mi", Units.DistanceUnit(UnitSystem.Imperial));
        Assert.Equal("km/h", Units.SpeedUnit(UnitSystem.Metric));
        Assert.Equal("mph", Units.SpeedUnit(UnitSystem.Imperial));
        Assert.Equal("ft", Units.ElevationUnit(UnitSystem.Imperial));
    }
}
