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
    public PrintSelectionViewModel(MainViewModel main, IDatabaseService db) : base(main.ErrorLog)
    {
        _main = main;
        _db = db;
        CancelCommand = new RelayCommand(() => _main.NavigateBackCommand.Execute(null), () => !IsBusy);
        PrintCommand = new AsyncRelayCommand(() => RunExportAsync(async token =>
        {
            var year = SelectedJahr;
            var rows = await _db.GetRechnungenForJahrAsync(year, token);
            if (rows.Count == 0)
            {
                StatusMessage = "Keine Daten für dieses Jahr vorhanden.";
                return;
            }
            using var file = await _main.Pdf.CostsAsync(year, rows, token);
            if (file == null)
                return;
            if (!await _main.Pdf.OpenAsync(file))
            {
                StatusMessage = "PDF gespeichert; Viewer nicht verfügbar.";
                return;
            }
            _main.ReturnFrom(this);
        }), () => !IsBusy && !IsLoading && Jahre.Contains(SelectedJahr));
        PropertyChanged += (_, e) => { if (e.PropertyName is nameof(IsBusy) or nameof(IsLoading) or nameof(SelectedJahr)) { PrintCommand.NotifyCanExecuteChanged(); CancelCommand.NotifyCanExecuteChanged(); } };
    }
    public override Task ResumeAsync() => InitializeAsync();
    public override Task InitializeAsync() => RunLoadAsync(async token => { var years = await _db.GetAvailableJahreAsync(token); token.ThrowIfCancellationRequested(); Jahre.Clear(); foreach (var y in years.OrderDescending()) Jahre.Add(y); SelectedJahr = Jahre.FirstOrDefault(); });
}
