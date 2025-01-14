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
        private readonly DatabaseService _dbService;

        public ObservableCollection<string> Anreden { get; } = new() { "Frau", "Herr", "Eheleute" };

        [ObservableProperty]
        private string platznr;

        [ObservableProperty]
        private string selectedAnrede;

        [ObservableProperty]
        private string vorname;

        [ObservableProperty]
        private string nachname;

        [ObservableProperty]
        private string straße;

        [ObservableProperty]
        private string plz;

        [ObservableProperty]
        private string ort;

        public EditCamperViewModel(DatabaseService dbService, CamperDisplayModel camper)
        {
            _dbService = dbService;

            Platznr = camper.Platznr;
            SelectedAnrede = camper.Anrede;
            Vorname = camper.Vorname;
            Nachname = camper.Nachname;
            Straße = camper.Straße;
            Plz = camper.PLZ;
            Ort = camper.Ort;

            SaveCommand = new AsyncRelayCommand(SaveCamperAsync);
        }

        public IAsyncRelayCommand SaveCommand { get; }

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
                Ort = Ort
            });

            CloseAction?.Invoke();
        }

        public Action? CloseAction { get; set; }
    }
}
