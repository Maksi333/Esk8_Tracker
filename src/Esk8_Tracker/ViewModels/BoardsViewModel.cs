using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Esk8_Tracker.Core.Data;
using Esk8_Tracker.Core.Models;

namespace Esk8_Tracker.ViewModels;

public partial class BoardsViewModel(Esk8Database db) : ObservableObject
{
    public ObservableCollection<Board> Boards { get; } = new();

    [ObservableProperty]
    private string _newBoardName = "";

    [RelayCommand]
    private async Task LoadAsync()
    {
        var boards = await db.GetActiveBoardsAsync();
        Boards.Clear();
        foreach (var board in boards)
            Boards.Add(board);
    }

    [RelayCommand]
    private async Task AddBoardAsync()
    {
        var name = NewBoardName.Trim();
        if (name.Length == 0) return;
        await db.AddBoardAsync(name);
        NewBoardName = "";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task RenameBoardAsync(Board board)
    {
        var newName = await Shell.Current.DisplayPromptAsync(
            "Rename board", "New name:", initialValue: board.Name);
        if (string.IsNullOrWhiteSpace(newName)) return;
        await db.RenameBoardAsync(board.Id, newName.Trim());
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ArchiveBoardAsync(Board board)
    {
        var confirmed = await Shell.Current.DisplayAlertAsync("Delete board",
            $"Remove \"{board.Name}\"? Its rides keep their history.", "Delete", "Cancel");
        if (!confirmed) return;
        await db.ArchiveBoardAsync(board.Id);
        await LoadAsync();
    }
}
