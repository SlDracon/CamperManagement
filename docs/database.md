# Datenbankumstellung

Die Anwendung kann `CAMPER_DB_CONNECTION` aus der Umgebung verwenden. Der bisherige lokale Standard bleibt für die vorhandene Installation erhalten. Tests und Migrationstool verwenden ausschließlich explizite Verbindungen.

Vor der ersten Nutzung der neuen Version sind die Migrationen 001 und 002 erforderlich. Zuerst alle anderen Anwendungsinstanzen schließen und eine vollständige konsistente Sicherung einschließlich Tabellen, Triggern und Routinen erstellen. Sicherungen sind vertraulich: `.local/backups/` ist von Git ausgeschlossen und sollte Modus 0700, die Dateien Modus 0600 erhalten. Die Sicherung enthält die bisherigen Tabellen; Rücksicherung nur bei gestoppter Anwendung und zunächst in eine separate Datenbank prüfen. DDL-Anweisungen sind unter MariaDB nicht gemeinsam transaktional rückgängig zu machen.

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
