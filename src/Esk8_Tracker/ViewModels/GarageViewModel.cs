using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Esk8_Tracker.Core;
using Esk8_Tracker.Core.Data;
using Esk8_Tracker.Core.Models;
using Esk8_Tracker.Services;
using Esk8_Tracker.Views;

namespace Esk8_Tracker.ViewModels;

public partial class GarageViewModel : ObservableObject
{
    private readonly Esk8Database _db;
    private readonly AppSettings _settings;
    private readonly IServiceProvider _services;

    public GarageViewModel(Esk8Database db, AppSettings settings, IServiceProvider services)
    {
        _db = db;
        _settings = settings;
        _services = services;
        BoardEditorViewModel.BoardsChanged += OnBoardsChanged;
        _settings.Changed += () => MainThread.BeginInvokeOnMainThread(() => _ = LoadAsync());
    }

    public ObservableCollection<BoardCard> Boards { get; } = new();

    [ObservableProperty] private bool _isEmpty;

    public async void OnShown() => await LoadAsync();

    private void OnBoardsChanged() => MainThread.BeginInvokeOnMainThread(() => _ = LoadAsync());

    private async Task LoadAsync()
    {
        var boards = await _db.GetActiveBoardsAsync();
        var odos = await _db.GetBoardOdometersAsync();
        Boards.Clear();
        foreach (var b in boards)
        {
            var odo = odos.TryGetValue(b.Id, out var m) ? m : 0;
            Boards.Add(new BoardCard(b, b.Id == _settings.ActiveBoardId, odo, _settings.Units));
        }
        IsEmpty = boards.Count == 0;
    }

    [RelayCommand]
    private async Task AddBoardAsync()
    {
        var page = _services.GetRequiredService<BoardEditorPage>();
        page.StartNew();
        if (Application.Current?.Windows[0].Page is NavigationPage nav)
            await nav.Navigation.PushModalAsync(page);
    }

    [RelayCommand]
    private async Task EditBoardAsync(BoardCard? card)
    {
        if (card is null) return;
        var page = _services.GetRequiredService<BoardEditorPage>();
        page.StartEdit(card.Board);
        if (Application.Current?.Windows[0].Page is NavigationPage nav)
            await nav.Navigation.PushModalAsync(page);
    }

    [RelayCommand]
    private async Task SetActiveAsync(BoardCard? card)
    {
        if (card is null) return;
        _settings.ActiveBoardId = card.Board.Id;
        await LoadAsync();
        await (RootPage.Current?.ShowToastAsync($"{card.Name} set active") ?? Task.CompletedTask);
    }

    [RelayCommand]
    private void GoRide() => RootPage.Current?.SelectTab(AppTab.Ride);
}

public partial class BoardCard : ObservableObject
{
    public BoardCard(Board board, bool isActive, double odometerMeters, UnitSystem units)
    {
        Board = board;
        IsActive = isActive;
        Name = board.Name;
        Color = SafeColor(board.ColorHex);
        ColorFaint = Color.WithAlpha(0.14f);
        BorderColor = isActive ? Color.WithAlpha(0.5f) : (Microsoft.Maui.Graphics.Color)App.Res("BorderDefault");

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(board.BatteryCells)) parts.Add(board.BatteryCells);
        if (board.BatteryWh is > 0) parts.Add($"{board.BatteryWh:0} Wh");
        parts.Add($"{Units.Speed(board.TopSpeedMps, units)} {Units.SpeedUnit(units)}");
        Specs = string.Join(" · ", parts);
        Odometer = $"{Units.Distance(odometerMeters, units, 0)} {Units.DistanceUnit(units)}";
        ShowSetActive = !isActive;
    }

    public Board Board { get; }
    public bool IsActive { get; }
    public string Name { get; }
    public Microsoft.Maui.Graphics.Color Color { get; }
    public Microsoft.Maui.Graphics.Color ColorFaint { get; }
    public Microsoft.Maui.Graphics.Color BorderColor { get; }
    public string Specs { get; }
    public string Odometer { get; }
    public bool ShowSetActive { get; }

    private static Microsoft.Maui.Graphics.Color SafeColor(string? hex)
    {
        try { return Microsoft.Maui.Graphics.Color.FromArgb(string.IsNullOrWhiteSpace(hex) ? "#4C8DFF" : hex); }
        catch { return Microsoft.Maui.Graphics.Color.FromArgb("#4C8DFF"); }
    }
}
