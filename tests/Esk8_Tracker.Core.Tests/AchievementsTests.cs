using Esk8_Tracker.Core;
using Esk8_Tracker.Core.Models;

namespace Esk8_Tracker.Core.Tests;

public class AchievementsTests
{
    private static readonly DateOnly Today = new(2026, 7, 1);

    private static Ride Ride(double km = 5, double maxKmh = 25, string day = "2026-06-15",
        int boardId = 1, string tags = "", double? lat = null, double? lon = null,
        double movingSec = 1200, double gain = 10, int hour = 12)
    {
        var local = DateTime.Parse($"{day}T{hour:D2}:00:00");
        return new Ride
        {
            BoardId = boardId,
            StartedAt = local.ToUniversalTime(),
            EndedAt = local.ToUniversalTime().AddSeconds(movingSec),
            DistanceMeters = km * 1000,
            MovingSeconds = movingSec,
            MaxSpeedMps = maxKmh / 3.6,
            Tags = tags,
            StartLatitude = lat,
            StartLongitude = lon,
            ElevationGainMeters = gain,
        };
    }

    private static AchievementStatus Get(IReadOnlyList<AchievementStatus> all, string id) =>
        all.Single(s => s.Def.Id == id);

    [Fact]
    public void FreshInstall_NothingEarned_NoProgress()
    {
        var result = Achievements.Evaluate(Array.Empty<Ride>(), Today);
        Assert.Equal(Achievements.Defs.Count, result.Count);
        Assert.All(result, s => Assert.Equal(AchievementState.Locked, s.State));
        Assert.All(result, s => Assert.Equal(0, s.Progress));
    }

    [Fact]
    public void FirstSavedRide_EarnsFirstRide()
    {
        var result = Achievements.Evaluate(new[] { Ride() }, Today);
        var s = Get(result, "first_ride");
        Assert.Equal(AchievementState.Earned, s.State);
        Assert.NotNull(s.EarnedAtUtc);
    }

    [Fact]
    public void Century_ProgressesThenEarns()
    {
        var halfway = Achievements.Evaluate(new[] { Ride(km: 50) }, Today);
        var s = Get(halfway, "century");
        Assert.Equal(AchievementState.InProgress, s.State);
        Assert.Equal(0.5, s.Progress, 2);

        var done = Achievements.Evaluate(new[] { Ride(km: 50, day: "2026-06-10"), Ride(km: 51) }, Today);
        Assert.Equal(AchievementState.Earned, Get(done, "century").State);
    }

    [Fact]
    public void SpeedDemon_EarnedAt40Kmh()
    {
        var result = Achievements.Evaluate(new[] { Ride(maxKmh: 41) }, Today);
        Assert.Equal(AchievementState.Earned, Get(result, "speed_40").State);
        Assert.Equal(AchievementState.InProgress, Get(result, "speed_50").State);
    }

    [Fact]
    public void NightAndDawn_ByLocalStartHour()
    {
        var night = Achievements.Evaluate(new[] { Ride(hour: 22) }, Today);
        Assert.Equal(AchievementState.Earned, Get(night, "night_owl").State);
        Assert.Equal(AchievementState.Locked, Get(night, "early_bird").State);

        var dawn = Achievements.Evaluate(new[] { Ride(hour: 5) }, Today);
        Assert.Equal(AchievementState.Earned, Get(dawn, "early_bird").State);
    }

    [Fact]
    public void Explorer_CountsDistinctStartAreas()
    {
        var rides = Enumerable.Range(0, 5)
            .Select(i => Ride(day: $"2026-06-{10 + i:D2}", lat: 55.0 + i * 0.1, lon: 12.0))
            .ToArray();
        var result = Achievements.Evaluate(rides, Today);
        Assert.Equal(AchievementState.Earned, Get(result, "explorer").State);

        var sameSpot = Enumerable.Range(0, 5)
            .Select(i => Ride(day: $"2026-06-{10 + i:D2}", lat: 55.0001, lon: 12.0001))
            .ToArray();
        var partial = Achievements.Evaluate(sameSpot, Today);
        Assert.Equal(AchievementState.InProgress, Get(partial, "explorer").State);
        Assert.Equal(0.2, Get(partial, "explorer").Progress, 2);
    }

    [Fact]
    public void Collector_ThreeDistinctBoards()
    {
        var rides = new[] { Ride(boardId: 1), Ride(boardId: 2, day: "2026-06-16"), Ride(boardId: 3, day: "2026-06-17") };
        Assert.Equal(AchievementState.Earned, Get(Achievements.Evaluate(rides, Today), "collector").State);
    }

    [Fact]
    public void Squad_EarnedByGroupTag()
    {
        var result = Achievements.Evaluate(new[] { Ride(tags: "Session,Group") }, Today);
        Assert.Equal(AchievementState.Earned, Get(result, "squad").State);
    }

    [Fact]
    public void Streak_UsesLiveStreakForProgress()
    {
        var rides = new[] { Ride(day: "2026-06-29"), Ride(day: "2026-06-30"), Ride(day: "2026-07-01") };
        var s = Get(Achievements.Evaluate(rides, Today), "streak_7");
        Assert.Equal(AchievementState.InProgress, s.State);
        Assert.Equal(3.0 / 7, s.Progress, 2);
    }
}
