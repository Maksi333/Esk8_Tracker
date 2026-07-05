namespace Esk8_Tracker.Services;

/// <summary>
/// Last-resort crash diagnostics. Writes unhandled exceptions to a file in app data
/// (retrievable via `adb` or the OS file picker) and to the debug log, so a crash on
/// a user's device can be diagnosed without a live debugger attached.
/// </summary>
public static class CrashLog
{
    public static string LogPath => Path.Combine(FileSystem.AppDataDirectory, "crash.log");

    /// <summary>Hook the process-wide unhandled-exception sources. Call once at startup.</summary>
    public static void Install()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Write("AppDomain.UnhandledException", e.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Write("UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
    }

    public static void Write(string source, Exception? ex)
    {
        var text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}\n{ex}\n\n";
        System.Diagnostics.Debug.WriteLine($"ESK8-CRASH {source}: {ex}");
        try
        {
            File.AppendAllText(LogPath, text);
        }
        catch
        {
            // Nothing more we can do if even logging fails.
        }
    }
}
