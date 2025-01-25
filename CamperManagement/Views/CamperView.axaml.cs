using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CamperManagement.Models;
using CamperManagement.Services;
using CamperManagement.ViewModels;
using System;

namespace CamperManagement.Views;

public partial class CamperView : UserControl
{
    public CamperView()
    {
        InitializeComponent();
    }

    private void OnCamperDoubleTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not DataGrid dataGrid || dataGrid.SelectedItem is not CamperDisplayModel selectedCamper) return;

        if (DataContext is CamperViewModel camperViewModel)
        {
            // Rufe die EditCamperCommand auf, die bereits mit der Logik verbunden ist
            camperViewModel.EditCamperCommand.Execute(selectedCamper);
        }
    }

}