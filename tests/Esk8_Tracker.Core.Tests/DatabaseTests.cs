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

    private Task<Board> AddBoardAsync(string name) =>
        _db.SaveBoardAsync(new Board { Name = name });

    private static TrackPoint Point(int rideId, double lat, double lon, double secondsAfterT0, double speed = 5) =>
        new()
        {
            RideId = rideId, Timestamp = T0.AddSeconds(secondsAfterT0),
            Latitude = lat, Longitude = lon, SpeedMps = speed, AccuracyMeters = 5,
        };

    [Fact]
    public async Task AddBoard_AppearsInActiveBoards()
    {
        var board = await AddBoardAsync("Meepo V5");
        var boards = await _db.GetActiveBoardsAsync();
        Assert.Single(boards);
        Assert.Equal("Meepo V5", boards[0].Name);
        Assert.True(board.Id > 0);
    }

    [Fact]
    public async Task ArchivedBoard_LeavesActiveList_ButStaysResolvable()
    {
        var board = await AddBoardAsync("Old faithful");
        await _db.ArchiveBoardAsync(board.Id);

        Assert.Empty(await _db.GetActiveBoardsAsync());
        var byId = await _db.GetBoardsByIdAsync();
        Assert.Equal("Old faithful", byId[board.Id].Name);
    }

    [Fact]
    public async Task SaveBoard_UpdatesExistingSpecs()
    {
        var board = await AddBoardAsync("Tpyo");
        board.Name = "Typo";
        board.TopSpeedKmh = 45;
        board.BatteryWh = 504;
        board.ColorHex = "#F5C51E";
        await _db.SaveBoardAsync(board);

        var boards = await _db.GetActiveBoardsAsync();
        Assert.Equal("Typo", boards[0].Name);
        Assert.Equal(45, boards[0].TopSpeedKmh);
        Assert.Equal(504, boards[0].BatteryWh);
        Assert.Equal("#F5C51E", boards[0].ColorHex);
    }

    [Fact]
    public async Task CreateRide_IsInProgress_AndExcludedFromCompleted()
    {
        var board = await AddBoardAsync("Board");
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
        var board = await AddBoardAsync("Board");
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
    public async Task FinalizeRide_CapturesStartCoordinates()
    {
        var board = await AddBoardAsync("Board");
        var rideId = await _db.CreateRideAsync(board.Id, T0);
        await _db.SavePointsAsync(new[] { Point(rideId, 55.5, 12.5, 0), Point(rideId, 55.5001, 12.5, 2) });
        await _db.FinalizeRideAsync(rideId, 11, 2, 5.5, 6, T0.AddSeconds(2), false);

        var ride = await _db.GetRideAsync(rideId);
        Assert.Equal(55.5, ride!.StartLatitude);
        Assert.Equal(12.5, ride.StartLongitude);
    }

    [Fact]
    public async Task SavePoints_RoundTripsOrderedByTimestamp()
    {
        var board = await AddBoardAsync("Board");
        var rideId = await _db.CreateRideAsync(board.Id, T0);
        await _db.SavePointsAsync(new[] { Point(rideId, 55.0001, 12, 2), Point(rideId, 55, 12, 0) });

        var points = await _db.GetTrackPointsAsync(rideId);
        Assert.Equal(2, points.Count);
        Assert.Equal(55.0, points[0].Latitude);   // earliest first
        Assert.Equal(55.0001, points[1].Latitude);
    }

    [Fact]
    public async Task UpdateRideMeta_PersistsNameTagsNotes()
    {
        var board = await AddBoardAsync("Board");
        var rideId = await _db.CreateRideAsync(board.Id, T0);
        await _db.FinalizeRideAsync(rideId, 500, 100, 5, 7, T0.AddMinutes(5), false);

        await _db.UpdateRideMetaAsync(rideId, "Evening commute", "Commute,Night", "smooth roads");

        var ride = await _db.GetRideAsync(rideId);
        Assert.Equal("Evening commute", ride!.Name);
        Assert.Equal(new[] { "Commute", "Night" }, ride.TagList);
        Assert.Equal("smooth roads", ride.Notes);
    }

    [Fact]
    public async Task DeleteRide_RemovesRideAndPoints()
    {
        var board = await AddBoardAsync("Board");
        var rideId = await _db.CreateRideAsync(board.Id, T0);
        await _db.SavePointsAsync(new[] { Point(rideId, 55, 12, 0) });
        await _db.FinalizeRideAsync(rideId, 500, 100, 5, 7, T0.AddMinutes(5), false);

        await _db.DeleteRideAsync(rideId);

        Assert.Null(await _db.GetRideAsync(rideId));
        Assert.Empty(await _db.GetTrackPointsAsync(rideId));
    }

    [Fact]
    public async Task UnfinishedRide_WithPoints_IsOfferedForRecovery()
    {
        var board = await AddBoardAsync("Board");
        var rideId = await _db.CreateRideAsync(board.Id, T0);
        await _db.SavePointsAsync(new[]
        {
            Point(rideId, 55.0000, 12, 0),
            Point(rideId, 55.0001, 12, 2),
        });

        var unfinished = await _db.GetUnfinishedRideAsync();

        Assert.NotNull(unfinished);
        Assert.Equal(rideId, unfinished!.Id);
        Assert.Null(unfinished.EndedAt);
    }

    [Fact]
    public async Task UnfinishedRide_WithoutPoints_IsDeletedNotOffered()
    {
        var board = await AddBoardAsync("Board");
        var rideId = await _db.CreateRideAsync(board.Id, T0);

        var unfinished = await _db.GetUnfinishedRideAsync();

        Assert.Null(unfinished);
        Assert.Null(await _db.GetRideAsync(rideId));
    }

    [Fact]
    public async Task FinalizeFromPoints_ComputesStatsAndMarksRecovered()
    {
        var board = await AddBoardAsync("Board");
        var rideId = await _db.CreateRideAsync(board.Id, T0);
        await _db.SavePointsAsync(new[]
        {
            Point(rideId, 55.0000, 12, 0),
            Point(rideId, 55.0001, 12, 2),
            Point(rideId, 55.0002, 12, 4),
        });
        var ride = await _db.GetRideAsync(rideId);

        await _db.FinalizeFromPointsAsync(ride!);

        ride = await _db.GetRideAsync(rideId);
        Assert.NotNull(ride!.EndedAt);
        Assert.Equal(T0.AddSeconds(4), ride.EndedAt!.Value);
        Assert.True(ride.WasRecovered);
        Assert.InRange(ride.DistanceMeters, 21, 23.4); // 2 hops of ~11.1 m
        Assert.Equal(4, ride.MovingSeconds, 3);
    }

    [Fact]
    public async Task CompletedRides_AreNotOfferedForRecovery()
    {
        var board = await AddBoardAsync("Board");
        var rideId = await _db.CreateRideAsync(board.Id, T0);
        await _db.FinalizeRideAsync(rideId, 500, 100, 5, 7, T0.AddMinutes(5), false);

        Assert.Null(await _db.GetUnfinishedRideAsync());
        var ride = await _db.GetRideAsync(rideId);
        Assert.False(ride!.WasRecovered);
        Assert.Equal(500, ride.DistanceMeters);
    }
}
