using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CamperManagement.ViewModels;
using Avalonia.Input;

namespace CamperManagement.Views;

public partial class AddRechnungWindow : Window
{
    public AddRechnungWindow(AddRechnungViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.SetFocusToNeuTextBox = () =>
        {
            NeuTextBox.Focus();
            NeuTextBox.SelectAll();
        };
        
        // Verknüpfen der CloseAction mit der Close-Methode
        viewModel.CloseAction = Close;
    }
}