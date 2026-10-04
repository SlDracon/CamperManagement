using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CamperManagement.Models;
using CamperManagement.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CamperManagement.ViewModels;

public partial class ContractCostIncreaseViewModel : ViewModelBase
{
    private readonly MainViewModel _main;
    private readonly int _camperId;
    private readonly string? _place;
    private bool _loaded, _booked;
    [ObservableProperty] private string identity;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentCostDisplay))]
    [NotifyPropertyChangedFor(nameof(NewTotalDisplay))]
    private decimal currentCost;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NewTotalDisplay))]
    private string increaseText = "";
    [ObservableProperty] private string description = "";
    public string CurrentCostDisplay => Money(CurrentCost);
    public string NewTotalDisplay
    {
        get
        {
            if (!BillingRules.TryDecimal(IncreaseText, out var increase)) return "–";
            try { return Money(ContractCostRules.NewTotal(CurrentCost, increase)); }
            catch (ArgumentException) { return "–"; }
        }
    }
    public bool CanEdit => _loaded && !_booked && !IsBusy && !IsLoading && !LoadFailed;
    public IAsyncRelayCommand BookCommand { get; }
    public IAsyncRelayCommand ReloadCommand { get; }
    public IRelayCommand HistoryCommand { get; }
    public ContractCostIncreaseViewModel(MainViewModel main, CamperDisplayModel camper) : base(main.ErrorLog)
    {
        _main = main; _camperId = camper.Id; _place = camper.Platznr;
        identity = DescribeCamper(camper); currentCost = camper.Vertragskosten;
        BookCommand = new AsyncRelayCommand(BookAsync, () => CanEdit);
        ReloadCommand = new AsyncRelayCommand(InitializeAsync, () => !IsBusy && !IsLoading && !_booked);
        HistoryCommand = new RelayCommand(() => main.NavigateToCommand.Execute(new CamperHistoryViewModel(main.Database, _place, Log)), () => !IsBusy);
        PropertyChanged += (_, e) => { if (e.PropertyName is nameof(IsBusy) or nameof(IsLoading) or nameof(LoadFailed)) RefreshCommands(); };
    }
    private static string Money(decimal value) => value.ToString("N2", CultureInfo.GetCultureInfo("de-DE")) + " €";
    private static string DescribeCamper(CamperDisplayModel c) => $"Platz {c.Platznr} · {c.Vorname} {c.Nachname}" + (c.HatZweitenVertragsnehmer ? $" und {c.ZweiterName}" : "");
    private void RefreshCommands()
    {
        OnPropertyChanged(nameof(CanEdit));
        BookCommand.NotifyCanExecuteChanged(); ReloadCommand.NotifyCanExecuteChanged(); HistoryCommand.NotifyCanExecuteChanged();
    }
    public override Task InitializeAsync() => RunLoadAsync(async token =>
    {
        _loaded = false;
        var rows = await _main.Database.GetActiveCampersAsync(token);
        token.ThrowIfCancellationRequested();
        var camper = rows.SingleOrDefault(c => c.Id == _camperId && c.Platznr == _place)
            ?? throw new InvalidOperationException("Der Camper ist nicht mehr aktiv.");
        CurrentCost = camper.Vertragskosten; Identity = DescribeCamper(camper);
        _loaded = true;
    });
    public override Task ResumeAsync() => InitializeAsync();
    private Task BookAsync()
    {
        if (!CanEdit) return Task.CompletedTask;
        return RunAsync(async () =>
        {
            if (!BillingRules.TryDecimal(IncreaseText, out var increase))
                throw new ArgumentException("Bitte die Erhöhung als Eurobetrag eingeben.");
            ContractCostRules.NewTotal(CurrentCost, increase);
            var reason = ContractCostRules.Description(Description);
            await _main.Database.IncreaseContractCostAsync(_camperId, _place, CurrentCost, BillingRules.Round(increase), reason);
            _booked = true;
            RefreshCommands();
            _main.ReturnFrom(this);
        });
    }
}
