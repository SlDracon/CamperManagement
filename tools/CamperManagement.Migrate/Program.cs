using CamperManagement.Services;
using MySqlConnector;
var connection=Environment.GetEnvironmentVariable("CAMPER_DB_CONNECTION");
if(string.IsNullOrWhiteSpace(connection)){Console.Error.WriteLine("CAMPER_DB_CONNECTION muss ausdrücklich gesetzt sein.");return 2;}
if(args.Length!=1 || args[0] is not ("--check" or "--apply")){Console.Error.WriteLine("Aufruf: --check oder --apply. Vor --apply vollständige Sicherung erstellen; siehe docs/database.md.");return 2;}
var db=new DatabaseService(connection);
try{
 if(args[0]=="--apply"){var unresolved=await SchemaMigration.ApplyAsync(db);Console.WriteLine($"Migration abgeschlossen. Nicht eindeutig zugeordnete Rechnungen: {unresolved}");return unresolved==0?0:3;}
 await using var c=await db.OpenConnectionAsync();
 await using var cmd=new MySqlCommand("SELECT VERSION(),(SELECT COUNT(*) FROM rechnungen)",c);await using var reader=await cmd.ExecuteReaderAsync();await reader.ReadAsync();Console.WriteLine($"Server: {reader.GetString(0)}; Rechnungen: {reader.GetInt32(1)}");return 0;
}catch(Exception ex){Console.Error.WriteLine($"Migration fehlgeschlagen ({ex.GetType().Name}). Keine Zugangsdaten werden ausgegeben. Datenbankprotokoll prüfen und Sicherung behalten.");return 1;}
