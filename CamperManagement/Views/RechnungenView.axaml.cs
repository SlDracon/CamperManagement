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

    private void OnRechnungDoubleTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not DataGrid dataGrid || dataGrid.SelectedItem is not RechnungDisplayModel selectedRechnung) return;

        if (DataContext is RechnungenViewModel rechnungViewModel)
        {
            // Rufe die EditCamperCommand auf, die bereits mit der Logik verbunden ist
            rechnungViewModel.EditRechnungCommand.Execute(selectedRechnung);
        }
    }
}