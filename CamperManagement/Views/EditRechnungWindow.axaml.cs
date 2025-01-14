using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CamperManagement.ViewModels;

namespace CamperManagement.Views;

public partial class EditRechnungWindow : Window
{
    public EditRechnungWindow(EditRechnungViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        if (DataContext is EditRechnungViewModel vm)
        {
            vm.CloseAction = Close;
        }
    }

    private void OnCancelButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}