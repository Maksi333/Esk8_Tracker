using Esk8_Tracker.ViewModels;

namespace Esk8_Tracker.Views;

public partial class HistorySection : ContentView, ISection
{
    private readonly HistoryViewModel _vm;

    public HistorySection(HistoryViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    public void OnShown() => _vm.OnShown();
}
