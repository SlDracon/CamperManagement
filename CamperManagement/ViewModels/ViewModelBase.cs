using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
namespace CamperManagement.ViewModels;

public partial class ViewModelBase : ObservableObject
{
    [ObservableProperty] private string? statusMessage;
    [ObservableProperty] private bool isBusy;
    public virtual Task InitializeAsync() => Task.CompletedTask;
    public virtual Task ResumeAsync() => Task.CompletedTask;
    public virtual void Deactivate()
    {
    }
    protected async Task<bool> RunAsync(Func<Task> action)
    {
        if (IsBusy)
            return false;
        IsBusy = true;
        StatusMessage = null;
        try
        {
            await action();
            return true;
        }
        catch (Exception ex) { StatusMessage = ex is ArgumentException or InvalidOperationException ? ex.Message : "Der Vorgang ist fehlgeschlagen. Bitte Verbindung und Eingaben prüfen und erneut versuchen."; return false; }
        finally { IsBusy = false; }
    }
}
