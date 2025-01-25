using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CamperManagement.ViewModels;
using Avalonia.Input;

namespace CamperManagement.Views;

public partial class AddRechnungView : UserControl
{
    public AddRechnungView()
    {
        InitializeComponent();

        if (DataContext is AddRechnungViewModel addRechnungViewModel)
        {
            addRechnungViewModel.SetFocusToNeuTextBox = () =>
            {
                NeuTextBox.Focus();
                NeuTextBox.SelectAll();
            };
        }
    }
}