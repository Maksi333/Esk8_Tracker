using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Esk8_Tracker.Controls;

/// <summary>
/// Onboarding illustration (210x210 design box): three faint concentric guide rings,
/// a partial velocity-ramp arc (first 72% of a 250-degree sweep), and a center disc.
/// The Material icon in the middle is overlaid by the page as a Label — not drawn here.
/// The design box is uniformly scaled to fit and centered in the control bounds.
/// </summary>
public class OnboardingIlloView : SKCanvasView
{
    private static readonly SKColor HairlineColor = new(0x1C, 0x24, 0x2B);
    private static readonly SKColor DiscFillColor = new(0x15, 0x1A, 0x1F);
    private static readonly SKColor DiscBorderColor = new(0x2A, 0x33, 0x3B);

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

        const float box = 210f;
        float s = Math.Min(W / box, H / box);
        canvas.Translate((W - box * s) / 2f, (H - box * s) / 2f);
        canvas.Scale(s);

        const float cx = 105f, cy = 105f;

        // Three concentric guide rings: r = 50, 76, 102.
        using var ring = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            Color = HairlineColor,
        };
        for (int i = 0; i < 3; i++)
            canvas.DrawCircle(cx, cy, 50f + i * 26f, ring);

        // Partial velocity-ramp arc: first 72% of 30 segments, radius 88,
        // from -215 degrees across a 250-degree total sweep.
        const int M = 30;
        const float r = 88f, start = -215f, sweep = 250f;
        var oval = new SKRect(cx - r, cy - r, cx + r, cy + r);
        using var arcPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 5f,
            StrokeCap = SKStrokeCap.Round,
        };
        for (int i = 0; i < M * 0.72; i++)
        {
            arcPaint.Color = VelocityRamp.ColorAt(i / (double)M);
            canvas.DrawArc(oval, start + sweep * (i / (float)M), sweep / M, false, arcPaint);
        }

        // Center disc, r = 44: card fill with 1px border.
        using var disc = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = DiscFillColor,
        };
        canvas.DrawCircle(cx, cy, 44f, disc);

        using var discBorder = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            Color = DiscBorderColor,
        };
        canvas.DrawCircle(cx, cy, 44f, discBorder);
    }
}
