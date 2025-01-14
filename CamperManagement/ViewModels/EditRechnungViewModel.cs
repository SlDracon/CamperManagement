using CamperManagement.Models;
using CamperManagement.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CamperManagement.ViewModels
{
    public partial class EditRechnungViewModel : ObservableObject
    {
        private readonly DatabaseService _dbService;

        public ObservableCollection<string> Arten { get; } = new() { "Strom", "Wasser" };
        public ObservableCollection<string> Platznummern { get; } = new();

        [ObservableProperty]
        private string selectedArt;

        [ObservableProperty]
        private string selectedPlatznummer;

        [ObservableProperty]
        private decimal alt;

        [ObservableProperty]
        private decimal neu;

        [ObservableProperty]
        private decimal verbrauch;

        [ObservableProperty]
        private decimal faktor;

        [ObservableProperty]
        private decimal betrag;

        [ObservableProperty]
        private int jahr;

        public EditRechnungViewModel(DatabaseService dbService, RechnungDisplayModel rechnung)
        {
            _dbService = dbService;

            // Initialisiere die Werte
            SelectedArt = rechnung.Art;
            SelectedPlatznummer = rechnung.Platznr;
            Alt = rechnung.Alt;
            Neu = rechnung.Neu;
            Verbrauch = rechnung.Verbrauch;
            Faktor = rechnung.Faktor;
            Betrag = rechnung.Betrag;
            Jahr = rechnung.Jahr;

            // Berechnungslogik einfügen
            PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SelectedArt))
                {
                    UpdateFaktor();
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
            CancelCommand = new RelayCommand(() => CloseAction?.Invoke());
        }

        public IAsyncRelayCommand SaveCommand { get; }
        public RelayCommand CancelCommand { get; }
        public Action? CloseAction { get; set; }
        public Action? OnRechnungChanged { get; set; }

        private void UpdateFaktor()
        {
            if (SelectedArt == "Strom")
            {
                Faktor = 0.35m;
            }
            else if (SelectedArt == "Wasser")
            {
                Faktor = 8.00m;
            }
        }

        private async Task SaveAsync()
        {
            var updatedRechnung = new Rechnung
            {
                PlatzId = await _dbService.GetPlatzIdByPlatznummerAsync(SelectedPlatznummer),
                Alt = Alt,
                Neu = Neu,
                Verbrauch = Verbrauch,
                Faktor = Faktor,
                Betrag = Betrag,
                Jahr = Jahr,
                Type = SelectedArt
            };

            await _dbService.UpdateRechnungAsync(updatedRechnung);
            OnRechnungChanged?.Invoke();
            CloseAction?.Invoke();
        }
    }
}
