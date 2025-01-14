using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CamperManagement.ViewModels;

namespace CamperManagement.Views;

public partial class EditCamperWindow : Window
{
    public EditCamperWindow(EditCamperViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel; 
        
        if (DataContext is EditCamperViewModel vm)
        {
            vm.CloseAction = Close;
        }
    }

    private void OnCancelButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}