using Esk8_Tracker.Core.Models;
using SQLite;

namespace Esk8_Tracker.Core.Data;

/// <summary>
/// Local SQLite persistence. Schema is created lazily on first use;
/// safe to resolve as a singleton and call from anywhere.
/// </summary>
public class Esk8Database : IRideStore
{
    private readonly SQLiteAsyncConnection _connection;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public Esk8Database(string dbPath)
    {
        _connection = new SQLiteAsyncConnection(dbPath,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        await _initLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_initialized) return;
            await _connection.CreateTableAsync<Board>().ConfigureAwait(false);
            await _connection.CreateTableAsync<Ride>().ConfigureAwait(false);
            await _connection.CreateTableAsync<TrackPoint>().ConfigureAwait(false);
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public Task CloseAsync() => _connection.CloseAsync();

    // ---- Boards ----

    public async Task<List<Board>> GetActiveBoardsAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        return await _connection.Table<Board>()
            .Where(b => !b.IsArchived)
            .OrderBy(b => b.Name)
            .ToListAsync().ConfigureAwait(false);
    }

    public async Task<Board> AddBoardAsync(string name)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        var board = new Board { Name = name, CreatedAt = DateTime.UtcNow };
        await _connection.InsertAsync(board).ConfigureAwait(false);
        return board;
    }

    public async Task RenameBoardAsync(int id, string name)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        await _connection.ExecuteAsync(
            "UPDATE Board SET Name = ? WHERE Id = ?", name, id).ConfigureAwait(false);
    }

    public async Task ArchiveBoardAsync(int id)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        await _connection.ExecuteAsync(
            "UPDATE Board SET IsArchived = 1 WHERE Id = ?", id).ConfigureAwait(false);
    }

    public async Task<Dictionary<int, string>> GetBoardNamesAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        var boards = await _connection.Table<Board>().ToListAsync().ConfigureAwait(false);
        return boards.ToDictionary(b => b.Id, b => b.Name);
    }

    // ---- Rides (IRideStore) ----

    public async Task<int> CreateRideAsync(int boardId, DateTime startedAtUtc)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        var ride = new Ride { BoardId = boardId, StartedAt = startedAtUtc };
        await _connection.InsertAsync(ride).ConfigureAwait(false);
        return ride.Id;
    }

    public async Task SavePointsAsync(IReadOnlyList<TrackPoint> points)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        await _connection.InsertAllAsync(points).ConfigureAwait(false);
    }

    public async Task FinalizeRideAsync(int rideId, double distanceMeters, double movingSeconds,
        double avgSpeedMps, double maxSpeedMps, DateTime endedAtUtc, bool wasRecovered)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        await _connection.ExecuteAsync(
            "UPDATE Ride SET DistanceMeters = ?, MovingSeconds = ?, AvgSpeedMps = ?, " +
            "MaxSpeedMps = ?, EndedAt = ?, WasRecovered = ? WHERE Id = ?",
            distanceMeters, movingSeconds, avgSpeedMps, maxSpeedMps,
            endedAtUtc, wasRecovered, rideId).ConfigureAwait(false);
    }

    // ---- Ride queries ----

    public async Task<List<Ride>> GetCompletedRidesAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        return await _connection.Table<Ride>()
            .Where(r => r.EndedAt != null)
            .OrderByDescending(r => r.StartedAt)
            .ToListAsync().ConfigureAwait(false);
    }

    public async Task<Ride?> GetRideAsync(int id)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        return await _connection.Table<Ride>()
            .Where(r => r.Id == id)
            .FirstOrDefaultAsync().ConfigureAwait(false);
    }

    public async Task<List<TrackPoint>> GetTrackPointsAsync(int rideId)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        return await _connection.Table<TrackPoint>()
            .Where(p => p.RideId == rideId)
            .OrderBy(p => p.Timestamp)
            .ToListAsync().ConfigureAwait(false);
    }

    // ---- Dashboard ----

    public async Task<DashboardStats> GetDashboardStatsAsync()
    {
        var rides = await GetCompletedRidesAsync().ConfigureAwait(false);
        var names = await GetBoardNamesAsync().ConfigureAwait(false);

        var monthly = rides
            .GroupBy(r => { var local = r.StartedAt.ToLocalTime(); return (local.Year, local.Month); })
            .Select(g => new MonthlyStat(g.Key.Year, g.Key.Month,
                g.Sum(r => r.DistanceMeters), g.Count()))
            .OrderByDescending(m => (m.Year, m.Month))
            .ToList();

        var perBoard = rides
            .GroupBy(r => r.BoardId)
            .Select(g => new BoardStat(
                names.TryGetValue(g.Key, out var name) ? name : "(unknown board)",
                g.Sum(r => r.DistanceMeters), g.Count()))
            .OrderByDescending(b => b.DistanceMeters)
            .ToList();

        return new DashboardStats
        {
            TotalDistanceMeters = rides.Sum(r => r.DistanceMeters),
            RideCount = rides.Count,
            TotalMovingSeconds = rides.Sum(r => r.MovingSeconds),
            TopSpeedMps = rides.Count > 0 ? rides.Max(r => r.MaxSpeedMps) : 0,
            LongestRideMeters = rides.Count > 0 ? rides.Max(r => r.DistanceMeters) : 0,
            Monthly = monthly,
            PerBoard = perBoard,
        };
    }

    // ---- Crash recovery ----

    public async Task<int> RecoverUnfinishedRidesAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        var unfinished = await _connection.Table<Ride>()
            .Where(r => r.EndedAt == null)
            .ToListAsync().ConfigureAwait(false);

        var recovered = 0;
        foreach (var ride in unfinished)
        {
            var points = await GetTrackPointsAsync(ride.Id).ConfigureAwait(false);
            if (points.Count == 0)
            {
                await _connection.DeleteAsync(ride).ConfigureAwait(false);
                continue;
            }

            var acc = new StatsAccumulator();
            foreach (var p in points)
                acc.Add(new GpsFix(p.Timestamp, p.Latitude, p.Longitude,
                    p.SpeedMps, p.AccuracyMeters, p.AltitudeMeters));

            await FinalizeRideAsync(ride.Id, acc.DistanceMeters, acc.MovingSeconds,
                acc.AvgSpeedMps, acc.MaxSpeedMps, points[^1].Timestamp,
                wasRecovered: true).ConfigureAwait(false);
            recovered++;
        }

        return recovered;
    }
}
