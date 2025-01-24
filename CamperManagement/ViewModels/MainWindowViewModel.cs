using CommunityToolkit.Mvvm.ComponentModel;

namespace CamperManagement.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public object MainView { get; }

    public MainWindowViewModel()
    {
        MainView = new MainViewModel();
    }
}