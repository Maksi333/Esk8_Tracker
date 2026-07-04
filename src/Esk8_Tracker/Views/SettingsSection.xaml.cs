using Esk8_Tracker.ViewModels;

namespace Esk8_Tracker.Views;

public partial class SettingsSection : ContentView, ISection
{
    private readonly SettingsViewModel _vm;

    public SettingsSection(SettingsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
        VoiceToggle.Toggled += (_, on) => _vm.OnVoiceToggled(on);
        GlanceToggle.Toggled += (_, on) => _vm.OnGlanceToggled(on);
        ColorizeToggle.Toggled += (_, on) => _vm.OnColorizeToggled(on);
    }

    public void OnShown() => _vm.OnShown();
}
