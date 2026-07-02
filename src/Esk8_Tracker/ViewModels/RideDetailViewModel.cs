using CommunityToolkit.Mvvm.ComponentModel;
using Esk8_Tracker.Core;
using Esk8_Tracker.Core.Data;

namespace Esk8_Tracker.ViewModels;

[QueryProperty(nameof(RideId), "rideId")]
public partial class RideDetailViewModel(Esk8Database db) : ObservableObject
{
    public int RideId { get; set; }

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private string _boardName = "";

    [ObservableProperty]
    private string _distance = "";

    [ObservableProperty]
    private string _duration = "";

    [ObservableProperty]
    private string _avgSpeed = "";

    [ObservableProperty]
    private string _maxSpeed = "";

    [ObservableProperty]
    private bool _wasRecovered;

    public List<(double Lat, double Lon)> RoutePoints { get; } = new();

    public async Task LoadAsync()
    {
        var ride = await db.GetRideAsync(RideId);
        if (ride is null) return;

        var names = await db.GetBoardNamesAsync();
        Title = ride.StartedAt.ToLocalTime().ToString("ddd d MMM yyyy HH:mm");
        BoardName = names.TryGetValue(ride.BoardId, out var name) ? name : "(unknown board)";
        Distance = Format.DistanceKm(ride.DistanceMeters);
        Duration = Format.Duration(ride.MovingSeconds);
        AvgSpeed = Format.SpeedKmh(ride.AvgSpeedMps);
        MaxSpeed = Format.SpeedKmh(ride.MaxSpeedMps);
        WasRecovered = ride.WasRecovered;

        var points = await db.GetTrackPointsAsync(RideId);
        RoutePoints.Clear();
        RoutePoints.AddRange(points.Select(p => (p.Latitude, p.Longitude)));
    }
}
