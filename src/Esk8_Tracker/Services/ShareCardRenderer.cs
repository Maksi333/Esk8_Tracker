using Esk8_Tracker.Controls;
using Esk8_Tracker.Core;
using Esk8_Tracker.Core.Models;
using SkiaSharp;

namespace Esk8_Tracker.Services;

/// <summary>Renders a shareable 1080×1350 ride card PNG (colorized route + headline stats).</summary>
public static class ShareCardRenderer
{
    public static Task<string> RenderAsync(Ride ride, IReadOnlyList<RideSample> samples,
        Board? board, UnitSystem units)
    {
        return Task.Run(() =>
        {
            const int W = 1080, H = 1350;
            using var surface = SKSurface.Create(new SKImageInfo(W, H));
            var canvas = surface.Canvas;
            canvas.Clear(new SKColor(0x0B, 0x0E, 0x11));

            var top = board?.TopSpeedMps ?? Math.Max(ride.MaxSpeedMps, 1);
            DrawRoute(canvas, samples, top, W, 760);

            SKTypeface heading = SafeType("SpaceGrotesk-SemiBold.ttf");
            var chakra = SkiaFonts.ChakraPetch;
            var chakraSemi = SkiaFonts.ChakraPetchSemiBold;

            var name = ride.Name.Length > 0 ? ride.Name : $"Ride {ride.StartedAt.ToLocalTime():d MMM}";
            using (var f = new SKFont(heading, 64))
            using (var p = new SKPaint { Color = new SKColor(0xF2, 0xF5, 0xF7), IsAntialias = true })
                canvas.DrawText(name, 60, 880, SKTextAlign.Left, f, p);

            using (var f = new SKFont(chakra, 34))
            using (var p = new SKPaint { Color = new SKColor(0x8A, 0x97, 0xA2), IsAntialias = true })
                canvas.DrawText(ride.StartedAt.ToLocalTime().ToString("dddd d MMM yyyy · HH:mm"),
                    60, 928, SKTextAlign.Left, f, p);

            // Three headline stat columns
            var cols = new (string Value, string Caption, SKColor Color)[]
            {
                (Units.Distance(ride.DistanceMeters, units) + " " + Units.DistanceUnit(units), "DISTANCE", new SKColor(0xF2, 0xF5, 0xF7)),
                (Units.Speed(ride.AvgSpeedMps, units) + " " + Units.SpeedUnit(units), "AVG SPEED", new SKColor(0xF2, 0xF5, 0xF7)),
                (Units.Speed(ride.MaxSpeedMps, units) + " " + Units.SpeedUnit(units), "TOP SPEED", VelocityRamp.ColorAt(ride.MaxSpeedMps, top)),
            };
            float colY = 1120;
            for (var i = 0; i < cols.Length; i++)
            {
                float cx = 60 + i * ((W - 120) / 3f);
                using (var vf = new SKFont(chakraSemi, 68))
                using (var vp = new SKPaint { Color = cols[i].Color, IsAntialias = true })
                    canvas.DrawText(cols[i].Value, cx, colY, SKTextAlign.Left, vf, vp);
                using (var cf = new SKFont(chakra, 28))
                using (var cp = new SKPaint { Color = new SKColor(0x8A, 0x97, 0xA2), IsAntialias = true })
                    canvas.DrawText(cols[i].Caption, cx, colY + 44, SKTextAlign.Left, cf, cp);
            }

            using (var f = new SKFont(heading, 30))
            using (var p = new SKPaint { Color = new SKColor(0x4A, 0x55, 0x60), IsAntialias = true })
                canvas.DrawText("ESK8 TRACKER", 60, 1290, SKTextAlign.Left, f, p);

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 92);
            var path = Path.Combine(FileSystem.CacheDirectory, $"ride-{ride.Id}.png");
            using (var fs = File.Create(path))
                data.SaveTo(fs);
            return path;
        });
    }

    private static void DrawRoute(SKCanvas canvas, IReadOnlyList<RideSample> samples, double top, int w, int h)
    {
        if (samples is null || samples.Count < 2) return;
        const float pad = 90;
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var s in samples)
        {
            minX = Math.Min(minX, s.X); maxX = Math.Max(maxX, s.X);
            minY = Math.Min(minY, s.Y); maxY = Math.Max(maxY, s.Y);
        }
        var spanX = (maxX - minX) == 0 ? 1 : maxX - minX;
        var spanY = (maxY - minY) == 0 ? 1 : maxY - minY;
        var sc = Math.Min((w - 2 * pad) / spanX, (h - 2 * pad) / spanY);
        var ox = pad + ((w - 2 * pad) - spanX * sc) / 2;
        var oy = pad + ((h - 2 * pad) - spanY * sc) / 2;
        SKPoint P(RideSample s) => new((float)(ox + (s.X - minX) * sc), (float)(oy + (s.Y - minY) * sc));

        for (var i = 0; i < samples.Count - 1; i++)
        {
            var a = P(samples[i]); var b = P(samples[i + 1]);
            var color = VelocityRamp.ColorAt((samples[i].SpeedMps + samples[i + 1].SpeedMps) / 2, top);
            using (var glow = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 24, StrokeCap = SKStrokeCap.Round, Color = color.WithAlpha(41), IsAntialias = true })
                canvas.DrawLine(a, b, glow);
            using (var line = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 10, StrokeCap = SKStrokeCap.Round, Color = color, IsAntialias = true })
                canvas.DrawLine(a, b, line);
        }
        var start = P(samples[0]);
        using (var dot = new SKPaint { Style = SKPaintStyle.Fill, Color = new SKColor(0x0B, 0x0E, 0x11), IsAntialias = true })
            canvas.DrawCircle(start, 10, dot);
        using (var ring = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 5, Color = new SKColor(0x22, 0xC5, 0x5E), IsAntialias = true })
            canvas.DrawCircle(start, 10, ring);
    }

    private static SKTypeface SafeType(string file)
    {
        try
        {
            var stream = FileSystem.OpenAppPackageFileAsync(file).GetAwaiter().GetResult();
            return SKTypeface.FromStream(new SKManagedStream(stream, true)) ?? SKTypeface.Default;
        }
        catch
        {
            return SKTypeface.Default;
        }
    }
}
