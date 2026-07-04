using Esk8_Tracker.ViewModels;

namespace Esk8_Tracker.Views;

public partial class RideSection : ContentView, ISection
{
    private readonly RideViewModel _vm;
    private CancellationTokenSource? _pulseCts;

    public RideSection(RideViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;

        ActiveHoldBar.Completed += async (_, _) => await _vm.StopFromHoldAsync();
        PausedHoldBar.Completed += async (_, _) => await _vm.StopFromHoldAsync();
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RideViewModel.UiState))
                UpdatePulse();
        };
    }

    public void OnShown()
    {
        _vm.OnShown();
        UpdatePulse();
    }

    private void UpdatePulse()
    {
        // Pulse the recording dot while active, the pause icon while paused.
        _pulseCts?.Cancel();
        if (_vm.IsActive)
            StartPulse(RecDot, 2000);
        else if (_vm.IsPaused)
            StartPulse(PauseIcon, 1800);
    }

    private void StartPulse(VisualElement target, int periodMs)
    {
        var cts = _pulseCts = new CancellationTokenSource();
        _ = Loop();

        async Task Loop()
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    await target.FadeTo(0.35, (uint)(periodMs / 2), Easing.SinInOut);
                    if (cts.IsCancellationRequested) break;
                    await target.FadeTo(1.0, (uint)(periodMs / 2), Easing.SinInOut);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Pulse loop ended: {ex.Message}");
            }
            finally
            {
                target.Opacity = 1;
            }
        }
    }

    private void OnTagTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject b && b.BindingContext is TagChip chip)
        {
            chip.IsSelected = !chip.IsSelected;
            if (sender is Border border)
            {
                border.BackgroundColor = chip.IsSelected
                    ? (Color)App.Res("SurfaceElevated")
                    : (Color)App.Res("SurfaceCard");
                border.Stroke = chip.IsSelected
                    ? (Color)App.Res("BorderStrong")
                    : (Color)App.Res("BorderDefault");
            }
        }
    }
}
