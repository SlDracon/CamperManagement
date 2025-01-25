using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CamperManagement.ViewModels;

namespace CamperManagement.Views;

public partial class EditCamperView : UserControl
{
    public EditCamperView()
    {
        InitializeComponent();
    }

    private void OnCancelButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        //Close();
    }
}