using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using CamperManagement.Models;
using CamperManagement.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
namespace CamperManagement.ViewModels;

public partial class InvoiceFormViewModel : ViewModelBase
{
    private readonly MainViewModel _main;
    private readonly IDatabaseService _db;
    private readonly int? _id;
    private bool _ready, _parsing, _active = true, _selectionValid;
    private int _selectionVersion;
    private string? _factorArt; private long? _factorVersion; private bool _calculationValid = true;
    public ObservableCollection<string> Arten { get; } = new() { "Strom", "Wasser" };
    public ObservableCollection<string> Platznummern { get; } = new();
    public ObservableCollection<int> Jahre
    {
        get;
    }
    [ObservableProperty] private string? selectedArt = "Strom";
    [ObservableProperty] private string? selectedPlatznummer;
    [ObservableProperty] private decimal alt;
    [ObservableProperty] private decimal neu;
    [ObservableProperty] private decimal verbrauch;
    [ObservableProperty] private decimal faktor = 0.5m;
    [ObservableProperty] private decimal betrag;
    [ObservableProperty] private int jahr;
    [ObservableProperty] private string neuText = "0";
    [ObservableProperty] private string faktorText = "0,5";
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string? warningMessage;
    public Task SelectionTask { get; private set; } = Task.CompletedTask;
    public Action? SetFocusToNeuTextBox
    {
        get; set;
    }
    public Func<Task>? OnSavedAsync
    {
        get; set;
    }
    public IAsyncRelayCommand SaveCommand
    {
        get;
    }
    public IAsyncRelayCommand SaveAndCloseCommand
    {
        get;
    }
    public IRelayCommand CancelCommand
    {
        get;
    }
    public InvoiceFormViewModel(MainViewModel main, IDatabaseService db, RechnungDisplayModel? invoice = null)
    {
        _main = main;
        _db = db;
        _id = invoice?.Id;
        var year = main.Clock.GetLocalNow().Year;
        Jahre = new() { year, year - 1 };
        Jahr = invoice?.Jahr ?? year;
        if (invoice != null)
        {
            SelectedArt = invoice.Art;
            SelectedPlatznummer = invoice.Platznr;
            Alt = invoice.Alt;
            Neu = invoice.Neu;
            Verbrauch = invoice.Verbrauch;
            Faktor = invoice.Faktor;
            Betrag = invoice.Betrag;
        }
        NeuText = Neu.ToString(CultureInfo.GetCultureInfo("de-DE"));
        FaktorText = Faktor.ToString(CultureInfo.GetCultureInfo("de-DE"));
        SaveCommand = new AsyncRelayCommand(() => SaveAsync(false), CanSave);
        SaveAndCloseCommand = new AsyncRelayCommand(() => SaveAsync(true), CanSave);
        CancelCommand = new RelayCommand(() => _main.NavigateBackCommand.Execute(null), () => !IsBusy);
        PropertyChanged += (_, e) => { if (e.PropertyName == nameof(IsBusy)) { NotifyCommands(); CancelCommand.NotifyCanExecuteChanged(); } };
        _ready = true;
        _selectionValid = _id != null;
        if (_id != null)
        {
            _factorArt = SelectedArt;
        }
        UpdateWarning();
    }
    public override async Task InitializeAsync()
    {
        _active = true;
        if (_id != null)
            return; // Editing preserves the original invoice tariff.
        await RunAsync(async () =>
        {
            var places = await _db.GetPlatznummernAsync();
            if (!_active)
                return;
            Platznummern.Clear();
            foreach (var p in places)
                Platznummern.Add(p);
            await (SelectionTask = RefreshSelectionAsync());
        });
    }
    public override void Deactivate()
    {
        _active = false;
        if (IsLoading)
            _selectionValid = false;
        ++_selectionVersion;
        SetFocusToNeuTextBox = null;
    }
    partial void OnSelectedArtChanged(string? value)
    {
        if (_ready)
            SelectionTask = RefreshSelectionAsync();
        NotifyCommands();
    }
    partial void OnSelectedPlatznummerChanged(string? value)
    {
        if (_ready)
            SelectionTask = RefreshSelectionAsync(false);
        NotifyCommands();
    }
    partial void OnJahrChanged(int value)
    {
        NotifyCommands();
    }
    partial void OnAltChanged(decimal value)
    {
        if (_ready)
            Recalculate();
    }
    partial void OnNeuChanged(decimal value)
    {
        if (!_parsing)
            NeuText = value.ToString(CultureInfo.GetCultureInfo("de-DE"));
        if (_ready)
            Recalculate();
    }
    partial void OnVerbrauchChanged(decimal value)
    {
        if (_ready)
            RecalculateAmount();
    }
    partial void OnFaktorChanged(decimal value)
    {
        if (!_parsing)
            FaktorText = value.ToString(CultureInfo.GetCultureInfo("de-DE"));
    } // Deliberately no recalculation.
    partial void OnNeuTextChanged(string value)
    {
        _parsing = true;
        try
        {
            if (BillingRules.TryDecimal(value, out var d))
                Neu = d;
        }
        finally { _parsing = false; }
        NotifyCommands();
    }
    partial void OnFaktorTextChanged(string value)
    {
        _parsing = true;
        try
        {
            if (BillingRules.TryDecimal(value, out var d))
                Faktor = d;
        }
        finally { _parsing = false; }
        NotifyCommands();
    }
    partial void OnIsLoadingChanged(bool value) => NotifyCommands();
    public string? ValidationMessage => !BillingRules.TryDecimal(NeuText, out _) ? "Bitte einen gültigen neuen Zählerstand eingeben." : !BillingRules.TryDecimal(FaktorText, out _) ? "Bitte einen gültigen Faktor eingeben." : null;
    private void NotifyCommands()
    {
        OnPropertyChanged(nameof(ValidationMessage));
        SaveCommand?.NotifyCanExecuteChanged();
        SaveAndCloseCommand?.NotifyCanExecuteChanged();
    }
    private void UpdateWarning() => WarningMessage = Neu < Alt ? "Warnung: Der neue Zählerstand ist kleiner als der alte. Speichern ist weiterhin möglich." : null;
    private void Recalculate()
    {
        try
        {
            _calculationValid = true;
            Verbrauch = checked(Neu - Alt);
            RecalculateAmount();
            UpdateWarning();
        }
        catch (OverflowException) { _calculationValid = false; NotifyCommands(); StatusMessage = "Die eingegebenen Werte sind zu groß."; }
    }
    private void RecalculateAmount()
    {
        try
        {
            Betrag = BillingRules.Amount(Verbrauch, Faktor);
            _calculationValid = true;
        }
        catch (OverflowException) { _calculationValid = false; NotifyCommands(); StatusMessage = "Der Rechnungsbetrag ist zu groß."; }
    }
    private async Task RefreshSelectionAsync(bool loadFactor = true)
    {
        var request = ++_selectionVersion;
        var art = SelectedArt;
        var place = SelectedPlatznummer;
        var year = Jahr;
        IsLoading = true;
        _selectionValid = false;
        try
        {
            if (art is not ("Strom" or "Wasser") || year < 1901 || year > 2155)
                return;
            var factors = (_id == null || loadFactor || _factorArt != art) ? await _db.GetStandardfaktorenAsync() : null;
            var previous = string.IsNullOrWhiteSpace(place) ? 0 : await _db.GetNeuFromLatestRechnungAsync(place, art);
            if (request != _selectionVersion || !_active)
                return;
            if (factors != null && (loadFactor || _factorArt != art || _factorVersion != factors.Version))
            {
                Faktor = factors.ForArt(art);
                _factorArt = art;
                _factorVersion = factors.Version;
            }
            if (_id == null)
                Alt = previous;
            Recalculate();
            _selectionValid = true;
            StatusMessage = null;
            SetFocusToNeuTextBox?.Invoke();
        }
        catch (Exception) { if (request == _selectionVersion && _active) StatusMessage = "Zählerstand oder Faktoren konnten nicht geladen werden. Bitte erneut laden."; }
        finally { if (request == _selectionVersion) { IsLoading = false; NotifyCommands(); } }
    }
    public override Task ResumeAsync()
    {
        _active = true;
        return _id != null && _selectionValid ? Task.CompletedTask : (SelectionTask = RefreshSelectionAsync(false));
    }
    private bool CanSave() => _active && _selectionValid && _calculationValid && !IsBusy && !IsLoading && !string.IsNullOrWhiteSpace(SelectedPlatznummer) && SelectedArt is "Strom" or "Wasser" && (_id != null || BillingRules.IsAllowedYear(Jahr, _main.Clock)) && BillingRules.TryDecimal(NeuText, out _) && BillingRules.TryDecimal(FaktorText, out _);
    private async Task SaveAsync(bool close)
    {
        if (!CanSave())
            return;
        await RunAsync(async () =>
        {
            var value = new Rechnung { Id = _id ?? 0, Alt = Alt, Neu = Neu, Verbrauch = Verbrauch, Faktor = Faktor, Betrag = Betrag, Jahr = Jahr, Type = SelectedArt };
            var place = SelectedPlatznummer;
            value.PlatzId = await _db.GetPlatzIdByPlatznummerAsync(place);
            BillingRules.ValidateInvoice(value, _main.Clock, _id == null);
            if (_id == null)
                await _db.AddRechnungAsync(value);
            else
                await _db.UpdateRechnungAsync(value);
            if (OnSavedAsync != null)
                await OnSavedAsync();
            if (close || _id != null)
            {
                _main.ReturnFrom(this);
                return;
            }
            Neu = 0;
            var index = Platznummern.IndexOf(place!);
            if (index >= 0 && Platznummern.Count > 0)
                SelectedPlatznummer = Platznummern[(index + 1) % Platznummern.Count];
            await (SelectionTask = RefreshSelectionAsync());
            if (_active)
                SetFocusToNeuTextBox?.Invoke();
        });
    }
}
