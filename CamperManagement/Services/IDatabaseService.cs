using System.Collections.Generic;
using System.Threading.Tasks;
using CamperManagement.Models;
namespace CamperManagement.Services;

public interface IDatabaseService
{
    Task<List<CamperDisplayModel>> GetActiveCampersAsync();
    Task<List<RechnungDisplayModel>> GetRechnungenAsync();
    Task<List<string>> GetPlatznummernAsync();
    Task DeactivateOldCamperAsync(string? platznummer);
    Task AddNewCamperAsync(CamperDisplayModel camper);
    Task UpdateCamperAsync(CamperDisplayModel camper);
    Task<List<int>> GetAvailableJahreAsync();
    Task<List<KostenEintrag>> GetRechnungenForJahrAsync(int jahr);
    Task MarkRechnungAsPrintedAsync(int id);
    Task MarkRechnungenAsPrintedAsync(IReadOnlyCollection<int> ids);
    Task AddRechnungAsync(Rechnung rechnung);
    Task<int> GetPlatzIdByPlatznummerAsync(string? platznummer);
    Task<decimal> GetNeuFromLatestRechnungAsync(string? platznummer, string? type);
    Task<List<AbleseEintrag>> GetAbleseTabelleAsync();
    Task UpdateRechnungAsync(Rechnung rechnung);
    Task<Standardfaktoren> GetStandardfaktorenAsync();
    Task SaveStandardfaktorenAsync(Standardfaktoren values);
}
