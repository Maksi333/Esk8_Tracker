using System;
using System.Collections.Generic;
using Esk8_Tracker.Core;
using Microsoft.Maui.Controls;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Esk8_Tracker.Controls;

/// <summary>
/// Mini speed sparkline of velocity-ramp colored bars, resampled to a fixed
/// bar count. Used on the last-ride card and history aggregates.
/// </summary>
public class SparkBarsView : SKCanvasView
{
    static void Invalidate(BindableObject b, object oldValue, object newValue)
        => ((SKCanvasView)b).InvalidateSurface();

    public static readonly BindableProperty SamplesProperty = BindableProperty.Create(
        nameof(Samples), typeof(IReadOnlyList<RideSample>), typeof(SparkBarsView), null,
        propertyChanged: Invalidate);

    public static readonly BindableProperty TopSpeedMpsProperty = BindableProperty.Create(
        nameof(TopSpeedMps), typeof(double), typeof(SparkBarsView), 11.11,
        propertyChanged: Invalidate);

    public static readonly BindableProperty BarCountProperty = BindableProperty.Create(
        nameof(BarCount), typeof(int), typeof(SparkBarsView), 16,
        propertyChanged: Invalidate);

    /// <summary>Analyzed ride samples to resample into bars. Null or empty draws nothing.</summary>
    public IReadOnlyList<RideSample>? Samples
    {
        get => (IReadOnlyList<RideSample>?)GetValue(SamplesProperty);
        set => SetValue(SamplesProperty, value);
    }

    /// <summary>Speed (m/s) that maps to a full-height bar on the velocity ramp.</summary>
    public double TopSpeedMps
    {
        get => (double)GetValue(TopSpeedMpsProperty);
        set => SetValue(TopSpeedMpsProperty, value);
    }

    /// <summary>Number of bars the sample list is resampled into.</summary>
    public int BarCount
    {
        get => (int)GetValue(BarCountProperty);
        set => SetValue(BarCountProperty, value);
    }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        base.OnPaintSurface(e);

        var canvas = e.Surface.Canvas;
        canvas.Clear();
        if (Width <= 0 || Height <= 0) return;
        var scale = (float)(e.Info.Width / Width);
        canvas.Scale(scale);
        float W = (float)Width, H = (float)Height;

        var samples = Samples;
        int n = BarCount;
        if (samples is null || samples.Count == 0 || n < 1) return;

        double top = TopSpeedMps > 0 ? TopSpeedMps : 1.0;
        double step = n > 1 ? (samples.Count - 1) / (double)(n - 1) : 0;
        float gap = W / n;
        float barW = gap * 0.62f;

        using var fill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };

        for (int i = 0; i < n; i++)
        {
            int idx = Math.Clamp((int)Math.Round(i * step), 0, samples.Count - 1);
            double v = samples[idx].SpeedMps;
            float barH = Math.Max(3f, (float)((v / top) * (H - 4)));
            float x = i * gap + gap * 0.19f;
            fill.Color = VelocityRamp.ColorAt(v / top);
            canvas.DrawRoundRect(SKRect.Create(x, H - barH, barW, barH), 1.5f, 1.5f, fill);
        }
    }
}
