namespace Esk8_Tracker.Core;

public enum AutoPauseSensitivity { Off, Low, Normal, High }

/// <summary>
/// Watches the raw fix stream and decides when the ride should freeze (rider stopped
/// at a light) and thaw (rolling again). Pure timestamp math off fix times — no wall
/// clock — so it is deterministic and testable. The owner wires the events to
/// RideRecorder.Pause()/Resume() and the UI.
/// </summary>
public class AutoPauseMonitor
{
    public const double PauseSpeedMps = 0.6;   // ~2.2 km/h: GPS jitter floor when standing
    public const double ResumeSpeedMps = 1.4;  // ~5 km/h: unmistakably rolling

    private DateTime? _slowSince;
    private int _fastFixes;

    public AutoPauseSensitivity Sensitivity { get; set; } = AutoPauseSensitivity.Normal;

    /// <summary>True while a pause triggered by this monitor is in effect.</summary>
    public bool IsAutoPaused { get; private set; }

    public event Action? AutoPauseTriggered;
    public event Action? AutoResumeTriggered;

    public static double HoldSeconds(AutoPauseSensitivity s) => s switch
    {
        AutoPauseSensitivity.Low => 12,
        AutoPauseSensitivity.Normal => 6,
        AutoPauseSensitivity.High => 3,
        _ => double.PositiveInfinity,
    };

    /// <summary>Feed every raw fix while a ride is active or auto-paused.</summary>
    public void OnFix(GpsFix fix, RecorderState state)
    {
        if (Sensitivity == AutoPauseSensitivity.Off) return;
        var speed = fix.SpeedMps ?? 0;

        if (state == RecorderState.Recording)
        {
            if (speed < PauseSpeedMps)
            {
                _slowSince ??= fix.TimestampUtc;
                if ((fix.TimestampUtc - _slowSince.Value).TotalSeconds >= HoldSeconds(Sensitivity))
                {
                    IsAutoPaused = true;
                    _slowSince = null;
                    _fastFixes = 0;
                    AutoPauseTriggered?.Invoke();
                }
            }
            else
            {
                _slowSince = null;
            }
        }
        else if (state == RecorderState.Paused && IsAutoPaused)
        {
            // Two consecutive fast fixes so a single jitter spike doesn't resume.
            if (speed > ResumeSpeedMps)
            {
                if (++_fastFixes >= 2)
                {
                    IsAutoPaused = false;
                    _fastFixes = 0;
                    AutoResumeTriggered?.Invoke();
                }
            }
            else
            {
                _fastFixes = 0;
            }
        }
    }

    /// <summary>Call on manual pause/resume/stop so stale auto-state can't fire later.</summary>
    public void Reset()
    {
        IsAutoPaused = false;
        _slowSince = null;
        _fastFixes = 0;
    }
}
