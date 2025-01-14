using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CamperManagement.Models;
using CamperManagement.Services;
using CamperManagement.ViewModels;

namespace CamperManagement.Views;

public partial class RechnungenView : UserControl
{
    public RechnungenView()
    {
        InitializeComponent();
    }

    private async void OnRechnungDoubleTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not DataGrid dataGrid || dataGrid.SelectedItem is not RechnungDisplayModel selectedRechnung) return;
        var viewModel = new EditRechnungViewModel(new DatabaseService(), selectedRechnung);
        var editRechnungWindow = new EditRechnungWindow(viewModel);

        await editRechnungWindow.ShowDialog(Avalonia.Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null);
    }
}