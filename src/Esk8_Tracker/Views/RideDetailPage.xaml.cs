using Esk8_Tracker.Maps;
using Esk8_Tracker.ViewModels;
using Mapsui.UI.Maui;

namespace Esk8_Tracker.Views;

public partial class RideDetailPage : ContentPage
{
    private readonly RideDetailViewModel _viewModel;
    private readonly RideMap _rideMap = new();

    public RideDetailPage(RideDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        MapHolder.Content = new MapControl { Map = _rideMap.Map };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
        _rideMap.SetRoute(_viewModel.RoutePoints);
        _rideMap.ZoomToRoute();
    }
}
