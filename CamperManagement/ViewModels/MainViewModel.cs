using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CamperManagement.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
namespace CamperManagement.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public IErrorLog ErrorLog => Log;
    private readonly IDatabaseConfiguration? _configuration;
    private readonly Func<string, Task>? _checkConnection;
    private readonly Stack<object> _navigationStack = new();
    public IDatabaseService Database
    {
        get;
    }
    public IPdfExporter Pdf
    {
        get;
    }
    public TimeProvider Clock
    {
        get;
    }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOverview))]
    private object currentView;
    public bool IsOverview => CurrentView is TabViewModel;
    [ObservableProperty] private bool canNavigateBack;
    public Task NavigationTask { get; private set; } = Task.CompletedTask;
    public IRelayCommand NavigateBackCommand
    {
        get;
    }
    public IRelayCommand<object> NavigateToCommand
    {
        get;
    }
    public IRelayCommand SettingsCommand
    {
        get;
    }
    public MainViewModel(IDatabaseService database, IPdfExporter? pdf = null, TimeProvider? clock = null, IErrorLog? log = null, IDatabaseConfiguration? configuration = null, Func<string, Task>? checkConnection = null) : base(log)
    {
        Database = database;
        Pdf = pdf ?? PdfExporter.Unavailable;
        _configuration = configuration;
        _checkConnection = checkConnection;
        Clock = clock ?? TimeProvider.System;
        currentView = new TabViewModel(new CamperViewModel(this), new RechnungenViewModel(this));
        NavigateBackCommand = new RelayCommand(Back, () => CanNavigateBack);
        NavigateToCommand = new RelayCommand<object>(Navigate);
        SettingsCommand = new RelayCommand(() => Navigate(new SettingsViewModel(Database, _configuration == null ? null : () => Navigate(CreateConnectionView()), Log)));
        if (_configuration is { IsConfigured: false }) CurrentView = CreateConnectionView();
    }
    public override Task InitializeAsync() => CurrentView is TabViewModel tabs ? Task.WhenAll(((ViewModelBase)tabs.CamperView).InitializeAsync(), ((ViewModelBase)tabs.RechnungenView).InitializeAsync()) : Task.CompletedTask;
    private DatabaseConnectionViewModel CreateConnectionView() => new(_configuration ?? throw new InvalidOperationException("Konfiguration fehlt."), async () =>
    {
        _navigationStack.Clear();
        CurrentView = new TabViewModel(new CamperViewModel(this), new RechnungenViewModel(this));
        UpdateNavigation();
        await InitializeAsync();
    }, _checkConnection, Log);
    private void Navigate(object? view)
    {
        if (IsNavigationBusy())
            return;
        if (view == null)
        {
            Back();
            return;
        }
        if (ReferenceEquals(view, CurrentView))
            return;
        DeactivateCurrent();
        _navigationStack.Push(CurrentView);
        CurrentView = view;
        UpdateNavigation();
        NavigationTask = view is ViewModelBase vm ? vm.InitializeAsync() : Task.CompletedTask;
    }
    private void DeactivateCurrent()
    {
        if (CurrentView is ViewModelBase vm) vm.Deactivate();
        if (CurrentView is TabViewModel tabs)
        {
            ((ViewModelBase)tabs.CamperView).Deactivate();
            ((ViewModelBase)tabs.RechnungenView).Deactivate();
        }
    }
    private bool IsNavigationBusy() => CurrentView is ViewModelBase { IsBusy: true } || CurrentView is TabViewModel tabs && (((ViewModelBase)tabs.CamperView).IsBusy || ((ViewModelBase)tabs.RechnungenView).IsBusy);
    public void ReturnFrom(ViewModelBase origin)
    {
        if (ReferenceEquals(CurrentView, origin))
            BackCore();
    }
    private void Back()
    {
        if (!IsNavigationBusy())
            BackCore();
    }
    private void BackCore()
    {
        if (_navigationStack.Count == 0)
            return;
        DeactivateCurrent();
        CurrentView = _navigationStack.Pop();
        UpdateNavigation();
        NavigationTask = CurrentView is ViewModelBase resumed ? resumed.ResumeAsync() : InitializeAsync();
    }
    private void UpdateNavigation()
    {
        CanNavigateBack = _navigationStack.Count > 0;
        NavigateBackCommand.NotifyCanExecuteChanged();
    }
}
