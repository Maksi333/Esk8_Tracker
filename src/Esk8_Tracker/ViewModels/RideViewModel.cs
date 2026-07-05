using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Esk8_Tracker.Controls;
using Esk8_Tracker.Core;
using Esk8_Tracker.Core.Data;
using Esk8_Tracker.Core.Models;
using Esk8_Tracker.Services;
using Esk8_Tracker.Views;

namespace Esk8_Tracker.ViewModels;

public enum RideUiState { Idle, Active, Paused, Summary }

/// <summary>
/// Owns the live ride and drives the Ride tab's state machine
/// (idle → active ⇄ paused → summary → idle). Registered as a singleton so state
/// survives tab switches. Formatting flows through <see cref="Units"/> + settings.
/// </summary>
public partial class RideViewModel : ObservableObject
{
    private readonly Esk8Database _db;
    private readonly RideRecorder _recorder;
    private readonly IRideRecordingController _controller;
    private readonly AppSettings _settings;
    private readonly AutoPauseMonitor _autoPause;
    private readonly IServiceProvider _services;
    private IDispatcherTimer? _timer;

    // Live route projection origin (first fix)
    private double _lat0, _lon0;
    private bool _haveOrigin;
    private readonly List<RideSample> _liveSamples = new();

    // Summary working state
    private int _summaryRideId;
    private Board? _summaryBoard;
    private RideRecords? _priorRecords;

    public RideViewModel(Esk8Database db, RideRecorder recorder, IRideRecordingController controller,
        AppSettings settings, AutoPauseMonitor autoPause, IServiceProvider services)
    {
        _db = db;
        _recorder = recorder;
        _controller = controller;
        _settings = settings;
        _autoPause = autoPause;
        _services = services;

        _recorder.StatsUpdated += OnStatsUpdated;
        _recorder.StateChanged += OnRecorderStateChanged;
        _recorder.FixAccepted += OnFixAccepted;
        _recorder.RawFix += fix => _autoPause.OnFix(fix, _recorder.State);
        _autoPause.AutoPauseTriggered += () => MainThread.BeginInvokeOnMainThread(() =>
        {
            PauseReason = "auto — stopped moving";
            _recorder.Pause();
        });
        _autoPause.AutoResumeTriggered += () => MainThread.BeginInvokeOnMainThread(() => _recorder.Resume());
        _settings.Changed += OnSettingsChanged;
    }

    // ── observable state ──
    [ObservableProperty] private RideUiState _uiState = RideUiState.Idle;
    [ObservableProperty] private string _speedDisplay = "0";
    [ObservableProperty] private Color _speedColor = Colors.White;
    [ObservableProperty] private double _currentSpeedMps;
    [ObservableProperty] private double _topSpeedMps = 11.11;
    [ObservableProperty] private string _distanceDisplay = "0.0";
    [ObservableProperty] private string _distanceUnit = "km";
    [ObservableProperty] private string _speedUnit = "km/h";
    [ObservableProperty] private string _elapsedDisplay = "0:00";
    [ObservableProperty] private string _pauseReason = "manual";
    [ObservableProperty] private bool _glanceMode;
    [ObservableProperty] private bool _mapPeek;
    [ObservableProperty] private bool _gpsLost;
    [ObservableProperty] private bool _voiceCuesOn;

    // Tiles
    [ObservableProperty] private string _tileTopValue = "0";
    [ObservableProperty] private Color _tileTopColor = Colors.White;
    [ObservableProperty] private string _tileAvgValue = "0";
    [ObservableProperty] private string _tileFourthLabel = "TOTAL";
    [ObservableProperty] private string _tileFourthValue = "0:00";
    [ObservableProperty] private string _tileFourthUnit = "";
    [ObservableProperty] private string _tileFourthIcon = MaterialIcons.Schedule;
    [ObservableProperty] private Color _tileFourthIconColor = Colors.White;

    // Idle
    [ObservableProperty] private string _gpsLabel = "GPS acquiring";
    [ObservableProperty] private string _gpsIcon = MaterialIcons.LocationSearching;
    [ObservableProperty] private Color _gpsColor = Colors.Gray;
    [ObservableProperty] private string _activeBoardName = "Add a board";
    [ObservableProperty] private Color _activeBoardColor = Colors.Gray;
    [ObservableProperty] private bool _hasActiveBoard;
    [ObservableProperty] private string _lifetimeSummary = "0 km · 0 rides";
    [ObservableProperty] private bool _showRecoveryBanner;
    [ObservableProperty] private string _recoverySubtitle = "";
    [ObservableProperty] private bool _showPermissionBanner;
    [ObservableProperty] private bool _hasLastRide;
    [ObservableProperty] private string _lastRideLabel = "";
    [ObservableProperty] private string _lastRideName = "";
    [ObservableProperty] private string _lastRideStat = "";
    [ObservableProperty] private IReadOnlyList<RideSample>? _lastRideSamples;
    [ObservableProperty] private double _lastRideTop = 11.11;

    // Summary
    [ObservableProperty] private IReadOnlyList<RideSample>? _summarySamples;
    [ObservableProperty] private bool _summaryHasRoute;
    [ObservableProperty] private string _rideName = "";
    [ObservableProperty] private bool _summaryHasBoard;
    [ObservableProperty] private string _summaryBoardName = "";
    [ObservableProperty] private Color _summaryBoardColor = Colors.Gray;
    [ObservableProperty] private string _summaryDistance = "0.0";
    [ObservableProperty] private string _summaryMoving = "0:00";
    [ObservableProperty] private string _summaryAvg = "0";
    [ObservableProperty] private string _summaryTop = "0";
    [ObservableProperty] private Color _summaryTopColor = Colors.White;
    [ObservableProperty] private bool _showPrCallout;
    [ObservableProperty] private string _prText = "";
    [ObservableProperty] private bool _showShortRideBanner;
    [ObservableProperty] private string _rideNotes = "";
    [ObservableProperty] private ObservableCollection<TagChip> _summaryTags = new();

    public IReadOnlyList<RideSample> LiveSamplesSnapshot => _liveSamples.ToList();

    // ── derived ──
    public bool IsIdle => UiState == RideUiState.Idle;
    public bool IsActive => UiState == RideUiState.Active;
    public bool IsPaused => UiState == RideUiState.Paused;
    public bool IsSummary => UiState == RideUiState.Summary;
    public bool IsSupported => _controller.IsSupported;

    partial void OnUiStateChanged(RideUiState value)
    {
        OnPropertyChanged(nameof(IsIdle));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(IsSummary));
        RootPage.Current?.SetNavVisible(value == RideUiState.Idle);
    }

    private void OnSettingsChanged()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _autoPause.Sensitivity = _settings.AutoPause;
            VoiceCuesOn = _settings.VoiceCues;
            if (IsIdle) _ = RefreshIdleAsync();
            else RecomputeLiveDisplays();
        });
    }

    // ── lifecycle ──
    // async void: an exception here would otherwise be unhandled and crash the app,
    // so failures are logged and swallowed — a data-load hiccup must not kill the UI.
    public async void OnShown()
    {
        try
        {
            VoiceCuesOn = _settings.VoiceCues;
            _autoPause.Sensitivity = _settings.AutoPause;
            if (IsIdle)
                await RefreshIdleAsync();
        }
        catch (Exception ex)
        {
            Services.CrashLog.Write("RideViewModel.OnShown", ex);
        }
    }

    private async Task RefreshIdleAsync()
    {
        DistanceUnit = Units.DistanceUnit(_settings.Units);
        SpeedUnit = Units.SpeedUnit(_settings.Units);

        var boards = await _db.GetActiveBoardsAsync();
        var active = boards.FirstOrDefault(b => b.Id == _settings.ActiveBoardId) ?? boards.FirstOrDefault();
        if (active is not null && _settings.ActiveBoardId != active.Id)
            _settings.ActiveBoardId = active.Id;
        HasActiveBoard = active is not null;
        ActiveBoardName = active?.Name ?? "Add a board";
        ActiveBoardColor = ParseColor(active?.ColorHex, Colors.Gray);
        TopSpeedMps = active?.TopSpeedMps ?? 11.11;

        var rides = await _db.GetCompletedRidesAsync();
        var totals = LifetimeStats.Totals(rides);
        LifetimeSummary = $"{Units.DistanceWhole(totals.DistanceMeters, _settings.Units)} " +
                          $"{Units.DistanceUnit(_settings.Units)} · {totals.RideCount} rides";

        if (rides.Count > 0)
        {
            var last = rides[0];
            HasLastRide = true;
            LastRideLabel = $"LAST RIDE · {RelativeDay(last.StartedAt)}";
            LastRideName = DisplayName(last);
            LastRideStat = $"{Units.Distance(last.DistanceMeters, _settings.Units)} {DistanceUnit} · " +
                           $"{Units.Duration(last.MovingSeconds)} · {Units.Speed(last.AvgSpeedMps, _settings.Units)} {SpeedUnit}";
            var boardsById = await _db.GetBoardsByIdAsync();
            LastRideTop = boardsById.TryGetValue(last.BoardId, out var lb) ? lb.TopSpeedMps : Math.Max(last.MaxSpeedMps, 1);
            var pts = await _db.GetTrackPointsAsync(last.Id);
            LastRideSamples = RideAnalysis.BuildSamples(pts);
        }
        else
        {
            HasLastRide = false;
        }

        var unfinished = await _db.GetUnfinishedRideAsync();
        if (unfinished is not null)
        {
            var pts = await _db.GetTrackPointsAsync(unfinished.Id);
            var samples = RideAnalysis.BuildSamples(pts);
            var dist = samples.Count > 0 ? samples[^1].DistanceMeters : 0;
            ShowRecoveryBanner = true;
            RecoverySubtitle = $"Recovered · {Units.Distance(dist, _settings.Units)} {DistanceUnit} so far";
            _recoveredRide = unfinished;
        }
        else
        {
            ShowRecoveryBanner = false;
            _recoveredRide = null;
        }

        ShowPermissionBanner = !PlatformPermissions.IsIgnoringBatteryOptimizations
                               || !await HasLocationAsync();

        _ = ProbeGpsAsync();
    }

    private Ride? _recoveredRide;

    private async Task<bool> HasLocationAsync()
    {
        try { return await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>() == PermissionStatus.Granted; }
        catch { return false; }
    }

    private async Task ProbeGpsAsync()
    {
        if (!IsIdle) return;
        try
        {
            if (!await HasLocationAsync())
            {
                GpsLabel = "GPS off"; GpsIcon = MaterialIcons.GpsOff; GpsColor = (Color)App.Res("Warn");
                return;
            }
            var loc = await Geolocation.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(5)));
            if (loc is not null && (loc.Accuracy ?? 99) < 25)
            {
                GpsLabel = "GPS strong"; GpsIcon = MaterialIcons.GpsFixed; GpsColor = (Color)App.Res("Go");
            }
            else
            {
                GpsLabel = "GPS acquiring"; GpsIcon = MaterialIcons.LocationSearching; GpsColor = (Color)App.Res("TextTertiary");
            }
        }
        catch
        {
            GpsLabel = "GPS acquiring"; GpsIcon = MaterialIcons.LocationSearching; GpsColor = (Color)App.Res("TextTertiary");
        }
    }

    // ── commands ──
    [RelayCommand]
    private async Task StartRideAsync()
    {
        if (!IsSupported)
        {
            await Toast("Ride recording only works on Android", MaterialIcons.Warning);
            return;
        }
        if (!await PlatformPermissions.EnsureLocationAsync())
        {
            await Toast("Location permission is required to track rides", MaterialIcons.Warning);
            return;
        }
        await PlatformPermissions.RequestNotificationsAsync();

        var boardId = _settings.ActiveBoardId;
        try
        {
            _liveSamples.Clear();
            _haveOrigin = false;
            _priorRecords = LifetimeStats.Records(await _db.GetCompletedRidesAsync());
            _autoPause.Reset();
            _autoPause.Sensitivity = _settings.AutoPause;
            await _recorder.StartAsync(boardId, DateTime.UtcNow);
            _controller.StartLocationService(boardId);
            StartTimer();
            GpsLost = false;
            RecomputeLiveDisplays();
            UiState = RideUiState.Active;
        }
        catch (Exception ex)
        {
            await Toast("Couldn't start the ride", MaterialIcons.Warning);
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    [RelayCommand]
    private async Task ResumeRecoveredAsync()
    {
        if (_recoveredRide is null) { ShowRecoveryBanner = false; return; }
        try
        {
            var pts = await _db.GetTrackPointsAsync(_recoveredRide.Id);
            _priorRecords = LifetimeStats.Records(await _db.GetCompletedRidesAsync());
            _autoPause.Reset();
            _autoPause.Sensitivity = _settings.AutoPause;
            _liveSamples.Clear();
            _liveSamples.AddRange(RideAnalysis.BuildSamples(pts));
            _haveOrigin = pts.Count > 0;
            if (_haveOrigin) { _lat0 = pts[0].Latitude; _lon0 = pts[0].Longitude; }
            _recorder.ResumeRecovered(_recoveredRide, pts);
            _controller.StartLocationService(_recoveredRide.BoardId);
            StartTimer();
            ShowRecoveryBanner = false;
            RecomputeLiveDisplays();
            UiState = RideUiState.Active;
        }
        catch (Exception ex)
        {
            await Toast("Couldn't resume the ride", MaterialIcons.Warning);
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    [RelayCommand]
    private async Task DismissRecoveryAsync()
    {
        if (_recoveredRide is not null)
        {
            await _db.FinalizeFromPointsAsync(_recoveredRide);
            _recoveredRide = null;
            await Toast("Ride saved to History");
        }
        ShowRecoveryBanner = false;
    }

    [RelayCommand]
    private async Task FixPermissionAsync()
    {
        await PlatformPermissions.EnsureLocationAsync();
        PlatformPermissions.RequestIgnoreBatteryOptimizations();
        ShowPermissionBanner = !PlatformPermissions.IsIgnoringBatteryOptimizations || !await HasLocationAsync();
    }

    [RelayCommand]
    private void Pause()
    {
        MarkInteraction();
        _autoPause.Reset();
        PauseReason = "manual";
        _recorder.Pause();
    }

    [RelayCommand]
    private void Resume()
    {
        MarkInteraction();
        _autoPause.Reset();
        _recorder.Resume();
    }

    [RelayCommand]
    private void ToggleGlance()
    {
        MarkInteraction();
        GlanceMode = !GlanceMode;
    }

    [RelayCommand]
    private void ExitGlance()
    {
        MarkInteraction();
        GlanceMode = false;
    }

    [RelayCommand]
    private void ToggleMapPeek()
    {
        MarkInteraction();
        MapPeek = !MapPeek;
        if (MapPeek) OnPropertyChanged(nameof(LiveSamplesSnapshot));
    }

    /// <summary>Resets the auto-glance idle countdown; call on any live-screen interaction.</summary>
    [RelayCommand]
    public void MarkInteraction() => _lastInteractionUtc = DateTime.UtcNow;

    private bool _stoppingToSummary;

    public async Task StopFromHoldAsync()
    {
        var rideId = _recorder.ActiveRideId;
        try
        {
            _stoppingToSummary = true; // suppress the idle handler; we're going to Summary
            _controller.StopLocationService();
            await _recorder.StopAsync(DateTime.UtcNow);
            StopTimer();
            GlanceMode = false;
            MapPeek = false;
            if (rideId is int id)
                await BuildSummaryAsync(id);
        }
        catch (Exception ex)
        {
            await Toast("Couldn't stop the ride — try again", MaterialIcons.Warning);
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            _stoppingToSummary = false;
        }
    }

    private async Task BuildSummaryAsync(int rideId)
    {
        _summaryRideId = rideId;
        var ride = await _db.GetRideAsync(rideId);
        if (ride is null) { UiState = RideUiState.Idle; return; }

        var pts = await _db.GetTrackPointsAsync(rideId);
        var samples = RideAnalysis.BuildSamples(pts);
        SummarySamples = samples;
        SummaryHasRoute = samples.Count >= 2;

        var boards = await _db.GetBoardsByIdAsync();
        _summaryBoard = boards.TryGetValue(ride.BoardId, out var b) ? b : null;
        SummaryHasBoard = _summaryBoard is not null;
        SummaryBoardName = _summaryBoard?.Name ?? "";
        SummaryBoardColor = ParseColor(_summaryBoard?.ColorHex, Colors.Gray);
        var top = _summaryBoard?.TopSpeedMps ?? Math.Max(ride.MaxSpeedMps, 1);

        var u = _settings.Units;
        SummaryDistance = $"{Units.Distance(ride.DistanceMeters, u)} {Units.DistanceUnit(u)}";
        SummaryMoving = Units.Duration(ride.MovingSeconds);
        SummaryAvg = $"{Units.Speed(ride.AvgSpeedMps, u)} {Units.SpeedUnit(u)}";
        SummaryTop = Units.Speed(ride.MaxSpeedMps, u);
        SummaryTopColor = VelocityRamp.MauiColorAt(top <= 0 ? 0 : ride.MaxSpeedMps / top);

        var hour = ride.StartedAt.ToLocalTime().Hour;
        RideName = DefaultName(hour);
        RideNotes = "";
        ShowShortRideBanner = ride.DistanceMeters < 200 || ride.MovingSeconds < 60;

        // Tag chips (auto-select Night for night hours)
        SummaryTags = new ObservableCollection<TagChip>(
            RideTags.Suggested.Select(t => new TagChip(t) { IsSelected = t == "Night" && (hour >= 21 || hour < 6) }));

        // PR: only when a genuine prior top-speed record existed and was beaten
        if (_priorRecords is { TopSpeedMps: > 0 } prior && ride.MaxSpeedMps > prior.TopSpeedMps)
        {
            ShowPrCallout = true;
            var on = _summaryBoard is not null ? $" on {_summaryBoard.Name}" : "";
            PrText = $"{Units.Speed(ride.MaxSpeedMps, u)} {Units.SpeedUnit(u)} — a personal best{on}";
        }
        else
        {
            ShowPrCallout = false;
        }

        UiState = RideUiState.Summary;
    }

    [RelayCommand]
    private async Task SaveRideAsync()
    {
        try
        {
            var tags = string.Join(",", SummaryTags.Where(t => t.IsSelected).Select(t => t.Name));
            var name = string.IsNullOrWhiteSpace(RideName) ? "" : RideName.Trim();
            await _db.UpdateRideMetaAsync(_summaryRideId, name, tags, RideNotes?.Trim() ?? "");
            UiState = RideUiState.Idle;
            await RefreshIdleAsync();
            await Toast("Ride saved to History");
        }
        catch (Exception ex)
        {
            await Toast("Couldn't save the ride", MaterialIcons.Warning);
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    [RelayCommand]
    private async Task DiscardRideAsync()
    {
        var confirmed = await (RootPage.Current?.ShowConfirmAsync(
            MaterialIcons.DeleteForever, "Discard this ride?",
            "This ride hasn't been saved. Distance, route and stats will be permanently lost.",
            "Discard", "Keep") ?? Task.FromResult(false));
        if (!confirmed) return;
        await _db.DeleteRideAsync(_summaryRideId);
        UiState = RideUiState.Idle;
        await RefreshIdleAsync();
        await Toast("Ride discarded");
    }

    [RelayCommand]
    private async Task AddPhotoAsync()
    {
        try
        {
            var photo = await MediaPicker.Default.PickPhotoAsync();
            if (photo is null) return;
            var dir = Path.Combine(FileSystem.AppDataDirectory, "photos");
            Directory.CreateDirectory(dir);
            var ride = await _db.GetRideAsync(_summaryRideId);
            var existing = ride?.PhotoPaths.ToList() ?? new List<string>();
            var dest = Path.Combine(dir, $"{_summaryRideId}_{existing.Count}.jpg");
            using (var src = await photo.OpenReadAsync())
            using (var fs = File.Create(dest))
                await src.CopyToAsync(fs);
            existing.Add(dest);
            await _db.UpdateRidePhotosAsync(_summaryRideId,
                System.Text.Json.JsonSerializer.Serialize(existing));
            await Toast("Photo added");
        }
        catch (Exception ex)
        {
            await Toast("Couldn't add the photo", MaterialIcons.Warning);
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    [RelayCommand]
    private async Task ShareRideAsync()
    {
        try
        {
            var ride = await _db.GetRideAsync(_summaryRideId);
            if (ride is null || SummarySamples is null) return;
            var path = await ShareCardRenderer.RenderAsync(ride, SummarySamples, _summaryBoard, _settings.Units);
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Share ride",
                File = new ShareFile(path),
            });
        }
        catch (Exception ex)
        {
            await Toast("Couldn't create the share image", MaterialIcons.Warning);
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    [RelayCommand]
    private async Task OpenBoardSwitcherAsync()
    {
        var boards = await _db.GetActiveBoardsAsync();
        if (boards.Count == 0)
        {
            RootPage.Current?.SelectTab(AppTab.Garage);
            return;
        }
        RootPage.Current?.ShowSheet(BuildSwitcherSheet(boards));
    }

    [RelayCommand]
    private async Task OpenLastRideAsync()
    {
        var rides = await _db.GetCompletedRidesAsync();
        if (rides.Count == 0) return;
        await OpenDetail(rides[0].Id);
    }

    private async Task OpenDetail(int rideId)
    {
        var page = _services.GetRequiredService<RideDetailPage>();
        await page.LoadAsync(rideId);
        if (Application.Current?.Windows[0].Page is NavigationPage nav)
            await nav.PushAsync(page);
    }

    private View BuildSwitcherSheet(IReadOnlyList<Board> boards)
    {
        var stack = new VerticalStackLayout { Spacing = 8 };
        stack.Add(new Label
        {
            Text = "Active board", FontFamily = "SpaceGroteskSemiBold", FontSize = 17,
            TextColor = (Color)App.Res("TextPrimary"), Margin = new Thickness(0, 0, 0, 6),
        });
        foreach (var b in boards)
        {
            var active = b.Id == _settings.ActiveBoardId;
            var well = new Border
            {
                WidthRequest = 36, HeightRequest = 36, StrokeThickness = 0,
                BackgroundColor = ParseColor(b.ColorHex, Colors.Gray).WithAlpha(0.14f),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                Content = new Label
                {
                    Text = MaterialIcons.Skateboarding, FontFamily = "MaterialRounded", FontSize = 22,
                    TextColor = ParseColor(b.ColorHex, Colors.Gray),
                    HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center,
                },
            };
            var texts = new VerticalStackLayout
            {
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label { Text = b.Name, FontFamily = "InterSemiBold", FontSize = 14.5, TextColor = (Color)App.Res("TextPrimary") },
                    new Label { Text = $"{b.BatteryCells} · {Units.Speed(b.TopSpeedMps, _settings.Units)} {Units.SpeedUnit(_settings.Units)}".TrimStart(' ', '·'), FontFamily = "ChakraPetch", FontSize = 11.5, TextColor = (Color)App.Res("TextTertiary") },
                },
            };
            var row = new Grid { ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 12 };
            row.Add(well, 0);
            row.Add(texts, 1);
            if (active)
                row.Add(new Label { Text = MaterialIcons.CheckCircle, FontFamily = "MaterialRounded", FontSize = 22, TextColor = (Color)App.Res("Go"), VerticalOptions = LayoutOptions.Center }, 2);
            var card = new Border
            {
                BackgroundColor = active ? (Color)App.Res("SurfaceElevated") : (Color)App.Res("BgRaised"),
                Stroke = active ? ParseColor(b.ColorHex, Colors.Gray).WithAlpha(0.5f) : (Color)App.Res("BorderDefault"),
                StrokeThickness = 1,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
                Padding = new Thickness(15, 13),
                Content = row,
            };
            var tap = new TapGestureRecognizer();
            var id = b.Id; var name = b.Name;
            tap.Tapped += async (_, _) =>
            {
                _settings.ActiveBoardId = id;
                RootPage.Current?.CloseSheet();
                await RefreshIdleAsync();
                await Toast($"{name} set active");
            };
            card.GestureRecognizers.Add(tap);
            stack.Add(card);
        }
        return stack;
    }

    // ── recorder events ──
    private void OnStatsUpdated(RideLiveStats stats) =>
        MainThread.BeginInvokeOnMainThread(RecomputeLiveDisplays);

    private void OnRecorderStateChanged() => MainThread.BeginInvokeOnMainThread(() =>
    {
        switch (_recorder.State)
        {
            case RecorderState.Recording when IsPaused || IsActive:
                UiState = RideUiState.Active;
                RecomputeLiveDisplays();
                break;
            case RecorderState.Paused when IsActive || IsPaused:
                UiState = RideUiState.Paused;
                RecomputeLiveDisplays();
                break;
            case RecorderState.Idle when (IsActive || IsPaused) && !_stoppingToSummary:
                // The ride ended outside the app (notification "Stop & save").
                StopTimer();
                GlanceMode = false; MapPeek = false;
                UiState = RideUiState.Idle;
                _ = RefreshIdleAsync();
                break;
        }
    });

    private void OnFixAccepted(GpsFix fix) => MainThread.BeginInvokeOnMainThread(() =>
    {
        if (!_haveOrigin)
        {
            _lat0 = fix.Latitude; _lon0 = fix.Longitude; _haveOrigin = true;
        }
        var mPerLon = 111_320.0 * Math.Cos(_lat0 * Math.PI / 180.0);
        var stats = _recorder.CurrentStats;
        _liveSamples.Add(new RideSample(
            (fix.TimestampUtc - (_recorder.RideStartedUtc ?? fix.TimestampUtc)).TotalSeconds,
            stats.DistanceMeters, stats.CurrentSpeedMps,
            (fix.Longitude - _lon0) * mPerLon, -(fix.Latitude - _lat0) * 110_540.0,
            fix.AltitudeMeters));
        if (MapPeek) OnPropertyChanged(nameof(LiveSamplesSnapshot));
    });

    private void RecomputeLiveDisplays()
    {
        var u = _settings.Units;
        DistanceUnit = Units.DistanceUnit(u);
        SpeedUnit = Units.SpeedUnit(u);
        var stats = _recorder.CurrentStats;
        CurrentSpeedMps = stats.CurrentSpeedMps;
        SpeedDisplay = Units.Speed(stats.CurrentSpeedMps, u);
        var frac = TopSpeedMps <= 0 ? 0 : stats.CurrentSpeedMps / TopSpeedMps;
        SpeedColor = GpsLost ? (Color)App.Res("TextFaint") : VelocityRamp.MauiColorAt(frac);
        DistanceDisplay = Units.Distance(stats.DistanceMeters, u);
        ElapsedDisplay = Units.Duration(stats.MovingSeconds);
        TileTopValue = Units.Speed(stats.MaxSpeedMps, u);
        TileTopColor = VelocityRamp.MauiColorAt(TopSpeedMps <= 0 ? 0 : stats.MaxSpeedMps / TopSpeedMps);
        TileAvgValue = Units.Speed(stats.AvgSpeedMps, u);

        // Fourth tile: RANGE when the active board has battery specs, else TOTAL time.
        var remaining = RangeEstimator.RemainingMeters(_activeBatteryWh, stats.DistanceMeters);
        if (remaining is { } m)
        {
            TileFourthLabel = "RANGE";
            TileFourthValue = "~" + Units.Distance(m, u, 0);
            TileFourthUnit = Units.DistanceUnit(u);
            TileFourthIcon = MaterialIcons.Battery5Bar;
            TileFourthIconColor = (Color)App.Res("Go");
        }
        else
        {
            TileFourthLabel = "TOTAL";
            var total = _recorder.RideStartedUtc is { } s ? (DateTime.UtcNow - s).TotalSeconds : 0;
            TileFourthValue = Units.Duration(total);
            TileFourthUnit = "";
            TileFourthIcon = MaterialIcons.Schedule;
            TileFourthIconColor = (Color)App.Res("TextTertiary");
        }
    }

    private double? _activeBatteryWh;
    private DateTime _lastInteractionUtc;
    public const double AutoGlanceIdleSeconds = 30;

    private void StartTimer()
    {
        _activeBatteryWh = null;
        _lastInteractionUtc = DateTime.UtcNow;
        _ = LoadActiveBatteryAsync();
        if (_timer is not null) return;
        _timer = Application.Current!.Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) =>
        {
            ElapsedDisplay = Units.Duration(_recorder.CurrentStats.MovingSeconds);
            var lost = _recorder.IsSignalLost(DateTime.UtcNow);
            if (lost != GpsLost) { GpsLost = lost; RecomputeLiveDisplays(); }
            if (IsActive && TileFourthLabel == "TOTAL") RecomputeLiveDisplays();

            // Auto glance-mode: dim to speed-only after a spell without interaction.
            if (IsActive && !GlanceMode && _settings.AutoGlance &&
                (DateTime.UtcNow - _lastInteractionUtc).TotalSeconds >= AutoGlanceIdleSeconds)
            {
                GlanceMode = true;
            }
        };
        _timer.Start();
    }

    private async Task LoadActiveBatteryAsync()
    {
        var boards = await _db.GetBoardsByIdAsync();
        _activeBatteryWh = boards.TryGetValue(_settings.ActiveBoardId, out var b) ? b.BatteryWh : null;
        if (boards.TryGetValue(_settings.ActiveBoardId, out var bb)) TopSpeedMps = bb.TopSpeedMps;
        RecomputeLiveDisplays();
    }

    private void StopTimer()
    {
        _timer?.Stop();
        _timer = null;
    }

    // ── helpers ──
    private static Task Toast(string msg, string icon = "") =>
        RootPage.Current?.ShowToastAsync(msg, icon) ?? Task.CompletedTask;

    private static Color ParseColor(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        try { return Color.FromArgb(hex); } catch { return fallback; }
    }

    private static string DisplayName(Ride r) =>
        r.Name.Length > 0 ? r.Name : $"Ride {r.StartedAt.ToLocalTime():d MMM}";

    private static string DefaultName(int hour) => hour switch
    {
        >= 5 and < 11 => "Morning ride",
        >= 11 and < 17 => "Afternoon ride",
        >= 17 and < 22 => "Evening ride",
        _ => "Night ride",
    };

    private static string RelativeDay(DateTime utc)
    {
        var local = utc.ToLocalTime().Date;
        var today = DateTime.Now.Date;
        var days = (today - local).Days;
        return days switch
        {
            0 => "Today",
            1 => "Yesterday",
            < 7 => local.ToString("ddd"),
            _ => local.ToString("d MMM"),
        };
    }
}

public partial class TagChip : ObservableObject
{
    public TagChip(string name) => Name = name;
    public string Name { get; }
    [ObservableProperty] private bool _isSelected;
}
