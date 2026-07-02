using Esk8_Tracker.Core;
using Esk8_Tracker.Maps;
using Esk8_Tracker.ViewModels;
using Mapsui.UI.Maui;

namespace Esk8_Tracker.Views;

public partial class RidePage : ContentPage
{
    private readonly RideViewModel _viewModel;
    private readonly RideRecorder _recorder;
    private readonly RideMap _rideMap = new();
    private bool _mapCentered;

    public RidePage(RideViewModel viewModel, RideRecorder recorder)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _recorder = recorder;

        MapHolder.Content = new MapControl { Map = _rideMap.Map };
        _recorder.FixAccepted += OnFixAccepted;
        _recorder.StateChanged += OnRecorderStateChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _viewModel.OnAppearingAsync();

            // Returning to the tab mid-ride: rebuild the drawn route from the recorder.
            if (_recorder.State != RecorderState.Idle && _recorder.RoutePoints.Count > 0)
            {
                _rideMap.SetRoute(_recorder.RoutePoints);
                var (lat, lon) = _recorder.RoutePoints[^1];
                _rideMap.UpdatePosition(lat, lon);
                _mapCentered = true;
            }
            else if (!_mapCentered)
            {
                await CenterOnLastKnownPositionAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RidePage appearing failed: {ex}");
        }
    }

    private async Task CenterOnLastKnownPositionAsync()
    {
        try
        {
            var last = await Geolocation.GetLastKnownLocationAsync();
            if (last is not null)
            {
                _rideMap.CenterOn(last.Latitude, last.Longitude);
                _mapCentered = true;
            }
        }
        catch (Exception)
        {
            // No permission yet or no cached position — the world view is fine.
        }
    }

    private void OnFixAccepted(GpsFix fix) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!_mapCentered)
            {
                _rideMap.CenterOn(fix.Latitude, fix.Longitude);
                _mapCentered = true;
            }
            _rideMap.UpdatePosition(fix.Latitude, fix.Longitude);
            _rideMap.AppendRoutePoint(fix.Latitude, fix.Longitude);
        });

    private void OnRecorderStateChanged() =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // A fresh ride starts with a clean polyline.
            if (_recorder.State == RecorderState.Recording && _recorder.RoutePoints.Count == 0)
                _rideMap.ClearRoute();
        });
}
