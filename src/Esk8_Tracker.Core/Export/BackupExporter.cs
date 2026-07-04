using System.Text.Json;
using System.Text.Json.Serialization;
using Esk8_Tracker.Core.Models;

namespace Esk8_Tracker.Core.Export;

/// <summary>
/// Full local backup as one JSON document: boards + rides + every track point.
/// This is the "your data is yours" story — everything needed to rebuild history.
/// </summary>
public static class BackupExporter
{
    public const int FormatVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public record Backup(
        int Version,
        DateTime ExportedAtUtc,
        IReadOnlyList<Board> Boards,
        IReadOnlyList<Ride> Rides,
        IReadOnlyList<TrackPoint> Points);

    public static string ToJson(IReadOnlyList<Board> boards, IReadOnlyList<Ride> rides,
        IReadOnlyList<TrackPoint> points, DateTime nowUtc) =>
        JsonSerializer.Serialize(new Backup(FormatVersion, nowUtc, boards, rides, points), Options);

    public static string FileName(DateTime nowLocal) => $"esk8-backup-{nowLocal:yyyy-MM-dd}.json";
}
