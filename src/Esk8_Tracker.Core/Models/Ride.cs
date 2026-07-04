using SQLite;

namespace Esk8_Tracker.Core.Models;

public static class RideTags
{
    public static readonly string[] Suggested = { "Commute", "Session", "Group", "Night", "Errand" };
}

public class Ride
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int BoardId { get; set; }

    public DateTime StartedAt { get; set; }

    /// <summary>Null while the ride is in progress; set when finalized.</summary>
    public DateTime? EndedAt { get; set; }

    public double DistanceMeters { get; set; }

    public double MovingSeconds { get; set; }

    public double AvgSpeedMps { get; set; }

    public double MaxSpeedMps { get; set; }

    public bool WasRecovered { get; set; }

    /// <summary>Rider-editable ride name; empty means "use the generated default".</summary>
    public string Name { get; set; } = "";

    /// <summary>Comma-separated tags (e.g. "Commute,Night").</summary>
    public string Tags { get; set; } = "";

    public string Notes { get; set; } = "";

    /// <summary>Total ascent in meters, computed from smoothed GPS altitude at finalize.</summary>
    public double ElevationGainMeters { get; set; }

    /// <summary>First accepted fix, denormalized at finalize (area/exploration stats).</summary>
    public double? StartLatitude { get; set; }

    public double? StartLongitude { get; set; }

    /// <summary>JSON array of app-local photo file paths attached on the summary screen.</summary>
    public string PhotosJson { get; set; } = "";

    [Ignore]
    public IReadOnlyList<string> PhotoPaths
    {
        get
        {
            if (PhotosJson.Length == 0) return Array.Empty<string>();
            try { return System.Text.Json.JsonSerializer.Deserialize<string[]>(PhotosJson) ?? Array.Empty<string>(); }
            catch { return Array.Empty<string>(); }
        }
    }

    [Ignore]
    public double TotalSeconds => EndedAt is { } end ? Math.Max(0, (end - StartedAt).TotalSeconds) : 0;

    [Ignore]
    public IReadOnlyList<string> TagList =>
        Tags.Length == 0 ? Array.Empty<string>() : Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
