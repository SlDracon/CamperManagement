using System;
using System.Globalization;
using System.Threading.Tasks;
using CamperManagement.Models;
using CamperManagement.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
namespace CamperManagement.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IDatabaseService _db;
    private Standardfaktoren? _loaded;
    private int _request;
    private bool _active = true;
    [ObservableProperty] private string stromText = "";
    [ObservableProperty] private string wasserText = "";
    public Task LoadTask { get; private set; } = Task.CompletedTask;
    public IAsyncRelayCommand LoadCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IRelayCommand? ConnectionCommand { get; }
    public bool CanConfigureConnection => ConnectionCommand != null;
    public SettingsViewModel(IDatabaseService db, Action? configureConnection = null)
    {
        _db = db;
        if (configureConnection != null)
            ConnectionCommand = new RelayCommand(configureConnection);
        LoadCommand = new AsyncRelayCommand(() => LoadTask = LoadAsync(), () => !IsBusy);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy && _loaded != null);
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IsBusy))
            {
                SaveCommand.NotifyCanExecuteChanged();
                LoadCommand.NotifyCanExecuteChanged();
            }
        };
    }
    public override Task InitializeAsync()
    {
        _active = true;
        return LoadTask = LoadAsync();
    }
    public override Task ResumeAsync() => InitializeAsync();
    public override void Deactivate()
    {
        _active = false;
        ++_request;
    }
    private async Task LoadAsync()
    {
        var request = ++_request;
        _loaded = null;
        IsBusy = true;
        StatusMessage = null;
        SaveCommand.NotifyCanExecuteChanged();
        try
        {
            var values = await _db.GetStandardfaktorenAsync();
            if (request != _request || !_active)
                return;
            _loaded = values;
            StromText = values.Strom.ToString(CultureInfo.GetCultureInfo("de-DE"));
            WasserText = values.Wasser.ToString(CultureInfo.GetCultureInfo("de-DE"));
        }
        catch (Exception)
        {
            if (request == _request && _active)
                StatusMessage = "Die Faktoren konnten nicht geladen werden. Bitte erneut versuchen.";
        }
        finally
        {
            if (request == _request)
                IsBusy = false;
        }
    }
    private Task SaveAsync() => RunAsync(async () =>
    {
        if (_loaded == null)
            throw new InvalidOperationException("Bitte zuerst die Faktoren laden.");
        if (!BillingRules.TryDecimal(StromText, out var strom) || !BillingRules.TryDecimal(WasserText, out var wasser))
            throw new ArgumentException("Bitte für Wasser und Strom gültige Zahlen eingeben.");
        var saved = _loaded with { Strom = strom, Wasser = wasser };
        await _db.SaveStandardfaktorenAsync(saved);
        _loaded = saved with { Version = saved.Version + 1 };
        StatusMessage = "Gespeichert. Die Faktoren gelten ab sofort für neue Rechnungen.";
    });
}
