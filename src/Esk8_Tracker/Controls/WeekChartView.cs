using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Esk8_Tracker.Controls;

/// <summary>
/// Weekly distance bar chart. One rounded bar per week (oldest to newest);
/// the newest bar is accent-colored and can carry a value label above it.
/// The bottom 16dp is left clear for a label strip drawn by the surrounding XAML.
/// </summary>
public class WeekChartView : SKCanvasView
{
    private const float LabelStrip = 16f;

    private static readonly SKColor AccentColor = new(0x4C, 0x8D, 0xFF); // accent
    private static readonly SKColor BarColor = new(0x2A, 0x33, 0x3B);    // border

    public static readonly BindableProperty ValuesProperty = BindableProperty.Create(
        nameof(Values),
        typeof(IReadOnlyList<double>),
        typeof(WeekChartView),
        null,
        propertyChanged: static (b, _, _) => ((SKCanvasView)b).InvalidateSurface());

    public static readonly BindableProperty HighlightLabelProperty = BindableProperty.Create(
        nameof(HighlightLabel),
        typeof(string),
        typeof(WeekChartView),
        null,
        propertyChanged: static (b, _, _) => ((SKCanvasView)b).InvalidateSurface());

    /// <summary>Distance per week, oldest to newest.</summary>
    public IReadOnlyList<double>? Values
    {
        get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    /// <summary>Value label drawn above the newest bar (e.g. "42"); empty draws none.</summary>
    public string? HighlightLabel
    {
        get => (string?)GetValue(HighlightLabelProperty);
        set => SetValue(HighlightLabelProperty, value);
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

        var values = Values;
        if (values is null || values.Count == 0)
            return;

        double mx = 1.0;
        for (int i = 0; i < values.Count; i++)
            if (values[i] > mx)
                mx = values[i];

        float gap = W / values.Count;
        float barW = gap * 0.55f;

        using var barPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
        };

        float lastX = 0f, lastY = 0f;
        for (int i = 0; i < values.Count; i++)
        {
            float barH = (float)(values[i] / mx) * (H - 20);
            float x = i * gap + gap * 0.22f;
            float y = H - barH - LabelStrip;

            bool isLast = i == values.Count - 1;
            barPaint.Color = isLast ? AccentColor : BarColor;
            canvas.DrawRoundRect(x, y, barW, barH, 3f, 3f, barPaint);

            if (isLast)
            {
                lastX = i * gap + gap * 0.5f;
                lastY = y;
            }
        }

        var label = HighlightLabel;
        if (!string.IsNullOrEmpty(label))
        {
            using var font = new SKFont(SkiaFonts.ChakraPetch, 11f);
            using var textPaint = new SKPaint
            {
                IsAntialias = true,
                Color = AccentColor,
            };
            canvas.DrawText(label, lastX, lastY - 6f, SKTextAlign.Center, font, textPaint);
        }
    }
}
