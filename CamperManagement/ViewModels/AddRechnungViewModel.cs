using CamperManagement.Services;
namespace CamperManagement.ViewModels;

public sealed class AddRechnungViewModel(MainViewModel main, IDatabaseService db) : InvoiceFormViewModel(main, db);
