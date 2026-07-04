using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Esk8_Tracker.Services;
using Esk8_Tracker.Views;

namespace Esk8_Tracker.ViewModels;

public record OnboardingStep(int Index, string IconGlyph, string Title, string Body,
    string Cta, string CtaIcon, string Skip, bool HasNote, string NoteIcon, string Note);

public partial class OnboardingViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly IServiceProvider _services;

    public OnboardingViewModel(AppSettings settings, IServiceProvider services)
    {
        _settings = settings;
        _services = services;
        Apply(Steps[0]);
    }

    public static readonly OnboardingStep[] Steps =
    {
        new(0, MaterialIcons.Skateboarding, "Track every ride.\nAll on your phone.",
            "A telemetry-grade ride tracker for your esk8. Live speed, distance, battery — and a history that's yours to keep.",
            "Get started", MaterialIcons.ArrowForward, "", false, "", ""),
        new(1, MaterialIcons.MyLocation, "We use GPS to draw\nyour route",
            "Location powers your speed, distance and the colorized route map. Nothing ever leaves your phone.",
            "Allow location", MaterialIcons.MyLocation, "Not now", true, MaterialIcons.Lock,
            "Location is only used while tracking. Our recording service keeps rides tracking even with the screen off."),
        new(2, MaterialIcons.BatteryChargingFull, "Keep tracking alive\nin your pocket",
            "Android may kill tracking to save battery — and a ride that silently stops is a lost ride. Grant a battery exemption so that never happens.",
            "Allow background", MaterialIcons.BatteryChargingFull, "I'll risk it", true, MaterialIcons.Warning,
            "This is the #1 reason rides don't save. One tap fixes it for good."),
        new(3, MaterialIcons.Garage, "Add your first board",
            "Your board's top speed scales the color ramp and its battery powers range estimates. You can always add more in the Garage.",
            "Add a board", MaterialIcons.Add, "Skip for now", false, "", ""),
    };

    [ObservableProperty] private int _index;
    [ObservableProperty] private string _iconGlyph = "";
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _body = "";
    [ObservableProperty] private string _cta = "";
    [ObservableProperty] private string _ctaIcon = "";
    [ObservableProperty] private string _skip = "";
    [ObservableProperty] private bool _hasNote;
    [ObservableProperty] private string _noteIcon = "";
    [ObservableProperty] private string _note = "";
    [ObservableProperty] private bool _hasSkip;

    public event Action? StepChanged;

    private void Apply(OnboardingStep s)
    {
        Index = s.Index;
        IconGlyph = s.IconGlyph;
        Title = s.Title;
        Body = s.Body;
        Cta = s.Cta;
        CtaIcon = s.CtaIcon;
        Skip = s.Skip;
        HasSkip = s.Skip.Length > 0;
        HasNote = s.HasNote;
        NoteIcon = s.NoteIcon;
        Note = s.Note;
        StepChanged?.Invoke();
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        switch (Index)
        {
            case 0:
                Apply(Steps[1]);
                break;
            case 1:
                await PlatformPermissions.EnsureLocationAsync();
                Apply(Steps[2]);
                break;
            case 2:
                PlatformPermissions.RequestIgnoreBatteryOptimizations();
                await PlatformPermissions.RequestNotificationsAsync();
                Apply(Steps[3]);
                break;
            case 3:
                await FinishAsync(openBoardEditor: true);
                break;
        }
    }

    [RelayCommand]
    private async Task SkipAsync()
    {
        if (Index < 3)
            Apply(Steps[Index + 1]);
        else
            await FinishAsync(openBoardEditor: false);
    }

    private async Task FinishAsync(bool openBoardEditor)
    {
        (Application.Current as App)?.ShowMainApp();
        RootPage.Current?.SelectTab(AppTab.Garage);
        if (openBoardEditor)
        {
            var page = _services.GetRequiredService<BoardEditorPage>();
            page.StartNew();
            if (Application.Current?.Windows[0].Page is NavigationPage nav)
                await nav.Navigation.PushModalAsync(page);
        }
    }
}
