using CamperManagement.Services;
using CamperManagement.ViewModels;
using MySqlConnector;

namespace CamperManagement.UnitTests;

public sealed class DatabaseConfigurationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "camper-config-" + Guid.NewGuid().ToString("N"));
    private const string Connection = "Server=localhost;Port=33307;Database=camper_test_config;User ID=fixture;Password=synthetic;AllowZeroDateTime=true;ConvertZeroDateTime=true";
    private string ConfigPath => Path.Combine(_directory, "database.json");
    private DatabaseConfiguration Store(string? environment = null) => new(ConfigPath, () => environment);

    [Fact]
    public void FirstStartHasNoImplicitProductionConnection()
    {
        Assert.Null(Store().Load());
        Assert.False(Store().IsConfigured);
    }
    [Fact]
    public void RoundTripPreservesOptionsAndUsesPrivateUnixPermissions()
    {
        Store().Save(Connection);
        var loaded = new MySqlConnectionStringBuilder(Store().Load()!);
        Assert.Equal("synthetic", loaded.Password);
        Assert.Equal((uint)33307, loaded.Port);
        Assert.True(loaded.AllowZeroDateTime);
        Assert.False(loaded.UseAffectedRows);
        Assert.True(Store().IsConfigured);
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(ConfigPath));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(_directory));
        }
        Store().Save(Connection.Replace("synthetic", "changed"));
        Assert.Equal("changed", new MySqlConnectionStringBuilder(Store().Load()!).Password);
        Assert.Single(Directory.GetFiles(_directory));
    }
    [Fact]
    public void EnvironmentTakesPrecedenceEvenWhenEmptyAndCannotBeOverwritten()
    {
        Store().Save(Connection);
        Assert.Equal(Connection, Store(Connection).Load());
        Assert.Equal("", Store("").Load());
        Assert.False(Store("").IsConfigured);
        Assert.Throws<InvalidOperationException>(() => Store("").Save(Connection));
        Assert.True(Store().IsConfigured);
    }
    [Fact]
    public void BrokenFileCanBeRepairedWithoutStartupCrash()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(ConfigPath, "{broken");
        Assert.False(Store().IsConfigured);
        Store().Save(Connection);
        Assert.True(Store().IsConfigured);
    }
    [Theory]
    [InlineData("")]
    [InlineData("Server=localhost")]
    [InlineData("Server=localhost;Database=test;User ID=user;Port=0")]
    [InlineData("Server=localhost;Database=test;User ID=user;Port=65536")]
    [InlineData("unknown-option=SECRET")]
    public void InvalidConfigurationIsNotSavedOrExposed(string value)
    {
        var error = Assert.Throws<ArgumentException>(() => Store().Save(value));
        Assert.DoesNotContain("SECRET", error.Message);
        Assert.False(File.Exists(ConfigPath));
    }
    [Fact]
    public async Task SaveChecksAsynchronouslyBeforePersistingOrNavigating()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var navigated = false;
        var vm = new DatabaseConnectionViewModel(Store(), () => { navigated = true; return Task.CompletedTask; }, async value =>
        {
            Assert.Equal("p;a=ss", new MySqlConnectionStringBuilder(value).Password);
            await gate.Task;
        }) { Server = "localhost", Port = "33307", DatabaseName = "camper_test_config", UserName = "fixture", Password = "p;a=ss" };
        var save = vm.SaveCommand.ExecuteAsync(null);
        Assert.True(vm.IsBusy);
        Assert.False(vm.SaveCommand.CanExecute(null));
        Assert.False(File.Exists(ConfigPath));
        Assert.False(navigated);
        gate.SetResult();
        await save;
        Assert.True(navigated);
        Assert.True(Store().IsConfigured);
        Assert.Equal("", vm.Password);
    }
    [Fact]
    public async Task FailedConnectionKeepsExistingSettingsAndDoesNotNavigate()
    {
        Store().Save(Connection);
        var previous = File.ReadAllText(ConfigPath);
        var navigated = false;
        var vm = new DatabaseConnectionViewModel(Store(), () => { navigated = true; return Task.CompletedTask; }, _ => throw new Exception("do not display secret"));
        vm.Server = "unreachable";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(previous, File.ReadAllText(ConfigPath));
        Assert.False(navigated);
        Assert.DoesNotContain("secret", vm.StatusMessage!);
        Assert.False(vm.IsBusy);
    }
    [Fact]
    public void EnvironmentOverrideDisablesUiSave()
    {
        var vm = new DatabaseConnectionViewModel(Store(Connection), () => Task.CompletedTask);
        Assert.True(vm.UsesEnvironment);
        Assert.False(vm.SaveCommand.CanExecute(null));
    }
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
