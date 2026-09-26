using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using CamperManagement.Models;
using CamperManagement.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
namespace CamperManagement.ViewModels;

public partial class CamperFormViewModel : ViewModelBase
{
    private readonly MainViewModel _main; private readonly IDatabaseService _db; private readonly int _id;
    public ObservableCollection<string> Platznummern { get; } = new();
    public ObservableCollection<string> Anreden { get; } = new() { "Frau", "Herr", "Eheleute" };
    [ObservableProperty] private string? selectedPlatznummer;
    public string? Platznr
    {
        get => SelectedPlatznummer; set => SelectedPlatznummer = value;
    }
    [ObservableProperty] private string? selectedAnrede;
    [ObservableProperty] private string? vorname;
    [ObservableProperty] private string? nachname;
    [ObservableProperty] private string? straße;
    [ObservableProperty] private string? plz;
    [ObservableProperty] private string? ort;
    [ObservableProperty] private string? email;
    [ObservableProperty] private string vertragskostenText = "";
    public Func<Task>? OnSavedAsync
    {
        get; set;
    }
    public IAsyncRelayCommand SaveCommand
    {
        get;
    }
    public IRelayCommand CancelCommand
    {
        get;
    }
    public CamperFormViewModel(MainViewModel main, IDatabaseService db, CamperDisplayModel? camper = null)
    {
        _main = main;
        _db = db;
        _id = camper?.Id ?? 0;
        if (camper != null)
        {
            SelectedPlatznummer = camper.Platznr;
            SelectedAnrede = camper.Anrede;
            Vorname = camper.Vorname;
            Nachname = camper.Nachname;
            Straße = camper.Straße;
            Plz = camper.PLZ;
            Ort = camper.Ort;
            Email = camper.Email;
            VertragskostenText = camper.Vertragskosten.ToString(CultureInfo.GetCultureInfo("de-DE"));
        }
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
        CancelCommand = new RelayCommand(() => _main.NavigateBackCommand.Execute(null), () => !IsBusy);
        PropertyChanged += (_, e) => { if (e.PropertyName == nameof(IsBusy)) { SaveCommand.NotifyCanExecuteChanged(); CancelCommand.NotifyCanExecuteChanged(); } };
    }
    public override Task InitializeAsync() => _id != 0 ? Task.CompletedTask : RunAsync(async () => { var places = await _db.GetPlatznummernAsync(); Platznummern.Clear(); foreach (var p in places) Platznummern.Add(p); });
    private Task SaveAsync() => RunAsync(async () =>
    {
        decimal cost = 0;
        if (!string.IsNullOrWhiteSpace(VertragskostenText) && !BillingRules.TryDecimal(VertragskostenText, out cost))
            throw new ArgumentException("Vertragskosten müssen eine Zahl sein oder leer bleiben.");
        var value = new CamperDisplayModel { Id = _id, Platznr = SelectedPlatznummer, Anrede = SelectedAnrede, Vorname = Vorname, Nachname = Nachname, Straße = Straße, PLZ = Plz, Ort = Ort, Email = Email, Vertragskosten = cost };
        BillingRules.ValidateCamper(value);
        if (_id == 0)
            await _db.AddNewCamperAsync(value);
        else
            await _db.UpdateCamperAsync(value);
        if (OnSavedAsync != null)
            await OnSavedAsync();
        _main.ReturnFrom(this);
    });
}
