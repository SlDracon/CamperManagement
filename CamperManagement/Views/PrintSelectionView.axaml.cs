using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CamperManagement.ViewModels;

namespace CamperManagement.Views;

public partial class PrintSelectionView : UserControl
{
    public PrintSelectionView()
    {
        InitializeComponent();

        //viewModel.CloseAction = Close; // Verknüpfe die CloseAction mit der Close-Methode des Fensters
    }

    private void OnCancelButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        //Close();
    }
}