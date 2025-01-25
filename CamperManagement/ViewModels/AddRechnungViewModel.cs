using CamperManagement.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CamperManagement.Models;

namespace CamperManagement.ViewModels
{
    public partial class AddRechnungViewModel : ObservableObject
    {
        private readonly MainViewModel _mainViewModel;
        private readonly DatabaseService _dbService;

        public ObservableCollection<string> Arten { get; } = new() { "Strom", "Wasser" };
        public ObservableCollection<string> Platznummern { get; } = new();

        [ObservableProperty]
        private string selectedArt;

        [ObservableProperty]
        private string? selectedPlatznummer;

        [ObservableProperty]
        private decimal alt;

        [ObservableProperty]
        private decimal neu;

        [ObservableProperty]
        private decimal verbrauch;

        [ObservableProperty]
        private decimal faktor = 0.35m;

        [ObservableProperty]
        private decimal betrag;

        [ObservableProperty]
        private int jahr = DateTime.Now.Year;

        public AddRechnungViewModel(MainViewModel mainViewModel, DatabaseService dbService)
        {
            _mainViewModel = mainViewModel;
            _dbService = dbService;
            SelectedArt = "Strom"; // Setzt "Strom" als Standardwert
            _ = LoadPlatznummernAsync();

            // Event für Änderungen an Art oder Platznummer
            PropertyChanged += async (_, e) =>
            {
                if (e.PropertyName == nameof(SelectedArt))
                {
                    // Setze den Faktor abhängig von der Art
                    Faktor = SelectedArt == "Wasser" ? 8m : 0.35m;
                }

                if (e.PropertyName == nameof(SelectedArt) || e.PropertyName == nameof(SelectedPlatznummer))
                {
                    await UpdateAltValueAsync();
                    // Fokus auf Neu setzen
                    SetFocusToNeuTextBox?.Invoke();
                }

                if (e.PropertyName == nameof(Neu) || e.PropertyName == nameof(Alt))
                {
                    Verbrauch = Neu - Alt;
                }

                if (e.PropertyName == nameof(Verbrauch) || e.PropertyName == nameof(SelectedArt))
                {
                    Betrag = Math.Round(Verbrauch * Faktor, 2);
                }
            };

            SaveCommand = new AsyncRelayCommand(SaveAsync);
            SaveAndCloseCommand = new AsyncRelayCommand(SaveAndCloseAsync);
            CancelCommand = new AsyncRelayCommand(Cancel);
        }

        public IAsyncRelayCommand SaveCommand { get; }
        public IAsyncRelayCommand SaveAndCloseCommand { get; }
        public IAsyncRelayCommand CancelCommand { get; }

        public Action? SetFocusToNeuTextBox { get; set; }
        public Action? OnRechnungAdded { get; set; }

        private async Task LoadPlatznummernAsync()
        {
            Platznummern.Clear();
            var platznummern = await _dbService.GetPlatznummernAsync();
            foreach (var platz in platznummern)
            {
                Platznummern.Add(platz);
            }
        }

        private async Task UpdateAltValueAsync()
        {
            if (!string.IsNullOrEmpty(SelectedPlatznummer) && !string.IsNullOrEmpty(SelectedArt))
            {
                Alt = await _dbService.GetNeuFromLatestRechnungAsync(SelectedPlatznummer, SelectedArt);
            }
        }

        private async Task SaveAsync()
        {
            int platzId = await _dbService.GetPlatzIdByPlatznummerAsync(SelectedPlatznummer);

            var newRechnung = new Rechnung
            {
                PlatzId = platzId,
                Alt = Alt,
                Neu = Neu,
                Verbrauch = Verbrauch,
                Faktor = Faktor,
                Betrag = Betrag,
                Jahr = Jahr,
                Type = SelectedArt,
                Created = DateTime.Now
            };

            await _dbService.AddRechnungAsync(newRechnung);

            // Ereignis auslösen und die neue Rechnung übergeben
            OnRechnungAdded?.Invoke();

            // Eingaben leeren
            Neu = 0;
            if (SelectedPlatznummer != null)
                SelectedPlatznummer =
                    Platznummern[(Platznummern.IndexOf(SelectedPlatznummer) + 1) % Platznummern.Count];

            // Fokus auf Neu setzen
            SetFocusToNeuTextBox?.Invoke();
        }

        private async Task SaveAndCloseAsync()
        {
            await SaveAsync();
            await Cancel();
        }

        private Task Cancel()
        {
            // Rufe NavigateBackCommand aus MainViewModel auf
            if (_mainViewModel.CanNavigateBack)
            {
                _mainViewModel.NavigateBackCommand.Execute(null);
            }

            return Task.CompletedTask;
        }
    }
}
