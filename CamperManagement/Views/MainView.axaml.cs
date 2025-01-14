using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CamperManagement.Models;
using CamperManagement.Services;
using CamperManagement.ViewModels;

namespace CamperManagement.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    private async void OnCamperDoubleTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not DataGrid dataGrid || dataGrid.SelectedItem is not CamperDisplayModel selectedCamper) return;
        var viewModel = new EditCamperViewModel(new DatabaseService(), selectedCamper);
        var editCamperWindow = new EditCamperWindow(viewModel);

        await editCamperWindow.ShowDialog(Avalonia.Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null);

        // Aktualisiere die Liste nach dem Bearbeiten
        if (DataContext is MainViewModel mainViewModel)
        {
            await mainViewModel.LoadDataAsync();
        }
    }
}