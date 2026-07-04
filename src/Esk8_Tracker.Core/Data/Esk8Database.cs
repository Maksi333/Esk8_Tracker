using Esk8_Tracker.Core.Models;
using SQLite;

namespace Esk8_Tracker.Core.Data;

/// <summary>
/// Local SQLite persistence. Schema is created lazily on first use (sqlite-net adds
/// new columns on model growth); safe to resolve as a singleton and call from anywhere.
/// </summary>
public class Esk8Database : IRideStore
{
    private readonly SQLiteAsyncConnection _connection;
    private readonly string _dbPath;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public Esk8Database(string dbPath)
    {
        _dbPath = dbPath;
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

    /// <summary>Size of the database file on disk ("Storage used" in Settings).</summary>
    public long StorageBytes()
    {
        try
        {
            var info = new FileInfo(_dbPath);
            return info.Exists ? info.Length : 0;
        }
        catch
        {
            return 0;
        }
    }

    // ---- Boards ----

    public async Task<List<Board>> GetActiveBoardsAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        return await _connection.Table<Board>()
            .Where(b => !b.IsArchived)
            .OrderBy(b => b.CreatedAt)
            .ToListAsync().ConfigureAwait(false);
    }

    public async Task<Board?> GetBoardAsync(int id)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        return await _connection.Table<Board>()
            .Where(b => b.Id == id)
            .FirstOrDefaultAsync().ConfigureAwait(false);
    }

    public async Task<Board> SaveBoardAsync(Board board)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        if (board.Id == 0)
        {
            board.CreatedAt = DateTime.UtcNow;
            await _connection.InsertAsync(board).ConfigureAwait(false);
        }
        else
        {
            await _connection.UpdateAsync(board).ConfigureAwait(false);
        }
        return board;
    }

    public async Task ArchiveBoardAsync(int id)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        await _connection.ExecuteAsync(
            "UPDATE Board SET IsArchived = 1 WHERE Id = ?", id).ConfigureAwait(false);
    }

    public async Task<Dictionary<int, Board>> GetBoardsByIdAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        var boards = await _connection.Table<Board>().ToListAsync().ConfigureAwait(false);
        return boards.ToDictionary(b => b.Id);
    }

    /// <summary>Total saved distance per board (the per-board odometer in the Garage).</summary>
    public async Task<Dictionary<int, double>> GetBoardOdometersAsync()
    {
        var rides = await GetCompletedRidesAsync().ConfigureAwait(false);
        return rides.GroupBy(r => r.BoardId)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.DistanceMeters));
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

        // Derive elevation gain + start coordinates from the stored track once, at the end.
        var points = await GetTrackPointsAsync(rideId).ConfigureAwait(false);
        var gain = RideAnalysis.ElevationGainMeters(points);
        double? startLat = points.Count > 0 ? points[0].Latitude : null;
        double? startLon = points.Count > 0 ? points[0].Longitude : null;

        await _connection.ExecuteAsync(
            "UPDATE Ride SET DistanceMeters = ?, MovingSeconds = ?, AvgSpeedMps = ?, " +
            "MaxSpeedMps = ?, EndedAt = ?, WasRecovered = ?, ElevationGainMeters = ?, " +
            "StartLatitude = ?, StartLongitude = ? WHERE Id = ?",
            distanceMeters, movingSeconds, avgSpeedMps, maxSpeedMps,
            endedAtUtc, wasRecovered, gain, startLat, startLon, rideId).ConfigureAwait(false);
    }

    public async Task UpdateRideMetaAsync(int rideId, string name, string tags, string notes)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        await _connection.ExecuteAsync(
            "UPDATE Ride SET Name = ?, Tags = ?, Notes = ? WHERE Id = ?",
            name, tags, notes, rideId).ConfigureAwait(false);
    }

    public async Task UpdateRidePhotosAsync(int rideId, string photosJson)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        await _connection.ExecuteAsync(
            "UPDATE Ride SET PhotosJson = ? WHERE Id = ?", photosJson, rideId).ConfigureAwait(false);
    }

    public async Task DeleteRideAsync(int rideId)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        await _connection.ExecuteAsync(
            "DELETE FROM TrackPoint WHERE RideId = ?", rideId).ConfigureAwait(false);
        await _connection.ExecuteAsync(
            "DELETE FROM Ride WHERE Id = ?", rideId).ConfigureAwait(false);
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

    public async Task<List<TrackPoint>> GetAllTrackPointsAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        return await _connection.Table<TrackPoint>()
            .OrderBy(p => p.RideId)
            .ToListAsync().ConfigureAwait(false);
    }

    // ---- Crash recovery ----

    /// <summary>
    /// The interrupted ride to offer in the "Resume unsaved ride?" banner, or null.
    /// Rides with no stored points are deleted (nothing to recover).
    /// </summary>
    public async Task<Ride?> GetUnfinishedRideAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        var unfinished = await _connection.Table<Ride>()
            .Where(r => r.EndedAt == null)
            .OrderByDescending(r => r.StartedAt)
            .ToListAsync().ConfigureAwait(false);

        Ride? keep = null;
        foreach (var ride in unfinished)
        {
            var count = await _connection.Table<TrackPoint>()
                .Where(p => p.RideId == ride.Id)
                .CountAsync().ConfigureAwait(false);
            if (count == 0)
            {
                await _connection.DeleteAsync(ride).ConfigureAwait(false);
            }
            else if (keep is null)
            {
                keep = ride;
            }
            else
            {
                // Older interrupted ride behind the newest one: finalize it silently.
                await FinalizeFromPointsAsync(ride).ConfigureAwait(false);
            }
        }
        return keep;
    }

    /// <summary>Finalize an interrupted ride from its stored points (dismissed recovery).</summary>
    public async Task FinalizeFromPointsAsync(Ride ride)
    {
        var points = await GetTrackPointsAsync(ride.Id).ConfigureAwait(false);
        if (points.Count == 0)
        {
            await _connection.DeleteAsync(ride).ConfigureAwait(false);
            return;
        }

        var acc = new StatsAccumulator();
        foreach (var p in points)
            acc.Add(new GpsFix(p.Timestamp, p.Latitude, p.Longitude,
                p.SpeedMps, p.AccuracyMeters, p.AltitudeMeters));

        await FinalizeRideAsync(ride.Id, acc.DistanceMeters, acc.MovingSeconds,
            acc.AvgSpeedMps, acc.MaxSpeedMps, points[^1].Timestamp,
            wasRecovered: true).ConfigureAwait(false);
    }
}
