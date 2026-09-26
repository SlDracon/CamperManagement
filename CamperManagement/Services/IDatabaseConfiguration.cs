namespace CamperManagement.Services;
public interface IDatabaseConfiguration
{
    bool IsConfigured { get; }
    bool IsEnvironmentOverride { get; }
    string? Load();
    void Save(string connectionString);
}
