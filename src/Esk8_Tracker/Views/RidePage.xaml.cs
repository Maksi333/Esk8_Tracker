using Esk8_Tracker.ViewModels;

namespace Esk8_Tracker.Views;

public partial class RidePage : ContentPage
{
    private readonly RideViewModel _viewModel;

    public RidePage(RideViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.OnAppearingAsync();
    }
}
