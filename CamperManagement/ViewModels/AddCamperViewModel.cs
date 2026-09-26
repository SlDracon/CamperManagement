using CamperManagement.Services;
namespace CamperManagement.ViewModels;

public sealed class AddCamperViewModel(MainViewModel main, IDatabaseService db) : CamperFormViewModel(main, db);
