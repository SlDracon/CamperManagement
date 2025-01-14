using CamperManagement.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CamperManagement.ViewModels
{
    public partial class MainNavigationViewModel : ObservableObject
    {
        [ObservableProperty]
        private int selectedTabIndex;

        public object CamperView { get; }
        public object RechnungenView { get; }

        public MainNavigationViewModel()
        {
            CamperView = new MainViewModel();
            RechnungenView = new RechnungenViewModel();
            SelectedTabIndex = 0; // Standardtab
        }
    }

}
