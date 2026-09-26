using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CamperManagement.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
namespace CamperManagement.ViewModels;

public partial class PrintSelectionViewModel : ViewModelBase
{
    private readonly MainViewModel _main; private readonly IDatabaseService _db;
    public ObservableCollection<int> Jahre { get; } = new();
    [ObservableProperty] private int selectedJahr;
    public IAsyncRelayCommand PrintCommand
    {
        get;
    }
    public IRelayCommand CancelCommand
    {
        get;
    }
    public PrintSelectionViewModel(MainViewModel main, IDatabaseService db)
    {
        _main = main;
        _db = db;
        CancelCommand = new RelayCommand(() => _main.NavigateBackCommand.Execute(null));
        PrintCommand = new AsyncRelayCommand(() => RunAsync(async () =>
        {
            var year = SelectedJahr;
            var rows = await _db.GetRechnungenForJahrAsync(year);
            if (rows.Count == 0)
            {
                StatusMessage = "Keine Daten für dieses Jahr vorhanden.";
                return;
            }
            using var file = await _main.Pdf.CostsAsync(year, rows);
            if (file == null)
                return;
            if (!await _main.Pdf.OpenAsync(file))
            {
                StatusMessage = "PDF gespeichert; Viewer nicht verfügbar.";
                return;
            }
            _main.ReturnFrom(this);
        }));
    }
    public override Task InitializeAsync() => RunAsync(async () => { var years = await _db.GetAvailableJahreAsync(); Jahre.Clear(); foreach (var y in years.OrderDescending()) Jahre.Add(y); SelectedJahr = Jahre.FirstOrDefault(); });
}
