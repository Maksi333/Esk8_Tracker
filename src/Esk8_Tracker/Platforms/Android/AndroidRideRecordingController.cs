using Android.Content;
using Esk8_Tracker.Services;

namespace Esk8_Tracker;

public class AndroidRideRecordingController : IRideRecordingController
{
    public bool IsSupported => true;

    public bool HasPreciseLocation =>
        AndroidX.Core.Content.ContextCompat.CheckSelfPermission(
            Android.App.Application.Context,
            Android.Manifest.Permission.AccessFineLocation)
        == Android.Content.PM.Permission.Granted;

    public void StartLocationService(int boardId)
    {
        var context = Android.App.Application.Context;
        context.StartForegroundService(new Intent(context, typeof(RideRecordingService)));
    }

    public void StopLocationService()
    {
        var context = Android.App.Application.Context;
        context.StopService(new Intent(context, typeof(RideRecordingService)));
    }
}
