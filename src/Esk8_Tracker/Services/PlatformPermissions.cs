namespace Esk8_Tracker.Services;

/// <summary>
/// Runtime permission + battery-optimization helpers, primed with context by the
/// onboarding flow and re-checkable from the Ride idle banner and Settings.
/// </summary>
public static class PlatformPermissions
{
    /// <summary>Request foreground location; returns whether it was granted.</summary>
    public static async Task<bool> EnsureLocationAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            return status == PermissionStatus.Granted;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Location permission request failed: {ex}");
            return false;
        }
    }

    /// <summary>Android 13+ notification permission. Optional — never throws or blocks.</summary>
    public static async Task RequestNotificationsAsync()
    {
        try
        {
            await Permissions.RequestAsync<Permissions.PostNotifications>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Notification permission request failed: {ex}");
        }
    }

#if ANDROID
    public static bool IsIgnoringBatteryOptimizations
    {
        get
        {
            try
            {
                var context = Android.App.Application.Context;
                var pm = context.GetSystemService(Android.Content.Context.PowerService)
                    as Android.OS.PowerManager;
                return pm?.IsIgnoringBatteryOptimizations(context.PackageName) ?? true;
            }
            catch
            {
                return true;
            }
        }
    }

    public static void RequestIgnoreBatteryOptimizations()
    {
        try
        {
            var context = Android.App.Application.Context;
            var intent = new Android.Content.Intent(
                Android.Provider.Settings.ActionRequestIgnoreBatteryOptimizations);
            intent.SetData(Android.Net.Uri.Parse($"package:{context.PackageName}"));
            intent.SetFlags(Android.Content.ActivityFlags.NewTask);
            context.StartActivity(intent);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Battery-exemption intent failed, opening list: {ex}");
            try
            {
                var context = Android.App.Application.Context;
                var intent = new Android.Content.Intent(
                    Android.Provider.Settings.ActionIgnoreBatteryOptimizationSettings);
                intent.SetFlags(Android.Content.ActivityFlags.NewTask);
                context.StartActivity(intent);
            }
            catch (Exception inner)
            {
                System.Diagnostics.Debug.WriteLine($"Battery settings fallback failed: {inner}");
            }
        }
    }
#else
    public static bool IsIgnoringBatteryOptimizations => true;

    public static void RequestIgnoreBatteryOptimizations() { }
#endif
}
