using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Esk8_Tracker.Controls;

/// <summary>
/// Hero live-ride speedometer. Draws only the segmented gauge graphics (velocity-ramp
/// segments + white position marker); the big speed number and unit are overlaid by the
/// page as MAUI Labels. Three variants ported 1:1 from the design prototype:
/// "arc" (default, 300x205 design box), "radial" (260x260), "bar" (28 stacked bars).
/// The design box is uniformly scaled to fit and centered in the control bounds.
/// </summary>
public class SpeedometerView : SKCanvasView
{
    public static readonly BindableProperty SpeedMpsProperty = BindableProperty.Create(
        nameof(SpeedMps), typeof(double), typeof(SpeedometerView), 0.0,
        propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty TopSpeedMpsProperty = BindableProperty.Create(
        nameof(TopSpeedMps), typeof(double), typeof(SpeedometerView), 11.11,
        propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty VariantProperty = BindableProperty.Create(
        nameof(Variant), typeof(string), typeof(SpeedometerView), "arc",
        propertyChanged: OnVisualPropertyChanged);

    /// <summary>Current speed in meters per second.</summary>
    public double SpeedMps
    {
        get => (double)GetValue(SpeedMpsProperty);
        set => SetValue(SpeedMpsProperty, value);
    }

    /// <summary>Board top speed in meters per second (full-scale of the gauge).</summary>
    public double TopSpeedMps
    {
        get => (double)GetValue(TopSpeedMpsProperty);
        set => SetValue(TopSpeedMpsProperty, value);
    }

    /// <summary>Gauge style: "arc" (default), "radial" or "bar".</summary>
    public string Variant
    {
        get => (string)GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    /// <summary>
    /// Fraction of the control height at which the page should vertically center the
    /// overlaid speed number for a given variant. Returns <see cref="double.NaN"/> for
    /// "bar", where the number sits above the gauge rather than inside it.
    /// </summary>
    public static double NumberCenterYFraction(string? variant) =>
        (variant ?? "arc").Trim().ToLowerInvariant() switch
        {
            "radial" => 0.5,
            "bar" => double.NaN,
            _ => 0.62,
        };

    private static void OnVisualPropertyChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((SKCanvasView)bindable).InvalidateSurface();

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        base.OnPaintSurface(e);

        var canvas = e.Surface.Canvas;
        canvas.Clear();
        if (Width <= 0 || Height <= 0)
            return;

        var scale = (float)(e.Info.Width / Width);
        canvas.Scale(scale);
        float W = (float)Width, H = (float)Height;

        double top = TopSpeedMps;
        double cur = top <= 0 ? 0 : Math.Clamp(SpeedMps / top, 0.0, 1.0);
        if (double.IsNaN(cur))
            cur = 0;

        switch ((Variant ?? "arc").Trim().ToLowerInvariant())
        {
            case "radial":
                DrawRadial(canvas, W, H, cur);
                break;
            case "bar":
                DrawBars(canvas, W, H, cur);
                break;
            default:
                DrawArc(canvas, W, H, cur);
                break;
        }
    }

    private static void DrawArc(SKCanvas canvas, float W, float H, double cur)
    {
        const float boxW = 300f, boxH = 205f;
        canvas.Save();
        FitBox(canvas, W, H, boxW, boxH);
        DrawSegmentedGauge(canvas, cx: 150f, cy: 165f, r: 126f,
            startDeg: -215f, endDeg: 35f, segments: 54,
            activeWidth: 16f, inactiveWidth: 10f, cur: cur);
        canvas.Restore();
    }

    private static void DrawRadial(SKCanvas canvas, float W, float H, double cur)
    {
        const float boxW = 260f, boxH = 260f;
        canvas.Save();
        FitBox(canvas, W, H, boxW, boxH);
        DrawSegmentedGauge(canvas, cx: 130f, cy: 130f, r: 104f,
            startDeg: 130f, endDeg: 410f, segments: 64,
            activeWidth: 15f, inactiveWidth: 9f, cur: cur);
        canvas.Restore();
    }

    private static void DrawBars(SKCanvas canvas, float W, float H, double cur)
    {
        const int M = 28;
        float barW = W * 0.86f;
        float x0 = (W - barW) / 2f;

        float barH = 11f, gap = 4f, corner = 3f;
        float total = M * barH + (M - 1) * gap;
        if (total > H && total > 0f)
        {
            float s = H / total;
            barH *= s;
            gap *= s;
            corner *= s;
            total = H;
        }

        float yBottom = H - (H - total) / 2f; // stack vertically centered, built bottom-to-top

        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        for (int i = 0; i < M; i++)
        {
            SKColor color = VelocityRamp.ColorAt((i + 0.5) / M);
            bool active = i / (double)M < cur;
            paint.Color = active ? color : color.WithAlpha(41); // 0.16 opacity when inactive

            float yTop = yBottom - i * (barH + gap) - barH;
            canvas.DrawRoundRect(new SKRect(x0, yTop, x0 + barW, yTop + barH), corner, corner, paint);
        }
    }

    /// <summary>Uniformly scales + centers the design box inside the control bounds.</summary>
    private static void FitBox(SKCanvas canvas, float W, float H, float boxW, float boxH)
    {
        float s = Math.Min(W / boxW, H / boxH);
        canvas.Translate((W - boxW * s) / 2f, (H - boxH * s) / 2f);
        canvas.Scale(s);
    }

    private static void DrawSegmentedGauge(
        SKCanvas canvas, float cx, float cy, float r,
        float startDeg, float endDeg, int segments,
        float activeWidth, float inactiveWidth, double cur)
    {
        var oval = new SKRect(cx - r, cy - r, cx + r, cy + r);
        float sweepTotal = endDeg - startDeg;

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
        };

        for (int i = 0; i < segments; i++)
        {
            float t0 = i / (float)segments;
            float t1 = (i + 1) / (float)segments;
            bool active = t0 < cur;

            SKColor color = VelocityRamp.ColorAt((t0 + t1) / 2.0);
            paint.Color = active ? color : color.WithAlpha(38); // 0.15 opacity when inactive
            paint.StrokeWidth = active ? activeWidth : inactiveWidth;

            canvas.DrawArc(oval, startDeg + sweepTotal * t0, sweepTotal * (t1 - t0), false, paint);
        }

        // White marker dot at the current speed position on the gauge radius.
        double markerRad = (startDeg + sweepTotal * cur) * Math.PI / 180.0;
        using var marker = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = SKColors.White,
        };
        canvas.DrawCircle(
            cx + r * (float)Math.Cos(markerRad),
            cy + r * (float)Math.Sin(markerRad),
            8f, marker);
    }
}
