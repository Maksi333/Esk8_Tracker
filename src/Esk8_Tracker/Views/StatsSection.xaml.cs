using Esk8_Tracker.ViewModels;

namespace Esk8_Tracker.Views;

public partial class StatsSection : ContentView, ISection
{
    private readonly StatsViewModel _vm;

    public StatsSection(StatsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    public void OnShown() => _vm.OnShown();

    private void OnBadgeTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject b && b.BindingContext is AchievementTile tile)
            _vm.ShowBadge(tile);
    }
}
