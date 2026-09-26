using System;
using System.Threading;
using System.Threading.Tasks;
using CamperManagement.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
namespace CamperManagement.ViewModels;

public partial class ViewModelBase : ObservableObject
{
    private CancellationTokenSource? _load, _export;
    private int _statusMessageVersion;
    protected IErrorLog Log { get; }
    [ObservableProperty] private string? statusMessage;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSaving))]
    private bool isBusy;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private bool loadFailed;
    public IAsyncRelayCommand RetryLoadCommand { get; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSaving))]
    private bool isExporting;
    public bool IsSaving => IsBusy && !IsExporting;
    public IRelayCommand CancelOperationCommand { get; }
    public ViewModelBase(IErrorLog? log = null)
    {
        Log = log ?? NullErrorLog.Instance;
        RetryLoadCommand = new AsyncRelayCommand(ResumeAsync, () => !IsBusy && !IsLoading);
        PropertyChanged += (_, e) => { if (e.PropertyName is nameof(IsBusy) or nameof(IsLoading)) RetryLoadCommand.NotifyCanExecuteChanged(); };
        CancelOperationCommand = new RelayCommand(() => { _load?.Cancel(); _export?.Cancel(); }, () => _load != null || _export != null);
    }
    partial void OnStatusMessageChanged(string? value) => ++_statusMessageVersion;
    protected void ShowTemporaryStatus(string message)
    {
        StatusMessage = message;
        // Reset the deadline even when the same success message is shown again.
        _ = ClearTemporaryStatusAsync(++_statusMessageVersion);
    }
    private async Task ClearTemporaryStatusAsync(int version)
    {
        await Task.Delay(TimeSpan.FromSeconds(5));
        // A previous notification must never clear a newer status or error.
        if (version == _statusMessageVersion)
            StatusMessage = null;
    }
    public virtual Task InitializeAsync() => Task.CompletedTask;
    public virtual Task ResumeAsync() => Task.CompletedTask;
    public virtual void Deactivate() => _load?.Cancel();
    protected async Task RunLoadAsync(Func<CancellationToken, Task> action)
    {
        _load?.Cancel();
        using var source = new CancellationTokenSource();
        _load = source;
        IsLoading = true;
        LoadFailed = false;
        StatusMessage = null;
        CancelOperationCommand.NotifyCanExecuteChanged();
        try { await action(source.Token); }
        catch (OperationCanceledException) when (source.IsCancellationRequested)
        {
            if (ReferenceEquals(_load, source)) { LoadFailed = true; StatusMessage = "Laden abgebrochen."; }
        }
        catch (Exception ex)
        {
            Log.Write(ErrorOperation.Load, ex);
            if (ReferenceEquals(_load, source) && !source.IsCancellationRequested)
            {
                LoadFailed = true;
                StatusMessage = "Daten konnten nicht geladen werden. Bitte erneut laden.";
            }
        }
        finally
        {
            if (ReferenceEquals(_load, source))
            {
                _load = null;
                IsLoading = false;
                CancelOperationCommand.NotifyCanExecuteChanged();
            }
        }
    }
    // A DB commit is deliberately not interruptible through the UI: its result must be known.
    protected Task<bool> RunAsync(Func<Task> action) => IsLoading ? Task.FromResult(false) : RunOperationAsync(action, ErrorOperation.Save);
    protected async Task<bool> RunExportAsync(Func<CancellationToken, Task> action)
    {
        if (IsBusy || IsLoading) return false;
        using var source = new CancellationTokenSource();
        _export = source;
        IsExporting = true;
        CancelOperationCommand.NotifyCanExecuteChanged();
        try { return await RunOperationAsync(() => action(source.Token), ErrorOperation.Export); }
        finally { _export = null; IsExporting = false; CancelOperationCommand.NotifyCanExecuteChanged(); }
    }
    protected void CompleteExportCancellation()
    {
        // All files are saved. From here the short DB transaction must finish atomically.
        _export = null;
        IsExporting = false;
        CancelOperationCommand.NotifyCanExecuteChanged();
    }
    private async Task<bool> RunOperationAsync(Func<Task> action, ErrorOperation operation)
    {
        if (IsBusy) return false;
        IsBusy = true;
        StatusMessage = null;
        try { await action(); return true; }
        catch (OperationCanceledException) when (_export?.IsCancellationRequested == true)
        { StatusMessage = "Export abgebrochen. Druckstatus unverändert. Dateien können unvollständig sein; bitte erneut exportieren."; return false; }
        catch (Exception ex)
        {
            Log.Write(operation, ex);
            StatusMessage = ex is ArgumentException or InvalidOperationException ? ex.Message : "Der Vorgang ist fehlgeschlagen. Bitte Verbindung und Eingaben prüfen und erneut versuchen.";
            return false;
        }
        finally { IsBusy = false; }
    }
}
