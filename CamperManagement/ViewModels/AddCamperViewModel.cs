using CamperManagement.Models;
using CamperManagement.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CamperManagement.ViewModels
{
    public partial class AddCamperViewModel : ObservableObject
    {
        private readonly MainViewModel _mainViewModel;
        private readonly DatabaseService _dbService;

        public ObservableCollection<string> Platznummern { get; } = new();
        public ObservableCollection<string> Anreden { get; } = new() { "Frau", "Herr", "Eheleute" };

        [ObservableProperty]
        private string? selectedPlatznummer;

        [ObservableProperty]
        private string? selectedAnrede;

        [ObservableProperty]
        private string? vorname;

        [ObservableProperty]
        private string? nachname;

        [ObservableProperty]
        private string? straße;

        [ObservableProperty]
        private string? plz;

        [ObservableProperty]
        private string? ort;

        [ObservableProperty]
        private decimal vertragskosten;

        public AddCamperViewModel(MainViewModel mainViewModel, DatabaseService dbService)
        {
            _mainViewModel = mainViewModel;
            _dbService = dbService;
            _ = LoadPlatznummern();
            SaveCommand = new AsyncRelayCommand(SaveCamperAsync);
            CancelCommand = new AsyncRelayCommand(Cancel);
        }

        public IAsyncRelayCommand SaveCommand { get; }
        public IAsyncRelayCommand CancelCommand { get; }

        private async Task LoadPlatznummern()
        {
            var platznummern = await _dbService.GetPlatznummernAsync();
            Platznummern.Clear();
            foreach (var platz in platznummern)
            {
                Platznummern.Add(platz);
            }
        }

        private async Task SaveCamperAsync()
        {
            // Deaktiviere den alten Camper
            await _dbService.DeactivateOldCamperAsync(SelectedPlatznummer);

            // Füge den neuen Camper hinzu und aktualisiere `camper_personen`
            await _dbService.AddNewCamperAsync(new CamperDisplayModel
            {
                Platznr = SelectedPlatznummer,
                Anrede = SelectedAnrede,
                Vorname = Vorname,
                Nachname = Nachname,
                Straße = Straße,
                PLZ = Plz,
                Ort = Ort,
                Vertragskosten = Vertragskosten
            });

            // Fenster schließen
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
