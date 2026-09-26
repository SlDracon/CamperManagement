using Avalonia;
using Avalonia.Controls;
using CamperManagement.ViewModels;
namespace CamperManagement.Views;

public partial class AddRechnungView : UserControl
{
    private AddRechnungViewModel? _model;
    public AddRechnungView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => BindFocus();
        AttachedToVisualTree += (_, _) => BindFocus();
        DetachedFromVisualTree += (_, _) => Unbind();
    }
    private void Unbind()
    {
        if (_model != null)
            _model.SetFocusToNeuTextBox = null;
        _model = null;
    }
    private void BindFocus()
    {
        Unbind();
        if (DataContext is AddRechnungViewModel vm)
        {
            _model = vm;
            vm.SetFocusToNeuTextBox = () => { NeuTextBox.Focus(); NeuTextBox.SelectAll(); };
        }
    }
}
