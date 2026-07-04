using Esk8_Tracker.Core;

namespace Esk8_Tracker.Services;

/// <summary>
/// Preferences-backed app settings. Singleton; raise <see cref="Changed"/> so open
/// screens can re-format immediately (the design requires instant unit switching).
/// </summary>
public class AppSettings
{
    public event Action? Changed;

    public UnitSystem Units
    {
        get => Preferences.Get("units", "metric") == "imperial" ? UnitSystem.Imperial : UnitSystem.Metric;
        set { Preferences.Set("units", value == UnitSystem.Imperial ? "imperial" : "metric"); Changed?.Invoke(); }
    }

    public AutoPauseSensitivity AutoPause
    {
        get => Enum.TryParse<AutoPauseSensitivity>(Preferences.Get("autoPause", nameof(AutoPauseSensitivity.Normal)), out var s)
            ? s : AutoPauseSensitivity.Normal;
        set { Preferences.Set("autoPause", value.ToString()); Changed?.Invoke(); }
    }

    public bool VoiceCues
    {
        get => Preferences.Get("voiceCues", false);
        set { Preferences.Set("voiceCues", value); Changed?.Invoke(); }
    }

    public bool AutoGlance
    {
        get => Preferences.Get("autoGlance", false);
        set { Preferences.Set("autoGlance", value); Changed?.Invoke(); }
    }

    public bool ColorizeMap
    {
        get => Preferences.Get("colorizeMap", true);
        set { Preferences.Set("colorizeMap", value); Changed?.Invoke(); }
    }

    /// <summary>0 = none selected yet (fresh install / all boards deleted).</summary>
    public int ActiveBoardId
    {
        get => Preferences.Get("activeBoardId", 0);
        set { Preferences.Set("activeBoardId", value); Changed?.Invoke(); }
    }

    public bool OnboardingDone
    {
        get => Preferences.Get("onboardingDone", false);
        set => Preferences.Set("onboardingDone", value);
    }
}
