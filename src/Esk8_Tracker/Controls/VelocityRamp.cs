using SkiaSharp;

namespace Esk8_Tracker.Controls;

/// <summary>
/// The core visual system: maps a speed fraction (v / topSpeed, clamped 0..1) to the
/// velocity ramp color. Stops: 0.00 #2E7CF6 (cruise), 0.40 #22C55E (flow),
/// 0.72 #F5C51E (push), 1.00 #F03E3E (send). Linear per-channel RGB interpolation
/// between adjacent stops, matching the design prototype's <c>ramp()</c>.
/// </summary>
public static class VelocityRamp
{
    private static readonly (double Pos, SKColor Color)[] Stops =
    {
        (0.00, new SKColor(0x2E, 0x7C, 0xF6)),
        (0.40, new SKColor(0x22, 0xC5, 0x5E)),
        (0.72, new SKColor(0xF5, 0xC5, 0x1E)),
        (1.00, new SKColor(0xF0, 0x3E, 0x3E)),
    };

    /// <summary>Ramp color for a speed fraction (clamped to 0..1).</summary>
    public static SKColor ColorAt(double fraction)
    {
        double f = double.IsNaN(fraction) ? 0.0 : Math.Clamp(fraction, 0.0, 1.0);
        for (int i = 0; i < Stops.Length - 1; i++)
        {
            if (f <= Stops[i + 1].Pos)
            {
                double t = (f - Stops[i].Pos) / (Stops[i + 1].Pos - Stops[i].Pos);
                return Lerp(Stops[i].Color, Stops[i + 1].Color, t);
            }
        }

        return Stops[^1].Color;
    }

    /// <summary>Ramp color for a speed <paramref name="v"/> against a top speed <paramref name="top"/>.</summary>
    public static SKColor ColorAt(double v, double top) => ColorAt(top <= 0 ? 0 : v / top);

    /// <summary>Same ramp as <see cref="ColorAt(double)"/> but as a MAUI color, for Labels/Brushes.</summary>
    public static Color MauiColorAt(double fraction)
    {
        SKColor c = ColorAt(fraction);
        return Color.FromRgb(c.Red, c.Green, c.Blue);
    }

    private static SKColor Lerp(SKColor a, SKColor b, double t) => new(
        (byte)Math.Round(a.Red + (b.Red - a.Red) * t),
        (byte)Math.Round(a.Green + (b.Green - a.Green) * t),
        (byte)Math.Round(a.Blue + (b.Blue - a.Blue) * t));
}
