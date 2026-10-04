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
    private readonly decimal _originalContractCost;
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
    public ObservableCollection<string> EinzelAnreden { get; } = new() { "", "Frau", "Herr" };
    public ObservableCollection<string> Rechnungsadressen { get; } = new() { "Erster Vertragsnehmer", "Zweiter Vertragsnehmer" };
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatSeparateAdresse))]
    private bool hatZweitenVertragsnehmer;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatSeparateAdresse))]
    private bool gemeinsameAdresse = true;
    [ObservableProperty] private string? zweiteAnrede;
    [ObservableProperty] private string? zweiterVorname;
    [ObservableProperty] private string? zweiterNachname;
    [ObservableProperty] private string? zweiteStraße;
    [ObservableProperty] private string? zweitePlz;
    [ObservableProperty] private string? zweiterOrt;
    [ObservableProperty] private string? zweiteEmail;
    [ObservableProperty] private int rechnungsadresseIndex;
    public bool HatSeparateAdresse => HatZweitenVertragsnehmer && !GemeinsameAdresse;
    partial void OnHatZweitenVertragsnehmerChanged(bool value)
    {
        if (!value) RechnungsadresseIndex = 0;
    }
    partial void OnGemeinsameAdresseChanged(bool value)
    {
        if (value) RechnungsadresseIndex = 0;
    }
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
    public CamperFormViewModel(MainViewModel main, IDatabaseService db, CamperDisplayModel? camper = null) : base(main.ErrorLog)
    {
        _main = main;
        _db = db;
        _id = camper?.Id ?? 0;
        _originalContractCost = camper?.Vertragskosten ?? 0;
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
            HatZweitenVertragsnehmer = camper.HatZweitenVertragsnehmer;
            GemeinsameAdresse = camper.GemeinsameAdresse;
            ZweiteAnrede = camper.ZweiteAnrede;
            ZweiterVorname = camper.ZweiterVorname;
            ZweiterNachname = camper.ZweiterNachname;
            ZweiteStraße = camper.ZweiteStraße;
            ZweitePlz = camper.ZweitePLZ;
            ZweiterOrt = camper.ZweiterOrt;
            ZweiteEmail = camper.ZweiteEmail;
            RechnungsadresseIndex = camper.NutztZweiteRechnungsadresse ? 1 : 0;
            VertragskostenText = camper.Vertragskosten.ToString(CultureInfo.GetCultureInfo("de-DE"));
        }
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy && !IsLoading);
        CancelCommand = new RelayCommand(() => _main.NavigateBackCommand.Execute(null), () => !IsBusy);
        PropertyChanged += (_, e) => { if (e.PropertyName is nameof(IsBusy) or nameof(IsLoading)) { SaveCommand.NotifyCanExecuteChanged(); CancelCommand.NotifyCanExecuteChanged(); } };
    }
    public override Task InitializeAsync() => _id != 0 ? Task.CompletedTask : RunLoadAsync(async token => { var places = await _db.GetPlatznummernAsync(token); token.ThrowIfCancellationRequested(); Platznummern.Clear(); foreach (var p in places) Platznummern.Add(p); });
    public override Task ResumeAsync() => InitializeAsync();
    private Task SaveAsync() => RunAsync(async () =>
    {
        decimal cost = 0;
        if (!string.IsNullOrWhiteSpace(VertragskostenText) && !BillingRules.TryDecimal(VertragskostenText, out cost))
            throw new ArgumentException("Vertragskosten müssen eine Zahl sein oder leer bleiben.");
        var value = new CamperDisplayModel { Id = _id, Platznr = SelectedPlatznummer, Anrede = SelectedAnrede, Vorname = Vorname, Nachname = Nachname, Straße = Straße, PLZ = Plz, Ort = Ort, Email = Email, Vertragskosten = cost,
            HatZweitenVertragsnehmer = HatZweitenVertragsnehmer, GemeinsameAdresse = !HatZweitenVertragsnehmer || GemeinsameAdresse,
            ZweiteAnrede = ZweiteAnrede, ZweiterVorname = ZweiterVorname, ZweiterNachname = ZweiterNachname,
            ZweiteStraße = ZweiteStraße, ZweitePLZ = ZweitePlz, ZweiterOrt = ZweiterOrt, ZweiteEmail = ZweiteEmail,
            RechnungsadresseZweiterVertragsnehmer = HatSeparateAdresse && RechnungsadresseIndex == 1 };
        BillingRules.ValidateCamper(value);
        if (_id == 0)
            await _db.AddNewCamperAsync(value);
        else
            await _db.UpdateCamperAsync(value, _originalContractCost);
        if (OnSavedAsync != null)
            await OnSavedAsync();
        _main.ReturnFrom(this);
    });
}
