using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CamperManagement.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
namespace CamperManagement.ViewModels;

public partial class MainViewModel : ViewModelBase
{
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
    [ObservableProperty] private object currentView;
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
    public MainViewModel() : this(new DatabaseService()) { }
    public MainViewModel(IDatabaseService database, IPdfExporter? pdf = null, TimeProvider? clock = null)
    {
        Database = database;
        Pdf = pdf ?? new PdfExporter();
        Clock = clock ?? TimeProvider.System;
        currentView = new TabViewModel(new CamperViewModel(this), new RechnungenViewModel(this));
        NavigateBackCommand = new RelayCommand(Back, () => CanNavigateBack);
        NavigateToCommand = new RelayCommand<object>(Navigate);
        SettingsCommand = new RelayCommand(() => Navigate(new SettingsViewModel(Database)));
    }
    public override Task InitializeAsync() => RunAsync(async () => { if (CurrentView is TabViewModel tabs) { await ((ViewModelBase)tabs.CamperView).InitializeAsync(); await ((ViewModelBase)tabs.RechnungenView).InitializeAsync(); } });
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
        if (CurrentView is ViewModelBase previous)
            previous.Deactivate();
        _navigationStack.Push(CurrentView);
        CurrentView = view;
        UpdateNavigation();
        NavigationTask = view is ViewModelBase vm ? vm.InitializeAsync() : Task.CompletedTask;
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
        if (CurrentView is ViewModelBase previous)
            previous.Deactivate();
        CurrentView = _navigationStack.Pop();
        UpdateNavigation();
        NavigationTask = CurrentView is ViewModelBase resumed ? resumed.ResumeAsync() : Task.CompletedTask;
    }
    private void UpdateNavigation()
    {
        CanNavigateBack = _navigationStack.Count > 0;
        NavigateBackCommand.NotifyCanExecuteChanged();
    }
}
