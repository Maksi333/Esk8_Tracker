using Android.Content;
using Esk8_Tracker.Services;

namespace Esk8_Tracker;

public class AndroidRideRecordingController : IRideRecordingController
{
    public bool IsSupported => true;

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
