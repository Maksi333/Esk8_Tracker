using Esk8_Tracker.Core;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Esk8_Tracker.Controls;

/// <summary>
/// Speed-over-distance graph. The line is stroked per segment with the velocity
/// ramp color for the segment's mean speed, over a soft vertical-gradient area
/// fill, with dashed gridlines at 50% and 100% of top speed and an optional
/// scrub marker.
/// </summary>
public class SpeedGraphView : SKCanvasView
{
    private const float Pad = 6f;

    private static readonly SKColor GridlineColor = new(0x1C, 0x24, 0x2B);
    private static readonly SKColor AreaTopColor = new(0xF5, 0xC5, 0x1E, (byte)(0.28 * 255));    // #F5C51E @ 28%
    private static readonly SKColor AreaBottomColor = new(0x2E, 0x7C, 0xF6, (byte)(0.03 * 255)); // #2E7CF6 @ 3%

    public static readonly BindableProperty SamplesProperty = BindableProperty.Create(
        nameof(Samples),
        typeof(IReadOnlyList<RideSample>),
        typeof(SpeedGraphView),
        null,
        propertyChanged: static (b, _, _) => ((SKCanvasView)b).InvalidateSurface());

    public static readonly BindableProperty TopSpeedMpsProperty = BindableProperty.Create(
        nameof(TopSpeedMps),
        typeof(double),
        typeof(SpeedGraphView),
        11.11,
        propertyChanged: static (b, _, _) => ((SKCanvasView)b).InvalidateSurface());

    public static readonly BindableProperty ScrubDistanceMetersProperty = BindableProperty.Create(
        nameof(ScrubDistanceMeters),
        typeof(double),
        typeof(SpeedGraphView),
        -1.0,
        propertyChanged: static (b, _, _) => ((SKCanvasView)b).InvalidateSurface());

    public static readonly BindableProperty ScrubSpeedMpsProperty = BindableProperty.Create(
        nameof(ScrubSpeedMps),
        typeof(double),
        typeof(SpeedGraphView),
        0.0,
        propertyChanged: static (b, _, _) => ((SKCanvasView)b).InvalidateSurface());

    public IReadOnlyList<RideSample>? Samples
    {
        get => (IReadOnlyList<RideSample>?)GetValue(SamplesProperty);
        set => SetValue(SamplesProperty, value);
    }

    public double TopSpeedMps
    {
        get => (double)GetValue(TopSpeedMpsProperty);
        set => SetValue(TopSpeedMpsProperty, value);
    }

    /// <summary>Distance of the scrub marker; negative hides the marker.</summary>
    public double ScrubDistanceMeters
    {
        get => (double)GetValue(ScrubDistanceMetersProperty);
        set => SetValue(ScrubDistanceMetersProperty, value);
    }

    public double ScrubSpeedMps
    {
        get => (double)GetValue(ScrubSpeedMpsProperty);
        set => SetValue(ScrubSpeedMpsProperty, value);
    }

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

        var samples = Samples;
        if (samples is null || samples.Count < 2)
            return;

        double maxD = Math.Max(samples[samples.Count - 1].DistanceMeters, 1.0);
        double top = TopSpeedMps > 0 ? TopSpeedMps : 1.0;

        float X(double d) => Pad + (float)(d / maxD) * (W - 2 * Pad);
        float Y(double v) => H - Pad - (float)(v / top) * (H - 2 * Pad);

        // Area fill under the speed curve.
        using (var areaPath = new SKPath())
        {
            areaPath.MoveTo(X(0), H - Pad);
            for (int i = 0; i < samples.Count; i++)
                areaPath.LineTo(X(samples[i].DistanceMeters), Y(samples[i].SpeedMps));
            areaPath.LineTo(X(maxD), H - Pad);
            areaPath.Close();

            using var areaPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                Shader = SKShader.CreateLinearGradient(
                    new SKPoint(0, 0),
                    new SKPoint(0, H),
                    new[] { AreaTopColor, AreaBottomColor },
                    null,
                    SKShaderTileMode.Clamp),
            };
            canvas.DrawPath(areaPath, areaPaint);
        }

        // Dashed gridlines at 50% and 100% of top speed.
        using (var gridPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            Color = GridlineColor,
            PathEffect = SKPathEffect.CreateDash(new float[] { 3, 4 }, 0),
        })
        {
            float yHalf = Y(0.5 * top);
            float yFull = Y(top);
            canvas.DrawLine(Pad, yHalf, W - Pad, yHalf, gridPaint);
            canvas.DrawLine(Pad, yFull, W - Pad, yFull, gridPaint);
        }

        // Speed line, one ramp-colored stroke per segment.
        using (var linePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2.4f,
            StrokeCap = SKStrokeCap.Round,
        })
        {
            for (int i = 0; i < samples.Count - 1; i++)
            {
                var a = samples[i];
                var b = samples[i + 1];
                double meanV = (a.SpeedMps + b.SpeedMps) * 0.5;
                linePaint.Color = VelocityRamp.ColorAt(meanV / top);
                canvas.DrawLine(
                    X(a.DistanceMeters), Y(a.SpeedMps),
                    X(b.DistanceMeters), Y(b.SpeedMps),
                    linePaint);
            }
        }

        // Scrub marker.
        double scrubD = ScrubDistanceMeters;
        if (scrubD >= 0)
        {
            float sx = X(scrubD);

            using (var scrubLinePaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1f,
                StrokeCap = SKStrokeCap.Round,
                Color = SKColors.White.WithAlpha((byte)(0.5 * 255)),
            })
            {
                canvas.DrawLine(sx, Pad, sx, H - Pad, scrubLinePaint);
            }

            using var dotPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                Color = SKColors.White,
            };
            canvas.DrawCircle(sx, Y(ScrubSpeedMps), 5f, dotPaint);
        }
    }
}
