using CamperManagement.Models;
using CamperManagement.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;

namespace CamperManagement.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly Stack<object> _navigationStack = new();

    [ObservableProperty]
    private object currentView;

    [ObservableProperty]
    private bool canNavigateBack;

    public IRelayCommand NavigateBackCommand { get; }
    public IRelayCommand<object> NavigateToCommand { get; }

    public MainViewModel()
    {
        // Standard-Tabs
        var camperView = new CamperViewModel(this);
        var rechnungenView = new RechnungenViewModel(this);

        CurrentView = new TabViewModel(camperView, rechnungenView);
        CanNavigateBack = false;

        NavigateBackCommand = new RelayCommand(NavigateBack, () => CanNavigateBack);
        NavigateToCommand = new RelayCommand<object>(NavigateTo);
    }

    private void NavigateTo(object view)
    {
        if (CurrentView != null)
        {
            _navigationStack.Push(CurrentView);
        }
        CurrentView = view;
        CanNavigateBack = true;
        NavigateBackCommand.NotifyCanExecuteChanged();
    }

    private void NavigateBack()
    {
        if (_navigationStack.Count > 0)
        {
            CurrentView = _navigationStack.Pop();
            CanNavigateBack = _navigationStack.Count > 0;
            NavigateBackCommand.NotifyCanExecuteChanged();
        }
    }
}
