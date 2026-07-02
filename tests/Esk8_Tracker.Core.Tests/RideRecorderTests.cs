using Esk8_Tracker.Core;
using Esk8_Tracker.Core.Data;
using Esk8_Tracker.Core.Models;

namespace Esk8_Tracker.Core.Tests;

public class FakeRideStore : IRideStore
{
    public int NextRideId = 42;
    public List<TrackPoint> SavedPoints = new();
    public int SaveCalls;
    public (int RideId, double Distance, double Moving, double Avg, double Max, DateTime EndedAt, bool Recovered)? Finalized;

    public Task<int> CreateRideAsync(int boardId, DateTime startedAtUtc) => Task.FromResult(NextRideId);

    public bool FailNextSave;

    public Task SavePointsAsync(IReadOnlyList<TrackPoint> points)
    {
        SaveCalls++;
        if (FailNextSave)
        {
            FailNextSave = false;
            throw new InvalidOperationException("simulated save failure");
        }
        SavedPoints.AddRange(points);
        return Task.CompletedTask;
    }

    public Task FinalizeRideAsync(int rideId, double distanceMeters, double movingSeconds,
        double avgSpeedMps, double maxSpeedMps, DateTime endedAtUtc, bool wasRecovered)
    {
        Finalized = (rideId, distanceMeters, movingSeconds, avgSpeedMps, maxSpeedMps, endedAtUtc, wasRecovered);
        return Task.CompletedTask;
    }
}

public class RideRecorderTests
{
    private static readonly DateTime T0 = new(2026, 7, 2, 10, 0, 0, DateTimeKind.Utc);

    private static GpsFix Fix(double lat, double lon, double secondsAfterT0,
        double accuracy = 5, double? speed = null) =>
        new(T0.AddSeconds(secondsAfterT0), lat, lon, speed, accuracy, null);

    private readonly FakeRideStore _store = new();
    private readonly RideRecorder _recorder;

    public RideRecorderTests() => _recorder = new RideRecorder(_store);

    [Fact]
    public async Task Start_CreatesRide_AndEntersRecording()
    {
        var stateChanges = 0;
        _recorder.StateChanged += () => stateChanges++;

        var id = await _recorder.StartAsync(boardId: 7, T0);

        Assert.Equal(42, id);
        Assert.Equal(RecorderState.Recording, _recorder.State);
        Assert.Equal(42, _recorder.ActiveRideId);
        Assert.Equal(T0, _recorder.RideStartedUtc);
        Assert.Equal(1, stateChanges);
    }

    [Fact]
    public async Task Start_WhileRecording_Throws()
    {
        await _recorder.StartAsync(7, T0);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _recorder.StartAsync(8, T0));
    }

    [Fact]
    public async Task Fix_WhileIdle_IsIgnored()
    {
        var events = 0;
        _recorder.FixAccepted += _ => events++;
        await _recorder.OnFixAsync(Fix(55, 12, 0));
        Assert.Equal(0, events);
        Assert.Empty(_store.SavedPoints);
    }

    [Fact]
    public async Task AcceptedFix_RaisesEvents_AndTracksRoute()
    {
        await _recorder.StartAsync(7, T0);
        GpsFix? accepted = null;
        RideLiveStats? stats = null;
        _recorder.FixAccepted += f => accepted = f;
        _recorder.StatsUpdated += s => stats = s;

        await _recorder.OnFixAsync(Fix(55, 12, 1, speed: 5));

        Assert.NotNull(accepted);
        Assert.NotNull(stats);
        Assert.Equal(5, stats!.CurrentSpeedMps);
        Assert.Single(_recorder.RoutePoints);
    }

    [Fact]
    public async Task RejectedFix_RaisesNothing()
    {
        await _recorder.StartAsync(7, T0);
        var events = 0;
        _recorder.FixAccepted += _ => events++;

        await _recorder.OnFixAsync(Fix(55, 12, 1, accuracy: 99));

        Assert.Equal(0, events);
        Assert.Empty(_recorder.RoutePoints);
    }

    [Fact]
    public async Task Buffer_FlushesAfterTenPoints()
    {
        await _recorder.StartAsync(7, T0);
        for (var i = 0; i < 9; i++)
            await _recorder.OnFixAsync(Fix(55 + i * 0.00001, 12, i, speed: 3));
        Assert.Equal(0, _store.SaveCalls);

        await _recorder.OnFixAsync(Fix(55.0001, 12, 9.5, speed: 3));

        Assert.Equal(1, _store.SaveCalls);
        Assert.Equal(10, _store.SavedPoints.Count);
        Assert.All(_store.SavedPoints, p => Assert.Equal(42, p.RideId));
    }

    [Fact]
    public async Task Buffer_FlushesAfterTenSeconds()
    {
        await _recorder.StartAsync(7, T0);
        await _recorder.OnFixAsync(Fix(55, 12, 0, speed: 3));
        Assert.Equal(0, _store.SaveCalls);

        await _recorder.OnFixAsync(Fix(55.0001, 12, 11, speed: 3));

        Assert.Equal(1, _store.SaveCalls);
        Assert.Equal(2, _store.SavedPoints.Count);
    }

    [Fact]
    public async Task Pause_DiscardsFixes_AndResumeBreaksSegment()
    {
        await _recorder.StartAsync(7, T0);
        await _recorder.OnFixAsync(Fix(55, 12, 0, speed: 3));
        _recorder.Pause();
        Assert.Equal(RecorderState.Paused, _recorder.State);

        await _recorder.OnFixAsync(Fix(55.005, 12, 5, speed: 3)); // ignored
        Assert.Single(_recorder.RoutePoints);

        _recorder.Resume();
        Assert.Equal(RecorderState.Recording, _recorder.State);

        // Far away after pause: must not add ~550 m of distance across the pause.
        await _recorder.OnFixAsync(Fix(55.005, 12, 20, speed: 3));
        Assert.Equal(0, _recorder.CurrentStats.DistanceMeters, 3);
    }

    [Fact]
    public async Task Stop_FlushesRemainder_Finalizes_AndGoesIdle()
    {
        await _recorder.StartAsync(7, T0);
        await _recorder.OnFixAsync(Fix(55, 12, 0, speed: 3));
        await _recorder.OnFixAsync(Fix(55.0001, 12, 2, speed: 3));

        await _recorder.StopAsync(T0.AddSeconds(30));

        Assert.Equal(RecorderState.Idle, _recorder.State);
        Assert.Null(_recorder.ActiveRideId);
        Assert.Equal(2, _store.SavedPoints.Count);
        Assert.NotNull(_store.Finalized);
        Assert.Equal(42, _store.Finalized!.Value.RideId);
        Assert.Equal(T0.AddSeconds(30), _store.Finalized!.Value.EndedAt);
        Assert.False(_store.Finalized!.Value.Recovered);
        Assert.InRange(_store.Finalized!.Value.Distance, 10.5, 11.7);
    }

    [Fact]
    public async Task Stop_WhilePaused_IsAllowed()
    {
        await _recorder.StartAsync(7, T0);
        _recorder.Pause();
        await _recorder.StopAsync(T0.AddSeconds(10));
        Assert.Equal(RecorderState.Idle, _recorder.State);
        Assert.NotNull(_store.Finalized);
    }

    [Fact]
    public async Task SignalLost_WhenNoFixForFiveSeconds()
    {
        await _recorder.StartAsync(7, T0);
        // No fix yet: lost once 5 s have passed since start.
        Assert.False(_recorder.IsSignalLost(T0.AddSeconds(4)));
        Assert.True(_recorder.IsSignalLost(T0.AddSeconds(6)));

        await _recorder.OnFixAsync(Fix(55, 12, 10));
        Assert.False(_recorder.IsSignalLost(T0.AddSeconds(14)));
        Assert.True(_recorder.IsSignalLost(T0.AddSeconds(16)));
    }

    [Fact]
    public void SignalLost_IsFalseWhenIdle()
    {
        Assert.False(_recorder.IsSignalLost(T0));
    }

    [Fact]
    public async Task SecondStart_BeginsCleanRide()
    {
        await _recorder.StartAsync(7, T0);
        await _recorder.OnFixAsync(Fix(55, 12, 0, speed: 3));
        await _recorder.OnFixAsync(Fix(55.0001, 12, 2, speed: 3));
        await _recorder.StopAsync(T0.AddSeconds(5));

        _store.NextRideId = 43;
        await _recorder.StartAsync(7, T0.AddMinutes(5));

        Assert.Empty(_recorder.RoutePoints);
        Assert.Equal(0, _recorder.CurrentStats.DistanceMeters);
        Assert.Equal(43, _recorder.ActiveRideId);
    }

    [Fact]
    public async Task FlushFailure_KeepsPointsAndRetriesNextFlush()
    {
        await _recorder.StartAsync(7, T0);
        _store.FailNextSave = true;

        // 10th point trips the count trigger; the save fails but must not throw here.
        for (var i = 0; i < 10; i++)
            await _recorder.OnFixAsync(Fix(55 + i * 0.00001, 12, i, speed: 3));

        Assert.Equal(1, _store.SaveCalls);
        Assert.Empty(_store.SavedPoints);
        Assert.Equal(RecorderState.Recording, _recorder.State);

        // Next fix retriggers the count flush (11 buffered >= 10): nothing lost.
        await _recorder.OnFixAsync(Fix(55.0001, 12, 10, speed: 3));

        Assert.Equal(2, _store.SaveCalls);
        Assert.Equal(11, _store.SavedPoints.Count);
        Assert.Equal(T0, _store.SavedPoints[0].Timestamp);
        Assert.Equal(T0.AddSeconds(10), _store.SavedPoints[^1].Timestamp);
    }

    [Fact]
    public async Task StopAsync_PropagatesStoreFailure_AndStaysActive()
    {
        await _recorder.StartAsync(7, T0);
        await _recorder.OnFixAsync(Fix(55, 12, 0, speed: 3));
        await _recorder.OnFixAsync(Fix(55.0001, 12, 2, speed: 3));

        _store.FailNextSave = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => _recorder.StopAsync(T0.AddSeconds(30)));

        Assert.Equal(RecorderState.Recording, _recorder.State);
        Assert.Equal(42, _recorder.ActiveRideId);
        Assert.Null(_store.Finalized);

        // Store healthy again: retrying Stop succeeds with all points intact.
        await _recorder.StopAsync(T0.AddSeconds(31));

        Assert.Equal(RecorderState.Idle, _recorder.State);
        Assert.Equal(2, _store.SavedPoints.Count);
        Assert.NotNull(_store.Finalized);
    }
}
