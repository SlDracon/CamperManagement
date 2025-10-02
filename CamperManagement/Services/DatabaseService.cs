using System;
using CamperManagement.Models;
using MySqlConnector;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CamperManagement.Services
{
    public class DatabaseService
    {
        private const string ConnectionString =
            "Server=192.168.1.51;Port=15000;User=camper;Password=GlJzTYAWaa5FWuNH;Database=camper;AllowZeroDateTime=True;ConvertZeroDateTime=True;";

        public async Task<List<CamperDisplayModel>> GetActiveCampersAsync()
        {
            var campers = new List<CamperDisplayModel>();

            await using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync();

            var query = @"
            SELECT 
                p.platznr, 
                pers.anrede, 
                pers.vorname, 
                pers.nachname, 
                pers.strasse, 
                pers.plz, 
                pers.ort,
                pers.email,
                c.Vertragskosten
            FROM camper c
            JOIN plaetze p ON c.platz_id = p.id
            JOIN camper_personen cp ON c.id = cp.camper_id
            JOIN personen pers ON cp.personen_id = pers.id
            WHERE c.deactivated = '0000-00-00 00:00:00'
            ORDER BY p.platznr ASC";

            await using var command = new MySqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                campers.Add(new CamperDisplayModel
                {
                    Platznr = reader.GetString("platznr"),
                    Anrede = reader.GetString("anrede"),
                    Vorname = reader.GetString("vorname"),
                    Nachname = reader.GetString("nachname"),
                    Straße = reader.GetString("strasse"),
                    PLZ = reader["plz"].ToString() ?? string.Empty,
                    Ort = reader.GetString("ort"),
                    Email = reader["email"]?.ToString() ?? string.Empty,
                    Vertragskosten = reader.GetDecimal("Vertragskosten")
                });
            }

            return campers;
        }

        public async Task<List<RechnungDisplayModel>> GetRechnungenAsync()
        {
            var rechnungen = new List<RechnungDisplayModel>();

            await using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync();

            string query = @"
        SELECT 
            r.id, 
            p.platznr, 
            r.alt, 
            r.neu, 
            r.verbrauch, 
            r.faktor, 
            r.betrag, 
            r.jahr, 
            r.type AS art, 
            r.printed AS gedruckt,
            pers.anrede, 
            pers.vorname, 
            pers.nachname, 
            pers.strasse, 
            pers.plz, 
            pers.ort,
            pers.email
        FROM rechnungen r
        JOIN plaetze p ON r.platz_id = p.id
        JOIN camper c ON c.platz_id = p.id
        JOIN camper_personen cp ON c.id = cp.camper_id
        JOIN personen pers ON cp.personen_id = pers.id
        WHERE c.active = 1
        ORDER BY r.id DESC";

            await using var command = new MySqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                rechnungen.Add(new RechnungDisplayModel
                {
                    Id = reader.GetInt32("id"),
                    Platznr = reader.GetString("platznr"),
                    Alt = reader.GetDecimal("alt"),
                    Neu = reader.GetDecimal("neu"),
                    Verbrauch = reader.GetDecimal("verbrauch"),
                    Faktor = reader.GetDecimal("faktor"),
                    Betrag = reader.GetDecimal("betrag"),
                    Jahr = reader.GetInt32("jahr"),
                    Art = reader.GetString("art"),
                    Gedruckt = reader.GetBoolean("gedruckt") ? "Ja" : "Nein",
                    Anrede = reader.GetString("anrede"),
                    Vorname = reader.GetString("vorname"),
                    Nachname = reader.GetString("nachname"),
                    Straße = reader.GetString("strasse"),
                    PLZ = reader["plz"].ToString(),
                    Ort = reader.GetString("ort")
                });
            }

            return rechnungen;
        }

        public async Task<List<string>> GetPlatznummernAsync()
        {
            var platznummern = new List<string>();

            await using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync();

            string query = "SELECT platznr FROM plaetze ORDER BY platznr ASC";

            await using var command = new MySqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                platznummern.Add(reader.GetString("platznr"));
            }

            return platznummern;
        }

        public async Task DeactivateOldCamperAsync(string? platznummer)
        {
            await using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync();

            string query = @"
        UPDATE camper c
        JOIN plaetze p ON c.platz_id = p.id
        SET c.deactivated = @Deactivated, c.active = 0
        WHERE p.platznr = @Platznr AND c.deactivated = '0000-00-00 00:00:00'";

            await using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@Deactivated", DateTime.Now);
            command.Parameters.AddWithValue("@Platznr", platznummer);

            await command.ExecuteNonQueryAsync();
        }

        public async Task AddNewCamperAsync(CamperDisplayModel camper)
        {
            await using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync();

            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                // Füge die Person in die Tabelle `personen` ein
                string insertPersonQuery = @"
            INSERT INTO personen (vorname, nachname, anrede, strasse, plz, ort, email, created, updated)
            VALUES (@Vorname, @Nachname, @Anrede, @Straße, @PLZ, @Ort, @Email, @Created, @Updated);";

                await using var insertPersonCommand = new MySqlCommand(insertPersonQuery, connection, transaction);
                insertPersonCommand.Parameters.AddWithValue("@Vorname", camper.Vorname);
                insertPersonCommand.Parameters.AddWithValue("@Nachname", camper.Nachname);
                insertPersonCommand.Parameters.AddWithValue("@Anrede", camper.Anrede);
                insertPersonCommand.Parameters.AddWithValue("@Straße", camper.Straße);
                insertPersonCommand.Parameters.AddWithValue("@PLZ", camper.PLZ);
                insertPersonCommand.Parameters.AddWithValue("@Ort", camper.Ort);
                insertPersonCommand.Parameters.AddWithValue("@Email", camper.Email ?? string.Empty);
                insertPersonCommand.Parameters.AddWithValue("@Created", DateTime.Now);
                insertPersonCommand.Parameters.AddWithValue("@Updated", DateTime.Now);

                await insertPersonCommand.ExecuteNonQueryAsync();

                // Hole die neue Personen-ID
                var newPersonId = insertPersonCommand.LastInsertedId;

                // Füge den Camper in die Tabelle `camper` ein
                var insertCamperQuery = @"
            INSERT INTO camper (platz_id, active, created, updated, Vertragskosten)
            VALUES (
                (SELECT id FROM plaetze WHERE platznr = @Platznr),
                @Active,
                @Created,
                @Updated,
                @Vertragskosten
            );";

                await using var insertCamperCommand = new MySqlCommand(insertCamperQuery, connection, transaction);
                insertCamperCommand.Parameters.AddWithValue("@Platznr", camper.Platznr);
                insertCamperCommand.Parameters.AddWithValue("@Active", 1);
                insertCamperCommand.Parameters.AddWithValue("@Created", DateTime.Now);
                insertCamperCommand.Parameters.AddWithValue("@Updated", DateTime.Now);
                insertCamperCommand.Parameters.AddWithValue("@Vertragskosten", camper.Vertragskosten);

                await insertCamperCommand.ExecuteNonQueryAsync();

                // Hole die neue Camper-ID
                var newCamperId = insertCamperCommand.LastInsertedId;

                // Aktualisiere den Eintrag in der Tabelle `camper_personen`
                var updateCamperPersonQuery = @"
            UPDATE camper_personen
            SET camper_id = @NewCamperId, personen_id = @NewPersonId, rechnungsadresse = 1
            WHERE camper_id IN (
                SELECT id FROM camper WHERE platz_id = (SELECT id FROM plaetze WHERE platznr = @Platznr)
            );";

                await using var updateCamperPersonCommand =
                    new MySqlCommand(updateCamperPersonQuery, connection, transaction);
                updateCamperPersonCommand.Parameters.AddWithValue("@NewCamperId", newCamperId);
                updateCamperPersonCommand.Parameters.AddWithValue("@NewPersonId", newPersonId);
                updateCamperPersonCommand.Parameters.AddWithValue("@Platznr", camper.Platznr);

                await updateCamperPersonCommand.ExecuteNonQueryAsync();

                // Transaktion abschließen
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task UpdateCamperAsync(CamperDisplayModel camper)
        {
            await using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync();

            var query = @"
        UPDATE camper c
        JOIN plaetze p ON c.platz_id = p.id
        JOIN camper_personen cp ON c.id = cp.camper_id
        JOIN personen pers ON cp.personen_id = pers.id
        SET pers.anrede = @Anrede, 
            pers.vorname = @Vorname, 
            pers.nachname = @Nachname, 
            pers.strasse = @Straße, 
            pers.plz = @PLZ, 
            pers.ort = @Ort,
            pers.email = @Email,
            c.Vertragskosten = @Vertragskosten,
            c.updated = @Updated
        WHERE p.platznr = @Platznr;";

            await using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@Platznr", camper.Platznr);
            command.Parameters.AddWithValue("@Anrede", camper.Anrede);
            command.Parameters.AddWithValue("@Vorname", camper.Vorname);
            command.Parameters.AddWithValue("@Nachname", camper.Nachname);
            command.Parameters.AddWithValue("@Straße", camper.Straße);
            command.Parameters.AddWithValue("@PLZ", camper.PLZ);
            command.Parameters.AddWithValue("@Ort", camper.Ort);
            command.Parameters.AddWithValue("@Email", camper.Email ?? string.Empty);
            command.Parameters.AddWithValue("@Vertragskosten", camper.Vertragskosten);
            command.Parameters.AddWithValue("@Updated", DateTime.Now);

            await command.ExecuteNonQueryAsync();
        }

        public async Task<List<int>> GetAvailableJahreAsync()
        {
            await using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync();

            var query = "SELECT DISTINCT jahr FROM rechnungen ORDER BY jahr DESC";

            var jahre = new List<int>();
            await using var command = new MySqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                jahre.Add(reader.GetInt32("jahr"));
            }

            return jahre;
        }

        public async Task<List<KostenEintrag>> GetRechnungenForJahrAsync(int jahr)
        {
            var kostenEintraege = new List<KostenEintrag>();

            await using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync();

            var query = @"
        SELECT 
            p.platznr,
            pers.vorname,
            pers.nachname,
            SUM(CASE WHEN r.type = 'Wasser' THEN r.betrag ELSE 0 END) AS wasser_betrag,
            SUM(CASE WHEN r.type = 'Strom' THEN r.betrag ELSE 0 END) AS strom_betrag,
            c.Vertragskosten
        FROM rechnungen r
        JOIN camper c ON r.platz_id = c.platz_id
        JOIN camper_personen cp ON c.id = cp.camper_id
        JOIN personen pers ON cp.personen_id = pers.id
        JOIN plaetze p ON c.platz_id = p.id
        WHERE r.jahr = @Jahr 
              AND c.active = 1
              AND c.deactivated = '0000-00-00 00:00:00'
        GROUP BY p.platznr, pers.vorname, pers.nachname
        ORDER BY p.platznr;";

            await using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@Jahr", jahr);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                kostenEintraege.Add(new KostenEintrag
                {
                    PlatzNr = reader.GetString("platznr"),
                    Vorname = reader.GetString("vorname"),
                    Nachname = reader.GetString("nachname"),
                    WasserBetrag = reader.GetDecimal("wasser_betrag"),
                    StromBetrag = reader.GetDecimal("strom_betrag"),
                    Vertragskosten = reader.GetDecimal("Vertragskosten")
                });
            }

            return kostenEintraege;
        }

        public async Task MarkRechnungAsPrintedAsync(int rechnungId)
        {
            await using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync();

            var query = @"
        UPDATE rechnungen
        SET printed = 1
        WHERE id = @RechnungId";

            await using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@RechnungId", rechnungId);

            await command.ExecuteNonQueryAsync();
        }

        public async Task AddRechnungAsync(Rechnung rechnung)
        {
            await using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync();

            var query = @"
        INSERT INTO rechnungen (platz_id, alt, neu, verbrauch, faktor, betrag, jahr, type, created)
        VALUES (@PlatzId, @Alt, @Neu, @Verbrauch, @Faktor, @Betrag, @Jahr, @Type, @Created);";

            await using var command = new MySqlCommand(query, connection);

            command.Parameters.AddWithValue("@PlatzId", rechnung.PlatzId);
            command.Parameters.AddWithValue("@Alt", rechnung.Alt);
            command.Parameters.AddWithValue("@Neu", rechnung.Neu);
            command.Parameters.AddWithValue("@Verbrauch", rechnung.Verbrauch);
            command.Parameters.AddWithValue("@Faktor", rechnung.Faktor);
            command.Parameters.AddWithValue("@Betrag", rechnung.Betrag);
            command.Parameters.AddWithValue("@Jahr", rechnung.Jahr);
            command.Parameters.AddWithValue("@Type", rechnung.Type);
            command.Parameters.AddWithValue("@Created", DateTime.Now);

            await command.ExecuteNonQueryAsync();
        }

        public async Task<int> GetPlatzIdByPlatznummerAsync(string? platznummer)
        {
            await using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync();

            var query = "SELECT id FROM plaetze WHERE platznr = @Platznummer LIMIT 1;";

            await using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@Platznummer", platznummer);

            var result = await command.ExecuteScalarAsync();

            if (result == null || result == DBNull.Value)
            {
                throw new Exception($"Keine Platz-ID für Platznummer '{platznummer}' gefunden.");
            }

            return Convert.ToInt32(result);
        }

        public async Task<decimal> GetNeuFromLatestRechnungAsync(string? platznummer, string art)
        {
            await using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync();

            var query = @"
        SELECT r.neu
        FROM rechnungen r
        JOIN plaetze p ON r.platz_id = p.id
        WHERE p.platznr = @Platznummer AND r.type = @Art
        ORDER BY r.created DESC
        LIMIT 1;";

            await using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@Platznummer", platznummer);
            command.Parameters.AddWithValue("@Art", art);

            var result = await command.ExecuteScalarAsync();

            if (result == null || result == DBNull.Value)
            {
                return 0; // Rückgabewert, wenn keine frühere Rechnung vorhanden ist
            }

            return Convert.ToDecimal(result);
        }

        public async Task<List<AbleseEintrag>> GetAbleseTabelleAsync()
        {
            var ableseEintraege = new List<AbleseEintrag>();

            await using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync();

            var query = @"
        SELECT 
            p.platznr,
            pers.vorname,
            pers.nachname,
            COALESCE((
                SELECT r_w.neu
                FROM rechnungen r_w
                WHERE r_w.platz_id = p.id
                  AND r_w.type = 'Wasser'
                ORDER BY r_w.updated DESC, r_w.created DESC, r_w.jahr DESC, r_w.id DESC
                LIMIT 1
            ), 0) AS wasser_alt,
            COALESCE((
                SELECT r_s.neu
                FROM rechnungen r_s
                WHERE r_s.platz_id = p.id
                  AND r_s.type = 'Strom'
                ORDER BY r_s.updated DESC, r_s.created DESC, r_s.jahr DESC, r_s.id DESC
                LIMIT 1
            ), 0) AS strom_alt
        FROM camper c
        JOIN plaetze p ON c.platz_id = p.id
        JOIN camper_personen cp ON c.id = cp.camper_id
        JOIN personen pers ON cp.personen_id = pers.id
        WHERE c.active = 1
              AND c.deactivated = '0000-00-00 00:00:00'
              AND cp.rechnungsadresse = 1
        ORDER BY p.platznr;";

            await using var command = new MySqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                ableseEintraege.Add(new AbleseEintrag
                {
                    PlatzNr = reader.GetString("platznr"),
                    Vorname = reader.GetString("vorname"),
                    Nachname = reader.GetString("nachname"),
                    WasserAlt = Math.Round(reader.GetDecimal("wasser_alt"), 3),
                    StromAlt = Math.Round(reader.GetDecimal("strom_alt"), 2)
                });
            }

            return ableseEintraege;
        }

        public async Task UpdateRechnungAsync(Rechnung rechnung)
        {
            await using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync();

            var query = @"
        UPDATE rechnungen
        SET neu = @Neu, 
            verbrauch = @Verbrauch, 
            faktor = @Faktor, 
            betrag = @Betrag, 
            updated = NOW()
        WHERE id = @Id";

            await using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@Neu", rechnung.Neu);
            command.Parameters.AddWithValue("@Verbrauch", rechnung.Verbrauch);
            command.Parameters.AddWithValue("@Faktor", rechnung.Faktor);
            command.Parameters.AddWithValue("@Betrag", rechnung.Betrag);
            command.Parameters.AddWithValue("@Id", rechnung.Id);

            await command.ExecuteNonQueryAsync();
        }
    }
}