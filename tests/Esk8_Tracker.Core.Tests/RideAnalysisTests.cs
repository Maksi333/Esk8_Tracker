using Esk8_Tracker.Core;
using Esk8_Tracker.Core.Models;

namespace Esk8_Tracker.Core.Tests;

public class RideAnalysisTests
{
    private static readonly DateTime T0 = new(2026, 7, 2, 10, 0, 0, DateTimeKind.Utc);

    private static TrackPoint Pt(double sec, double lat, double lon, double speed = 5, double? ele = null) =>
        new()
        {
            RideId = 1, Timestamp = T0.AddSeconds(sec),
            Latitude = lat, Longitude = lon, SpeedMps = speed, AccuracyMeters = 5,
            AltitudeMeters = ele,
        };

    [Fact]
    public void BuildSamples_ProjectsNorthUp_AndAccumulatesDistance()
    {
        // Move due north: latitude increases → screen Y must decrease (north-up).
        var points = new List<TrackPoint> { Pt(0, 55.0, 12.0), Pt(10, 55.001, 12.0) };
        var s = RideAnalysis.BuildSamples(points);

        Assert.Equal(2, s.Count);
        Assert.Equal(0, s[0].DistanceMeters);
        Assert.InRange(s[1].DistanceMeters, 100, 122); // ~111 m per 0.001° lat
        Assert.True(s[1].Y < s[0].Y);
        Assert.Equal(s[1].X, s[0].X, 3);
        Assert.Equal(10, s[1].TimeSeconds);
    }

    [Fact]
    public void BuildSamples_DownsamplesHugeRides_KeepsLastPoint()
    {
        var points = new List<TrackPoint>();
        for (var i = 0; i < 4000; i++)
            points.Add(Pt(i, 55 + i * 0.00001, 12));
        var s = RideAnalysis.BuildSamples(points);

        Assert.True(s.Count <= RideAnalysis.MaxRenderSamples + 1);
        Assert.Equal(3999, s[^1].TimeSeconds);
    }

    [Fact]
    public void ElevationGain_IgnoresJitter_CountsRealClimb()
    {
        var points = new List<TrackPoint>();
        // 1 m of noise up/down repeatedly: below the 2 m hysteresis → no gain
        for (var i = 0; i < 20; i++)
            points.Add(Pt(i, 55 + i * 0.0001, 12, ele: 40 + (i % 2)));
        Assert.Equal(0, RideAnalysis.ElevationGainMeters(points), 1);

        // A genuine 30 m climb
        points.Clear();
        for (var i = 0; i <= 30; i++)
            points.Add(Pt(i, 55 + i * 0.0001, 12, ele: 40 + i));
        Assert.InRange(RideAnalysis.ElevationGainMeters(points), 24, 31);
    }

    [Fact]
    public void ElevationGain_NoAltitudes_IsZero()
    {
        var points = new List<TrackPoint> { Pt(0, 55, 12), Pt(5, 55.001, 12) };
        Assert.Equal(0, RideAnalysis.ElevationGainMeters(points));
    }

    [Fact]
    public void Splits_PerKm_ComputeAvgAndTop()
    {
        // Constant 10 m/s due north for 250 s → 2.5 km → 2 full splits + partial
        var points = new List<TrackPoint>();
        for (var i = 0; i <= 250; i++)
            points.Add(Pt(i, 55 + i * (10 / 110_540.0), 12, speed: 10));
        var samples = RideAnalysis.BuildSamples(points);
        var splits = RideAnalysis.BuildSplits(samples, 1000);

        Assert.InRange(splits.Count, 2, 3);
        Assert.Equal(1, splits[0].Index);
        Assert.InRange(splits[0].AvgSpeedMps, 9, 11);
        Assert.InRange(splits[0].TopSpeedMps, 9.5, 10.5);
    }

    [Fact]
    public void Splits_TinyRide_Empty()
    {
        var points = new List<TrackPoint> { Pt(0, 55, 12), Pt(5, 55.0005, 12) };
        var samples = RideAnalysis.BuildSamples(points);
        Assert.Empty(RideAnalysis.BuildSplits(samples, 1000));
    }
}
