using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Locations;
using Android.OS;
using AndroidX.Core.App;
using Esk8_Tracker.Core;
using Microsoft.Extensions.DependencyInjection;
// The Maui SDK's implicit global usings pull in Microsoft.Maui.Devices.Sensors, which also
// defines a `Location` type (Geolocation API), colliding with Android.Locations.Location.
using Location = Android.Locations.Location;

namespace Esk8_Tracker;

[Service(Exported = false, Enabled = true, ForegroundServiceType = ForegroundService.TypeLocation)]
public class RideRecordingService : Service, ILocationListener
{
    private const int NotificationId = 1001; // must not be 0
    private const string ChannelId = "ride_recording";

    private LocationManager? _locationManager;
    private RideRecorder? _recorder;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        _recorder = IPlatformApplication.Current!.Services.GetRequiredService<RideRecorder>();

        CreateNotificationChannel();
        // Android 14+: throws unless FOREGROUND_SERVICE_LOCATION is declared and
        // fine/coarse location was granted BEFORE this call (Task 9 guarantees it).
        // The typed overload exists only on API 29+; older versions take the
        // service type solely from the manifest.
        if (OperatingSystem.IsAndroidVersionAtLeast(29))
            StartForeground(NotificationId, BuildNotification(), ForegroundService.TypeLocation);
        else
            StartForeground(NotificationId, BuildNotification());
        StartLocationUpdates();

        // NotSticky: if the process dies mid-ride, recorder state is gone anyway;
        // startup crash recovery (App.OnStart) finalizes the ride from saved points.
        return StartCommandResult.NotSticky;
    }

    public override void OnDestroy()
    {
        _locationManager?.RemoveUpdates(this);
        _locationManager = null;
        base.OnDestroy();
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
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        // The AndroidX binding marks the builder's fluent returns as nullable even
        // though they always return the builder itself; build stepwise to avoid
        // false-positive CS8602/CS8603 on the chain.
        var builder = new NotificationCompat.Builder(this, ChannelId);
        builder.SetContentTitle("Recording ride");
        builder.SetContentText("Esk8 Tracker is recording your ride");
        builder.SetSmallIcon(Resource.Mipmap.appicon);
        builder.SetOngoing(true);
        builder.SetContentIntent(pending);
        builder.SetForegroundServiceBehavior(NotificationCompat.ForegroundServiceImmediate);
        return builder.Build()!;
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
