using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CamperManagement.ViewModels;

namespace CamperManagement.Views;

public partial class EditRechnungView : UserControl
{
    public EditRechnungView()
    {
        InitializeComponent();
    }

    private void OnCancelButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        //Close();
    }
}