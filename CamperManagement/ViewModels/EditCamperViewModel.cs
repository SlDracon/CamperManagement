using CamperManagement.Models;
using CamperManagement.Services;
namespace CamperManagement.ViewModels;

public sealed class EditCamperViewModel(MainViewModel main, IDatabaseService db, CamperDisplayModel camper) : CamperFormViewModel(main, db, camper);
