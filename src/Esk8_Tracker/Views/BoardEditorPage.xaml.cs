using Esk8_Tracker.Core.Models;
using Esk8_Tracker.ViewModels;

namespace Esk8_Tracker.Views;

public partial class BoardEditorPage : ContentPage
{
    private readonly BoardEditorViewModel _vm;

    public BoardEditorPage(BoardEditorViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
        _vm.RequestClose += async () =>
        {
            if (Navigation.ModalStack.Count > 0)
                await Navigation.PopModalAsync();
        };
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(BoardEditorViewModel.WheelType) or nameof(BoardEditorViewModel.Color) or nameof(BoardEditorViewModel.ColorHex))
                UpdateSelection();
        };
    }

    public void StartNew() { _vm.StartNew(); UpdateSelection(); }
    public void StartEdit(Board board) { _vm.StartEdit(board); UpdateSelection(); }

    private void UpdateSelection()
    {
        RadioStreet.IsVisible = _vm.WheelType == WheelTypes.Street;
        RadioAt.IsVisible = _vm.WheelType == WheelTypes.AllTerrain;
        RadioCustom.IsVisible = _vm.WheelType == WheelTypes.Custom;
        Highlight(WheelStreet, RadioStreet.IsVisible);
        Highlight(WheelAt, RadioAt.IsVisible);
        Highlight(WheelCustom, RadioCustom.IsVisible);
    }

    private static void Highlight(Border row, bool selected)
    {
        row.BackgroundColor = selected ? (Color)App.Res("SurfaceElevated") : (Color)App.Res("SurfaceCard");
        row.Stroke = selected ? (Color)App.Res("BorderStrong") : (Color)App.Res("BorderDefault");
    }
}
