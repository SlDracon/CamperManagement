using Avalonia.Controls;
using CamperManagement.ViewModels;
namespace CamperManagement.Views;

public partial class MainView : UserControl
{
    private MainViewModel? _initialized;
    public MainView()
    {
        InitializeComponent();
        SizeChanged += (_, args) => Classes.Set("compact", args.NewSize.Width < 440);
        Loaded += async (_, _) =>
        {
            if (DataContext is MainViewModel vm && !ReferenceEquals(vm, _initialized))
            {
                _initialized = vm;
                await vm.InitializeAsync();
                if (App.IsSmokeTest && Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var tabs = (TabViewModel)vm.CurrentView;
                    var success = ((ViewModelBase)tabs.CamperView).StatusMessage == null && ((ViewModelBase)tabs.RechnungenView).StatusMessage == null;
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => desktop.Shutdown(success ? 0 : 1), Avalonia.Threading.DispatcherPriority.Background);
                }
            }
        };
    }
}
