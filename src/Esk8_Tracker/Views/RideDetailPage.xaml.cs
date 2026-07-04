using Esk8_Tracker.ViewModels;

namespace Esk8_Tracker.Views;

public partial class RideDetailPage : ContentPage
{
    private readonly RideDetailViewModel _vm;

    public RideDetailPage(RideDetailViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
        _vm.RequestClose += async () =>
        {
            if (Navigation.NavigationStack.Count > 1)
                await Navigation.PopAsync();
        };
    }

    public Task LoadAsync(int rideId) => _vm.LoadAsync(rideId);

    protected override bool OnBackButtonPressed()
    {
        _ = Navigation.PopAsync();
        return true;
    }
}
