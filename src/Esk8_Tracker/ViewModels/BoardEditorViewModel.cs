using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Esk8_Tracker.Core;
using Esk8_Tracker.Core.Data;
using Esk8_Tracker.Core.Models;
using Esk8_Tracker.Services;
using Esk8_Tracker.Views;

namespace Esk8_Tracker.ViewModels;

public partial class BoardEditorViewModel : ObservableObject
{
    private readonly Esk8Database _db;
    private readonly AppSettings _settings;
    private int _editingId;

    /// <summary>Raised after a board is saved/deleted so the Garage reloads.</summary>
    public static event Action? BoardsChanged;

    public BoardEditorViewModel(Esk8Database db, AppSettings settings)
    {
        _db = db;
        _settings = settings;
    }

    [ObservableProperty] private string _title = "New board";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _colorHex = "#4C8DFF";
    [ObservableProperty] private Color _color = Color.FromArgb("#4C8DFF");
    [ObservableProperty] private Color _colorFaint = Color.FromArgb("#4C8DFF").WithAlpha(0.16f);
    [ObservableProperty] private string _wheelType = WheelTypes.Street;
    [ObservableProperty] private string _batteryCells = "";
    [ObservableProperty] private string _batteryWh = "";
    [ObservableProperty] private string _topSpeed = "40";
    [ObservableProperty] private string _speedUnit = "km/h";
    [ObservableProperty] private string _rampCaption = "Velocity ramp · 0 → 40 km/h";
    [ObservableProperty] private double _topSpeedMps = 40 / 3.6;
    [ObservableProperty] private bool _canDelete;

    public string[] Palette => Board.Colors;
    public event Action? RequestClose;

    public void StartNew()
    {
        _editingId = 0;
        Title = "New board";
        Name = "";
        SetColor("#4C8DFF");
        WheelType = WheelTypes.Street;
        BatteryCells = "";
        BatteryWh = "";
        SpeedUnit = Units.SpeedUnit(_settings.Units);
        SetTopFromKmh(40);
        CanDelete = false;
    }

    public void StartEdit(Board board)
    {
        _editingId = board.Id;
        Title = "Edit board";
        Name = board.Name;
        SetColor(board.ColorHex);
        WheelType = board.WheelType;
        BatteryCells = board.BatteryCells;
        BatteryWh = board.BatteryWh is > 0 ? board.BatteryWh.Value.ToString("0", CultureInfo.InvariantCulture) : "";
        SpeedUnit = Units.SpeedUnit(_settings.Units);
        SetTopFromKmh(board.TopSpeedKmh);
        CanDelete = true;
    }

    private void SetColor(string hex)
    {
        ColorHex = hex;
        Color = SafeColor(hex);
        ColorFaint = Color.WithAlpha(0.16f);
    }

    private void SetTopFromKmh(double kmh)
    {
        var display = _settings.Units == UnitSystem.Imperial ? kmh / Units.KmPerMile : kmh;
        TopSpeed = Math.Round(display).ToString(CultureInfo.InvariantCulture);
        UpdateRampPreview();
    }

    [RelayCommand]
    private void SelectColor(string hex) => SetColor(hex);

    [RelayCommand]
    private void SelectWheel(string wheel) => WheelType = wheel;

    partial void OnTopSpeedChanged(string value) => UpdateRampPreview();

    private void UpdateRampPreview()
    {
        var kmh = ParseTopKmh();
        TopSpeedMps = kmh / 3.6;
        var display = _settings.Units == UnitSystem.Imperial ? kmh / Units.KmPerMile : kmh;
        RampCaption = $"Velocity ramp · 0 → {Math.Round(display)} {SpeedUnit}";
    }

    private double ParseTopKmh()
    {
        if (!double.TryParse(TopSpeed, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) || v <= 0)
            v = _settings.Units == UnitSystem.Imperial ? 25 : 40;
        var kmh = _settings.Units == UnitSystem.Imperial ? v * Units.KmPerMile : v;
        return Math.Clamp(kmh, 5, 120);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var wasEmpty = (await _db.GetActiveBoardsAsync()).Count == 0;
        var board = _editingId != 0
            ? await _db.GetRideBoardOrNew(_editingId)
            : new Board();
        board.Id = _editingId;
        board.Name = string.IsNullOrWhiteSpace(Name) ? "My board" : Name.Trim();
        board.ColorHex = ColorHex;
        board.WheelType = WheelType;
        board.BatteryCells = BatteryCells.Trim();
        board.BatteryWh = double.TryParse(BatteryWh, NumberStyles.Any, CultureInfo.InvariantCulture, out var wh) && wh > 0 ? wh : null;
        board.TopSpeedKmh = ParseTopKmh();

        var saved = await _db.SaveBoardAsync(board);
        if (wasEmpty || _settings.ActiveBoardId == 0)
            _settings.ActiveBoardId = saved.Id;

        BoardsChanged?.Invoke();
        await (RootPage.Current?.ShowToastAsync("Board saved") ?? Task.CompletedTask);
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        var confirmed = await (RootPage.Current?.ShowConfirmAsync(
            MaterialIcons.DeleteForever, $"Delete {Name}?",
            "Rides on this board keep their history. This can't be undone.",
            "Delete", "Keep") ?? Task.FromResult(false));
        if (!confirmed) return;

        await _db.ArchiveBoardAsync(_editingId);
        if (_settings.ActiveBoardId == _editingId)
        {
            var remaining = await _db.GetActiveBoardsAsync();
            _settings.ActiveBoardId = remaining.FirstOrDefault()?.Id ?? 0;
        }
        BoardsChanged?.Invoke();
        await (RootPage.Current?.ShowToastAsync("Board deleted") ?? Task.CompletedTask);
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Close() => RequestClose?.Invoke();

    private static Color SafeColor(string? hex)
    {
        try { return Color.FromArgb(string.IsNullOrWhiteSpace(hex) ? "#4C8DFF" : hex); }
        catch { return Color.FromArgb("#4C8DFF"); }
    }
}

/// <summary>Small convenience so the editor can fetch-or-create without a null dance.</summary>
public static class BoardEditorDbExtensions
{
    public static async Task<Board> GetRideBoardOrNew(this Esk8Database db, int id) =>
        await db.GetBoardAsync(id) ?? new Board { Id = id };
}
