using Esk8_Tracker.Services;
using Esk8_Tracker.Views;

namespace Esk8_Tracker
{
    public partial class App : Application
    {
        private readonly IServiceProvider _services;
        private readonly AppSettings _settings;

        public App(IServiceProvider services, AppSettings settings)
        {
            InitializeComponent();
            _services = services;
            _settings = settings;
            UserAppTheme = AppTheme.Dark; // dark-only v1 per the design handoff

            // Eager singletons that work purely off recorder events.
            _services.GetRequiredService<VoiceCueService>();
        }

        /// <summary>Typed resource lookup for code-built UI (root chrome, overlays).</summary>
        public static object Res(string key) =>
            Current!.Resources.TryGetValue(key, out var value)
                ? value
                : throw new KeyNotFoundException($"Missing resource '{key}'");

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // First run: full-screen onboarding precedes the app chrome.
            Page root = _settings.OnboardingDone
                ? new NavigationPage(_services.GetRequiredService<RootPage>())
                : _services.GetRequiredService<OnboardingPage>();
            return new Window(root);
        }

        /// <summary>Called by onboarding when it completes; swaps in the main chrome.</summary>
        public void ShowMainApp()
        {
            _settings.OnboardingDone = true;
            if (Windows.Count > 0)
                Windows[0].Page = new NavigationPage(_services.GetRequiredService<RootPage>());
        }
    }
}
