# Einrichtung und Betrieb

[Zur Dokumentationsübersicht](../README.md)

## Voraussetzungen

| Bestandteil | Anforderung dieses Projekts |
|---|---|
| .NET | SDK 10; `global.json` fordert mindestens das Feature-Band 10.0.100 und erlaubt neuere .NET-10-Feature-Bands. Die CI verwendet 10.0.112. |
| Desktop | Grafische Linux-Sitzung; für das automatische Öffnen exportierter Dateien ein PDF-Viewer |
| Datenbank | Erreichbare MariaDB mit dem bestehenden CamperManagement-Basisschema; geprüft mit MariaDB 11.8.8 |
| Datenbankschema | Migrationen 001 bis 005 aus dem Repository angewendet |
| Pakete | Zugriff auf NuGet beim ersten Restore, sofern die Pakete nicht bereits lokal vorhanden sind |

Die Anwendung verbindet sich direkt mit der Datenbank. Sie hat keine lokale Offline-Datenbank und keinen Synchronisierungsdienst. Bei einem VLAN-Wechsel müssen Host, Port und Erreichbarkeit zwischen Arbeitsplatz und Datenbank zusammenpassen.

## Datenbankverbindung

Beim ersten Start auf einem neuen Gerät erscheint **Datenbank verbinden**. Server/IP-Adresse, Port, Datenbank, Benutzer und Passwort eingeben und **Verbindung prüfen und speichern** wählen. Geprüft werden die Verbindung und der Zugriff auf die aktuellen Standardfaktoren; das setzt die Standardfaktoren aus Migration 002 voraus. Für diese Anwendungsversion muss zusätzlich Migration 003 für zwei Vertragsnehmer, Migration 004 für die Camper-Historie und Migration 005 für begründete Vertragskostenerhöhungen angewendet sein. Nach erfolgreicher Prüfung werden die Listen geladen. Spätere Änderungen sind unter **Einstellungen → Datenbankverbindung** möglich.

Die Konfiguration liegt ausschließlich auf dem jeweiligen Gerät: unter Linux in `~/.local/share/CamperManagement/database.json` (beziehungsweise unter `XDG_DATA_HOME`), unter Windows in `%LOCALAPPDATA%/CamperManagement/database.json` und unter Android im privaten App-Datenverzeichnis. Unter Unix erhalten Verzeichnis und Datei die Rechte 0700 beziehungsweise 0600. Die Datei enthält das Passwort und ist nicht zusätzlich verschlüsselt; nicht teilen oder in Git aufnehmen. Eine Android-Deinstallation entfernt auch die lokale Konfiguration.

Die Umgebungsvariable **`CAMPER_DB_CONNECTION`** hat Vorrang vor dieser Datei. Ist sie gesetzt, lässt sich die Verbindung nicht über die Oberfläche speichern. Eine leere oder ungültige Variable ist ein Konfigurationsfehler; sie wird nicht still durch die lokale Datei ersetzt. Produktive Verbindungsdaten sind nicht mehr in der Anwendung eingebaut.

Aufbau eines Connection-Strings; alle großgeschriebenen Werte sind Platzhalter:

```text
Server=DB_HOST;Port=DB_PORT;Database=DB_NAME;User ID=DB_USER;Password=DB_PASSWORT;AllowZeroDateTime=True;ConvertZeroDateTime=True;
```

`DB_PORT` muss der tatsächlich bereitgestellte MariaDB-Port sein; bei einer Container-Portfreigabe kann er vom Standardport abweichen. Die beiden Datumsoptionen unterstützen die im bisherigen Schema vorhandenen Null-Datumswerte.

In einer Bash-Sitzung lässt sich der vollständige Connection-String verdeckt einlesen, ohne ihn als Befehl in der Shell-History abzulegen:

```bash
read -r -s -p 'Datenbankverbindung: ' CAMPER_DB_CONNECTION
printf '\n'
export CAMPER_DB_CONNECTION
```

Anschließend gestartete `dotnet`-Prozesse übernehmen diese Einstellung. Die Variable gilt für diese Sitzung; eine bereits laufende Anwendung erhält sie nicht nachträglich. In Rider die Variable in der Ausführungskonfiguration des Desktop-Projekts hinterlegen. Echte Zugangsdaten gehören weder in Dokumentationsbeispiele noch in Git.

## Bestehende Datenbank aktualisieren

Das Migrationstool führt Änderungen ausdrücklich aus. Der normale Anwendungsstart aktualisiert das Schema nicht.

1. Anwendung auf den verwendeten Geräten schließen.
2. Eine vollständige Datenbanksicherung erstellen und auf Vollständigkeit prüfen. Vorgehen und Speicherort stehen in der [Datenbankdokumentation](database.md).
3. `CAMPER_DB_CONNECTION` ausdrücklich auf die zu aktualisierende Datenbank setzen.
4. Verbindung prüfen und Migrationen anwenden:

```bash
dotnet run --project tools/CamperManagement.Migrate -- --check
dotnet run --project tools/CamperManagement.Migrate -- --apply
```

`--check` liest Serverversion und Rechnungsanzahl; es ersetzt weder eine Sicherungsprüfung noch eine vollständige Schemaprüfung. `--apply` führt noch fehlende Migrationen aus und meldet die Anzahl nicht eindeutig zugeordneter historischer Rechnungen.

| Exitcode | Bedeutung des Migrationstools |
|---|---|
| `0` | Prüfung erfolgreich beziehungsweise Migration ohne ungeklärte Rechnungsempfänger abgeschlossen |
| `1` | Fehler bei Verbindung oder Migration |
| `2` | Fehlende Verbindungskonfiguration oder ungültiger Aufruf |
| `3` | Migration ausgeführt, aber historische Rechnungsempfänger müssen noch geprüft werden |

Die Migrationen ergänzen und ändern das vorhandene Schema. Sie erstellen die ursprünglichen Tabellen `plaetze`, `camper`, `personen`, `camper_personen` und `rechnungen` nicht von Grund auf. Für einen neuen produktiven Server ist daher zunächst eine passende bestehende Datenbank beziehungsweise deren gesicherter Bestand bereitzustellen. Das Schema unter `tests/` ist eine Test-Fixture und keine Produktionsinstallation.

## Desktop mit der Kommandozeile

Die folgenden Befehle im Repository-Stamm ausführen. Der erste Build stellt benötigte NuGet-Pakete automatisch wieder her.

```bash
dotnet --list-sdks
dotnet build CamperManagement.Desktop/CamperManagement.Desktop.csproj -c Debug
dotnet run --project CamperManagement.Desktop/CamperManagement.Desktop.csproj -c Debug --no-build
```

Wird `dotnet` nicht gefunden, den Installationspfad des vorhandenen .NET-10-SDK verwenden oder in den Suchpfad aufnehmen. Auf dem bisher eingerichteten Arbeitsplatz liegt die CLI unter `$HOME/.dotnet/dotnet`.

Ein Release-Verzeichnis für Linux x64 erzeugen:

```bash
dotnet publish CamperManagement.Desktop/CamperManagement.Desktop.csproj \
  -c Release -r linux-x64 --self-contained false -o artifacts/linux
dotnet artifacts/linux/CamperManagement.Desktop.dll
```

Das gesamte Ausgabeverzeichnis wird benötigt. Wegen `--self-contained false` benötigt der Zielrechner zusätzlich die .NET-10-Laufzeit. Auch die veröffentlichte Anwendung benötigt ihre Datenbankverbindung und eine grafische Sitzung.

## Desktop und Tests in Rider

1. `CamperManagement.sln` öffnen und die installierte .NET-10-CLI als Toolchain verwenden.
2. Eine Ausführungskonfiguration für **CamperManagement.Desktop** mit Debug-Konfiguration auswählen beziehungsweise anlegen.
3. Darin bei Bedarf `CAMPER_DB_CONNECTION` setzen.
4. Gezielt das Desktop-Projekt bauen und starten.

Die vollständige Solution enthält auch iOS, Android und Browser. Fehlende mobile Workloads verhindern nicht die Arbeit am einzelnen Desktop-Projekt. Für reine Testarbeit steht zusätzlich **`CamperManagement.Tests.sln`** ohne mobile Projekte bereit. Datenbanktests benötigen eine eigene lokale Testinstanz und die Variable `CAMPER_TEST_DB`; siehe [Testanleitung](../tests/README.md).

Nach einem Build einer geänderten Version die Anwendung neu starten, damit die neuen Ansichten und Regeln geladen werden.

## Android, iOS und Browser

### Android

Benötigt werden der .NET-10-Android-Workload, Android SDK und JDK 21. Die SDK-/JDK-Pfade müssen zur lokalen Installation passen. Beispiel für einen bereits eingerichteten Rechner; den JDK-Platzhalter ersetzen:

```bash
dotnet workload install android
dotnet build CamperManagement.Android/CamperManagement.Android.csproj -c Debug \
  -p:AndroidSdkDirectory="$HOME/Android/Sdk" \
  -p:JavaSdkDirectory="/pfad/zum/jdk-21"
```

Der Build erzeugt ein Debug-APK unter `CamperManagement.Android/bin/Debug/net10.0-android/`. Für dessen Lauf sind zusätzlich ein Gerät oder Emulator sowie Netzwerkzugriff auf die Datenbank nötig. Für signierte APKs und portable Desktop-Pakete steht die [GitHub-Release-Pipeline](releases.md) bereit. Die bekannten 16-KB-Warnungen und noch ausstehenden Geräteprüfungen stehen unter [Plattformprüfungen](platform-checks.md).

### iOS

Für einen iOS-Build werden ein Mac, ein zur .NET-iOS-Toolchain passendes Xcode und der iOS-Workload benötigt. Auf dem bisher verwendeten Linux-Rechner wurde iOS nicht gebaut. Ein Projekt in der Solution ersetzt diese Voraussetzungen nicht.

### Browser

Das Browser-Projekt ist vorhanden und wurde kompiliert. Der Datenbankzugriff der Anwendung verwendet aber eine direkte MySQL-Verbindung. Für einen verwendbaren Browserbetrieb ist eine separate Backend-API nötig; allein das Bereitstellen der Browser-Build-Dateien reicht nicht aus.

## Fehlerhilfe

| Beobachtung | Nächster Schritt |
|---|---|
| Camper oder Rechnungen konnten nicht geladen werden | Verbindung, Host, freigegebenen Port, VLAN-Erreichbarkeit und Datenbankberechtigungen prüfen; danach **Neu laden** |
| Faktoren konnten nicht geladen werden | Datenbankverbindung und Anwendung der Migration 002 prüfen |
| Faktoren wurden auf einem anderen Gerät geändert | In den Einstellungen **Neu laden**, den aktuellen Stand prüfen und die gewünschte Änderung erneut speichern |
| Speichern im Rechnungsformular ist deaktiviert | Platz/Art und Zahlen prüfen; gültiges Jahr wählen und das Ende des Ladevorgangs abwarten |
| Rechnungsempfänger muss zugeordnet werden | Historische Zuordnung administrativ prüfen; keine PDF-Erstellung mit geratenem Empfänger |
| PDF gespeichert, Viewer nicht verfügbar | Datei direkt am gewählten Speicherort öffnen und PDF-Zuordnung des Betriebssystems prüfen |
| PDFs gespeichert, Druckstatus nicht gespeichert | Dateien prüfen, Verbindung wiederherstellen und Status nach erneutem Laden kontrollieren; ein erneuter Export kann zusätzliche Dateien erzeugen |
| Integrationstests melden fehlendes `CAMPER_TEST_DB` | Separate Testinstanz nach [Testanleitung](../tests/README.md) starten und die Testvariable setzen |
| Gesamte Solution baut unter Linux nicht | Zunächst nur Desktop beziehungsweise die Test-Solution bauen; Fehler zu iOS oder fehlenden mobilen Workloads getrennt behandeln |

Bei einem Prozessabsturz zunächst feststellen, ob Rider oder die gestartete Anwendung abstürzt. Einen Fehler der Anwendung möglichst mit dem gezielten Desktop-Build und einem Start im Terminal nachvollziehen. SDK-Version, Plattform, vollständige Fehlermeldung und auslösenden Arbeitsschritt festhalten. Für Testfälle synthetische Daten verwenden; Verbindungskennwörter aus weitergegebenen Protokollen entfernen.

## Lokale Fehlerprotokolle

Technische Fehler werden unter `LocalApplicationData/CamperManagement/logs/` gespeichert: unter Linux normalerweise `~/.local/share/CamperManagement/logs/`, unter Windows `%LOCALAPPDATA%/CamperManagement/logs/`, unter Android im privaten App-Verzeichnis. Es gibt höchstens zwei Dateien (`errors.jsonl` und `errors.previous.jsonl`) mit jeweils 256 KiB. Unix-Verzeichnisse haben Rechte 0700, Dateien 0600.

Die Einträge enthalten UTC-Zeit, Operationsart, Exception-Typ, HResult, Typ der inneren Exception und bis zu zwölf Methodennamen. Kennwörter, Connection-Strings, SQL, Kundendaten, Exception-Nachrichten und lokale Quelldateipfade werden nicht geschrieben. Kann das Protokoll nicht geschrieben werden, bleibt die ursprüngliche Benutzeraktion davon unabhängig. Das Protokoll erfasst behandelte Anwendungsfehler; für native Prozessabstürze sind zusätzlich Systemprotokolle bzw. Android `logcat` nötig. Die bestehende Speicherung der Datenbankzugangsdaten bleibt unverändert.
