using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Esk8_Tracker.Services;

namespace Esk8_Tracker
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            // Capture Android-native unhandled exceptions to a diagnosable log.
            AndroidEnvironment.UnhandledExceptionRaiser += (_, e) =>
            {
                CrashLog.Write("Android.UnhandledExceptionRaiser", e.Exception);
                // Let the runtime continue its default handling.
            };
            base.OnCreate(savedInstanceState);
        }
    }
}
