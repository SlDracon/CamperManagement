using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CamperManagement.ViewModels;

namespace CamperManagement.Views;

public partial class AddCamperWindow : Window
{
    public AddCamperWindow(AddCamperViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;

        if (DataContext is AddCamperViewModel vm)
        {
            vm.CloseAction = Close;
        }
    }

    private void OnCancelButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(); // Schlieﬂt das Fenster
    }
}