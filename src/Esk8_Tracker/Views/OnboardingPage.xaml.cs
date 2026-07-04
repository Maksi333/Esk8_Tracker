using Esk8_Tracker.ViewModels;

namespace Esk8_Tracker.Views;

public partial class OnboardingPage : ContentPage
{
    private readonly OnboardingViewModel _vm;
    private BoxView[] _dots = Array.Empty<BoxView>();

    public OnboardingPage(OnboardingViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
        _dots = new[] { Dot0, Dot1, Dot2, Dot3 };
        _vm.StepChanged += OnStepChanged;
        OnStepChanged();
    }

    private async void OnStepChanged()
    {
        for (var i = 0; i < _dots.Length; i++)
        {
            var active = i == _vm.Index;
            _dots[i].WidthRequest = active ? 26 : 7;
            _dots[i].Color = active ? (Color)App.Res("Go") : (Color)App.Res("BorderDefault");
        }

        // esk-rise: fade + slide the text block in on each step.
        TextBlock.Opacity = 0;
        TextBlock.TranslationY = 10;
        await Task.WhenAll(
            TextBlock.FadeTo(1, 400, Easing.CubicOut),
            TextBlock.TranslateTo(0, 0, 400, Easing.CubicOut));
    }

    protected override bool OnBackButtonPressed() => true; // no back out of onboarding
}
