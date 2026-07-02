using Esk8_Tracker.Core;

namespace Esk8_Tracker.Core.Tests;

public class GeoMathTests
{
    [Fact]
    public void SamePoint_IsZero()
    {
        Assert.Equal(0.0, GeoMath.HaversineMeters(55.6761, 12.5683, 55.6761, 12.5683), 6);
    }

    [Fact]
    public void OneDegreeOfLongitudeAtEquator_IsAbout111Km()
    {
        // 2 * pi * 6371000 / 360 = 111194.93 m
        var d = GeoMath.HaversineMeters(0, 0, 0, 1);
        Assert.InRange(d, 111_100, 111_300);
    }

    [Fact]
    public void IsSymmetric()
    {
        var a = GeoMath.HaversineMeters(55.6761, 12.5683, 55.6867, 12.5700);
        var b = GeoMath.HaversineMeters(55.6867, 12.5700, 55.6761, 12.5683);
        Assert.Equal(a, b, 9);
    }

    [Fact]
    public void ShortHop_IsPlausible()
    {
        // ~0.0001 deg latitude is ~11.1 m
        var d = GeoMath.HaversineMeters(55.6761, 12.5683, 55.6762, 12.5683);
        Assert.InRange(d, 10.5, 11.7);
    }
}
