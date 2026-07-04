using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Esk8_Tracker.Controls;

/// <summary>
/// Full-width preview of the velocity ramp (board editor): the control rect is
/// filled with 40 vertical slices sampled across the ramp. Corner rounding, if
/// any, comes from the XAML container's clip.
/// </summary>
public class RampBarView : SKCanvasView
{
    private const int SliceCount = 40;

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

        float sliceW = W / SliceCount;

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
        };

        for (int i = 0; i < SliceCount; i++)
        {
            paint.Color = VelocityRamp.ColorAt((i + 0.5) / SliceCount);
            // Slight overdraw (+0.6) so antialiased edges don't show seams.
            canvas.DrawRect(i * sliceW, 0, sliceW + 0.6f, H, paint);
        }
    }
}
