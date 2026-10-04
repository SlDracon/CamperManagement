# Datenbankumstellung

[Zur Dokumentationsübersicht](../README.md)

Die Anwendung kann `CAMPER_DB_CONNECTION` aus der Umgebung verwenden. Der bisherige lokale Standard bleibt für die vorhandene Installation erhalten. Tests und Migrationstool verwenden ausschließlich explizite Verbindungen.

Vor der ersten Nutzung der neuen Version sind die Migrationen 001 bis 005 erforderlich. Zuerst alle anderen Anwendungsinstanzen schließen und eine vollständige konsistente Sicherung einschließlich Tabellen, Triggern und Routinen erstellen. Sicherungen sind vertraulich: `.local/backups/` ist von Git ausgeschlossen und sollte Modus 0700, die Dateien Modus 0600 erhalten. Die Sicherung enthält die bisherigen Tabellen; Rücksicherung nur bei gestoppter Anwendung und zunächst in eine separate Datenbank prüfen. DDL-Anweisungen sind unter MariaDB nicht gemeinsam transaktional rückgängig zu machen.

```bash
# Connection-String im geschützten Benutzerkontext setzen, niemals ins Repository schreiben.
dotnet run --project tools/CamperManagement.Migrate -- --check
dotnet run --project tools/CamperManagement.Migrate -- --apply
```

Das Tool verlangt `CAMPER_DB_CONNECTION`; es verwendet nie automatisch die produktive Standardverbindung. Migrationen laufen unter einem Datenbanklock und werden in `camper_schema_version` versioniert. Wiederholte Anwendung verändert vorhandene Snapshots nicht.

Migration 001:

- Rechnungswerte von FLOAT zu DECIMAL(18,6), Beträge zu DECIMAL(18,2). Alte FLOAT-Rundungsfehler lassen sich nicht rekonstruieren; die Umstellung verhindert neue binäre Gleitkommaabweichungen. Historische Faktorwerte werden nicht mit neuen Tarifen überschrieben.
- PLZ als Text mit Erhalt deutscher führender Nullen.
- `rechnung_empfaenger`: dauerhafter Camperbezug und Kopie der Rechnungsanschrift/Vertragskosten. Wechsel und Änderungen von Stammdaten verändern alte Rechnungen nicht.
- Historische Zuordnung anhand des Erfassungszeitpunkts und der Belegungsintervalle. Bei exakt identischen Rechnungsdaten in doppelten Stammsätzen wird der jüngste passende Belegerdatensatz verwendet; unterschiedliche mögliche Empfänger werden ausdrücklich nicht geraten. Ungeklärte Rechnungen bleiben sichtbar und können nicht versehentlich an den falschen Empfänger exportiert werden.
- `jahresfaktoren`: ursprüngliche jahresbezogene Speicherung. Sie bleibt nach Migration 002 als Altbestand erhalten und wird von der Anwendung nicht mehr verwendet.

Migration 002:

- `standardfaktoren`: genau ein zentraler Datensatz für den aktuellen Strom- und Wasserpreis mit Versionsprüfung gegen konkurrierende Änderungen.
- Übernimmt bei der Umstellung die Werte des aktuellen beziehungsweise letzten verfügbaren vergangenen Jahres. Gibt es ausschließlich zukünftige Werte, werden diese übernommen; ohne vorhandene Werte gelten 0,5 und 8. Bereits angelegte aktuelle Faktoren werden bei einer Wiederholung niemals überschrieben.
- Speichern wirkt ab sofort auf neue Rechnungen, unabhängig vom Abrechnungsjahr. Es gibt keine Jahresauswahl, Vorjahresübernahme oder automatische Tarifänderung zum Jahreswechsel mehr.
- Bestehende Rechnungen behalten ihren gespeicherten Faktor. Beim ausdrücklich ausgelösten Wechsel zwischen Wasser und Strom gilt weiterhin der aktuelle Standardfaktor des gewählten Typs. Die Änderung eines Faktors allein löst weiterhin keine automatische Neuberechnung aus.
- Neue Formulare und die fortlaufende Erfassung laden aktuelle Faktoren erneut. Bereits gespeicherte Rechnungen und ihre Beträge werden durch die Migration nicht verändert.

Camperwechsel sperren den betroffenen Platz und führen Deaktivierung, Person, Camper und Verknüpfung in einer Transaktion aus. Druckstatus wird erst nach vollständigem PDF-Export und dann für die ganze Auswahl in einer Transaktion gespeichert. Exportierte PDFs werden bei einem späteren Statusfehler nicht gelöscht; die Anwendung meldet beide Ergebnisse getrennt.

## Sicherung und Wiederherstellung

Eine vollständige Sicherung enthält neben den Daten das Tabellenschema und vorhandene Trigger beziehungsweise Routinen. Für eine konsistente Sicherung mit `--single-transaction` müssen die betroffenen Tabellen eine transaktionale Engine wie InnoDB verwenden; währenddessen keine Schemaänderungen ausführen. Die bisherigen Tabellen verwenden InnoDB.

Mit einem installierten MariaDB-Client kann die Sicherung wie folgt vorbereitet werden:

```bash
umask 077
mkdir -p .local/backups
chmod 700 .local .local/backups
```

Die Client-Zugangsdaten lokal in `.local/mariadb-client.cnf` hinterlegen, Platzhalter ersetzen und Dateirechte auf `0600` setzen. Diese Datei wird wegen `.local/` nicht von Git erfasst. Sie ist eine separate Konfiguration für die MariaDB-CLI; die Anwendung selbst liest weiterhin `CAMPER_DB_CONNECTION`.

```ini
[client]
host=DB_HOST
port=DB_PORT
user=DB_USER
password="DB_PASSWORT"
```

Nach dem Erstellen der Datei:

```bash
chmod 600 .local/mariadb-client.cnf
camper_backup_file=".local/backups/camper-$(date +%Y%m%d-%H%M%S).sql"
mariadb-dump --defaults-extra-file=.local/mariadb-client.cnf \
  --single-transaction --routines --triggers --hex-blob DB_NAME \
  > "$camper_backup_file" && sha256sum "$camper_backup_file" > "$camper_backup_file.sha256"
```

`DB_NAME` durch den richtigen Datenbanknamen ersetzen. Der verwendete Benutzer benötigt die Rechte zum Lesen der gesicherten Daten und Objekte. Bei einem Fehler ist eine eventuell schon angelegte Datei keine verifizierte Sicherung. Exitcode, Dateigröße und Dump-Abschluss prüfen; eine Prüfsumme allein weist keine Wiederherstellbarkeit nach.

Eine Wiederherstellung zunächst manuell auf einem getrennten Server beziehungsweise in einer separat vorbereiteten Datenbank erproben. Tabellen, Rechnungsempfänger, Rechnungsanzahl und Jahressummen prüfen. Eine produktive Rücksicherung benötigt ein abgestimmtes Wartungsfenster mit geschlossenen Anwendungsinstanzen und dem zur Sicherung passenden Anwendungsstand. Produktive Sicherungen nicht als Datenquelle der automatisierten Tests verwenden; diese arbeiten mit eigenen synthetischen Fixtures.

## Migration 003: Zwei Vertragsnehmer

Die Migration ergänzt `camper_personen.vertragsnehmer_nr` (1 oder 2, pro Camper eindeutig), `camper.gemeinsame_adresse` und die zweiten Namensfelder in `rechnung_empfaenger`. Der bisher ausgewählte Rechnungsempfänger wird zum ersten Vertragsnehmer. Zusätzliche Kontakte werden nicht automatisch zu Vertragsnehmern; kombinierte Namen und „Eheleute“-Einträge werden nicht aufgeteilt.

Beide Personen und die Wahl der Rechnungsadresse werden zusammen in einer Transaktion gespeichert. Bei gemeinsamer Anschrift gilt die Adresse der ersten Person. Das Entfernen des zweiten Vertragsnehmers entfernt nur dessen Vertragsverknüpfung; zusätzliche Kontakte und historische Rechnungen bleiben erhalten. Neue Rechnungen speichern beide Namen und die gewählte Anschrift als unveränderliche Kopie.

Vor Nutzung der neuen Felder Migration 003 ausführen und alle verwendeten Clients auf diese Version aktualisieren. Alte Clients kennen die zwei Rollen nicht und dürfen diese Stammdaten anschließend nicht mehr bearbeiten. Eine Wiederholung der Migration überschreibt keine bereits eingerichteten Rollen und ergänzt keine zweiten Namen in alten Rechnungen.

## Migration 004: Camper-Historie

`camper_historie` speichert Ereignisart, UTC-Zeitpunkt sowie vollständige Vorher-/Nachher-Stände in versionierten JSON-Dokumenten. Die Oberfläche zeigt Zeitpunkte in der lokalen Zeitzone an. Migration 004 erzeugt eine Bestandsaufnahme für jede bisherige Belegung, auch inaktive und unvollständig zugeordnete Datensätze. Vorhandene `created`-/`deactivated`-Werte bleiben als Belegungsdaten erhalten; der Zeitpunkt der Bestandsaufnahme wird nicht als ursprünglicher Änderungszeitpunkt ausgegeben. Wiederholung erzeugt keine doppelten Bestandsaufnahmen und verändert keine vorhandenen Historieneinträge.

Die Anwendung schreibt Historie und Stammdaten in derselben Transaktion. Ein Fehler beim Historieneintrag rollt den gesamten Schreibvorgang zurück. Bei Belegungswechseln werden das Ende der alten und der Beginn der neuen Belegung getrennt aufgezeichnet. Rechnungsdaten und frühere Rechnungsempfänger bleiben unverändert.

Alle schreibenden Clients müssen auf diese Version aktualisiert werden. Ältere Clients oder direkte SQL-Änderungen umgehen die Anwendungsprotokollierung. Die Historie enthält personenbezogene Daten und gehört wie die übrigen Tabellen in die geschützte Datenbanksicherung. Sie ist kein manipulationssicheres Auditprotokoll; eine Benutzerzuordnung wird mangels Benutzeranmeldung nicht behauptet.

## Migration 005: Vertragskostenerhöhungen

Die Migration ergänzt die optionale Spalte `camper_historie.beschreibung` (`VARCHAR(1000)`). Bestehende Historieneinträge, Camperpreise und Rechnungskopien bleiben unverändert; es werden keine Erhöhungen automatisch gebucht. Die Migration ist nach einem Abbruch wiederholbar.

Eine Buchung aktualisiert `camper.Vertragskosten` und schreibt das Ereignis `cost_increased` samt Begründung und vollständigen Vorher-/Nachher-Ständen in derselben Transaktion. Der Erhöhungsbetrag ergibt sich aus den beiden gespeicherten Preisen. Ein Fehler beim Historieneintrag rollt den Preis zurück. Alle Schreibvorgänge sperren zuerst den Platz; Buchung und Stammdatenbearbeitung prüfen zusätzlich den vom Formular erwarteten bisherigen Vertragsbetrag. Bei einer Preisänderung seit dem Laden wird ohne Schreibwirkung abgebrochen. Alte Clients besitzen diese Prüfung nicht; deshalb alle schreibenden Clients aktualisieren.

Buchungen sind an die aktive Belegung gebunden, nicht an ihren späteren Nachfolger. Bereits gespeicherte Rechnungsempfänger und Vertragskosten bleiben unverändert. Die bestehende Historienansicht zeigt und durchsucht die Begründung; ihre bisherigen Grenzen für direkte SQL-Zugriffe und externe Änderungen gelten weiterhin.
