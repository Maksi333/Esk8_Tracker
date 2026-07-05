using Esk8_Tracker.Core;
using Esk8_Tracker.Core.Data;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace Esk8_Tracker
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            Services.CrashLog.Install();

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseSkiaSharp() // custom-drawn speedometer, route maps, graphs
                .ConfigureFonts(fonts =>
                {
                    // Chakra Petch: telemetry numerics (digits retabularized at build prep)
                    fonts.AddFont("ChakraPetch-Regular.ttf", "ChakraPetch");
                    fonts.AddFont("ChakraPetch-Medium.ttf", "ChakraPetchMedium");
                    fonts.AddFont("ChakraPetch-SemiBold.ttf", "ChakraPetchSemiBold");
                    fonts.AddFont("ChakraPetch-Bold.ttf", "ChakraPetchBold");
                    // Space Grotesk: display / headings
                    fonts.AddFont("SpaceGrotesk-Regular.ttf", "SpaceGrotesk");
                    fonts.AddFont("SpaceGrotesk-Medium.ttf", "SpaceGroteskMedium");
                    fonts.AddFont("SpaceGrotesk-SemiBold.ttf", "SpaceGroteskSemiBold");
                    fonts.AddFont("SpaceGrotesk-Bold.ttf", "SpaceGroteskBold");
                    // Inter: UI body
                    fonts.AddFont("Inter-Regular.ttf", "Inter");
                    fonts.AddFont("Inter-Medium.ttf", "InterMedium");
                    fonts.AddFont("Inter-SemiBold.ttf", "InterSemiBold");
                    fonts.AddFont("Inter-Bold.ttf", "InterBold");
                    // Material Symbols Rounded (subset; see MaterialIcons)
                    fonts.AddFont("MaterialSymbolsRounded-Regular.ttf", "MaterialRounded");
                    fonts.AddFont("MaterialSymbolsRounded-Filled.ttf", "MaterialRoundedFilled");
                });

            builder.Services.AddSingleton(_ =>
                new Esk8Database(Path.Combine(FileSystem.AppDataDirectory, "esk8.db3")));
            builder.Services.AddSingleton<IRideStore>(sp => sp.GetRequiredService<Esk8Database>());
            builder.Services.AddSingleton<RideRecorder>();

#if ANDROID
            builder.Services.AddSingleton<Services.IRideRecordingController, AndroidRideRecordingController>();
#else
            builder.Services.AddSingleton<Services.IRideRecordingController,
                Services.UnsupportedRideRecordingController>();
#endif
            builder.Services.AddSingleton<Services.AppSettings>();
            builder.Services.AddSingleton<Services.VoiceCueService>();
            builder.Services.AddSingleton<AutoPauseMonitor>();

            // Root chrome + sections (RootPage caches section instances itself)
            builder.Services.AddTransient<Views.RootPage>();
            builder.Services.AddSingleton<ViewModels.RideViewModel>(); // owns live ride state
            builder.Services.AddTransient<Views.RideSection>();
            builder.Services.AddTransient<ViewModels.HistoryViewModel>();
            builder.Services.AddTransient<Views.HistorySection>();
            builder.Services.AddTransient<ViewModels.GarageViewModel>();
            builder.Services.AddTransient<Views.GarageSection>();
            builder.Services.AddTransient<ViewModels.StatsViewModel>();
            builder.Services.AddTransient<Views.StatsSection>();
            builder.Services.AddTransient<ViewModels.SettingsViewModel>();
            builder.Services.AddTransient<Views.SettingsSection>();

            // Pushed pages
            builder.Services.AddTransient<ViewModels.RideDetailViewModel>();
            builder.Services.AddTransient<Views.RideDetailPage>();
            builder.Services.AddTransient<ViewModels.BoardEditorViewModel>();
            builder.Services.AddTransient<Views.BoardEditorPage>();
            builder.Services.AddTransient<ViewModels.OnboardingViewModel>();
            builder.Services.AddTransient<Views.OnboardingPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
