using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Esk8_Tracker.Core;
using Esk8_Tracker.Core.Data;
using Esk8_Tracker.Core.Models;
using Esk8_Tracker.Services;
using Esk8_Tracker.Views;

namespace Esk8_Tracker.ViewModels;

public enum HistoryPeriod { Week, Month, Year, All }

public partial class HistoryViewModel : ObservableObject
{
    private readonly Esk8Database _db;
    private readonly AppSettings _settings;
    private readonly IServiceProvider _services;
    private List<Ride> _allRides = new();
    private Dictionary<int, Board> _boards = new();

    public HistoryViewModel(Esk8Database db, AppSettings settings, IServiceProvider services)
    {
        _db = db;
        _settings = settings;
        _services = services;
        _settings.Changed += () => MainThread.BeginInvokeOnMainThread(() => _ = LoadAsync());
    }

    public ObservableCollection<RideListItem> Rides { get; } = new();

    [ObservableProperty] private HistoryPeriod _period = HistoryPeriod.Month;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private bool _searchVisible;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private int _boardFilterId; // 0 = all
    [ObservableProperty] private string _aggregateLabel = "THIS MONTH";
    [ObservableProperty] private string _aggregateValue = "0 km · 0 rides";
    [ObservableProperty] private IReadOnlyList<RideSample>? _aggregateSpark;
    [ObservableProperty] private double _aggregateSparkTop = 11.11;

    public bool WeekActive => Period == HistoryPeriod.Week;
    public bool MonthActive => Period == HistoryPeriod.Month;
    public bool YearActive => Period == HistoryPeriod.Year;
    public bool AllActive => Period == HistoryPeriod.All;

    public async void OnShown()
    {
        try { await LoadAsync(); }
        catch (Exception ex) { Services.CrashLog.Write("HistoryViewModel.OnShown", ex); }
    }

    partial void OnPeriodChanged(HistoryPeriod value)
    {
        OnPropertyChanged(nameof(WeekActive));
        OnPropertyChanged(nameof(MonthActive));
        OnPropertyChanged(nameof(YearActive));
        OnPropertyChanged(nameof(AllActive));
        ApplyFilters();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();

    private async Task LoadAsync()
    {
        _allRides = await _db.GetCompletedRidesAsync();
        _boards = await _db.GetBoardsByIdAsync();
        ApplyFilters();
    }

    [RelayCommand] private void SelectWeek() => Period = HistoryPeriod.Week;
    [RelayCommand] private void SelectMonth() => Period = HistoryPeriod.Month;
    [RelayCommand] private void SelectYear() => Period = HistoryPeriod.Year;
    [RelayCommand] private void SelectAll() => Period = HistoryPeriod.All;

    [RelayCommand]
    private void ToggleSearch()
    {
        SearchVisible = !SearchVisible;
        if (!SearchVisible) SearchText = "";
    }

    [RelayCommand]
    private void OpenFilter()
    {
        var stack = new VerticalStackLayout { Spacing = 8 };
        stack.Add(new Label { Text = "Filter by board", FontFamily = "SpaceGroteskSemiBold", FontSize = 17, TextColor = (Color)App.Res("TextPrimary"), Margin = new Thickness(0, 0, 0, 6) });
        stack.Add(FilterRow(0, "All boards", null));
        foreach (var b in _boards.Values.Where(b => !b.IsArchived || _allRides.Any(r => r.BoardId == b.Id)))
            stack.Add(FilterRow(b.Id, b.Name, b.ColorHex));
        RootPage.Current?.ShowSheet(stack);
    }

    private View FilterRow(int id, string name, string? colorHex)
    {
        var row = new HorizontalStackLayout { Spacing = 12, Padding = new Thickness(4, 10) };
        if (colorHex is not null)
            row.Add(new BoxView { WidthRequest = 12, HeightRequest = 12, CornerRadius = 3, Color = SafeColor(colorHex), VerticalOptions = LayoutOptions.Center });
        row.Add(new Label { Text = name, FontFamily = "InterMedium", FontSize = 15, TextColor = (Color)App.Res("TextPrimary"), VerticalOptions = LayoutOptions.Center });
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => { BoardFilterId = id; RootPage.Current?.CloseSheet(); ApplyFilters(); };
        row.GestureRecognizers.Add(tap);
        return row;
    }

    private void ApplyFilters()
    {
        var now = DateTime.Now;
        IEnumerable<Ride> filtered = _allRides;

        filtered = Period switch
        {
            HistoryPeriod.Week => filtered.Where(r => r.StartedAt.ToLocalTime() >= StartOfWeek(now)),
            HistoryPeriod.Month => filtered.Where(r => { var l = r.StartedAt.ToLocalTime(); return l.Year == now.Year && l.Month == now.Month; }),
            HistoryPeriod.Year => filtered.Where(r => r.StartedAt.ToLocalTime().Year == now.Year),
            _ => filtered,
        };

        if (BoardFilterId != 0)
            filtered = filtered.Where(r => r.BoardId == BoardFilterId);
        if (!string.IsNullOrWhiteSpace(SearchText))
            filtered = filtered.Where(r => DisplayName(r).Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        var list = filtered.ToList();

        AggregateLabel = Period switch
        {
            HistoryPeriod.Week => "THIS WEEK",
            HistoryPeriod.Month => "THIS MONTH",
            HistoryPeriod.Year => "THIS YEAR",
            _ => "ALL TIME",
        };
        var dist = list.Sum(r => r.DistanceMeters);
        AggregateValue = $"{Units.Distance(dist, _settings.Units)} {Units.DistanceUnit(_settings.Units)} · {list.Count} rides";

        Rides.Clear();
        foreach (var r in list)
        {
            var board = _boards.TryGetValue(r.BoardId, out var b) ? b : null;
            Rides.Add(new RideListItem(r, board, _settings.Units, _db));
        }
        IsEmpty = _allRides.Count == 0;

        // Aggregate sparkline from the most recent ride in view.
        if (list.Count > 0)
        {
            var recent = list[0];
            AggregateSparkTop = _boards.TryGetValue(recent.BoardId, out var rb) ? rb.TopSpeedMps : Math.Max(recent.MaxSpeedMps, 1);
            _ = LoadAggregateSpark(recent.Id);
        }
        else
        {
            AggregateSpark = null;
        }
    }

    private async Task LoadAggregateSpark(int rideId)
    {
        var pts = await _db.GetTrackPointsAsync(rideId);
        AggregateSpark = RideAnalysis.BuildSamples(pts);
    }

    [RelayCommand]
    private async Task OpenRideAsync(RideListItem? item)
    {
        if (item is null) return;
        var page = _services.GetRequiredService<RideDetailPage>();
        await page.LoadAsync(item.RideId);
        if (Application.Current?.Windows[0].Page is NavigationPage nav)
            await nav.PushAsync(page);
    }

    [RelayCommand]
    private void GoRide() => RootPage.Current?.SelectTab(AppTab.Ride);

    private static DateTime StartOfWeek(DateTime now)
    {
        var day = now.Date;
        var diff = ((int)day.DayOfWeek + 6) % 7; // Monday start
        return day.AddDays(-diff);
    }

    private static string DisplayName(Ride r) =>
        r.Name.Length > 0 ? r.Name : $"Ride {r.StartedAt.ToLocalTime():d MMM}";

    private static Color SafeColor(string? hex)
    {
        try { return Color.FromArgb(string.IsNullOrWhiteSpace(hex) ? "#4C8DFF" : hex); }
        catch { return Color.FromArgb("#4C8DFF"); }
    }
}

public partial class RideListItem : ObservableObject
{
    private static readonly LinkedList<int> LruOrder = new();
    private static readonly Dictionary<int, IReadOnlyList<RideSample>> Cache = new();
    private const int CacheCap = 60;

    private readonly Esk8Database _db;

    public RideListItem(Ride ride, Board? board, UnitSystem units, Esk8Database db)
    {
        _db = db;
        RideId = ride.Id;
        Name = ride.Name.Length > 0 ? ride.Name : $"Ride {ride.StartedAt.ToLocalTime():d MMM}";
        Stat = $"{Units.Distance(ride.DistanceMeters, units)} {Units.DistanceUnit(units)} · " +
               $"{Units.Duration(ride.MovingSeconds)} · {Units.Speed(ride.AvgSpeedMps, units)} {Units.SpeedUnit(units)}";
        BoardName = board?.Name ?? "No board";
        BoardColor = SafeColor(board?.ColorHex);
        Date = RelativeDay(ride.StartedAt);
        WasRecovered = ride.WasRecovered;
        Top = board?.TopSpeedMps ?? Math.Max(ride.MaxSpeedMps, 1);
        _ = LoadSamplesAsync();
    }

    public int RideId { get; }
    public string Name { get; }
    public string Stat { get; }
    public string BoardName { get; }
    public Color BoardColor { get; }
    public string Date { get; }
    public bool WasRecovered { get; }
    public double Top { get; }

    [ObservableProperty] private IReadOnlyList<RideSample>? _samples;

    private async Task LoadSamplesAsync()
    {
        if (Cache.TryGetValue(RideId, out var cached)) { Samples = cached; return; }
        var pts = await _db.GetTrackPointsAsync(RideId);
        var samples = RideAnalysis.BuildSamples(pts);
        Cache[RideId] = samples;
        LruOrder.AddFirst(RideId);
        while (LruOrder.Count > CacheCap)
        {
            var evict = LruOrder.Last!.Value;
            LruOrder.RemoveLast();
            Cache.Remove(evict);
        }
        Samples = samples;
    }

    private static string RelativeDay(DateTime utc)
    {
        var local = utc.ToLocalTime().Date;
        var days = (DateTime.Now.Date - local).Days;
        return days switch
        {
            0 => "Today",
            1 => "Yesterday",
            < 7 => local.ToString("ddd"),
            _ => local.ToString("d MMM"),
        };
    }

    private static Color SafeColor(string? hex)
    {
        try { return Color.FromArgb(string.IsNullOrWhiteSpace(hex) ? "#8A97A2" : hex); }
        catch { return Color.FromArgb("#8A97A2"); }
    }
}
