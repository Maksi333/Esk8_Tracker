using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Esk8_Tracker.Controls;

/// <summary>
/// Achievement progress ring (60x60 design box): a #2A333B track circle (r=22, width 5)
/// with a round-capped progress arc in <see cref="RingColor"/> starting at 12 o'clock.
/// The design box is uniformly scaled to fit and centered in the control bounds.
/// </summary>
public class ProgressRingView : SKCanvasView
{
    private static readonly SKColor TrackColor = new(0x2A, 0x33, 0x3B);

    public static readonly BindableProperty ProgressProperty = BindableProperty.Create(
        nameof(Progress), typeof(double), typeof(ProgressRingView), 0.0,
        propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty RingColorProperty = BindableProperty.Create(
        nameof(RingColor), typeof(Color), typeof(ProgressRingView), Color.FromArgb("#4C8DFF"),
        propertyChanged: OnVisualPropertyChanged);

    /// <summary>Completion percentage, 0..100.</summary>
    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    /// <summary>Color of the progress arc (default accent #4C8DFF).</summary>
    public Color RingColor
    {
        get => (Color)GetValue(RingColorProperty);
        set => SetValue(RingColorProperty, value);
    }

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

        const float box = 60f;
        float s = Math.Min(W / box, H / box);
        canvas.Translate((W - box * s) / 2f, (H - box * s) / 2f);
        canvas.Scale(s);

        const float cx = 30f, cy = 30f, r = 22f;

        using var track = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 5f,
            Color = TrackColor,
        };
        canvas.DrawCircle(cx, cy, r, track);

        float sweep = 360f * (float)(Math.Clamp(Progress, 0.0, 100.0) / 100.0);
        if (sweep <= 0f)
            return;

        using var arc = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 5f,
            StrokeCap = SKStrokeCap.Round,
            Color = (RingColor ?? Color.FromArgb("#4C8DFF")).ToSKColor(),
        };
        var oval = new SKRect(cx - r, cy - r, cx + r, cy + r);
        canvas.DrawArc(oval, -90f, sweep, false, arc);
    }
}
