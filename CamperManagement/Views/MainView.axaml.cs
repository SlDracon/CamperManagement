using Avalonia.Controls;
using CamperManagement.ViewModels;

namespace CamperManagement.Views
{
    public partial class MainView : UserControl
    {
        public MainView()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }
}