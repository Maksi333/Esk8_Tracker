using System.IO.Compression;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Esk8_Tracker.Core;
using Esk8_Tracker.Core.Data;
using Esk8_Tracker.Core.Export;
using Esk8_Tracker.Services;
using Esk8_Tracker.Views;

namespace Esk8_Tracker.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly Esk8Database _db;
    private readonly AppSettings _settings;

    public SettingsViewModel(Esk8Database db, AppSettings settings)
    {
        _db = db;
        _settings = settings;
    }

    [ObservableProperty] private bool _metric = true;
    [ObservableProperty] private bool _imperial;
    [ObservableProperty] private bool _autoOff;
    [ObservableProperty] private bool _autoLow;
    [ObservableProperty] private bool _autoNormal;
    [ObservableProperty] private bool _autoHigh;
    [ObservableProperty] private bool _voiceCues;
    [ObservableProperty] private bool _autoGlance;
    [ObservableProperty] private bool _colorizeMap;
    [ObservableProperty] private string _storageText = "0 rides · 0.0 MB on device";
    [ObservableProperty] private string _version = "";

    public async void OnShown()
    {
        Metric = _settings.Units == UnitSystem.Metric;
        Imperial = !Metric;
        AutoOff = _settings.AutoPause == AutoPauseSensitivity.Off;
        AutoLow = _settings.AutoPause == AutoPauseSensitivity.Low;
        AutoNormal = _settings.AutoPause == AutoPauseSensitivity.Normal;
        AutoHigh = _settings.AutoPause == AutoPauseSensitivity.High;
        VoiceCues = _settings.VoiceCues;
        AutoGlance = _settings.AutoGlance;
        ColorizeMap = _settings.ColorizeMap;
        Version = $"ESK8 Tracker · v{AppInfo.Current.VersionString}";

        var rides = await _db.GetCompletedRidesAsync();
        var mb = _db.StorageBytes() / 1048576.0;
        StorageText = $"{rides.Count} rides · {mb:F1} MB on device";
    }

    [RelayCommand]
    private void SetMetric()
    {
        _settings.Units = UnitSystem.Metric;
        Metric = true; Imperial = false;
    }

    [RelayCommand]
    private void SetImperial()
    {
        _settings.Units = UnitSystem.Imperial;
        Metric = false; Imperial = true;
    }

    [RelayCommand]
    private void SetAutoPause(string level)
    {
        _settings.AutoPause = Enum.Parse<AutoPauseSensitivity>(level);
        AutoOff = level == "Off";
        AutoLow = level == "Low";
        AutoNormal = level == "Normal";
        AutoHigh = level == "High";
    }

    public void OnVoiceToggled(bool on) => _settings.VoiceCues = on;
    public void OnGlanceToggled(bool on) => _settings.AutoGlance = on;
    public void OnColorizeToggled(bool on) => _settings.ColorizeMap = on;

    [RelayCommand]
    private async Task ExportAsync()
    {
        var rides = await _db.GetCompletedRidesAsync();
        if (rides.Count == 0)
        {
            await Toast("No rides to export yet", MaterialIcons.Warning);
            return;
        }
        var page = Application.Current?.Windows[0].Page;
        var choice = await (page?.DisplayActionSheet("Export all rides", "Cancel", null, "JSON backup", "GPX archive (zip)") ?? Task.FromResult<string?>(null));
        try
        {
            if (choice == "JSON backup")
            {
                var boards = (await _db.GetBoardsByIdAsync()).Values.ToList();
                var points = await _db.GetAllTrackPointsAsync();
                var json = BackupExporter.ToJson(boards, rides, points, DateTime.UtcNow);
                var path = Path.Combine(FileSystem.CacheDirectory, BackupExporter.FileName(DateTime.Now));
                await File.WriteAllTextAsync(path, json);
                await Share.Default.RequestAsync(new ShareFileRequest { Title = "Export backup", File = new ShareFile(path) });
                await Toast($"Exported {rides.Count} rides");
            }
            else if (choice == "GPX archive (zip)")
            {
                var dir = Path.Combine(FileSystem.CacheDirectory, "gpx-export");
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
                Directory.CreateDirectory(dir);
                foreach (var r in rides)
                {
                    var pts = await _db.GetTrackPointsAsync(r.Id);
                    await File.WriteAllTextAsync(Path.Combine(dir, GpxExporter.FileName(r)), GpxExporter.ToGpx(r, pts));
                }
                var zip = Path.Combine(FileSystem.CacheDirectory, "esk8-rides-gpx.zip");
                if (File.Exists(zip)) File.Delete(zip);
                ZipFile.CreateFromDirectory(dir, zip);
                await Share.Default.RequestAsync(new ShareFileRequest { Title = "Export GPX", File = new ShareFile(zip) });
                await Toast($"Exported {rides.Count} rides");
            }
        }
        catch (Exception ex)
        {
            await Toast("Export failed", MaterialIcons.Warning);
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private static Task Toast(string msg, string icon = "") =>
        RootPage.Current?.ShowToastAsync(msg, icon) ?? Task.CompletedTask;
}
