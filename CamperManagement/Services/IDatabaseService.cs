using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using CamperManagement.Models;
namespace CamperManagement.Services;

public interface IDatabaseService
{
    Task<List<CamperDisplayModel>> GetActiveCampersAsync(CancellationToken cancellationToken = default);
    Task<List<RechnungDisplayModel>> GetRechnungenAsync(CancellationToken cancellationToken = default);
    Task<List<string>> GetPlatznummernAsync(CancellationToken cancellationToken = default);
    Task DeactivateOldCamperAsync(string? platznummer);
    Task AddNewCamperAsync(CamperDisplayModel camper);
    Task UpdateCamperAsync(CamperDisplayModel camper);
    Task<List<int>> GetAvailableJahreAsync(CancellationToken cancellationToken = default);
    Task<List<KostenEintrag>> GetRechnungenForJahrAsync(int jahr, CancellationToken cancellationToken = default);
    Task MarkRechnungAsPrintedAsync(int id);
    Task MarkRechnungenAsPrintedAsync(IReadOnlyCollection<int> ids);
    Task AddRechnungAsync(Rechnung rechnung);
    Task<int> GetPlatzIdByPlatznummerAsync(string? platznummer, CancellationToken cancellationToken = default);
    Task<decimal> GetNeuFromLatestRechnungAsync(string? platznummer, string? type, CancellationToken cancellationToken = default);
    Task<List<AbleseEintrag>> GetAbleseTabelleAsync(CancellationToken cancellationToken = default);
    Task UpdateRechnungAsync(Rechnung rechnung);
    Task<Standardfaktoren> GetStandardfaktorenAsync(CancellationToken cancellationToken = default);
    Task SaveStandardfaktorenAsync(Standardfaktoren values);
}
