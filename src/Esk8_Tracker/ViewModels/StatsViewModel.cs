using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Esk8_Tracker.Controls;
using Esk8_Tracker.Core;
using Esk8_Tracker.Core.Data;
using Esk8_Tracker.Core.Models;
using Esk8_Tracker.Services;
using Esk8_Tracker.Views;

namespace Esk8_Tracker.ViewModels;

public partial class StatsViewModel : ObservableObject
{
    private readonly Esk8Database _db;
    private readonly AppSettings _settings;

    private static readonly Dictionary<string, string> IconMap = new()
    {
        ["looks_one"] = MaterialIcons.LooksOne,
        ["straighten"] = MaterialIcons.Straighten,
        ["public"] = MaterialIcons.Public,
        ["bolt"] = MaterialIcons.Bolt,
        ["rocket_launch"] = MaterialIcons.RocketLaunch,
        ["local_fire_department"] = MaterialIcons.LocalFireDepartment,
        ["calendar_month"] = MaterialIcons.CalendarMonth,
        ["nightlight"] = MaterialIcons.Nightlight,
        ["terrain"] = MaterialIcons.Terrain,
        ["route"] = MaterialIcons.Route,
        ["explore"] = MaterialIcons.Explore,
        ["garage"] = MaterialIcons.Garage,
        ["wb_sunny"] = MaterialIcons.WbSunny,
        ["star"] = MaterialIcons.Star,
        ["timer"] = MaterialIcons.Timer,
        ["groups"] = MaterialIcons.Groups,
    };

    public StatsViewModel(Esk8Database db, AppSettings settings)
    {
        _db = db;
        _settings = settings;
        _settings.Changed += () => MainThread.BeginInvokeOnMainThread(() => _ = LoadAsync());
    }

    [ObservableProperty] private string _lifetimeDistance = "0 km";
    [ObservableProperty] private string _lifetimeRides = "0";
    [ObservableProperty] private string _lifetimeMoving = "0m";
    [ObservableProperty] private string _lifetimeClimb = "↑ 0 m";
    [ObservableProperty] private bool _hasRides;

    [ObservableProperty] private string _recordTop = "—";
    [ObservableProperty] private Color _recordTopColor = Colors.Gray;
    [ObservableProperty] private string _recordLongest = "—";
    [ObservableProperty] private Color _recordLongestColor = Colors.Gray;
    [ObservableProperty] private string _recordSession = "—";
    [ObservableProperty] private Color _recordSessionColor = Colors.Gray;
    [ObservableProperty] private string _recordStreak = "—";
    [ObservableProperty] private Color _recordStreakColor = Colors.Gray;

    [ObservableProperty] private IReadOnlyList<double>? _weekValues;
    [ObservableProperty] private string _weekHighlight = "";
    [ObservableProperty] private string _achievementCount = "0 / 16";

    public ObservableCollection<AchievementTile> Achievements { get; } = new();

    public async void OnShown() => await LoadAsync();

    private async Task LoadAsync()
    {
        var rides = await _db.GetCompletedRidesAsync();
        var u = _settings.Units;
        HasRides = rides.Count > 0;

        var totals = LifetimeStats.Totals(rides);
        LifetimeDistance = $"{Units.DistanceWhole(totals.DistanceMeters, u)} {Units.DistanceUnit(u)}";
        LifetimeRides = totals.RideCount.ToString();
        LifetimeMoving = FormatHours(totals.MovingSeconds);
        LifetimeClimb = $"↑ {Units.Elevation(totals.ClimbMeters, u)} {Units.ElevationUnit(u)}";

        var records = LifetimeStats.Records(rides);
        var faint = (Color)App.Res("TextFaint");
        if (HasRides)
        {
            RecordTop = $"{Units.Speed(records.TopSpeedMps, u)} {Units.SpeedUnit(u)}";
            RecordTopColor = (Color)App.Res("Danger");
            RecordLongest = $"{Units.Distance(records.LongestRideMeters, u, 0)} {Units.DistanceUnit(u)}";
            RecordLongestColor = (Color)App.Res("Accent");
            RecordSession = Units.DurationCompact(records.LongestSessionSeconds);
            RecordSessionColor = (Color)App.Res("Go");
            RecordStreak = $"{records.BestStreakDays} days";
            RecordStreakColor = (Color)App.Res("Warn");
        }
        else
        {
            RecordTop = RecordLongest = RecordSession = RecordStreak = "—";
            RecordTopColor = RecordLongestColor = RecordSessionColor = RecordStreakColor = faint;
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        var weeksMeters = LifetimeStats.WeeklyDistanceMeters(rides, today, 12);
        WeekValues = weeksMeters.Select(m => Units.DistanceValue(m, u)).ToArray();
        var lastWeek = WeekValues[^1];
        WeekHighlight = lastWeek > 0 ? Math.Round(lastWeek).ToString() : "";

        var statuses = Core.Achievements.Evaluate(rides, today);
        AchievementCount = $"{statuses.Count(s => s.State == AchievementState.Earned)} / {statuses.Count}";
        Achievements.Clear();
        foreach (var s in statuses)
            Achievements.Add(new AchievementTile(s, IconMap.GetValueOrDefault(s.Def.IconKey, MaterialIcons.Star)));
    }

    public void ShowBadge(AchievementTile tile)
    {
        var content = new VerticalStackLayout { Spacing = 0, HorizontalOptions = LayoutOptions.Center };
        var badge = new Border
        {
            WidthRequest = 88, HeightRequest = 88, StrokeThickness = 1,
            BackgroundColor = tile.Background, Stroke = tile.BorderColor,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 24 },
            HorizontalOptions = LayoutOptions.Center,
            Content = new Label
            {
                Text = tile.Icon, FontFamily = "MaterialRounded", FontSize = 46, TextColor = tile.IconColor,
                HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center,
            },
        };
        content.Add(badge);
        content.Add(new Label { Text = tile.Title, FontFamily = "SpaceGroteskSemiBold", FontSize = 20, TextColor = (Color)App.Res("TextPrimary"), HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(0, 16, 0, 6) });
        content.Add(new Label { Text = tile.Criteria, FontSize = 13.5, TextColor = (Color)App.Res("TextTertiary"), LineHeight = 1.5, HorizontalTextAlignment = TextAlignment.Center, MaximumWidthRequest = 290, Margin = new Thickness(0, 0, 0, 16) });

        View status = tile.Status.State switch
        {
            AchievementState.Earned => StatusRow(MaterialIcons.Verified, $"Earned {tile.Status.EarnedAtUtc?.ToLocalTime():d MMM yyyy}", (Color)App.Res("Go")),
            AchievementState.InProgress => InProgressRow(tile),
            _ => StatusRow(MaterialIcons.Lock, "Locked — keep riding", (Color)App.Res("TextTertiary")),
        };
        content.Add(new Border
        {
            BackgroundColor = (Color)App.Res("BgRaised"), Stroke = (Color)App.Res("BorderDefault"), StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 13 },
            Padding = 14, Content = status,
        });
        RootPage.Current?.ShowSheet(content);
    }

    private static View StatusRow(string icon, string text, Color color) => new HorizontalStackLayout
    {
        HorizontalOptions = LayoutOptions.Center, Spacing = 8,
        Children =
        {
            new Label { Text = icon, FontFamily = "MaterialRounded", FontSize = 19, TextColor = color, VerticalOptions = LayoutOptions.Center },
            new Label { Text = text, FontFamily = "InterSemiBold", FontSize = 13, TextColor = color, VerticalOptions = LayoutOptions.Center },
        },
    };

    private View InProgressRow(AchievementTile tile)
    {
        var ring = new ProgressRingView { WidthRequest = 60, HeightRequest = 60, Progress = tile.Status.Progress * 100, RingColor = (Color)App.Res("Accent") };
        var texts = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = $"{tile.Status.Progress * 100:0}%", FontFamily = "ChakraPetch", FontSize = 20, TextColor = (Color)App.Res("Accent") },
                new Label { Text = "In progress", FontSize = 11.5, TextColor = (Color)App.Res("TextTertiary") },
            },
        };
        return new HorizontalStackLayout { Spacing = 14, Children = { ring, texts } };
    }

    private static string FormatHours(double seconds)
    {
        var t = TimeSpan.FromSeconds(seconds);
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}h" : $"{t.Minutes}m";
    }
}

public class AchievementTile
{
    public AchievementTile(AchievementStatus status, string icon)
    {
        Status = status;
        Icon = icon;
        Title = status.Def.Title;
        Short = status.Def.Short;
        Criteria = status.Def.Criteria;

        switch (status.State)
        {
            case AchievementState.Earned:
                Background = Color.FromArgb("#1A4C8DFF");
                BorderColor = Color.FromArgb("#594C8DFF");
                IconColor = (Color)App.Res("Accent");
                Opacity = 1;
                break;
            case AchievementState.InProgress:
                Background = (Color)App.Res("SurfaceCard2");
                BorderColor = (Color)App.Res("BorderHairline");
                IconColor = (Color)App.Res("TextTertiary");
                Opacity = 1;
                break;
            default:
                Background = (Color)App.Res("SurfaceCard2");
                BorderColor = (Color)App.Res("BorderHairline");
                IconColor = Color.FromArgb("#3A4550");
                Opacity = 0.5;
                break;
        }
        ShowProgress = status.State == AchievementState.InProgress;
        ProgressPercent = status.Progress * 100;
    }

    public AchievementStatus Status { get; }
    public string Icon { get; }
    public string Title { get; }
    public string Short { get; }
    public string Criteria { get; }
    public Color Background { get; }
    public Color BorderColor { get; }
    public Color IconColor { get; }
    public double Opacity { get; }
    public bool ShowProgress { get; }
    public double ProgressPercent { get; }
    public ColumnDefinitionCollection ProgressColumns => new()
    {
        new ColumnDefinition(new GridLength(ProgressPercent <= 0 ? 0.001 : ProgressPercent, GridUnitType.Star)),
        new ColumnDefinition(new GridLength(ProgressPercent >= 100 ? 0.001 : 100 - ProgressPercent, GridUnitType.Star)),
    };
}
