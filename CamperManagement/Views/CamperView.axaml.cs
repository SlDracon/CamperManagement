using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CamperManagement.Models;
using CamperManagement.Services;
using CamperManagement.ViewModels;

namespace CamperManagement.Views;

public partial class CamperView : UserControl
{
    public CamperView()
    {
        InitializeComponent();
    }

    private async void OnCamperDoubleTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not DataGrid dataGrid || dataGrid.SelectedItem is not CamperDisplayModel selectedCamper) return;
        var viewModel = new EditCamperViewModel(new DatabaseService(), selectedCamper);
        var editCamperWindow = new EditCamperWindow(viewModel);

        await editCamperWindow.ShowDialog(Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null);

        // Aktualisiere die Liste nach dem Bearbeiten
        if (DataContext is CamperViewModel camperViewModel)
        {
            await camperViewModel.LoadDataAsync();
        }
    }
}