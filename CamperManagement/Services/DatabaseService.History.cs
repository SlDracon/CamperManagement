using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CamperManagement.Models;
using MySqlConnector;

namespace CamperManagement.Services;

public sealed partial class DatabaseService
{
    private static readonly JsonSerializerOptions HistoryJson = new() { IgnoreReadOnlyProperties = true };
    private static async Task<List<CamperHistoryState>> ReadCamperStatesAsync(MySqlConnection c, MySqlTransaction tx, string where, params (string, object?)[] args)
    {
        await using var cmd = Command(c, $"SELECT {CamperColumns},({Active}) history_active,c.created history_created,NULLIF(c.deactivated,'0000-00-00 00:00:00') history_deactivated FROM camper c JOIN plaetze p ON p.id=c.platz_id {JoinPersons(true)} WHERE {where} ORDER BY c.id FOR UPDATE", tx, args);
        await using var reader = await cmd.ExecuteReaderAsync();
        var rows = new List<CamperHistoryState>();
        while (await reader.ReadAsync())
            rows.Add(new(MapCamper(reader), reader.GetBoolean("history_active"), HistoryDate(reader, "history_created"), HistoryDate(reader, "history_deactivated")));
        return rows;
    }
    private static DateTime? HistoryDate(MySqlDataReader reader, string column)
    {
        if (reader.IsDBNull(reader.GetOrdinal(column))) return null;
        var date = reader.GetMySqlDateTime(column);
        return date.IsValidDateTime ? date.GetDateTime() : null;
    }
    private static async Task<CamperHistoryState> ReadCamperStateAsync(MySqlConnection c, MySqlTransaction tx, int id) =>
        (await ReadCamperStatesAsync(c, tx, "c.id=@id", ("@id", id))).Single();
    private static async Task RecordCamperHistoryAsync(MySqlConnection c, MySqlTransaction tx, string kind, CamperHistoryState? before, CamperHistoryState after, string? description = null)
    {
        var previous = before == null ? null : JsonSerializer.Serialize(before, HistoryJson);
        var current = JsonSerializer.Serialize(after, HistoryJson);
        // A Save without actual changes must not create a misleading history entry.
        if (previous == current) return;
        // Migration 004 also calls this before the optional description column exists.
        var sql = description == null
            ? "INSERT INTO camper_historie(camper_id,ereignis,vorher,nachher) VALUES(@c,@e,@v,@n)"
            : "INSERT INTO camper_historie(camper_id,ereignis,vorher,nachher,beschreibung) VALUES(@c,@e,@v,@n,@b)";
        await using var cmd = Command(c, sql, tx,
            ("@c", after.Camper.Id), ("@e", kind), ("@v", previous), ("@n", current), ("@b", description));
        await cmd.ExecuteNonQueryAsync();
    }
    private static async Task DeactivateWithHistoryAsync(MySqlConnection c, MySqlTransaction tx, int placeId)
    {
        var before = await ReadCamperStatesAsync(c, tx, $"c.platz_id=@p AND {Active}", ("@p", placeId));
        if (before.Count == 0) return;
        await using (var cmd = Command(c, $"UPDATE camper c SET active=0,deactivated=NOW(),updated=NOW() WHERE platz_id=@p AND {Active}", tx, ("@p", placeId)))
            await cmd.ExecuteNonQueryAsync();
        foreach (var state in before)
            await RecordCamperHistoryAsync(c, tx, "deactivated", state, await ReadCamperStateAsync(c, tx, state.Camper.Id));
    }
    internal static async Task SeedCamperHistoryAsync(MySqlConnection c)
    {
        await using var tx = await c.BeginTransactionAsync();
        var states = await ReadCamperStatesAsync(c, tx, "NOT EXISTS(SELECT 1 FROM camper_historie h WHERE h.camper_id=c.id)");
        foreach (var state in states)
            await RecordCamperHistoryAsync(c, tx, "baseline", null, state);
        await tx.CommitAsync();
    }
    public async Task<List<CamperHistoryEntry>> GetCamperHistoryAsync(string? platznummer = null, CancellationToken cancellationToken = default)
    {
        // Deserialize off the UI context; callers resume on their own dispatcher.
        await using var c = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = Command(c, """
            SELECT h.* FROM camper_historie h JOIN camper c ON c.id=h.camper_id JOIN plaetze p ON p.id=c.platz_id
            WHERE (@p IS NULL OR p.platznr=@p) ORDER BY h.recorded_at DESC,h.id DESC
            """, null, ("@p", platznummer));
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var rows = new List<CamperHistoryEntry>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (reader.GetInt32("schema_version") != 1) throw new InvalidOperationException("Die Historie benötigt eine neuere Anwendungsversion.");
            var entry = new CamperHistoryEntry
            {
                Id = reader.GetInt64("id"), Kind = reader.GetString("ereignis"),
                Description = Text(reader, "beschreibung"),
                RecordedAtUtc = DateTime.SpecifyKind(reader.GetDateTime("recorded_at"), DateTimeKind.Utc),
                Before = reader["vorher"] is DBNull ? null : JsonSerializer.Deserialize<CamperHistoryState>(reader.GetString("vorher"), HistoryJson),
                After = JsonSerializer.Deserialize<CamperHistoryState>(reader.GetString("nachher"), HistoryJson) ?? throw new InvalidOperationException("Historieneintrag ist unvollständig.")
            };
            _ = entry.SearchText; // Cache formatted search values while still off the UI context.
            rows.Add(entry);
        }
        return rows;
    }
}
