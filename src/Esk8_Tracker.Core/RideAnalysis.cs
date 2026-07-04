using Esk8_Tracker.Core.Models;

namespace Esk8_Tracker.Core;

public record RideSplit(int Index, double AvgSpeedMps, double TopSpeedMps, double DistanceMeters);

/// <summary>
/// Turns a ride's stored track points into render-ready samples, elevation gain,
/// and per-split stats. Pure functions; all UI meaning (units, colors) lives elsewhere.
/// </summary>
public static class RideAnalysis
{
    /// <summary>Ignore altitude wiggle below this when accumulating climb (GPS noise).</summary>
    public const double ElevationHysteresisMeters = 2.0;

    /// <summary>Cap for render samples; graphs don't benefit from more.</summary>
    public const int MaxRenderSamples = 500;

    public static IReadOnlyList<RideSample> BuildSamples(IReadOnlyList<TrackPoint> points)
    {
        if (points.Count == 0) return Array.Empty<RideSample>();

        var lat0 = points[0].Latitude;
        var lon0 = points[0].Longitude;
        var mPerLon = 111_320.0 * Math.Cos(lat0 * Math.PI / 180.0);
        const double mPerLat = 110_540.0;

        var smoothedEle = SmoothElevation(points);

        var stride = Math.Max(1, (int)Math.Ceiling(points.Count / (double)MaxRenderSamples));
        var t0 = points[0].Timestamp;
        var samples = new List<RideSample>(Math.Min(points.Count, MaxRenderSamples) + 1);
        double dist = 0;
        for (var i = 0; i < points.Count; i++)
        {
            if (i > 0)
                dist += GeoMath.HaversineMeters(
                    points[i - 1].Latitude, points[i - 1].Longitude,
                    points[i].Latitude, points[i].Longitude);
            if (i % stride != 0 && i != points.Count - 1) continue;

            var p = points[i];
            samples.Add(new RideSample(
                (p.Timestamp - t0).TotalSeconds,
                dist,
                Math.Max(0, p.SpeedMps),
                (p.Longitude - lon0) * mPerLon,
                // north-up: latitude grows upward, screen Y grows downward
                -(p.Latitude - lat0) * mPerLat,
                smoothedEle[i]));
        }
        return samples;
    }

    /// <summary>Total ascent with hysteresis so meters of GPS jitter don't sum into fake climb.</summary>
    public static double ElevationGainMeters(IReadOnlyList<TrackPoint> points)
    {
        var ele = SmoothElevation(points);
        double gain = 0;
        double? anchor = null;
        foreach (var e in ele)
        {
            if (e is null) continue;
            if (anchor is null) { anchor = e; continue; }
            var delta = e.Value - anchor.Value;
            if (delta >= ElevationHysteresisMeters)
            {
                gain += delta;
                anchor = e;
            }
            else if (delta <= -ElevationHysteresisMeters)
            {
                anchor = e; // descended; re-anchor without counting
            }
        }
        return gain;
    }

    public static IReadOnlyList<RideSplit> BuildSplits(IReadOnlyList<RideSample> samples, double splitMeters)
    {
        if (samples.Count < 2 || splitMeters <= 0) return Array.Empty<RideSplit>();

        var splits = new List<RideSplit>();
        var index = 1;
        double splitStartDist = 0, splitStartTime = samples[0].TimeSeconds, top = 0;

        foreach (var s in samples)
        {
            top = Math.Max(top, s.SpeedMps);
            while (s.DistanceMeters - splitStartDist >= splitMeters)
            {
                var elapsed = Math.Max(1, s.TimeSeconds - splitStartTime);
                splits.Add(new RideSplit(index++, splitMeters / elapsed, top, splitMeters));
                splitStartDist += splitMeters;
                splitStartTime = s.TimeSeconds;
                top = s.SpeedMps;
            }
        }

        // Trailing partial split (only if it's a meaningful fraction)
        var last = samples[^1];
        var rem = last.DistanceMeters - splitStartDist;
        if (rem >= splitMeters * 0.2)
        {
            var elapsed = Math.Max(1, last.TimeSeconds - splitStartTime);
            splits.Add(new RideSplit(index, rem / elapsed, top, rem));
        }
        return splits;
    }

    private static double?[] SmoothElevation(IReadOnlyList<TrackPoint> points)
    {
        // 5-point moving average over available altitudes
        var result = new double?[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            if (points[i].AltitudeMeters is null) { result[i] = null; continue; }
            double sum = 0; var n = 0;
            for (var j = Math.Max(0, i - 2); j <= Math.Min(points.Count - 1, i + 2); j++)
            {
                if (points[j].AltitudeMeters is { } a) { sum += a; n++; }
            }
            result[i] = sum / n;
        }
        return result;
    }
}
