using Esk8_Tracker.Core;

namespace Esk8_Tracker.Core.Tests;

public class AutoPauseMonitorTests
{
    private static readonly DateTime T0 = new(2026, 7, 2, 10, 0, 0, DateTimeKind.Utc);

    private static GpsFix Fix(double secondsAfterT0, double speedMps) =>
        new(T0.AddSeconds(secondsAfterT0), 55, 12, speedMps, 5, null);

    [Fact]
    public void SlowFixes_TriggerAutoPause_AfterHoldWindow()
    {
        var monitor = new AutoPauseMonitor { Sensitivity = AutoPauseSensitivity.Normal };
        var paused = 0;
        monitor.AutoPauseTriggered += () => paused++;

        for (var t = 0; t <= 7; t++)
            monitor.OnFix(Fix(t, 0.2), RecorderState.Recording);

        Assert.Equal(1, paused);
        Assert.True(monitor.IsAutoPaused);
    }

    [Fact]
    public void BriefSlowdown_DoesNotPause()
    {
        var monitor = new AutoPauseMonitor { Sensitivity = AutoPauseSensitivity.Normal };
        var paused = 0;
        monitor.AutoPauseTriggered += () => paused++;

        monitor.OnFix(Fix(0, 0.2), RecorderState.Recording);
        monitor.OnFix(Fix(3, 0.2), RecorderState.Recording);
        monitor.OnFix(Fix(4, 5.0), RecorderState.Recording); // rolling again resets the window
        monitor.OnFix(Fix(9, 0.2), RecorderState.Recording);

        Assert.Equal(0, paused);
    }

    [Fact]
    public void SensitivityOff_NeverPauses()
    {
        var monitor = new AutoPauseMonitor { Sensitivity = AutoPauseSensitivity.Off };
        var paused = 0;
        monitor.AutoPauseTriggered += () => paused++;

        for (var t = 0; t <= 60; t++)
            monitor.OnFix(Fix(t, 0), RecorderState.Recording);

        Assert.Equal(0, paused);
    }

    [Fact]
    public void TwoFastFixes_AutoResume_SingleSpikeDoesNot()
    {
        var monitor = new AutoPauseMonitor { Sensitivity = AutoPauseSensitivity.High };
        var resumed = 0;
        monitor.AutoResumeTriggered += () => resumed++;

        for (var t = 0; t <= 4; t++)
            monitor.OnFix(Fix(t, 0.1), RecorderState.Recording);
        Assert.True(monitor.IsAutoPaused);

        monitor.OnFix(Fix(5, 3.0), RecorderState.Paused);  // one spike
        monitor.OnFix(Fix(6, 0.2), RecorderState.Paused);  // back to still
        Assert.Equal(0, resumed);

        monitor.OnFix(Fix(7, 3.0), RecorderState.Paused);
        monitor.OnFix(Fix(8, 3.2), RecorderState.Paused);
        Assert.Equal(1, resumed);
        Assert.False(monitor.IsAutoPaused);
    }

    [Fact]
    public void ManualPause_Reset_SuppressesAutoResume()
    {
        var monitor = new AutoPauseMonitor { Sensitivity = AutoPauseSensitivity.High };
        for (var t = 0; t <= 4; t++)
            monitor.OnFix(Fix(t, 0.1), RecorderState.Recording);
        Assert.True(monitor.IsAutoPaused);

        monitor.Reset(); // rider pressed pause themselves / stop
        var resumed = 0;
        monitor.AutoResumeTriggered += () => resumed++;
        monitor.OnFix(Fix(10, 5), RecorderState.Paused);
        monitor.OnFix(Fix(11, 5), RecorderState.Paused);

        Assert.Equal(0, resumed);
    }
}
