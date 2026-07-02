using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Esk8_Tracker.Core;
using Esk8_Tracker.Core.Data;
using Esk8_Tracker.Core.Models;
using Esk8_Tracker.Services;

namespace Esk8_Tracker.ViewModels;

public partial class RideViewModel : ObservableObject
{
    private const string LastBoardKey = "LastBoardId";

    private readonly Esk8Database _db;
    private readonly RideRecorder _recorder;
    private readonly IRideRecordingController _controller;
    private IDispatcherTimer? _timer;

    public RideViewModel(Esk8Database db, RideRecorder recorder, IRideRecordingController controller)
    {
        _db = db;
        _recorder = recorder;
        _controller = controller;
        _recorder.StatsUpdated += OnStatsUpdated;
        _recorder.StateChanged += OnStateChanged;
    }

    public ObservableCollection<Board> Boards { get; } = new();

    [ObservableProperty]
    private Board? _selectedBoard;

    [ObservableProperty]
    private string _speedDisplay = "0.0";

    [ObservableProperty]
    private string _distanceDisplay = "0.00";

    [ObservableProperty]
    private string _durationDisplay = "0:00:00";

    [ObservableProperty]
    private string _maxDisplay = "0.0";

    [ObservableProperty]
    private bool _isGpsLost;

    public bool IsSupported => _controller.IsSupported;
    public bool IsIdle => _recorder.State == RecorderState.Idle;
    public bool IsRecording => _recorder.State == RecorderState.Recording;
    public bool IsPaused => _recorder.State == RecorderState.Paused;
    public bool IsActive => !IsIdle;
    public string PauseResumeText => IsPaused ? "Resume" : "Pause";

    public async Task OnAppearingAsync()
    {
        var boards = await _db.GetActiveBoardsAsync();
        var lastBoardId = Preferences.Get(LastBoardKey, 0);
        Boards.Clear();
        foreach (var board in boards)
            Boards.Add(board);
        SelectedBoard = Boards.FirstOrDefault(b => b.Id == lastBoardId) ?? Boards.FirstOrDefault();

        RefreshStateProperties();
        if (!IsIdle)
        {
            OnStatsUpdated(_recorder.CurrentStats);
            StartTimer();
        }
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (!IsSupported)
        {
            await Shell.Current.DisplayAlertAsync("Not supported",
                "Ride recording only works in the Android app.", "OK");
            return;
        }
        if (SelectedBoard is null)
        {
            await Shell.Current.DisplayAlertAsync("No board",
                "Add a board on the Boards tab before recording a ride.", "OK");
            return;
        }

        var location = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        if (location != PermissionStatus.Granted)
        {
            var openSettings = await Shell.Current.DisplayAlertAsync("Location needed",
                "Esk8 Tracker needs location access to record your ride. " +
                "Grant it in system settings.", "Open settings", "Cancel");
            if (openSettings) AppInfo.ShowSettingsUI();
            return;
        }
        // Optional on Android 13+: only affects whether the recording
        // notification is visible in the drawer. Must NOT block recording.
        await Permissions.RequestAsync<Permissions.PostNotifications>();

        Preferences.Set(LastBoardKey, SelectedBoard.Id);
        await _recorder.StartAsync(SelectedBoard.Id, DateTime.UtcNow);
        _controller.StartLocationService(SelectedBoard.Id);
        StartTimer();
    }

    [RelayCommand]
    private void PauseResume()
    {
        if (IsPaused) _recorder.Resume();
        else if (IsRecording) _recorder.Pause();
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        var confirmed = await Shell.Current.DisplayAlertAsync("Stop ride",
            "Finish and save this ride?", "Stop", "Keep riding");
        if (!confirmed) return;

        _controller.StopLocationService();
        await _recorder.StopAsync(DateTime.UtcNow);
        StopTimer();
        SpeedDisplay = "0.0";
        IsGpsLost = false;
    }

    private void OnStatsUpdated(RideLiveStats stats) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SpeedDisplay = Format.SpeedKmh(stats.CurrentSpeedMps);
            DistanceDisplay = Format.DistanceKm(stats.DistanceMeters);
            DurationDisplay = Format.Duration(stats.MovingSeconds);
            MaxDisplay = Format.SpeedKmh(stats.MaxSpeedMps);
        });

    private void OnStateChanged() =>
        MainThread.BeginInvokeOnMainThread(RefreshStateProperties);

    private void RefreshStateProperties()
    {
        OnPropertyChanged(nameof(IsIdle));
        OnPropertyChanged(nameof(IsRecording));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(PauseResumeText));
    }

    private void StartTimer()
    {
        if (_timer is not null) return;
        _timer = Application.Current!.Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) =>
        {
            DurationDisplay = Format.Duration(_recorder.CurrentStats.MovingSeconds);
            IsGpsLost = _recorder.IsSignalLost(DateTime.UtcNow);
        };
        _timer.Start();
    }

    private void StopTimer()
    {
        _timer?.Stop();
        _timer = null;
    }
}
