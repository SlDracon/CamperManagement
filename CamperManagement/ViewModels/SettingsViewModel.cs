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
    [ObservableProperty] private string stromText = "";
    [ObservableProperty] private string wasserText = "";
    public Task LoadTask { get; private set; } = Task.CompletedTask;
    public IAsyncRelayCommand LoadCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IRelayCommand? ConnectionCommand { get; }
    public bool CanEdit => !IsBusy && !IsLoading && _loaded != null;
    public bool CanConfigureConnection => ConnectionCommand != null;
    public SettingsViewModel(IDatabaseService db, Action? configureConnection = null, IErrorLog? log = null) : base(log)
    {
        _db = db;
        if (configureConnection != null)
            ConnectionCommand = new RelayCommand(configureConnection);
        LoadCommand = new AsyncRelayCommand(() => LoadTask = LoadAsync(), () => !IsBusy);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy && !IsLoading && _loaded != null);
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(IsBusy) or nameof(IsLoading))
            {
                OnPropertyChanged(nameof(CanEdit));
                SaveCommand.NotifyCanExecuteChanged();
                LoadCommand.NotifyCanExecuteChanged();
            }
        };
    }
    public override Task InitializeAsync() => LoadTask = LoadAsync();
    public override Task ResumeAsync() => InitializeAsync();
    private Task LoadAsync() => RunLoadAsync(async token =>
    {
        _loaded = null;
        SaveCommand.NotifyCanExecuteChanged();
        var values = await _db.GetStandardfaktorenAsync(token);
        token.ThrowIfCancellationRequested();
        _loaded = values;
        StromText = values.Strom.ToString(CultureInfo.GetCultureInfo("de-DE"));
        WasserText = values.Wasser.ToString(CultureInfo.GetCultureInfo("de-DE"));
    });
    private Task SaveAsync() => RunAsync(async () =>
    {
        if (_loaded == null || IsLoading)
            throw new InvalidOperationException("Bitte zuerst die Faktoren laden.");
        if (!BillingRules.TryDecimal(StromText, out var strom) || !BillingRules.TryDecimal(WasserText, out var wasser))
            throw new ArgumentException("Bitte für Wasser und Strom gültige Zahlen eingeben.");
        var saved = _loaded with { Strom = strom, Wasser = wasser };
        await _db.SaveStandardfaktorenAsync(saved);
        _loaded = saved with { Version = saved.Version + 1 };
        StatusMessage = "Gespeichert. Die Faktoren gelten ab sofort für neue Rechnungen.";
    });
}
