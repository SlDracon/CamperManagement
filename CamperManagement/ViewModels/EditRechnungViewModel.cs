using CamperManagement.Models;
using CamperManagement.Services;
namespace CamperManagement.ViewModels;

public sealed class EditRechnungViewModel(MainViewModel main, IDatabaseService db, RechnungDisplayModel invoice) : InvoiceFormViewModel(main, db, invoice);
