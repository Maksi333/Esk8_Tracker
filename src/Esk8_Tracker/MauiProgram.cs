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
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseSkiaSharp() // required by Mapsui
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            builder.Services.AddSingleton(_ =>
                new Esk8Database(Path.Combine(FileSystem.AppDataDirectory, "esk8.db3")));
            builder.Services.AddSingleton<IRideStore>(sp => sp.GetRequiredService<Esk8Database>());
            builder.Services.AddSingleton<RideRecorder>();

            builder.Services.AddTransient<ViewModels.BoardsViewModel>();
            builder.Services.AddTransient<Views.BoardsPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
