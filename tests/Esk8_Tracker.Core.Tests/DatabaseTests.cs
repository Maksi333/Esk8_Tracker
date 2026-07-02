using Esk8_Tracker.Core;
using Esk8_Tracker.Core.Data;
using Esk8_Tracker.Core.Models;

namespace Esk8_Tracker.Core.Tests;

public sealed class DatabaseTests : IAsyncLifetime
{
    private static readonly DateTime T0 = new(2026, 7, 2, 10, 0, 0, DateTimeKind.Utc);

    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"esk8test_{Guid.NewGuid():N}.db3");
    private Esk8Database _db = null!;

    public Task InitializeAsync()
    {
        _db = new Esk8Database(_dbPath);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _db.CloseAsync();
        File.Delete(_dbPath);
    }

    private static TrackPoint Point(int rideId, double lat, double lon, double secondsAfterT0, double speed = 5) =>
        new()
        {
            RideId = rideId, Timestamp = T0.AddSeconds(secondsAfterT0),
            Latitude = lat, Longitude = lon, SpeedMps = speed, AccuracyMeters = 5,
        };

    [Fact]
    public async Task AddBoard_AppearsInActiveBoards()
    {
        var board = await _db.AddBoardAsync("Meepo V5");
        var boards = await _db.GetActiveBoardsAsync();
        Assert.Single(boards);
        Assert.Equal("Meepo V5", boards[0].Name);
        Assert.True(board.Id > 0);
    }

    [Fact]
    public async Task ArchivedBoard_LeavesActiveList_ButKeepsName()
    {
        var board = await _db.AddBoardAsync("Old faithful");
        await _db.ArchiveBoardAsync(board.Id);

        Assert.Empty(await _db.GetActiveBoardsAsync());
        var names = await _db.GetBoardNamesAsync();
        Assert.Equal("Old faithful", names[board.Id]);
    }

    [Fact]
    public async Task RenameBoard_ChangesName()
    {
        var board = await _db.AddBoardAsync("Tpyo");
        await _db.RenameBoardAsync(board.Id, "Typo");
        var boards = await _db.GetActiveBoardsAsync();
        Assert.Equal("Typo", boards[0].Name);
    }

    [Fact]
    public async Task CreateRide_IsInProgress_AndExcludedFromCompleted()
    {
        var board = await _db.AddBoardAsync("Board");
        var rideId = await _db.CreateRideAsync(board.Id, T0);

        Assert.True(rideId > 0);
        Assert.Empty(await _db.GetCompletedRidesAsync());
        var ride = await _db.GetRideAsync(rideId);
        Assert.NotNull(ride);
        Assert.Null(ride!.EndedAt);
    }

    [Fact]
    public async Task FinalizeRide_ShowsUpInCompleted_NewestFirst()
    {
        var board = await _db.AddBoardAsync("Board");
        var first = await _db.CreateRideAsync(board.Id, T0);
        await _db.FinalizeRideAsync(first, 1000, 300, 3.33, 8, T0.AddMinutes(10), false);
        var second = await _db.CreateRideAsync(board.Id, T0.AddHours(2));
        await _db.FinalizeRideAsync(second, 2000, 500, 4, 9, T0.AddHours(2).AddMinutes(15), false);

        var rides = await _db.GetCompletedRidesAsync();
        Assert.Equal(2, rides.Count);
        Assert.Equal(second, rides[0].Id);
        Assert.Equal(1000, rides[1].DistanceMeters);
    }

    [Fact]
    public async Task SavePoints_RoundTripsOrderedByTimestamp()
    {
        var board = await _db.AddBoardAsync("Board");
        var rideId = await _db.CreateRideAsync(board.Id, T0);
        await _db.SavePointsAsync(new[] { Point(rideId, 55.0001, 12, 2), Point(rideId, 55, 12, 0) });

        var points = await _db.GetTrackPointsAsync(rideId);
        Assert.Equal(2, points.Count);
        Assert.Equal(55.0, points[0].Latitude);   // earliest first
        Assert.Equal(55.0001, points[1].Latitude);
    }

    [Fact]
    public async Task Recovery_FinalizesUnfinishedRideFromPoints()
    {
        var board = await _db.AddBoardAsync("Board");
        var rideId = await _db.CreateRideAsync(board.Id, T0);
        await _db.SavePointsAsync(new[]
        {
            Point(rideId, 55.0000, 12, 0),
            Point(rideId, 55.0001, 12, 2),
            Point(rideId, 55.0002, 12, 4),
        });

        var recovered = await _db.RecoverUnfinishedRidesAsync();

        Assert.Equal(1, recovered);
        var ride = await _db.GetRideAsync(rideId);
        Assert.NotNull(ride!.EndedAt);
        Assert.Equal(T0.AddSeconds(4), ride.EndedAt!.Value);
        Assert.True(ride.WasRecovered);
        Assert.InRange(ride.DistanceMeters, 21, 23.4); // 2 hops of ~11.1 m
        Assert.Equal(4, ride.MovingSeconds, 3);
    }

    [Fact]
    public async Task Recovery_DeletesEmptyUnfinishedRide()
    {
        var board = await _db.AddBoardAsync("Board");
        var rideId = await _db.CreateRideAsync(board.Id, T0);

        var recovered = await _db.RecoverUnfinishedRidesAsync();

        Assert.Equal(0, recovered);
        Assert.Null(await _db.GetRideAsync(rideId));
    }

    [Fact]
    public async Task Recovery_LeavesCompletedRidesAlone()
    {
        var board = await _db.AddBoardAsync("Board");
        var rideId = await _db.CreateRideAsync(board.Id, T0);
        await _db.FinalizeRideAsync(rideId, 500, 100, 5, 7, T0.AddMinutes(5), false);

        var recovered = await _db.RecoverUnfinishedRidesAsync();

        Assert.Equal(0, recovered);
        var ride = await _db.GetRideAsync(rideId);
        Assert.False(ride!.WasRecovered);
        Assert.Equal(500, ride.DistanceMeters);
    }
}
