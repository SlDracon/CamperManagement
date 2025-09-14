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
    public partial class EditCamperViewModel : ObservableObject
    {
        private readonly MainViewModel _mainViewModel;
        private readonly DatabaseService _dbService;

        public ObservableCollection<string> Anreden { get; } = new() { "Frau", "Herr", "Eheleute" };

        [ObservableProperty]
        private string? platznr;

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
        private string? email;

        [ObservableProperty]
        private decimal vertragskosten;

        public EditCamperViewModel(MainViewModel mainViewModel, DatabaseService dbService, CamperDisplayModel camper)
        {
            _mainViewModel = mainViewModel;
            _dbService = dbService;

            Platznr = camper.Platznr;
            SelectedAnrede = camper.Anrede;
            Vorname = camper.Vorname;
            Nachname = camper.Nachname;
            Straße = camper.Straße;
            Plz = camper.PLZ;
            Ort = camper.Ort;
            Email = camper.Email;
            Vertragskosten = camper.Vertragskosten;

            SaveCommand = new AsyncRelayCommand(SaveCamperAsync);
            CancelCommand = new AsyncRelayCommand(Cancel);
        }

        public IAsyncRelayCommand SaveCommand { get; }
        public IAsyncRelayCommand CancelCommand { get; }
        public Action? OnCamperChanged { get; set; }

        private async Task SaveCamperAsync()
        {
            await _dbService.UpdateCamperAsync(new CamperDisplayModel
            {
                Platznr = Platznr,
                Anrede = SelectedAnrede,
                Vorname = Vorname,
                Nachname = Nachname,
                Straße = Straße,
                PLZ = Plz,
                Ort = Ort,
                Email = Email,
                Vertragskosten = Vertragskosten
            });

            OnCamperChanged?.Invoke();
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
