using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Esk8_Tracker.Controls;
using Esk8_Tracker.Core;
using Esk8_Tracker.Core.Data;
using Esk8_Tracker.Core.Export;
using Esk8_Tracker.Core.Models;
using Esk8_Tracker.Services;
using Esk8_Tracker.Views;

namespace Esk8_Tracker.ViewModels;

public partial class RideDetailViewModel : ObservableObject
{
    private readonly Esk8Database _db;
    private readonly AppSettings _settings;
    private Ride? _ride;
    private Board? _board;
    private IReadOnlyList<TrackPoint> _points = Array.Empty<TrackPoint>();

    public RideDetailViewModel(Esk8Database db, AppSettings settings)
    {
        _db = db;
        _settings = settings;
    }

    public event Action? RequestClose;

    [ObservableProperty] private IReadOnlyList<RideSample>? _samples;
    [ObservableProperty] private double _topSpeedMps = 11.11;
    [ObservableProperty] private double _unitScale = 3.6;
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _meta = "";
    [ObservableProperty] private bool _hasRoute;
    [ObservableProperty] private bool _hasElevation;
    [ObservableProperty] private bool _hasSplits;
    [ObservableProperty] private bool _hasNotes;
    [ObservableProperty] private string _notes = "";
    [ObservableProperty] private double _scrub = 100;

    // scrub header
    [ObservableProperty] private string _scrubSpeed = "0";
    [ObservableProperty] private Color _scrubSpeedColor = Colors.White;
    [ObservableProperty] private string _scrubDistance = "0.0";
    [ObservableProperty] private string _scrubElevation = "—";
    [ObservableProperty] private string _scrubTime = "0:00";
    [ObservableProperty] private double _routeUptoFraction = 1;
    [ObservableProperty] private double _scrubGraphDistance = -1;
    [ObservableProperty] private double _scrubGraphSpeed;

    // stat grid
    [ObservableProperty] private string _statDistance = "0";
    [ObservableProperty] private string _statTop = "0";
    [ObservableProperty] private Color _statTopColor = Colors.White;
    [ObservableProperty] private string _statMoving = "0:00";
    [ObservableProperty] private string _statAvg = "0";
    [ObservableProperty] private string _statGain = "0";
    [ObservableProperty] private string _statTotal = "0:00";
    [ObservableProperty] private string _distanceUnit = "km";
    [ObservableProperty] private string _speedUnit = "km/h";
    [ObservableProperty] private string _elevationUnit = "m";
    [ObservableProperty] private string _splitUnitLabel = "Splits · per km";

    public ObservableCollection<SplitRow> Splits { get; } = new();
    public ObservableCollection<string> Photos { get; } = new();

    public async Task LoadAsync(int rideId)
    {
        _ride = await _db.GetRideAsync(rideId);
        if (_ride is null) return;
        var boards = await _db.GetBoardsByIdAsync();
        _board = boards.TryGetValue(_ride.BoardId, out var b) ? b : null;
        _points = await _db.GetTrackPointsAsync(rideId);
        Samples = RideAnalysis.BuildSamples(_points);
        TopSpeedMps = _board?.TopSpeedMps ?? Math.Max(_ride.MaxSpeedMps, 1);

        var u = _settings.Units;
        UnitScale = u == UnitSystem.Imperial ? 2.23694 : 3.6;
        DistanceUnit = Units.DistanceUnit(u);
        SpeedUnit = Units.SpeedUnit(u);
        ElevationUnit = Units.ElevationUnit(u);

        Name = _ride.Name.Length > 0 ? _ride.Name : $"Ride {_ride.StartedAt.ToLocalTime():d MMM}";
        var parts = new List<string> { _ride.StartedAt.ToLocalTime().ToString("ddd d MMM"), _ride.StartedAt.ToLocalTime().ToString("HH:mm") };
        if (_board is not null) parts.Add(_board.Name);
        Meta = string.Join(" · ", parts);

        HasRoute = Samples.Count >= 2;
        HasElevation = _points.Any(p => p.AltitudeMeters is not null);
        Notes = _ride.Notes;
        HasNotes = Notes.Length > 0;

        StatDistance = $"{Units.Distance(_ride.DistanceMeters, u)} {DistanceUnit}";
        StatTop = $"{Units.Speed(_ride.MaxSpeedMps, u)} {SpeedUnit}";
        StatTopColor = VelocityRamp.MauiColorAt(TopSpeedMps <= 0 ? 0 : _ride.MaxSpeedMps / TopSpeedMps);
        StatMoving = Units.Duration(_ride.MovingSeconds);
        StatAvg = $"{Units.Speed(_ride.AvgSpeedMps, u)} {SpeedUnit}";
        StatGain = $"↑{Units.Elevation(_ride.ElevationGainMeters, u)} {ElevationUnit}";
        StatTotal = Units.Duration(_ride.TotalSeconds);

        // splits
        Splits.Clear();
        SplitUnitLabel = $"Splits · per {DistanceUnit}";
        var splits = RideAnalysis.BuildSplits(Samples, Units.SplitMeters(u));
        foreach (var s in splits)
            Splits.Add(new SplitRow(s, TopSpeedMps, u));
        HasSplits = Splits.Count > 0;

        // photos
        Photos.Clear();
        foreach (var p in _ride.PhotoPaths)
            if (File.Exists(p)) Photos.Add(p);

        Scrub = 100;
        UpdateScrub();
    }

    partial void OnScrubChanged(double value) => UpdateScrub();

    private void UpdateScrub()
    {
        if (Samples is null || Samples.Count == 0) return;
        var frac = Math.Clamp(Scrub / 100.0, 0, 1);
        var idx = (int)Math.Round(frac * (Samples.Count - 1));
        idx = Math.Clamp(idx, 0, Samples.Count - 1);
        var s = Samples[idx];
        var u = _settings.Units;

        ScrubSpeed = Units.Speed(s.SpeedMps, u);
        ScrubSpeedColor = VelocityRamp.MauiColorAt(TopSpeedMps <= 0 ? 0 : s.SpeedMps / TopSpeedMps);
        ScrubDistance = Units.Distance(s.DistanceMeters, u);
        ScrubElevation = s.ElevationMeters is { } e ? $"{Units.Elevation(e, u)}{ElevationUnit}" : "—";
        ScrubTime = Units.Duration(s.TimeSeconds);
        RouteUptoFraction = frac;
        ScrubGraphDistance = s.DistanceMeters;
        ScrubGraphSpeed = s.SpeedMps;
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (_ride is null) return;
        try
        {
            var gpx = GpxExporter.ToGpx(_ride, _points);
            var fileName = GpxExporter.FileName(_ride);
            var path = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllTextAsync(path, gpx);
            await Share.Default.RequestAsync(new ShareFileRequest { Title = "Export ride", File = new ShareFile(path) });
            await Toast($"Exported {fileName}");
        }
        catch (Exception ex)
        {
            await Toast("Couldn't export the ride", MaterialIcons.Warning);
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    [RelayCommand]
    private async Task MoreAsync()
    {
        if (_ride is null) return;
        var page = Application.Current?.Windows[0].Page;
        var choice = await (page?.DisplayActionSheet(null, "Cancel", null, "Rename", "Delete ride") ?? Task.FromResult<string?>(null));
        if (choice == "Rename")
        {
            var name = await (page!.DisplayPromptAsync("Rename ride", null, "Save", "Cancel", initialValue: Name) ?? Task.FromResult<string?>(null));
            if (!string.IsNullOrWhiteSpace(name))
            {
                await _db.UpdateRideMetaAsync(_ride.Id, name.Trim(), _ride.Tags, _ride.Notes);
                Name = name.Trim();
                await Toast("Ride renamed");
            }
        }
        else if (choice == "Delete ride")
        {
            var confirmed = await (RootPage.Current?.ShowConfirmAsync(
                MaterialIcons.DeleteForever, "Delete this ride?",
                "The ride, its route and stats will be permanently removed.", "Delete", "Keep") ?? Task.FromResult(false));
            if (confirmed)
            {
                await _db.DeleteRideAsync(_ride.Id);
                await Toast("Ride deleted");
                RequestClose?.Invoke();
            }
        }
    }

    [RelayCommand]
    private void Back() => RequestClose?.Invoke();

    private static Task Toast(string msg, string icon = "") =>
        RootPage.Current?.ShowToastAsync(msg, icon) ?? Task.CompletedTask;
}

public class SplitRow
{
    public SplitRow(RideSplit split, double topMps, UnitSystem units)
    {
        Index = split.Index.ToString();
        FillFraction = topMps <= 0 ? 0 : Math.Clamp(split.AvgSpeedMps / topMps, 0, 1);
        FillColor = VelocityRamp.MauiColorAt(FillFraction);
        Avg = Units.Speed(split.AvgSpeedMps, units);
        Top = Units.Speed(split.TopSpeedMps, units);
        TopColor = VelocityRamp.MauiColorAt(topMps <= 0 ? 0 : split.TopSpeedMps / topMps);
    }

    public string Index { get; }
    public double FillFraction { get; }
    public Color FillColor { get; }
    public string Avg { get; }
    public string Top { get; }
    public Color TopColor { get; }

    /// <summary>Two star columns (fill / remainder) so the ramp bar sizes by speed fraction.</summary>
    public ColumnDefinitionCollection BarColumns => new()
    {
        new ColumnDefinition(new GridLength(FillFraction <= 0 ? 0.001 : FillFraction, GridUnitType.Star)),
        new ColumnDefinition(new GridLength(FillFraction >= 1 ? 0.001 : 1 - FillFraction, GridUnitType.Star)),
    };
}
