using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Locations;
using Android.OS;
using AndroidX.Core.App;
using Esk8_Tracker.Core;
using Esk8_Tracker.Services;
using Microsoft.Extensions.DependencyInjection;
// The Maui SDK's implicit global usings pull in Microsoft.Maui.Devices.Sensors, which also
// defines a `Location` type (Geolocation API), colliding with Android.Locations.Location.
using Location = Android.Locations.Location;

namespace Esk8_Tracker;

[Service(Exported = false, Enabled = true, ForegroundServiceType = ForegroundService.TypeLocation)]
public class RideRecordingService : Service, ILocationListener
{
    public const string ActionPause = "com.simonandersen.esk8tracker.PAUSE";
    public const string ActionResume = "com.simonandersen.esk8tracker.RESUME";
    public const string ActionStop = "com.simonandersen.esk8tracker.STOP";

    private const int NotificationId = 1001; // must not be 0
    private const string ChannelId = "ride_recording";
    private const double NotifyThrottleSeconds = 5.0;

    private LocationManager? _locationManager;
    private RideRecorder? _recorder;
    private AppSettings? _settings;
    private DateTime _lastNotifyUtc;
    private bool _started;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        _recorder ??= IPlatformApplication.Current!.Services.GetRequiredService<RideRecorder>();
        _settings ??= IPlatformApplication.Current!.Services.GetRequiredService<AppSettings>();

        // Notification action taps re-enter here with an action set.
        switch (intent?.Action)
        {
            case ActionPause:
                _recorder.Pause();
                RefreshNotification(force: true);
                return StartCommandResult.NotSticky;
            case ActionResume:
                _recorder.Resume();
                RefreshNotification(force: true);
                return StartCommandResult.NotSticky;
            case ActionStop:
                StopRideAndSelf();
                return StartCommandResult.NotSticky;
        }

        if (_started) return StartCommandResult.NotSticky;
        _started = true;

        CreateNotificationChannel();
        // Android 14+: throws unless FOREGROUND_SERVICE_LOCATION is declared and
        // fine/coarse location was granted BEFORE this call (the VM guarantees it).
        // The typed overload exists only on API 29+; older versions take the
        // service type solely from the manifest.
        if (OperatingSystem.IsAndroidVersionAtLeast(29))
            StartForeground(NotificationId, BuildNotification(), ForegroundService.TypeLocation);
        else
            StartForeground(NotificationId, BuildNotification());
        try
        {
            StartLocationUpdates();
        }
        catch (Java.Lang.SecurityException ex)
        {
            // Permission revoked between the VM's check and service start.
            System.Diagnostics.Debug.WriteLine($"Location updates denied; stopping service: {ex}");
            StopForeground(StopForegroundFlags.Remove);
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        _recorder.StatsUpdated += OnStatsUpdated;
        _recorder.StateChanged += OnRecorderStateChanged;

        // NotSticky: if the process dies mid-ride, recorder state is gone anyway;
        // the "Resume unsaved ride?" recovery banner restores it from saved points.
        return StartCommandResult.NotSticky;
    }

    public override void OnDestroy()
    {
        if (_recorder is not null)
        {
            _recorder.StatsUpdated -= OnStatsUpdated;
            _recorder.StateChanged -= OnRecorderStateChanged;
        }
        _locationManager?.RemoveUpdates(this);
        _locationManager = null;
        base.OnDestroy();
    }

    private void OnStatsUpdated(RideLiveStats stats) => RefreshNotification(force: false);

    private void OnRecorderStateChanged()
    {
        if (_recorder!.State == RecorderState.Idle) return; // VM is tearing the service down
        RefreshNotification(force: true);
    }

    private void RefreshNotification(bool force)
    {
        var now = DateTime.UtcNow;
        if (!force && (now - _lastNotifyUtc).TotalSeconds < NotifyThrottleSeconds) return;
        _lastNotifyUtc = now;
        var manager = (NotificationManager)GetSystemService(NotificationService)!;
        manager.Notify(NotificationId, BuildNotification());
    }

    private void StopRideAndSelf()
    {
        // Trust the save: finalize from the notification without opening the app.
        var recorder = _recorder!;
        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                if (recorder.State != RecorderState.Idle)
                    await recorder.StopAsync(DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Stop from notification failed: {ex}");
            }
            finally
            {
                StopForeground(StopForegroundFlags.Remove);
                StopSelf();
            }
        });
    }

    private void CreateNotificationChannel()
    {
        var channel = new NotificationChannel(ChannelId, "Ride recording",
            NotificationImportance.Low)
        {
            Description = "Shown while a ride is being recorded",
        };
        var manager = (NotificationManager)GetSystemService(NotificationService)!;
        manager.CreateNotificationChannel(channel);
    }

    private Notification BuildNotification()
    {
        var openApp = new Intent(this, typeof(MainActivity));
        var pending = PendingIntent.GetActivity(this, 0, openApp,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;

        var recorder = _recorder;
        var units = _settings?.Units ?? UnitSystem.Metric;
        var paused = recorder?.State == RecorderState.Paused;
        var stats = recorder?.CurrentStats;

        var title = "Recording ride";
        var text = "Starting GPS…";
        if (stats is not null && stats.DistanceMeters > 0)
        {
            var dist = $"{Units.Distance(stats.DistanceMeters, units)} {Units.DistanceUnit(units)}";
            title = paused ? $"Ride paused · {dist}" : $"Recording · {dist}";
            text = $"{Units.Duration(stats.MovingSeconds)} moving · " +
                   $"{Units.Speed(stats.AvgSpeedMps, units)} {Units.SpeedUnit(units)} avg";
        }
        else if (paused)
        {
            title = "Ride paused";
            text = "Resume from here or in the app";
        }

        // The AndroidX binding marks the builder's fluent returns as nullable even
        // though they always return the builder itself; build stepwise to avoid
        // false-positive CS8602/CS8603 on the chain.
        var builder = new NotificationCompat.Builder(this, ChannelId);
        builder.SetContentTitle(title);
        builder.SetContentText(text);
        builder.SetSmallIcon(Resource.Mipmap.appicon);
        builder.SetOngoing(true);
        builder.SetOnlyAlertOnce(true);
        builder.SetContentIntent(pending);
        builder.SetForegroundServiceBehavior(NotificationCompat.ForegroundServiceImmediate);

        if (paused)
            builder.AddAction(0, "Resume", ActionIntent(ActionResume, 1));
        else
            builder.AddAction(0, "Pause", ActionIntent(ActionPause, 1));
        builder.AddAction(0, "Stop & save", ActionIntent(ActionStop, 2));

        return builder.Build()!;
    }

    private PendingIntent ActionIntent(string action, int requestCode)
    {
        var intent = new Intent(this, typeof(RideRecordingService));
        intent.SetAction(action);
        return PendingIntent.GetService(this, requestCode, intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
    }

    private void StartLocationUpdates()
    {
        _locationManager = (LocationManager)GetSystemService(LocationService)!;

        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            var request = new LocationRequest.Builder(1000)
                .SetMinUpdateIntervalMillis(1000)
                .SetMinUpdateDistanceMeters(0f)
                .Build();
            _locationManager.RequestLocationUpdates(LocationManager.GpsProvider!, request, MainExecutor!, this);
        }
        else
        {
            _locationManager.RequestLocationUpdates(LocationManager.GpsProvider!, 1000, 0f, this, Looper.MainLooper);
        }
    }

    public void OnLocationChanged(Location location)
    {
        var recorder = _recorder;
        if (recorder is null) return;

        var fix = new GpsFix(
            DateTimeOffset.FromUnixTimeMilliseconds(location.Time).UtcDateTime,
            location.Latitude,
            location.Longitude,
            location.HasSpeed ? location.Speed : null,
            location.HasAccuracy ? location.Accuracy : double.MaxValue,
            location.HasAltitude ? location.Altitude : null);

        _ = recorder.OnFixAsync(fix); // fire-and-forget; recorder is main-thread bound like this callback
    }

    public void OnProviderDisabled(string provider) { }

    public void OnProviderEnabled(string provider) { }

    public void OnStatusChanged(string? provider, Availability status, Bundle? extras) { }
}
