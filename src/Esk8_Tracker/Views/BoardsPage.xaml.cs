using Esk8_Tracker.ViewModels;

namespace Esk8_Tracker.Views;

public partial class BoardsPage : ContentPage
{
    private readonly BoardsViewModel _viewModel;

    public BoardsPage(BoardsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }
}
