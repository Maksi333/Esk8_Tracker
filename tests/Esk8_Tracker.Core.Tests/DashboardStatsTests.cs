using Esk8_Tracker.Core.Data;

namespace Esk8_Tracker.Core.Tests;

public sealed class DashboardStatsTests : IAsyncLifetime
{
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

    private async Task<int> AddCompletedRide(int boardId, DateTime startUtc,
        double distance, double moving, double max)
    {
        var id = await _db.CreateRideAsync(boardId, startUtc);
        await _db.FinalizeRideAsync(id, distance, moving,
            moving > 0 ? distance / moving : 0, max, startUtc.AddSeconds(moving), false);
        return id;
    }

    [Fact]
    public async Task EmptyDatabase_GivesZeroes()
    {
        var stats = await _db.GetDashboardStatsAsync();
        Assert.Equal(0, stats.TotalDistanceMeters);
        Assert.Equal(0, stats.RideCount);
        Assert.Equal(0, stats.TopSpeedMps);
        Assert.Equal(0, stats.LongestRideMeters);
        Assert.Empty(stats.Monthly);
        Assert.Empty(stats.PerBoard);
    }

    [Fact]
    public async Task TotalsRecordsMonthlyAndPerBoard_AreComputed()
    {
        var may = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);
        var june = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var board1 = await _db.AddBoardAsync("Alpha");
        var board2 = await _db.AddBoardAsync("Beta");

        await AddCompletedRide(board1.Id, may, 5000, 1000, 10);
        await AddCompletedRide(board1.Id, june, 3000, 600, 12);
        await AddCompletedRide(board2.Id, june.AddDays(1), 9000, 1500, 8);

        var stats = await _db.GetDashboardStatsAsync();

        Assert.Equal(17000, stats.TotalDistanceMeters);
        Assert.Equal(3, stats.RideCount);
        Assert.Equal(3100, stats.TotalMovingSeconds);
        Assert.Equal(12, stats.TopSpeedMps);
        Assert.Equal(9000, stats.LongestRideMeters);

        Assert.Equal(2, stats.Monthly.Count);
        Assert.Equal((2026, 6), (stats.Monthly[0].Year, stats.Monthly[0].Month)); // newest first
        Assert.Equal(12000, stats.Monthly[0].DistanceMeters);
        Assert.Equal(2, stats.Monthly[0].RideCount);
        Assert.Equal(5000, stats.Monthly[1].DistanceMeters);

        Assert.Equal(2, stats.PerBoard.Count);
        Assert.Equal("Beta", stats.PerBoard[0].BoardName);   // most distance first
        Assert.Equal(9000, stats.PerBoard[0].DistanceMeters);
        Assert.Equal("Alpha", stats.PerBoard[1].BoardName);
        Assert.Equal(2, stats.PerBoard[1].RideCount);
    }

    [Fact]
    public async Task InProgressRides_AreExcluded()
    {
        var board = await _db.AddBoardAsync("Alpha");
        await _db.CreateRideAsync(board.Id, DateTime.UtcNow); // never finalized

        var stats = await _db.GetDashboardStatsAsync();

        Assert.Equal(0, stats.RideCount);
        Assert.Empty(stats.PerBoard);
    }

    [Fact]
    public async Task ArchivedBoard_StillNamedInPerBoard()
    {
        var board = await _db.AddBoardAsync("Retired");
        await AddCompletedRide(board.Id, new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc), 1000, 200, 5);
        await _db.ArchiveBoardAsync(board.Id);

        var stats = await _db.GetDashboardStatsAsync();

        Assert.Equal("Retired", stats.PerBoard[0].BoardName);
    }
}
