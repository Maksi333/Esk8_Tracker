using Esk8_Tracker.Core;

namespace Esk8_Tracker.Services;

/// <summary>
/// Speaks ride stats to earbuds/helmet at distance milestones (every 2 km metric,
/// every mile imperial) so the rider never has to look at the phone.
/// </summary>
public class VoiceCueService
{
    private readonly AppSettings _settings;
    private double _nextAnnounceMeters;

    public VoiceCueService(RideRecorder recorder, AppSettings settings)
    {
        _settings = settings;
        recorder.StateChanged += () =>
        {
            if (recorder.State == RecorderState.Idle) Reset();
        };
        recorder.StatsUpdated += OnStats;
        Reset();
    }

    private double IntervalMeters =>
        _settings.Units == UnitSystem.Imperial ? Units.MetersPerMile : 2000.0;

    public void Reset() => _nextAnnounceMeters = 0;

    private void OnStats(RideLiveStats stats)
    {
        if (!_settings.VoiceCues) return;
        if (_nextAnnounceMeters <= 0) _nextAnnounceMeters = IntervalMeters;
        if (stats.DistanceMeters < _nextAnnounceMeters) return;
        _nextAnnounceMeters += IntervalMeters;

        var u = _settings.Units;
        var dist = Units.Distance(stats.DistanceMeters, u);
        var avg = Units.Speed(stats.AvgSpeedMps, u);
        var distUnit = u == UnitSystem.Imperial ? "miles" : "kilometers";
        var spdUnit = u == UnitSystem.Imperial ? "miles per hour" : "kilometers per hour";
        var text = $"{dist} {distUnit}. Average {avg} {spdUnit}.";

        // Fire-and-forget: a failed TTS utterance must never disturb recording.
        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try { await TextToSpeech.SpeakAsync(text); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Voice cue failed: {ex}"); }
        });
    }
}
