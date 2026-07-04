using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Esk8_Tracker.Controls;

/// <summary>
/// Map legend for the velocity ramp: an 8dp ramp bar across the full width with
/// "0", midpoint and top-speed labels beneath it, converted to display units
/// via <see cref="UnitScale"/> (default m/s to km/h).
/// </summary>
public class RampLegendView : SKCanvasView
{
    private const int SliceCount = 60;
    private const float BarHeight = 8f;
    private const float LabelBaseline = 24f;
    private const float LabelInset = 6f;

    private static readonly SKColor LabelColor = new(0x8A, 0x97, 0xA2); // tertiary

    public static readonly BindableProperty TopSpeedMpsProperty = BindableProperty.Create(
        nameof(TopSpeedMps),
        typeof(double),
        typeof(RampLegendView),
        11.11,
        propertyChanged: static (b, _, _) => ((SKCanvasView)b).InvalidateSurface());

    public static readonly BindableProperty UnitScaleProperty = BindableProperty.Create(
        nameof(UnitScale),
        typeof(double),
        typeof(RampLegendView),
        3.6,
        propertyChanged: static (b, _, _) => ((SKCanvasView)b).InvalidateSurface());

    public double TopSpeedMps
    {
        get => (double)GetValue(TopSpeedMpsProperty);
        set => SetValue(TopSpeedMpsProperty, value);
    }

    /// <summary>Multiplier from m/s to display units (3.6 for km/h, 2.23694 for mph).</summary>
    public double UnitScale
    {
        get => (double)GetValue(UnitScaleProperty);
        set => SetValue(UnitScaleProperty, value);
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

        // Ramp bar across the full width.
        float sliceW = W / SliceCount;
        using (var slicePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
        })
        {
            for (int i = 0; i < SliceCount; i++)
            {
                slicePaint.Color = VelocityRamp.ColorAt((i + 0.5) / SliceCount);
                // Slight overdraw (+0.6) so antialiased edges don't show seams.
                canvas.DrawRect(i * sliceW, 0, sliceW + 0.6f, BarHeight, slicePaint);
            }
        }

        // Labels: 0 / mid / top in display units.
        double topDisplay = TopSpeedMps * UnitScale;
        string midLabel = ((int)Math.Round(topDisplay * 0.5)).ToString();
        string topLabel = ((int)Math.Round(topDisplay)).ToString();

        using var font = new SKFont(SkiaFonts.ChakraPetch, 10f);
        using var textPaint = new SKPaint
        {
            IsAntialias = true,
            Color = LabelColor,
        };

        canvas.DrawText("0", LabelInset, LabelBaseline, SKTextAlign.Left, font, textPaint);
        canvas.DrawText(midLabel, W / 2f, LabelBaseline, SKTextAlign.Center, font, textPaint);
        canvas.DrawText(topLabel, W - LabelInset, LabelBaseline, SKTextAlign.Right, font, textPaint);
    }
}
