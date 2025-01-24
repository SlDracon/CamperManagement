using CommunityToolkit.Mvvm.ComponentModel;

namespace CamperManagement.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private int selectedTabIndex;

    public object CamperView { get; }
    public object RechnungenView { get; }

    public MainViewModel()
    {
        CamperView = new CamperViewModel();
        RechnungenView = new RechnungenViewModel();
        SelectedTabIndex = 0; // Standardtab
    }
}
