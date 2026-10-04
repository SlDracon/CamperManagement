using System;
using System.Linq;
using System.Threading.Tasks;

namespace CamperManagement.Services;

public sealed partial class DatabaseService
{
    private static void EnsureContractCostUnchanged(decimal current, decimal expected)
    {
        if (current != expected)
            throw new InvalidOperationException("Die Vertragskosten wurden inzwischen geändert. Bitte neu laden und den Endpreis prüfen.");
    }
    public async Task IncreaseContractCostAsync(int camperId, string? platznummer, decimal expectedContractCost, decimal increase, string description)
    {
        var reason = ContractCostRules.Description(description);
        var total = ContractCostRules.NewTotal(expectedContractCost, increase);
        await using var c = await OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        // Same lock order as editing, invoices and replacement: place first, then occupation.
        var placeId = await LockPlatzAsync(c, tx, platznummer);
        var before = (await ReadCamperStatesAsync(c, tx, $"c.id=@id AND c.platz_id=@p AND {Active}", ("@id", camperId), ("@p", placeId))).SingleOrDefault()
            ?? throw new InvalidOperationException("Der Camper wurde inzwischen geändert oder deaktiviert. Bitte neu laden.");
        EnsureContractCostUnchanged(before.Camper.Vertragskosten, expectedContractCost);
        await using (var cmd = Command(c, "UPDATE camper SET Vertragskosten=@k,updated=NOW() WHERE id=@id", tx, ("@k", total), ("@id", camperId)))
            await cmd.ExecuteNonQueryAsync();
        await RecordCamperHistoryAsync(c, tx, "cost_increased", before, await ReadCamperStateAsync(c, tx, camperId), reason);
        await tx.CommitAsync();
    }
}
