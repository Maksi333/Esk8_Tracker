using System;
using System.Collections.Generic;
using Esk8_Tracker.Core;
using Microsoft.Maui.Controls;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Esk8_Tracker.Controls;

/// <summary>
/// The signature velocity-ramp route rendered on an abstract dark map field.
/// Fits the ride's local-projection X/Y to its own bounds and colors each
/// segment by speed fraction. Supports partial draw (live/scrub) via
/// <see cref="UptoFraction"/> and a live head dot via <see cref="ShowHeadDot"/>.
/// </summary>
public class RouteFieldView : SKCanvasView
{
    static readonly SKColor BgColor = new(0x0B, 0x0E, 0x11);
    static readonly SKColor GridColor = new(0x15, 0x1C, 0x22);
    static readonly SKColor RoadColor = new(0x1B, 0x24, 0x2B);
    static readonly SKColor WaterColor = new(0x0E, 0x1A, 0x22);
    static readonly SKColor GoColor = new(0x22, 0xC5, 0x5E);

    static void Invalidate(BindableObject b, object oldValue, object newValue)
        => ((SKCanvasView)b).InvalidateSurface();

    public static readonly BindableProperty SamplesProperty = BindableProperty.Create(
        nameof(Samples), typeof(IReadOnlyList<RideSample>), typeof(RouteFieldView), null,
        propertyChanged: Invalidate);

    public static readonly BindableProperty TopSpeedMpsProperty = BindableProperty.Create(
        nameof(TopSpeedMps), typeof(double), typeof(RouteFieldView), 11.11,
        propertyChanged: Invalidate);

    public static readonly BindableProperty UptoFractionProperty = BindableProperty.Create(
        nameof(UptoFraction), typeof(double), typeof(RouteFieldView), 1.0,
        propertyChanged: Invalidate);

    public static readonly BindableProperty ShowGlowProperty = BindableProperty.Create(
        nameof(ShowGlow), typeof(bool), typeof(RouteFieldView), true,
        propertyChanged: Invalidate);

    public static readonly BindableProperty ShowBackgroundProperty = BindableProperty.Create(
        nameof(ShowBackground), typeof(bool), typeof(RouteFieldView), true,
        propertyChanged: Invalidate);

    public static readonly BindableProperty ShowHeadDotProperty = BindableProperty.Create(
        nameof(ShowHeadDot), typeof(bool), typeof(RouteFieldView), false,
        propertyChanged: Invalidate);

    public static readonly BindableProperty RoutePaddingProperty = BindableProperty.Create(
        nameof(RoutePadding), typeof(double), typeof(RouteFieldView), 22.0,
        propertyChanged: Invalidate);

    public static readonly BindableProperty RouteStrokeWidthProperty = BindableProperty.Create(
        nameof(RouteStrokeWidth), typeof(double), typeof(RouteFieldView), 4.0,
        propertyChanged: Invalidate);

    /// <summary>Analyzed ride samples to render. Null or fewer than 2 samples draws only the background field.</summary>
    public IReadOnlyList<RideSample>? Samples
    {
        get => (IReadOnlyList<RideSample>?)GetValue(SamplesProperty);
        set => SetValue(SamplesProperty, value);
    }

    /// <summary>Speed (m/s) that maps to the top of the velocity ramp.</summary>
    public double TopSpeedMps
    {
        get => (double)GetValue(TopSpeedMpsProperty);
        set => SetValue(TopSpeedMpsProperty, value);
    }

    /// <summary>Draw the polyline only up to this fraction (0..1) of the sample list; for live/scrub views.</summary>
    public double UptoFraction
    {
        get => (double)GetValue(UptoFractionProperty);
        set => SetValue(UptoFractionProperty, value);
    }

    /// <summary>Whether to draw the soft wide glow pass underneath the crisp route line.</summary>
    public bool ShowGlow
    {
        get => (bool)GetValue(ShowGlowProperty);
        set => SetValue(ShowGlowProperty, value);
    }

    /// <summary>Whether to draw the abstract dark map field (grid, road, water) behind the route.</summary>
    public bool ShowBackground
    {
        get => (bool)GetValue(ShowBackgroundProperty);
        set => SetValue(ShowBackgroundProperty, value);
    }

    /// <summary>When true the route ends in a live "head" dot; when false, a small ramp-colored square marker.</summary>
    public bool ShowHeadDot
    {
        get => (bool)GetValue(ShowHeadDotProperty);
        set => SetValue(ShowHeadDotProperty, value);
    }

    /// <summary>Padding (dp) between the control edge and the fitted route bounds.</summary>
    public double RoutePadding
    {
        get => (double)GetValue(RoutePaddingProperty);
        set => SetValue(RoutePaddingProperty, value);
    }

    /// <summary>Stroke width (dp) of the crisp route polyline; the glow pass is this + 7.</summary>
    public double RouteStrokeWidth
    {
        get => (double)GetValue(RouteStrokeWidthProperty);
        set => SetValue(RouteStrokeWidthProperty, value);
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

        if (ShowBackground)
            DrawBackground(canvas, W, H);

        var samples = Samples;
        if (samples is null || samples.Count < 2) return;

        double top = TopSpeedMps > 0 ? TopSpeedMps : 1.0;

        // Fit-to-bounds over ALL samples (stable frame while UptoFraction animates).
        double mnX = double.MaxValue, mnY = double.MaxValue;
        double mxX = double.MinValue, mxY = double.MinValue;
        for (int i = 0; i < samples.Count; i++)
        {
            var s = samples[i];
            if (s.X < mnX) mnX = s.X;
            if (s.X > mxX) mxX = s.X;
            if (s.Y < mnY) mnY = s.Y;
            if (s.Y > mxY) mxY = s.Y;
        }
        double spanX = mxX - mnX; if (spanX == 0) spanX = 1;
        double spanY = mxY - mnY; if (spanY == 0) spanY = 1;

        float pad = (float)RoutePadding;
        double innerW = Math.Max(1, W - 2 * pad);
        double innerH = Math.Max(1, H - 2 * pad);
        double sc = Math.Min(innerW / spanX, innerH / spanY);
        double oX = pad + (innerW - spanX * sc) / 2;
        double oY = pad + (innerH - spanY * sc) / 2;

        SKPoint Project(RideSample s) => new(
            (float)(oX + (s.X - mnX) * sc),
            (float)(oY + (s.Y - mnY) * sc));

        double upto = UptoFraction;
        if (double.IsNaN(upto)) upto = 1;
        int lim = (int)Math.Clamp(Math.Round(Math.Clamp(upto, 0, 1) * (samples.Count - 1)), 1, samples.Count - 1);

        var pts = new SKPoint[lim + 1];
        for (int i = 0; i <= lim; i++)
            pts[i] = Project(samples[i]);

        using var stroke = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
        };

        // Glow pass: wide, translucent, ramp-colored.
        if (ShowGlow)
        {
            stroke.StrokeWidth = (float)RouteStrokeWidth + 7f;
            for (int i = 0; i < lim; i++)
            {
                double f = ((samples[i].SpeedMps + samples[i + 1].SpeedMps) / 2) / top;
                stroke.Color = VelocityRamp.ColorAt(f).WithAlpha(41); // 0.16 * 255
                canvas.DrawLine(pts[i], pts[i + 1], stroke);
            }
        }

        // Crisp pass.
        stroke.StrokeWidth = (float)RouteStrokeWidth;
        for (int i = 0; i < lim; i++)
        {
            double f = ((samples[i].SpeedMps + samples[i + 1].SpeedMps) / 2) / top;
            stroke.Color = VelocityRamp.ColorAt(f);
            canvas.DrawLine(pts[i], pts[i + 1], stroke);
        }

        using var fill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };

        // Start marker: dark disc with a green ring.
        fill.Color = BgColor;
        canvas.DrawCircle(pts[0], 5f, fill);
        stroke.StrokeWidth = 3f;
        stroke.Color = GoColor;
        canvas.DrawCircle(pts[0], 5f, stroke);

        // End marker.
        var en = pts[lim];
        if (ShowHeadDot)
        {
            stroke.StrokeWidth = 2f;
            stroke.Color = SKColors.White.WithAlpha(89); // 0.35 * 255
            canvas.DrawCircle(en, 11f, stroke);
            fill.Color = SKColors.White;
            canvas.DrawCircle(en, 6f, fill);
        }
        else
        {
            var rect = SKRect.Create(en.X - 4f, en.Y - 4f, 8f, 8f);
            fill.Color = VelocityRamp.ColorAt(samples[lim].SpeedMps / top);
            canvas.DrawRoundRect(rect, 2f, 2f, fill);
            stroke.StrokeWidth = 2f;
            stroke.Color = BgColor;
            canvas.DrawRoundRect(rect, 2f, 2f, stroke);
        }
    }

    static void DrawBackground(SKCanvas canvas, float W, float H)
    {
        using var fill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };

        // Field.
        fill.Color = BgColor;
        canvas.DrawRect(0, 0, W, H, fill);

        // 38dp grid.
        using var grid = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            Color = GridColor,
        };
        for (float gx = 0; gx < W; gx += 38f)
            canvas.DrawLine(gx, 0, gx, H, grid);
        for (float gy = 0; gy < H; gy += 38f)
            canvas.DrawLine(0, gy, W, gy, grid);

        // Decorative road.
        using var road = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 14f,
            StrokeCap = SKStrokeCap.Round,
            Color = RoadColor,
        };
        canvas.DrawLine(-20f, H * 0.7f, W + 20f, H * 0.3f, road);

        // Decorative water.
        fill.Color = WaterColor;
        canvas.DrawOval(W * 0.8f, H * 0.18f, 70f, 48f, fill);
    }
}
