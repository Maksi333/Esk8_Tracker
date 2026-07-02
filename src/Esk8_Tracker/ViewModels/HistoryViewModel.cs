using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Esk8_Tracker.Core;
using Esk8_Tracker.Core.Data;

namespace Esk8_Tracker.ViewModels;

public record RideListItem(
    int RideId,
    string Title,
    string BoardName,
    string Distance,
    string Duration,
    string AvgSpeed,
    string MaxSpeed,
    bool WasRecovered);

public partial class HistoryViewModel(Esk8Database db) : ObservableObject
{
    public ObservableCollection<RideListItem> Rides { get; } = new();

    [RelayCommand]
    private async Task LoadAsync()
    {
        var rides = await db.GetCompletedRidesAsync();
        var names = await db.GetBoardNamesAsync();

        Rides.Clear();
        foreach (var ride in rides)
        {
            Rides.Add(new RideListItem(
                ride.Id,
                ride.StartedAt.ToLocalTime().ToString("ddd d MMM yyyy HH:mm"),
                names.TryGetValue(ride.BoardId, out var name) ? name : "(unknown board)",
                Format.DistanceKm(ride.DistanceMeters),
                Format.Duration(ride.MovingSeconds),
                Format.SpeedKmh(ride.AvgSpeedMps),
                Format.SpeedKmh(ride.MaxSpeedMps),
                ride.WasRecovered));
        }
    }

    [RelayCommand]
    private async Task OpenRideAsync(RideListItem item) =>
        await Shell.Current.GoToAsync($"{nameof(Views.RideDetailPage)}?rideId={item.RideId}");
}
