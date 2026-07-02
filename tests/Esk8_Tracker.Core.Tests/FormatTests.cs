using System.Globalization;
using Esk8_Tracker.Core;

namespace Esk8_Tracker.Core.Tests;

public class FormatTests
{
    public FormatTests()
    {
        // Machine culture is da-DK (comma decimals); pin per-test-thread for stable assertions.
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    [Fact]
    public void SpeedKmh_ConvertsAndFormats()
    {
        Assert.Equal("36.0", Format.SpeedKmh(10));
        Assert.Equal("0.0", Format.SpeedKmh(0));
    }

    [Fact]
    public void DistanceKm_ConvertsAndFormats()
    {
        Assert.Equal("1.50", Format.DistanceKm(1500));
        Assert.Equal("0.00", Format.DistanceKm(0));
    }

    [Fact]
    public void Duration_FormatsHoursMinutesSeconds()
    {
        Assert.Equal("0:05:30", Format.Duration(330));
        Assert.Equal("1:00:00", Format.Duration(3600));
        Assert.Equal("0:00:00", Format.Duration(0));
    }
}
