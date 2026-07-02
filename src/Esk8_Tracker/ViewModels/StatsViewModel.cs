using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Esk8_Tracker.Core;
using Esk8_Tracker.Core.Data;

namespace Esk8_Tracker.ViewModels;

public record MonthlyDisplay(string Month, string Distance, int Rides);

public record BoardDisplay(string Board, string Distance, int Rides);

public partial class StatsViewModel(Esk8Database db) : ObservableObject
{
    [ObservableProperty]
    private string _totalDistance = "0.00";

    [ObservableProperty]
    private string _totalRides = "0";

    [ObservableProperty]
    private string _totalTime = "0:00:00";

    [ObservableProperty]
    private string _topSpeed = "0.0";

    [ObservableProperty]
    private string _longestRide = "0.00";

    public ObservableCollection<MonthlyDisplay> Monthly { get; } = new();
    public ObservableCollection<BoardDisplay> PerBoard { get; } = new();

    [RelayCommand]
    private async Task LoadAsync()
    {
        var stats = await db.GetDashboardStatsAsync();

        TotalDistance = Format.DistanceKm(stats.TotalDistanceMeters);
        TotalRides = stats.RideCount.ToString();
        TotalTime = Format.Duration(stats.TotalMovingSeconds);
        TopSpeed = Format.SpeedKmh(stats.TopSpeedMps);
        LongestRide = Format.DistanceKm(stats.LongestRideMeters);

        Monthly.Clear();
        foreach (var m in stats.Monthly)
            Monthly.Add(new MonthlyDisplay(
                new DateTime(m.Year, m.Month, 1).ToString("MMMM yyyy"),
                Format.DistanceKm(m.DistanceMeters), m.RideCount));

        PerBoard.Clear();
        foreach (var b in stats.PerBoard)
            PerBoard.Add(new BoardDisplay(b.BoardName,
                Format.DistanceKm(b.DistanceMeters), b.RideCount));
    }
}
