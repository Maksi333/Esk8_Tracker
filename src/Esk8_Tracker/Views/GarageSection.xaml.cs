using Esk8_Tracker.ViewModels;

namespace Esk8_Tracker.Views;

public partial class GarageSection : ContentView, ISection
{
    private readonly GarageViewModel _vm;

    public GarageSection(GarageViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    public void OnShown() => _vm.OnShown();
}
