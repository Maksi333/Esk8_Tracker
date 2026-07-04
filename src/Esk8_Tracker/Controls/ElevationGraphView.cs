using Esk8_Tracker.Core;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Esk8_Tracker.Controls;

/// <summary>
/// Elevation-over-distance profile: a muted gray line with a faint area fill.
/// Samples without an elevation value are skipped; fewer than two usable
/// samples draws nothing.
/// </summary>
public class ElevationGraphView : SKCanvasView
{
    private const float Pad = 4f;

    private static readonly SKColor LineColor = new(0x8A, 0x97, 0xA2);                       // tertiary
    private static readonly SKColor AreaColor = new(0x8A, 0x97, 0xA2, (byte)(0.10 * 255));   // tertiary @ 10%

    public static readonly BindableProperty SamplesProperty = BindableProperty.Create(
        nameof(Samples),
        typeof(IReadOnlyList<RideSample>),
        typeof(ElevationGraphView),
        null,
        propertyChanged: static (b, _, _) => ((SKCanvasView)b).InvalidateSurface());

    public IReadOnlyList<RideSample>? Samples
    {
        get => (IReadOnlyList<RideSample>?)GetValue(SamplesProperty);
        set => SetValue(SamplesProperty, value);
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
        if (samples is null || samples.Count == 0)
            return;

        // Keep only samples that carry an elevation.
        var usable = new List<(double Distance, double Elevation)>(samples.Count);
        for (int i = 0; i < samples.Count; i++)
        {
            var s = samples[i];
            if (s.ElevationMeters is double ele)
                usable.Add((s.DistanceMeters, ele));
        }

        if (usable.Count < 2)
            return;

        double maxD = Math.Max(usable[usable.Count - 1].Distance, 1.0);

        double minEle = double.MaxValue, maxEle = double.MinValue;
        foreach (var (_, ele) in usable)
        {
            if (ele < minEle) minEle = ele;
            if (ele > maxEle) maxEle = ele;
        }

        double range = maxEle - minEle;
        if (range <= 0)
            range = 1.0;

        float X(double d) => Pad + (float)(d / maxD) * (W - 2 * Pad);
        float Y(double ele) => H - Pad - (float)((ele - minEle) / range) * (H - 2 * Pad);

        using var linePath = new SKPath();
        linePath.MoveTo(X(usable[0].Distance), Y(usable[0].Elevation));
        for (int i = 1; i < usable.Count; i++)
            linePath.LineTo(X(usable[i].Distance), Y(usable[i].Elevation));

        // Area fill: the profile closed down to the bottom edge.
        using (var areaPath = new SKPath(linePath))
        {
            areaPath.LineTo(X(usable[usable.Count - 1].Distance), H - Pad);
            areaPath.LineTo(X(usable[0].Distance), H - Pad);
            areaPath.Close();

            using var areaPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                Color = AreaColor,
            };
            canvas.DrawPath(areaPath, areaPaint);
        }

        using var linePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.6f,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            Color = LineColor,
        };
        canvas.DrawPath(linePath, linePaint);
    }
}
