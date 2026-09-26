using System;
using System.IO;
using System.Threading.Tasks;
using MySqlConnector;
namespace CamperManagement.Services;

public static class SchemaMigration
{
    // Invoked explicitly by the migration tool, never as a side effect of a view constructor.
    public static async Task<int> ApplyAsync(DatabaseService db)
    {
        await using var c = await db.OpenConnectionAsync();
        var lockName = $"CamperManagement:migration:{c.Database}";
        await using var acquire = new MySqlCommand("SELECT GET_LOCK(@name,30)", c);
        acquire.Parameters.AddWithValue("@name", lockName);
        if (Convert.ToInt32(await acquire.ExecuteScalarAsync()) != 1)
            throw new InvalidOperationException("Die Datenbank wird bereits aktualisiert.");
        try
        {
            await using (var cmd = new MySqlCommand("CREATE TABLE IF NOT EXISTS camper_schema_version(version INT PRIMARY KEY,applied DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP)", c))
                await cmd.ExecuteNonQueryAsync();
            await using var check = new MySqlCommand("SELECT COUNT(*) FROM camper_schema_version WHERE version=1", c);
            if (Convert.ToInt32(await check.ExecuteScalarAsync()) == 0)
            {
                using var stream = typeof(SchemaMigration).Assembly.GetManifestResourceStream("CamperManagement.Migrations.001_billing.sql")!;
                using var reader = new StreamReader(stream);
                var sql = await reader.ReadToEndAsync();
                await using (var cmd = new MySqlCommand(sql, c) { CommandTimeout = 120 })
                    await cmd.ExecuteNonQueryAsync();
                // Preserve postal codes including leading zeroes; future non-numeric postcodes are allowed.
                await using (var type = new MySqlCommand("SELECT DATA_TYPE FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='personen' AND COLUMN_NAME='plz'", c))
                    if (Convert.ToString(await type.ExecuteScalarAsync()) != "varchar")
                    {
                        await using (var alter = new MySqlCommand("ALTER TABLE personen MODIFY plz VARCHAR(32) NOT NULL", c))
                            await alter.ExecuteNonQueryAsync();
                        await using (var pad = new MySqlCommand("UPDATE personen SET plz=LPAD(plz,5,'0') WHERE CHAR_LENGTH(plz)<5", c))
                            await pad.ExecuteNonQueryAsync();
                    }
                await using (var done = new MySqlCommand("INSERT INTO camper_schema_version(version) VALUES(1)", c))
                    await done.ExecuteNonQueryAsync();
            }
            await using var checkCurrent = new MySqlCommand("SELECT COUNT(*) FROM camper_schema_version WHERE version=2", c);
            if (Convert.ToInt32(await checkCurrent.ExecuteScalarAsync()) == 0)
            {
                using var stream = typeof(SchemaMigration).Assembly.GetManifestResourceStream("CamperManagement.Migrations.002_current_factors.sql")!;
                using var reader = new StreamReader(stream);
                await using (var cmd = new MySqlCommand(await reader.ReadToEndAsync(), c) { CommandTimeout = 120 })
                    await cmd.ExecuteNonQueryAsync();
                await using (var done = new MySqlCommand("INSERT INTO camper_schema_version(version) VALUES(2)", c))
                    await done.ExecuteNonQueryAsync();
            }
            await using var unresolved = new MySqlCommand("SELECT COUNT(*) FROM rechnungen r LEFT JOIN rechnung_empfaenger s ON s.rechnung_id=r.id WHERE s.rechnung_id IS NULL", c);
            return Convert.ToInt32(await unresolved.ExecuteScalarAsync());
        }
        finally { await using var release = new MySqlCommand("SELECT RELEASE_LOCK(@name)", c); release.Parameters.AddWithValue("@name", lockName); await release.ExecuteScalarAsync(); }
    }
}
