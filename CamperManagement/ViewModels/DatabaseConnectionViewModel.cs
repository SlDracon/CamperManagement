using System;
using System.Globalization;
using System.Threading.Tasks;
using CamperManagement.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MySqlConnector;

namespace CamperManagement.ViewModels;

public partial class DatabaseConnectionViewModel : ViewModelBase
{
    private readonly DatabaseConfiguration _configuration;
    private readonly Func<string, Task> _check;
    private readonly Func<Task> _saved;
    private readonly MySqlConnectionStringBuilder _previous;
    [ObservableProperty] private string server = "";
    [ObservableProperty] private string port = "3306";
    [ObservableProperty] private string databaseName = "";
    [ObservableProperty] private string userName = "";
    [ObservableProperty] private string password = "";
    public bool UsesEnvironment => _configuration.IsEnvironmentOverride;
    public IAsyncRelayCommand SaveCommand { get; }
    public DatabaseConnectionViewModel(DatabaseConfiguration configuration, Func<Task> saved, Func<string, Task>? check = null)
    {
        _configuration = configuration;
        _saved = saved;
        _check = check ?? (async value => { await new DatabaseService(value).GetStandardfaktorenAsync(); });
        try { _previous = new MySqlConnectionStringBuilder(configuration.Load() ?? ""); }
        catch (ArgumentException) { _previous = new MySqlConnectionStringBuilder(); }
        Server = _previous.Server;
        Port = _previous.Port.ToString(CultureInfo.InvariantCulture);
        DatabaseName = _previous.Database;
        UserName = _previous.UserID;
        Password = _previous.Password;
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy && !UsesEnvironment);
        PropertyChanged += (_, e) => { if (e.PropertyName == nameof(IsBusy)) SaveCommand.NotifyCanExecuteChanged(); };
    }
    private async Task SaveAsync()
    {
        var saved = await RunAsync(async () =>
        {
            if (!uint.TryParse(Port, out var parsedPort) || parsedPort is 0 or > 65535)
                throw new ArgumentException("Bitte einen Port zwischen 1 und 65535 eingeben.");
            var builder = new MySqlConnectionStringBuilder(_previous.ConnectionString)
            {
                Server = Server.Trim(), Port = parsedPort, Database = DatabaseName.Trim(),
                UserID = UserName.Trim(), Password = Password, AllowZeroDateTime = true,
                ConvertZeroDateTime = true, ConnectionTimeout = 10, UseAffectedRows = false
            };
            var value = DatabaseConfiguration.Validate(builder.ConnectionString);
            await _check(value);
            _configuration.Save(value);
            Password = "";
        });
        if (saved)
            await _saved();
    }
}
