using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CamperManagement.ViewModels;

namespace CamperManagement.Views;

public partial class AddCamperView : UserControl
{
    public AddCamperView(AddCamperViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;

        if (DataContext is AddCamperViewModel vm)
        {
            //vm.CloseAction = Close;
        }
    }

    public AddCamperView()
    {
        InitializeComponent();
    }

    private void OnCancelButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        //Close(); // Schließt das Fenster
    }
}