using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Esk8_Tracker.Controls;

/// <summary>
/// Hold-to-stop bar: a red fill grows left→right over 850 ms; releasing before the
/// end cancels. Prevents an accidental tap from ending a ride. Raises
/// <see cref="Completed"/> when the fill reaches 100%.
/// </summary>
public class HoldToStopBar : SKCanvasView
{
    private const double HoldMillis = 850;

    private IDispatcherTimer? _timer;
    private DateTime _pressStart;
    private double _progress; // 0..1

    public event EventHandler? Completed;

    public HoldToStopBar()
    {
        EnableTouchEvents = true;
        HeightRequest = 52;
        Touch += OnTouch;
    }

    private void OnTouch(object? sender, SKTouchEventArgs e)
    {
        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                StartHold();
                break;
            case SKTouchAction.Released:
            case SKTouchAction.Cancelled:
            case SKTouchAction.Exited:
                CancelHold();
                break;
        }
        e.Handled = true;
    }

    private void StartHold()
    {
        if (_timer is not null) return;
        _pressStart = DateTime.UtcNow;
        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(24);
        _timer.Tick += (_, _) =>
        {
            _progress = Math.Min(1.0, (DateTime.UtcNow - _pressStart).TotalMilliseconds / HoldMillis);
            InvalidateSurface();
            if (_progress >= 1.0)
            {
                CancelHold();
                Completed?.Invoke(this, EventArgs.Empty);
            }
        };
        _timer.Start();
    }

    private void CancelHold()
    {
        _timer?.Stop();
        _timer = null;
        _progress = 0;
        InvalidateSurface();
    }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        base.OnPaintSurface(e);
        var canvas = e.Surface.Canvas;
        canvas.Clear();
        if (Width <= 0 || Height <= 0) return;

        var scale = (float)(e.Info.Width / Width);
        canvas.Scale(scale);
        float w = (float)Width, h = (float)Height;

        var rect = new SKRect(0, 0, w, h);
        var round = new SKRoundRect(rect, 15f);

        using (var bg = new SKPaint { Style = SKPaintStyle.Fill, Color = new SKColor(0x1A, 0x13, 0x15), IsAntialias = true })
            canvas.DrawRoundRect(round, bg);
        using (var border = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 1, Color = new SKColor(0x3A, 0x22, 0x26), IsAntialias = true })
            canvas.DrawRoundRect(round, border);

        if (_progress > 0)
        {
            canvas.Save();
            canvas.ClipRoundRect(round, antialias: true);
            using var fill = new SKPaint { Style = SKPaintStyle.Fill, Color = new SKColor(0xF0, 0x3E, 0x3E), IsAntialias = true };
            canvas.DrawRect(new SKRect(0, 0, w * (float)_progress, h), fill);
            canvas.Restore();
        }

        var textColor = _progress > 0.45 ? SKColors.White : new SKColor(0xF0, 0x3E, 0x3E);
        var label = _progress > 0 ? "KEEP HOLDING…" : "STOP · HOLD";

        using var iconFont = new SKFont(SkiaFontsExtra.MaterialRounded, 20f);
        using var textFont = new SKFont(SkiaFontsExtra.SpaceGroteskSemiBold, 14f);
        using var paint = new SKPaint { Color = textColor, IsAntialias = true };

        var iconGlyph = ""; // stop_circle
        iconGlyph = MaterialIcons.StopCircle;
        var iconWidth = iconFont.MeasureText(iconGlyph);
        var textWidth = textFont.MeasureText(label);
        var gap = 8f;
        var total = iconWidth + gap + textWidth;
        var startX = (w - total) / 2f;
        var midY = h / 2f;

        canvas.DrawText(iconGlyph, startX, midY + iconFont.Size * 0.36f, SKTextAlign.Left, iconFont, paint);
        canvas.DrawText(label, startX + iconWidth + gap, midY + textFont.Size * 0.36f, SKTextAlign.Left, textFont, paint);
    }
}
