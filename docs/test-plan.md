# Testplan CamperManagement

> Historischer Planungsstand vor der Umsetzung. Den aktuellen Nachweis und verbleibende Geräteprüfungen dokumentieren `test-coverage.md` und `platform-checks.md`.

> Spätere fachliche Korrektur: Geänderte Faktoren gelten nach dem Speichern sofort für neue Rechnungen, unabhängig vom Abrechnungsjahr. Die folgenden ursprünglichen Jahresfaktor-/Vorjahresübernahme-Szenarien sind dadurch überholt; bestehende Rechnungen behalten ihre Faktoren.

Stand: 26. September 2026. Grundlage: Commit `34ca942` plus die aktuell noch nicht committeten PDF-/Threading-Korrekturen und der Regressionstest-Runner. Projekt: `/home/sldracon/Dokumente/CamperManagement`.

Dieser Plan enthält **120 konkrete Testszenarien**. Mehrere Szenarien werden parametrisiert; die Zahl der Testausführungen wird deshalb höher liegen. Die Zahl ist ein aus den Risiken abgeleiteter Startumfang, keine Quote. Neue fachliche Entscheidungen und gefundene Fehler ergänzen ihn. In diesem Schritt wurden keine weiteren Produkttests oder Fehlerkorrekturen implementiert.

## 1. Tatsächlicher Ausgangsstand

- Fünf .NET-10-Projekte: gemeinsame Avalonia-Anwendung, Desktop, Android, iOS und Browser.
- Rund 2.800 Zeilen in Services, ViewModels, Modellen und Selection-Behavior; zwei zentrale Services, 14 öffentliche Datenbankoperationen, fünf PDF-Exportabläufe.
- Es gibt inzwischen `tests/CamperManagement.RegressionTests`: einen selbstgebauten Console-Runner, keinen regulär vom Testadapter entdeckten xUnit/NUnit-Testbestand. Der Runner wurde bei der Bestandsaufnahme erfolgreich ausgeführt.
- Bereits geprüft: gewählte Dateihandles/Dateinamen, Unicode-Ziele, Abbruch, tatsächliche PDF-Erzeugung/Seiten, Auswahllisten-Snapshot, Temp-Cleanup, Gruppenexport/Duplikatnamen, Launcher und simulierte UI-Responsiveness. Diese Tests übernehmen und erweitern, nicht wegwerfen.
- Noch nicht geprüft: echte Datenbankabfragen/Transaktionen, Geldberechnung, Formular-Workflows, echte Avalonia-Bindings, native Dateidialoge und reale Geräte. Der aktuelle Runner prüft insbesondere **nicht**, ob nach einem Abbruch wirklich kein Datenbankstatus geändert wird; dafür fehlt ein Workflow-Test mit austauschbarem Repository.
- Kein versioniertes SQL-Schema, keine Migrationen und keine CI-Konfiguration im durchgesehenen Projekt gefunden. Datenbanktyp, Version, Constraints, Defaults, Collation und `sql_mode` wurden nicht live erhoben. Keine Produktivdaten gelesen oder verändert.

## 2. Konkrete Risiken aus dem Code

| Befund | Codebezug im Projekt | Konsequenz für Tests |
|---|---|---|
| Art ist im Edit-Formular veränderbar, Update-SQL speichert `type` nicht | `EditRechnungView.axaml`; `DatabaseService.UpdateRechnungAsync`, ab Zeile 484 | V10/D21 prüfen den fachlich festgelegten Edit-Vertrag nach erneutem Laden. |
| Deaktivierung liegt außerhalb der Transaktion zum Anlegen des neuen Campers | `AddCamperViewModel.SaveCamperAsync`, ab Zeile 72 | V12/D17: Fehler darf nicht den bisherigen Beleger verlieren lassen. |
| Personenverknüpfung wird nur per UPDATE geändert; kein INSERT für leeren Platz | `DatabaseService.AddNewCamperAsync`, ab Zeile 162 | D15 prüft erstmals belegten Platz und Sichtbarkeit nach Lesen. |
| Camper-Update begrenzt sich auf Platznummer, nicht aktiven Camper | `DatabaseService.UpdateCamperAsync`, ab Zeile 241 | D18 schützt Historie und weitere Personen. |
| Aktivitätsfilter und Bestimmung des letzten Messwerts unterscheiden sich | `GetActiveCampersAsync`, `GetRechnungenAsync`, `GetNeuFromLatestRechnungAsync`, `GetAbleseTabelleAsync` | D01 prüft konsistente Aktivitätsfilter; D12 prüft die bestätigte Auswahl nach Erfassungsdatum und Rechnungstyp. |
| Rechnungen werden über Platz an Camper/Personen gejoint | `GetRechnungenAsync`, `GetRechnungenForJahrAsync` | D02/D07/D09: historische Zuordnung und Summenvervielfachung prüfen; Schema fehlt noch. |
| Async-PropertyChanged und nicht abgewartete Ladeaufrufe | `AddRechnungViewModel` sowie mehrere Konstruktoren | A01–A04 kontrollieren Antwortreihenfolge, Fehler und Doppelaktionen. |
| MainView erzeugt eigenen MainViewModel; Bootstrap erzeugt ebenfalls einen | `Views/MainView.axaml.cs:11`, `MainWindowViewModel`, `App` | U03 prüft doppelte Instanzen und Datenzugriffe im echten View-Aufbau. |
| Fokus-Callback wird nur im View-Konstruktor bei bereits gesetztem DataContext registriert | `Views/AddRechnungView.axaml.cs:15` | U06 setzt DataContext absichtlich erst nach Erstellung. |
| AddCamper lädt die Liste unmittelbar nach Öffnen des Formulars neu | `CamperViewModel.OpenAddCamperDialog`, ab Zeile 66 | V15 fordert Aktualisierung nach erfolgreichem Speichern. |
| Neue Hintergrundexports verwenden Listen-Snapshots mit veränderbaren Objektreferenzen | `PdfService` und `RechnungenViewModel` | A07 unterscheidet Listenänderungen von Änderungen einzelner Rechnungswerte. |

Das sind statisch begründete Befunde/Risiken, keine bereits ausgeführten SQL-Fehlernachweise. Die Soll-Regeln dürfen nicht durch Kopieren des aktuellen fehlerhaften Verhaltens in Tests festgeschrieben werden.

## 3. Testaufbau und kleine notwendige Vorarbeiten

Vorgeschlagene Projekte unter `tests/`:

| Projekt | Inhalt | Laufzeitumgebung |
|---|---|---|
| `CamperManagement.UnitTests` | Berechnung, Suche, Navigation, Formulare/Workflows mit Fakes | .NET 10, ohne Netzwerk/Display |
| `CamperManagement.IntegrationTests` | Alle DatabaseService-Operationen und Transaktionen | Isolierte Datenbankcontainer |
| `CamperManagement.PdfTests` | Echte PDFs, Storage-/Launcher-Fakes, Dateifehler | Lokales Temp-Verzeichnis, kein Drucker |
| `CamperManagement.UiTests` | Controls, Bindings, Selection-Behavior, echter Dispatcher | Avalonia Headless |

Ein gemeinsamer, in Rider und `dotnet test` entdeckbarer Testadapter. Für Avalonia 11 ist dessen passendes `Avalonia.Headless.XUnit` mit einer kompatiblen xUnit-Generation zu wählen; kein ungetestetes Mischen mit xUnit v3. Versionen zentral festhalten. Die [Avalonia-11-Dokumentation](https://v11.docs.avaloniaui.net/docs/concepts/headless/headless-xunit/) beschreibt die UI-Thread-Integration über AvaloniaFact/AvaloniaTheory. Ein Framework-Upgrade auf Avalonia 12 ist dafür nicht nötig.

Vor den jeweiligen Testgruppen gezielt vorbereiten:

1. `DatabaseService` über eine injizierbare Schnittstelle und Connection-Factory/Connection-String verfügbar machen. Konstruktoren dürfen keine versteckten Produktivverbindungen aufbauen. Kein vollständiger Architekturumbau erforderlich.
2. Initialisierung als abwartbares `InitializeAsync`/Lade-Command modellieren; Navigation und PDF-Export für Workflow-Tests austauschbar machen. Test-App bootstrappt mit Fakes statt mit dem Produktions-`MainViewModel`.
3. Rechenregeln und Suchparser soweit nötig in reine Funktionen auslagern. Keine Tests auf private Methoden per Reflection, keine Getter-/Setter-Tests ohne Verhalten.
4. Zeit über `TimeProvider`/injizierte Uhr kontrollieren. Testdaten werden mit festem Jahr/Datum erzeugt; finanzielle Sollwerte stammen aus Beispielen, nicht aus der getesteten Rechenfunktion.
5. Test-Solution/Filter für gemeinsame Bibliothek und Tests, damit `dotnet test` nicht an fehlenden iOS/Android-Workloads scheitert. Testprojekte nicht von mobilen Heads abhängig machen.
6. Bestehenden Console-Runner in einzeln entdeckbare Tests überführen, sodass ein Fehler nicht alle folgenden Fälle überspringt. Globales `TMPDIR`/Culture/Dispatcher nicht ungeschützt zwischen parallelen Tests ändern; besser Temp-Pfad injizieren oder betroffene Tests isolieren.

Für SQL echte MySQL-/MariaDB-Container entsprechend dem tatsächlich verwendeten Server einsetzen; [Testcontainers bietet beide Module](https://dotnet.testcontainers.org/modules/). Vorher Schema/Constraints/Defaults ausschließlich lesend erheben, danach versionierte Test-DDL und synthetische Seeds erstellen. Keine Produktionsdatenkopie und keine produktiven Schreibtests. Der Container-Endpunkt wird explizit injiziert; kein Fallback auf den derzeit hart codierten Server.

## 4. Testdaten und Sollregeln

Datenbank-Fixture: freie und belegte Plätze, alphanumerische Platznummern, aktiver und historischer Beleger, zwei Personen pro Camper mit genau einer Rechnungsadresse, beide Rechnungsarten, zwei Jahre, korrigierte ältere Rechnung, gleiche Zeitstempel mit verschiedenen IDs, optionale NULL-Felder und mehrere Druckzustände. Für Failures eigene Transaktionen/Verbindungen und kontrollierte Constraint-Verletzungen einsetzen. Schema pro Test oder sicherer Reset; kein gemeinsamer veränderbarer Bestand zwischen Tests.

PDF-Fixture: rein erfundene Namen/Adressen, deutsche Sonderzeichen, lange Werte, bekannte Dezimalbeträge. Text/Summen/Seitengröße/-reihenfolge prüfen; wegen Zeitstempeln und internen PDF-IDs kein vollständiger Bytevergleich als Normalfall. Ein kleiner gerenderter Layoutsatz ergänzt die Inhaltsprüfung.

Bestätigte Fachregel (Nutzerrückmeldung vom 26. September 2026): Eine reine Änderung des Faktors löst absichtlich keine Neuberechnung aus. Faktoränderungen kommen selten vor und betreffen nur ein neues Jahr. Dieses Verhalten ist kein Fehler; R03 sichert es ab. Daraus wird keine zusätzliche automatische Tarifumstellung abgeleitet.

Bestätigte Rundungsregel: Rechnungsbeträge werden kaufmännisch auf zwei Nachkommastellen gerundet (`MidpointRounding.AwayFromZero`): 1,005 € → 1,01 € und 1,015 € → 1,02 €. R05 prüft diese Regel; die bisherige ToEven-Rundung ist entsprechend anzupassen. Jede Rechnungsposition wird zuerst kaufmännisch auf Cent gerundet; anschließend werden die gerundeten Positionsbeträge summiert. Beispiel: zwei Positionen à 1,005 € ergeben 1,01 € + 1,01 € = 2,02 €. R05 sowie die Summenprüfungen D09, P06 und P11 sichern diese Reihenfolge ab. Diese Entscheidungen wurden hier nur dokumentiert, noch nicht implementiert.

Bestätigte Zählerstandsregel: Ein neuer Zählerstand unter dem alten ist zulässig, muss aber eine sichtbare Warnung auslösen. Die Speicherung bleibt möglich. Daraus folgt keine automatische Sonderberechnung für einen Zählerwechsel; eine solche wurde nicht festgelegt.

Bestätigte historische Zuordnung: Ältere Rechnungen bleiben nach einem Camperwechsel dem damaligen Camper zugeordnet. Der neue Beleger desselben Platzes darf nicht automatisch Empfänger alter Rechnungen werden. D02/D07/D09 und PDF-Empfängerprüfungen müssen dies absichern.

Bestätigte Auswahl des alten Zählerstands: Für den betreffenden Platz den neuen Zählerstand aus der zuletzt erfassten Rechnung desselben Typs (Wasser bzw. Strom) übernehmen. Maßgeblich ist das Erfassungsdatum, nicht das Abrechnungsjahr oder der letzte Bearbeitungszeitpunkt. D12/R04 und die asynchronen Auswahltests sichern die Trennung der Typen und die Reihenfolge ab.

Bestätigter Druckstatus: Erst wenn alle beim Export ausgewählten Rechnungen erfolgreich als PDF gespeichert wurden, wird die gesamte exportierte Auswahl als „gedruckt“ markiert. Bei einem Teilfehler darf für keine Rechnung dieser Auswahl ein neuer Druckstatus gesetzt werden; bestehende Status bleiben unverändert. Bereits gespeicherte Dateien allein reichen nicht für eine Teilmarkierung aus. Diese Regel fordert nicht das Löschen bereits gespeicherter PDFs. Das Öffnen im Viewer und ein tatsächlicher Druckerauftrag sind keine Voraussetzung. Abbruch oder fehlgeschlagene Speicherung dürfen den Status nicht setzen; ein anschließender Viewer-Fehler ändert den erfolgreichen Export nicht. Workflow- und Exporttests prüfen diese Regel.

Bestätigte Gesamtsumme: Nur Wasser und Strom werden zur Gesamtsumme addiert. Vertragskosten gehen nicht in diese Summe ein. Separat angezeigte Vertragskosten dürfen bestehen bleiben. R10/D09/P06 prüfen insbesondere, dass positive Vertragskosten die Gesamtsumme nicht erhöhen.

Bestätigter Artwechsel: Beim Wechsel zwischen Wasser und Strom wird automatisch der jeweilige Standardfaktor eingesetzt. Ein zuvor manuell eingegebener Faktor wird dabei überschrieben. R04 prüft beide Wechselrichtungen und den Fall eines zuvor manuell geänderten Faktors. Die Regel zur reinen Faktoränderung ohne automatische Neuberechnung bleibt bestehen.

Gewünschte Erweiterung: Eine Einstellungsseite soll die Standardfaktoren für Wasser und Strom bearbeitbar machen. Bestätigt: Standardfaktoren für Wasser und Strom werden je Abrechnungsjahr gespeichert. Neue Rechnungen verwenden die Faktoren ihres Abrechnungsjahres. Bestehende Rechnungen behalten ihren gespeicherten Faktor; Änderungen an den Einstellungen dürfen alte Rechnungen nicht rückwirkend verändern. Bei einem ausdrücklich ausgelösten Artwechsel gilt weiterhin die bestätigte Regel zum Einsetzen des passenden Standardfaktors. Fehlen Faktoren für ein neues Abrechnungsjahr, werden die Vorjahreswerte übernommen. Eine Änderung der übernommenen Werte darf die Werte des Vorjahres oder bestehende Rechnungen nicht verändern. Tests müssen die Übernahme beider Arten und diese Unabhängigkeit prüfen. Die Jahresfaktoren werden zentral in der vorhandenen TrueNAS-Datenbank gespeichert und gelten auf allen Geräten. Das konkrete Schema ist technisch festzulegen. Tests müssen Persistenz, Abruf durch eine zweite App-Instanz und sichtbare Fehler bei nicht erreichbarer Datenbank absichern. Dies ist zusätzlicher Funktionsumfang; E01–E10 ergänzen die bisherigen 110 Testszenarien um zehn gezielte Einstellungsszenarien. In dieser Planungsrunde wurde die Seite noch nicht implementiert.

Bestätigte Pflichtfelder beim Anlegen eines Campers: Platznummer, Vorname, Nachname sowie vollständige Anschrift (Straße, PLZ und Ort). Anrede und E-Mail sind optional. V14 prüft jedes fehlende Pflichtfeld einzeln, reine Leerzeichen in Pflichttextfeldern sowie erfolgreiches Speichern ohne Anrede/E-Mail.

Bestätigter Jahresbereich für neue Rechnungen: Zulässig sind ausschließlich das aktuelle Kalenderjahr und das unmittelbar vorherige Jahr (2026 also 2025/2026). Ältere oder zukünftige Abrechnungsjahre dürfen beim Anlegen nicht gespeichert werden. V01/V03 prüfen beide erlaubten Jahre, die angrenzenden ungültigen Jahre sowie den Jahreswechsel mit injizierter Uhr. Die Regel beschränkt die Neuanlage; daraus folgt weder das Ausblenden noch das Löschen bestehender älterer Rechnungen.

Bestätigte Vertragskostenregel: Vertragskosten sind optional. Ein leeres Feld wird als 0 € gespeichert. Ein eingegebener Betrag bleibt separat erhalten und erhöht die Gesamtsumme aus Wasser und Strom nicht. Nicht numerische Eingaben dürfen nicht stillschweigend zu 0 € werden.

Technische Vorarbeiten und Festlegungen für die Umsetzung:

- Jahreseinstellungen: Versioniertes Schema für Faktoren pro Jahr/Art und Initialisierung anhand der vorhandenen Standardfaktoren vorsehen. Vorhandene Rechnungsfaktoren bei der Migration erhalten; E01–E10 decken die Erweiterung ab.
- Technisch anhand des Datenbankschemas klären: Welche Beziehung sichert die bestätigte historische Rechnungszuordnung dauerhaft? Die fachliche Empfängerregel ist entschieden.
- Technische Festlegung für identische Erfassungszeitpunkte: einen stabilen Tie-Breaker anhand des Schemas wählen. Die Auswahl nach Erfassungsdatum und Rechnungstyp ist fachlich entschieden.
- Vorhandene Datenbank-Constraints und Aktivitätskennzeichen lesend prüfen; historische Daten und leere optionale Formularfelder müssen mit dem Schema vereinbar sein. Technische Entscheidungen dokumentieren, ohne sie als vom Nutzer bestätigte Fachregeln auszugeben.

## 5. Testkatalog

P0: schützt Geldbeträge, Datenbestand oder Kernablauf; vor Freigabe umsetzen. P1: normale Benutzerabläufe, Fehlertoleranz und Plattformverhalten. P2: nur bei entsprechendem Produktziel/Hardware verfügbar. Die Tabellen beschreiben Szenarien, nicht zwingend je eine Testmethode.

### R: Berechnung und Zahlenformate — 10 Szenarien

Unit; AddRechnungViewModel, EditRechnungViewModel, RechnungDisplayModel, AbleseEintrag, KostenEintrag.

| ID | Priorität | Szenario | Erwartung / Varianten |
|---|---|---|---|
| R01 | P0 | Strom und Wasser berechnen | Alt 100, Neu 112,5; Faktoren 0,5 und 8 → Verbrauch 12,5 und Betrag 6,25 bzw. 100,00. Als fachlich bestätigte Eingabeparameter prüfen, nicht dauerhaft Tarifwerte im Test festschreiben. |
| R02 | P0 | Alt und Neu nachträglich ändern | Verbrauch und Betrag werden nach jeder Änderung gemeinsam aktualisiert; Add und Edit verhalten sich identisch. |
| R03 | P1 | Nur Faktor ändern | Bestätigtes Verhalten in Add/Edit: Bei Verbrauch 10 und Betrag 5 bleibt nach reiner Faktoränderung 0,5 → 0,6 der Betrag 5. Kein automatischer Neuberechnungsauslöser; die seltene Anpassung betrifft ein neues Jahr. |
| R04 | P0 | Art wechseln | Standardfaktor der gewählten Art ersetzt auch einen zuvor manuell eingegebenen Faktor; beide Wechselrichtungen prüfen. Passender letzter Zählerstand aus der zuletzt erfassten Rechnung desselben Typs; kein gemischter Strom-/Wasserzustand. |
| R05 | P0 | Rundungsgrenzen | Produkte mit dritter Nachkommastelle 4/5/6 sowie 1,005 und 1,015; kaufmännisch auf Cent (AwayFromZero), insbesondere 1,005 → 1,01 und 1,015 → 1,02; Sollwerte unabhängig vom Implementierungscode. Zwei Positionen à 1,005 ergeben nach einzelner Rundung zusammen 2,02, nicht 2,01. |
| R06 | P0 | Negativer Verbrauch und Zählerwechsel | Neu kleiner Alt löst eine sichtbare Warnung aus; Speicherung bleibt möglich. Neu gleich Alt und Neu größer Alt lösen diese Warnung nicht aus. Negative absolute Eingaben gesondert von einer negativen Differenz behandeln; keine unbestätigte Zählerwechsel-Sonderberechnung voraussetzen. |
| R07 | P1 | Wasseranzeige | Alt/Neu/Verbrauch mit drei Nachkommastellen, deutschem Dezimaltrennzeichen, Null und Rundungsgrenzen; auch Art in anderer Großschreibung. |
| R08 | P1 | Stromanzeige | Alt/Neu/Verbrauch mit zwei Nachkommastellen; Verhalten für unbekannte/null Art ausdrücklich festlegen. |
| R09 | P1 | Kulturwechsel | de-DE, en-US und invariant: fachliche Berechnung identisch, bewusst deutsche Anzeigen bleiben deutsch; Datum-/Eurodarstellung gesondert prüfen. |
| R10 | P0 | Kosten-Gesamtsummen | Mehrere Datensätze, nur Wasser, nur Strom, leere Daten, große Dezimalwerte; Gesamtbetrag ist ausschließlich Wasser + Strom; auch positive Vertragskosten verändern ihn nicht. Überlauf kontrolliert behandeln. |

### V: Formulare und fachliche Abläufe — 18 Szenarien

Unit/Workflow; Add/EditCamper, Add/EditRechnung, RechnungenViewModel, PrintSelectionViewModel.

| ID | Priorität | Szenario | Erwartung / Varianten |
|---|---|---|---|
| V01 | P1 | Neue Rechnung initialisieren | Strom, vorhandener Standardfaktor, kontrolliertes aktuelles Jahr; keine unbemerkten Netzwerkaufrufe im Konstruktor. |
| V02 | P0 | Pflichtauswahl fehlt | Ohne gültigen Platz oder gültige Art kein Datenbankschreiben, verständliche Validierung. |
| V03 | P0 | Ungültige Zahlen/Jahre | Leere Texte, Buchstaben, Dezimalkomma/-punkt, Jahre außerhalb von aktuellem Kalenderjahr und Vorjahr bei Neuanlage; beide zulässigen Jahre und den Jahreswechsel prüfen; keine Speicherung eines alten versteckten Binding-Werts. |
| V04 | P0 | Neue Rechnung speichern | Repository erhält Platz-ID, beide Zählerstände, Verbrauch, Faktor, Betrag, Jahr und Art konsistent; genau ein Insert. |
| V05 | P1 | Erfolgreich zum nächsten Platz | Nächster Platz nach Erfolg; letzter Platz springt gemäß bisherigem Verhalten zum ersten; Fokus und Zählerwerte passend. |
| V06 | P0 | Leere oder veränderte Platzliste | Keine Division durch null/Indexfehler; keine Speicherung ohne gültige Auswahl; verschwundener Platz kontrolliert behandelt. |
| V07 | P0 | Speichern schlägt fehl | Keine Navigation, kein Erfolgs-Callback, keine Eingaben löschen oder Platz weiterschalten; erneuter Versuch möglich. |
| V08 | P1 | Speichern und Schließen | Erst nach erfolgreicher Speicherung zurück; auch wenn der aktuell auskommentierte UI-Button später wieder aktiviert wird. |
| V09 | P0 | Rechnung bearbeiten | Bestehende Werte und ID korrekt laden; eigene Formularkopie; Abbrechen verändert weder Original noch Datenbank. |
| V10 | P0 | Art einer Rechnung bearbeiten | Nach Speichern und Neuladen bleiben die ausgewählte Art und der zugehörige Faktor erhalten; aktuelle UI/SQL-Diskrepanz abdecken. Artwechsel überschreibt den manuell gesetzten Faktor wie bestätigt. |
| V11 | P0 | Camperwechsel erfolgreich | Alter Camper deaktiviert, neuer Camper mit Person und Rechnungsadresse sichtbar; genau ein aktiver Beleger des Platzes. |
| V12 | P0 | Camperwechsel schlägt fehl | Fehler beim neuen Camper/Person/Link lässt den alten Beleger vollständig aktiv; keine verwaisten Personen oder halben Wechsel. |
| V13 | P1 | Camperdaten bearbeiten | Alle sichtbaren Felder, Unicode, führende Null in PLZ, optionale E-Mail; nur beabsichtigter Camper wird geändert. |
| V14 | P1 | Camperformular abbrechen/validieren | Abbrechen schreibt nichts. Beim Anlegen sind Platznummer, Vorname, Nachname, Straße, PLZ und Ort Pflicht; fehlende Werte/reine Leerzeichen verhindern das Speichern. Anrede und E-Mail dürfen leer bleiben. Vertragskosten optional: leer → 0 €, eingegebene gültige Beträge bleiben erhalten; nicht numerische Eingaben zeigen einen Fehler statt stiller Speicherung als 0 €. Die Gesamtsumme bleibt Wasser + Strom. |
| V15 | P1 | Listen nach Speichern aktualisieren | Nach Add und Edit erscheinen Änderungen genau einmal; Reload nach Abschluss, nicht unmittelbar beim Öffnen des Formulars. |
| V16 | P0 | Export und Gedruckt-Status | Erst nach erfolgreicher Speicherung aller ausgewählten Rechnungen die vollständige Snapshot-Auswahl markieren; nicht aktuelle/veränderte Auswahl. Abbruch, PDF-Fehler oder Teilfehler im Gruppenexport lassen alle bisherigen Druckstatus unverändert. |
| V17 | P0 | Export erfolgreich, Statusspeicherung fehlschlägt | PDF bleibt auffindbar; Teilfehler sichtbar; Wiederholung idempotent und nach festgelegter Batch-Regel. Kein falscher Gesamterfolg. |
| V18 | P1 | Jahresauswahl und Export ohne Daten | Jahre absteigend und neuestes gewählt; leere Daten/fehlender Provider zeigen einen brauchbaren Zustand; Abbruch bleibt im Formular. |

### D: SQL und Datenintegrität — 26 Szenarien

Integration mit echter isolierter MySQL-/MariaDB-Instanz; sämtliche 14 öffentlichen DatabaseService-Methoden.

| ID | Priorität | Szenario | Erwartung / Varianten |
|---|---|---|---|
| D01 | P0 | Aktive Camper lesen | Kombinationen active 0/1 und deactivated Null/Null-Datum/Datum nach bestätigter Aktiv-Regel; in allen Listen dieselbe Population. |
| D02 | P0 | Personen und Rechnungsadresse | Ein Camper mit mehreren Personen und genau einer Rechnungsadresse; keine versehentlichen Duplikate oder falschen Empfänger. |
| D03 | P1 | Datenbankwerte abbilden | DBNull bei erlaubten optionalen Spalten, Umlaute, Apostrophe, PLZ mit führender Null, Dezimalpräzision; keine fehlerhaften GetString-Aufrufe. |
| D04 | P1 | Platznummern lesen | Leer, numerisch wirkende Werte 2/10/02 und alphanumerische Plätze; eindeutige bestätigte Sortierung ohne Annahme rein numerischer IDs. |
| D05 | P1 | Platz-ID auflösen | Vorhandener Platz korrekt, unbekannter/null Platz expliziter Fehler; kein nachfolgender Insert. |
| D06 | P1 | Rechnungsjahre lesen | DISTINCT, absteigend, mehrere Rechnungen je Jahr, leere Tabelle. |
| D07 | P0 | Rechnungen nach Belegerwechsel | Historische Rechnungen bleiben dem damaligen Camper zugeordnet; SQL-Join nur über Platz darf keinen unbemerkten Empfängerwechsel erzeugen. |
| D08 | P0 | Rechnungsliste sortieren/abbilden | IDs absteigend, alle Messwerte und printed korrekt; mehrere Beleger/Personen erzeugen keine Mehrfachzeilen. |
| D09 | P0 | Jahressummen | Nur gewähltes Jahr, getrennte Strom-/Wassersummen, richtige Person; Gesamtsumme nur Wasser + Strom, Vertragskosten separat; mehrere Personen dürfen Summen nicht vervielfachen. |
| D10 | P1 | Jahressummen unter echtem SQL-Modus | Schema, Collation und sql_mode der Zielinstallation; insbesondere GROUP BY mit Vertragskosten sowie erlaubte Null-Daten. Keine SQLite-Ersatztests. |
| D11 | P0 | Letzter Zählerstand pro Platz/Art | Andere Plätze/Arten ausgeschlossen, kein Vorgänger → 0, Dezimalwerte unverändert; Bindungsparameter korrekt. |
| D12 | P0 | Letzter Zählerstand nach Korrektur | created, updated, jahr und id gezielt widersprüchlich; jüngstes Erfassungsdatum derselben Art am selben Platz gewinnt, unabhängig von Bearbeitung und Abrechnungsjahr. Für gleiche Erfassungszeitpunkte einen anhand des Schemas festgelegten stabilen Tie-Breaker prüfen; Erfassungsformular und Ablesetabelle verwenden dieselbe Regel. |
| D13 | P0 | Ablesetabelle | Nur berechtigte aktive Beleger mit Rechnungsadresse; Wasser drei/Strom zwei Nachkommastellen; fehlende Rechnung → 0. |
| D14 | P1 | Leere Datenbank und fehlende Beziehungen | Leere Listen, fehlende Person/Link/Platz und Integritätsverletzung jeweils bewusst behandeln; keine still verlorenen neu angelegten Camper. |
| D15 | P0 | Camper auf bisher unbenutztem Platz anlegen | Person, Camper und neuer Link werden angelegt; UPDATE-only für camper_personen darf nicht einen unsichtbaren Camper erzeugen. |
| D16 | P0 | Bestehenden Camper ersetzen | Neue Beziehungen korrekt, Historie erhalten; andere Beleger und Personen nicht umhängen. |
| D17 | P0 | Rollback je Schreibschritt | Fehler nach Personenanlage, Camperanlage oder Linkänderung: alle neuen Zeilen zurückgerollt; gesamter Wechsel einschließlich Deaktivierung atomar. |
| D18 | P0 | Camper bearbeiten mit Historie | Nur beabsichtigter aktueller Datensatz/Person ändert sich; historische Datensätze am selben Platz bleiben erhalten. |
| D19 | P0 | Camper deaktivieren | Nur aktiven Beleger des ausgewählten Platzes ändern; Zeitstempel gesetzt; Wiederholung und unbekannter Platz nach definiertem Vertrag. |
| D20 | P0 | Rechnung einfügen | Exakte Dezimalwerte, Art, Jahr, Zeitstempel und Platz-FK; unzulässiger Platz hinterlässt keine Zeile. |
| D21 | P0 | Rechnung aktualisieren | Nach Read-back alle im Formular veränderbaren Werte korrekt; unveränderliche Felder bleiben unverändert; Update genau einer ID. |
| D22 | P0 | Unbekannte Update-ID | Null betroffene Zeilen melden keinen falschen Erfolg; betrifft Rechnung, Camper und Statusmarkierung. |
| D23 | P0 | Gedruckt markieren | Nur IDs der vollständig erfolgreich exportierten Auswahl, wiederholbar; ein Teilfehler beim PDF-Export setzt keinen neuen Druckstatus, auch nicht für bereits gespeicherte Dateien. |
| D24 | P0 | Gleichzeitiger Camperwechsel | Zwei Verbindungen für denselben Platz: kein doppelter aktiver Beleger, definierter Konflikt statt stiller Überschreibung. |
| D25 | P0 | Parametrisierung und Textdaten | Apostroph/Unicode/SQL-artiger Text wird als Text gespeichert; keine Manipulation fremder Zeilen oder Tabellen. |
| D26 | P1 | Verbindungsabbruch/Timeout | Vor/nach Transaktionsstart und bei Commit; Fehler wird weitergegeben, Verbindungen freigegeben, Wiederholung ohne Doppelanlage gemäß Vertrag. |

### S: Suche und Filter — 10 Szenarien

Parametrisierte Unit-Tests; CamperViewModel und RechnungenViewModel.

| ID | Priorität | Szenario | Erwartung / Varianten |
|---|---|---|---|
| S01 | P1 | Leere Suche | Null soweit zugelassen, leer und nur Leerzeichen liefern alle Datensätze, auch bei leerer Liste. |
| S02 | P1 | Ein Begriff über alle Suchfelder | Jedes tatsächlich unterstützte Feld einmal; Groß-/Kleinschreibung, Teiltreffer und Nichttreffer. |
| S03 | P1 | Mehrere Begriffe | UND-Verknüpfung, wobei Begriffe in verschiedenen Feldern vorkommen dürfen; Reihenfolge der Begriffe unerheblich. |
| S04 | P1 | Zitierte Wortgruppe | Mehrwortname in Anführungszeichen bleibt zusammen; Mischung aus Wortgruppe und unzitiertem Begriff. |
| S05 | P1 | Fehlerhafte Anführungszeichen | Nicht geschlossene, leere und direkt aneinandergrenzende Quotes; definiertes Verhalten statt Zufall aus zwei Parserimplementierungen. |
| S06 | P1 | Leerraumvarianten | Mehrfache Spaces, Tab, Zeilenumbruch, führender/nachfolgender Leerraum. |
| S07 | P1 | Unvollständige Stammdaten | Ein fehlendes optionales Feld darf einen Treffer in einem vorhandenen Feld nicht grundsätzlich ausschließen. |
| S08 | P1 | Unicode | Umlaute, ß und Namen mit Apostroph; keine unerwünschte Normalisierung; genaue gewünschte Suchsemantik dokumentieren. |
| S09 | P1 | Zahlen und Kultur | IDs/Jahre, 0, negative Werte, deutsches Komma und en-US-Punkt; Anzeige und Suchmöglichkeit bewusst abstimmen. |
| S10 | P1 | Suche nach Reload | Aktueller Filter bleibt nach Add/Edit/Reload wirksam; Löschen der Suche stellt aktuelle Gesamtliste wieder her. |

### U: Navigation, Bindings und Controls — 10 Szenarien

Avalonia-Headless-Tests plus kleine Navigation-Unit-Tests.

| ID | Priorität | Szenario | Erwartung / Varianten |
|---|---|---|---|
| U01 | P1 | Navigationsstapel | Start ohne Zurück, mehrere Ansichten, LIFO-Rückkehr, leeren Stapel sicher behandeln; dieselbe Tabelleninstanz/Filter erhalten. |
| U02 | P1 | Null-Navigation | Vorhandene Sonderbehandlung von null bewusst definieren; kein unerwartetes zusätzliches Zurückspringen. |
| U03 | P0 | MainView übernimmt Kontext | Vom Bootstrap gesetzter MainViewModel bleibt wirksam; keine zweite Instanz und keine doppelten Datenladevorgänge im View-Konstruktor. |
| U04 | P1 | Alle DataTemplates | Jedes produktiv verwendete ViewModel erzeugt die richtige View; keine Not-Found-Ansicht; ViewLocator nur testen, falls tatsächlich eingebunden. |
| U05 | P1 | Numerische TextBox-Bindings | Tippen, Fokuswechsel und ungültige Eingaben; sichtbare Validierung und passender Command-Zustand statt Speicherung alter Werte. |
| U06 | P1 | Fokus bei Rechnungserfassung | DataContext wird erst nach View-Erzeugung gesetzt/ersetzt; NeuTextBox erhält Fokus und Markierung; alte Callback-Verbindungen lösen. |
| U07 | P1 | Doppelklick auf Tabellen | Camper/Rechnung öffnet genau passenden Datensatz; ohne Auswahl keine Aktion. |
| U08 | P1 | DataGrid-Mehrfachauswahl | Hinzufügen, Entfernen, Clear, wiederholte Ereignisse; keine Duplikate; passende ViewModel-Auswahl. |
| U09 | P1 | Behavior-Lebenszyklus | Attach/Detach/Reattach, Austausch der gebundenen Liste, null Liste; keine alten Eventhandler oder verwaisten Auswahlen. |
| U10 | P0 | Command-Aktivierung | Export ohne Auswahl gesperrt; Änderungen und Austausch von SelectedRechnungen aktualisieren CanExecute; während laufender Aktion kein widersprüchlicher zweiter Start. |

### P: PDF-Inhalt, Dateien und Export — 18 Szenarien

Service-Tests mit echten PDFs und Fake-Storage; bestehende Regressionen übernehmen.

| ID | Priorität | Szenario | Erwartung / Varianten |
|---|---|---|---|
| P01 | P0 | Gewählte Datei unverändert | Alle vier Dateiexporte geben exakt das gewählte Storage-Objekt zurück; vom Vorschlag abweichender Dateiname und Ordner mit Ä/Ö/%/#/Leerzeichen. |
| P02 | P0 | Dialogabbruch | Alle Datei- und Ordnerexporte: kein Rendering, Launcher oder Markieren; leerer Ergebnisstatus unterscheidbar von technischem Fehler. |
| P03 | P1 | Fehlender Storage/Launcher | Kontrollierter Zustand, kein Absturz; Exporterfolg und Viewerfehler nicht verwechseln. |
| P04 | P1 | Nichtlokale Dateien | content-URI und Provider ohne lokalen Pfad; Schreiben/Öffnen über Storage-Objekt, nicht File.Exists oder zusammengebaute Pfade. |
| P05 | P0 | Schreib-/Abschlussfehler | Fehler bei OpenWrite, Write, Flush und Dispose/Close; kein falscher Erfolg oder Datenbankstatus; Ressourcen freigegeben. |
| P06 | P0 | Kostenübersicht Inhalt | Gewähltes Jahr, alle Namen/Plätze, Wasser/Strom/Vertrag korrekt ausgewiesen; Gesamtsumme ausschließlich Wasser + Strom; konkrete Beträge im extrahierten PDF-Text prüfen. |
| P07 | P1 | Rechnungstabelle Inhalt | Nur gefilterte Datensätze, richtige Spalten, Art/Jahr/Beträge und Dezimaldarstellung. |
| P08 | P1 | Ablesetabelle Inhalt | Richtige vorherige Werte; Felder für neue Ablesung leer; deutsche Formate und richtige Namen. |
| P09 | P0 | Einzelrechnung Inhalt | Wasser und Strom: Adresse, Abrechnungszeitraum, alte/neue Werte, Verbrauch, Faktor und Endbetrag; keine falsche Person/Art. |
| P10 | P0 | Zusammengeführte Rechnungen | Genau ausgewählte IDs und Reihenfolge; kein Verlust/Duplikat; nicht nur minimale Seitenzahl, auch Inhalt identifizieren. |
| P11 | P0 | Gruppenexport | Ein Dokument pro Platz, sortierte Rechnungen und passende Zusammenfassung, exakte Gruppensumme; mehrere Plätze und mehrere Jahre/Arten. |
| P12 | P1 | Vorhandene Zieldatei | Suffixe _1/_2 usw.; vorhandene Dateien bytegleich erhalten; Namen nach Sanitizing dürfen nicht kollidierend überschrieben werden. |
| P13 | P1 | Problematische Platznamen | Leer, Slash, Backslash, Sonderzeichen, sehr lange Namen; plattformgeeignete Dateinamen ohne Verlassen des Zielordners. |
| P14 | P0 | Temporäre Dateien bei Erfolg | Nur Temp-Verzeichnis, eindeutige Namen, vollständige Bereinigung; niemals Dokumente-Ordner als Zwischenablage. |
| P15 | P0 | Temporäre Dateien bei Fehler | Fehler bei einzelner Rechnung, Zusammenfassung und Merge: auch angefangene PDFs aufräumen; ursprünglichen Fehler nicht verschleiern. |
| P16 | P1 | Leere und große Exporte | Keine leere ungültige Rechnung erzeugen; mehrseitige Tabellen mit langen Namen/Adressen und größeren Datenmengen lesbar. |
| P17 | P1 | Viewer-Aufruf | Genau gewählte Datei einmal öffnen; false/Exception des Launchers sichtbar behandeln; tatsächlicher Druckauftrag bleibt separate, derzeit nicht implementierte Funktion. |
| P18 | P1 | Layout-Stichproben | Wasser-, Stromrechnung und lange mehrseitige Tabelle rendern: abgeschnittene Zeilen, Seitenumbrüche, Kopfzeilen und Euro/Umlaute prüfen; keine fragilen binären PDF-Snapshots. |

### A: Asynchronität, Parallelität und Fehlerzustände — 10 Szenarien

Deterministische Unit-/Workflow- und Headless-Tests.

| ID | Priorität | Szenario | Erwartung / Varianten |
|---|---|---|---|
| A01 | P0 | Verspätete Zählerstandantwort | Platz/Art A→B, Antwort A kommt zuletzt: A darf B nicht überschreiben; TaskCompletionSource statt Sleep. |
| A02 | P0 | Initiales Laden und Fehler | Init-Task abwartbar, kein async-void-Absturz; Ladezustand und Fehlermeldung bei DB-Ausfall, Retry möglich. |
| A03 | P1 | Konkurrierende Reloads | Zwei Laden-Aufrufe mit vertauschter Antwortreihenfolge: neuester Stand gewinnt, keine gemischten oder doppelten Einträge. |
| A04 | P0 | Parallele Speicherbefehle | Doppelklick sowie Save gegen SaveAndClose: genau eine fachliche Speicherung; keine Platzfortschaltung mitten im zweiten Save. |
| A05 | P0 | PDF-Responsiveness | Jede Exportart: kontrolliert langsamer Schreibstream, UI-Heartbeat weiterhin ausführbar; bestehende simulierte Schleife um echten Avalonia-Dispatcher ergänzen. |
| A06 | P0 | Thread-Grenzen | Picker, Storage-Handle-Erzeugung, Launcher, ObservableCollections und Statusänderungen auf UI-Thread; PDF-Rendering/Merge außerhalb. |
| A07 | P0 | Auswahl während Export ändern | Auswahlliste und Werte einzelner Rechnungsobjekte ändern; exportierte Daten und markierte IDs müssen zum initialen fachlichen Snapshot passen. ToList allein kopiert keine Objekte. |
| A08 | P1 | Navigation während Laden/Export | Zurück/Ansichtswechsel: keine Updates in abgelöste Views, kein Fokus-Sprung zurück; definierte Fortsetzung oder Abbruch. |
| A09 | P1 | Statuskonkurrenz | Fortschritt, Erfolg, Fehler und verzögertes Löschen von StatusMessage dürfen neuere Aktionen nicht überschreiben. |
| A10 | P1 | Gleichzeitige Exporte | Keine gemeinsamen Tempnamen/Streams, eindeutige Zieldateien oder gemeinsamer Busy-Schutz; Ressourcen nach Fehlern wieder nutzbar. |

### E: Einstellungen und Jahresfaktoren — 10 Szenarien

Neue Funktion; Unit-/Workflow-, Datenbank- und Headless-Tests nach den bestätigten Anforderungen. Datenbanktests laufen ausschließlich mit synthetischen Daten in der isolierten Testinstanz.

| ID | Priorität | Szenario | Erwartung / Varianten |
|---|---|---|---|
| E01 | P1 | Einstellungsseite öffnen | Navigation öffnet die richtige Ansicht; gewähltes Jahr sowie Wasser- und Stromfaktor korrekt laden; Lade-/Fehlerzustand sichtbar. |
| E02 | P0 | Zentral speichern und erneut laden | Beide Faktoren mit Decimal-Präzision speichern; nach App-Neustart und beim Laden aus einer zweiten App-Instanz dieselben Werte aus der Datenbank erhalten. |
| E03 | P0 | Jahre und Arten getrennt halten | Änderungen für Jahr/Art verändern weder andere Jahre noch die andere Art. Wasser und Strom mit verschiedenen Werten und mindestens zwei Jahren prüfen. |
| E04 | P0 | Vorjahreswerte übernehmen | Fehlende Jahresfaktoren aus dem Vorjahr übernehmen; spätere Änderung der übernommenen Werte verändert das Vorjahr nicht. Vorhandene Einstellungen dürfen beim erneuten Laden nicht überschrieben werden. |
| E05 | P0 | Bestehende Rechnungsfaktoren erhalten | Einstellungen ändern und alte Rechnungen neu laden/exportieren: gespeicherte Faktoren und Beträge bleiben erhalten. Ein ausdrücklich ausgelöster Artwechsel ist getrennt nach R04 zu prüfen. |
| E06 | P0 | Neue Rechnung und Artwechsel | Für aktuelles Jahr und Vorjahr den jeweiligen Jahres-/Artfaktor verwenden; Wechsel zwischen Wasser und Strom ersetzt auch manuell eingegebene Faktoren. Eine reine Faktoränderung berechnet den Betrag weiterhin nicht automatisch neu. |
| E07 | P0 | Erstinitialisierung und Schema | Vorhandene Standardfaktoren zur Erstinitialisierung verwenden; Migration/Initialisierung wiederholbar ohne Duplikate oder Änderung gespeicherter Rechnungsfaktoren. Eindeutige Zuordnung je Jahr/Art absichern. |
| E08 | P1 | Eingabe und Abbrechen | Dezimalkomma, leere/nicht numerische Eingaben und Werte außerhalb der DB-Präzision kontrolliert behandeln; keine Speicherung eines alten unsichtbaren Binding-Werts. Abbrechen schreibt nichts. |
| E09 | P0 | Datenbankfehler beim Speichern | Fehler vor und während des Speicherns lassen den bisherigen Einstellungsstand erhalten; kein falscher Erfolg. Eingaben bleiben für einen erneuten Versuch verfügbar; dieser erzeugt keine Duplikate. |
| E10 | P1 | Mehrere Instanzen und langsames Laden | Gleichzeitige Initialisierung desselben Jahres erzeugt keine Duplikate; verspätete Antworten nach Jahreswechsel überschreiben nicht das aktuell gewählte Jahr. Konflikte beim Speichern dürfen nicht als Erfolg mit gemischten Werten erscheinen. |

### X: Plattform und Auslieferung — 8 Szenarien

Build-Matrix plus gezielte echte Plattform-Smokes.

| ID | Priorität | Szenario | Erwartung / Varianten |
|---|---|---|---|
| X01 | P0 | Linux Desktop Debug/Release | Mit .NET-10-SDK reproduzierbar bauen; keine neuen Warnungen; Tests separat von iOS/Android-Workloads ausführbar. |
| X02 | P1 | Linux echter Desktop | App-Start mit Testbackend, System-Dateidialog, Unicode-Ziel, PDF-Viewer; X11/Wayland soweit eingesetzt. Headless deckt Portale und Treiber nicht ab. |
| X03 | P1 | Desktop-Publish | Release-Publish auf festgelegtem Linux-RID starten; native Skia/HarfBuzz-Abhängigkeiten und Ressourcen/Icons vorhanden; saubere Testumgebung. |
| X04 | P1 | Android APK | Reproduzierbarer Build; separat dokumentierte 16-KB-Skia-Warnungen nicht als bestandenes Geräteverhalten ausgeben. |
| X05 | P1 | Android echter Lauf | Emulator/Handy: Formular, Tastatur, content-URI-Dateidialog und PDF-Öffnen, verweigerte Speicherberechtigungen und Netzwerkunterbrechung; je unterstütztem API-Level. |
| X06 | P2 | iOS | Auf später verfügbarem Mac/Xcode Build und Simulator-Smoke; aktuell explizit nicht ausgeführt, kein künstliches Grün auf Linux. |
| X07 | P2 | Browser | Build und Start getrennt von Funktionsfreigabe; direkte MySQL-Verbindung/Threads/Dateispeicherung als Architekturgrenzen prüfen, bei gewünschtem Webbetrieb zuerst Backend-API planen. |
| X08 | P0 | Test-/Release-Konfiguration | Tests dürfen nie auf die fest eingebettete Produktionsverbindung zurückfallen; keine Zugangsdaten in Testartefakten/Logs, deklarierte DB-/SDK-/Paketversionen reproduzierbar. |

## 6. Umsetzungsreihenfolge

1. **Infrastruktur und bestehende Regressionen:** Testadapter, Test-Solution, CI-Grundlauf, Produktionszugriffe verhindern; vorhandene PDF-Fälle übernehmen. Kein Verlust der bisherigen Prüfungen.
2. **Geldbeträge und sicherer Camperwechsel:** R01–R06/R10, V02–V04/V07/V10–V12, D15–D24. Erst fehlschlagenden Regressionstest schreiben, dann eng begrenzt korrigieren. Das erhobene SQL-Schema und die oben bestätigten Fachregeln bilden die Grundlage.
3. **Lesepfade und Zuordnung:** sämtliche D01–D14 plus zugehörige Filter-/Workflowtests; richtige historische Rechnungsdaten und Summen sind wichtiger als kosmetische UI-Tests.
4. **Async- und Export-Workflows:** V16/V17 und A01–A10, vollständige PDF-Inhalte und Dateifehler. Fakes steuern Reihenfolge/Fehler mit TaskCompletionSource; keine Sleeps oder Timeouts als normale Synchronisierung.
5. **Einstellungsseite und Jahresfaktoren:** E01–E10, versionierte Speicherung, Vorjahresübernahme und Schutz bestehender Rechnungen. Mit den Berechnungs- und Formularprüfungen verbinden.
6. **Bedienung und Auslieferung:** U/S vervollständigen, Linux-Desktop- und Android-Smokes. iOS erst bei verfügbarem Mac; Web-Funktionsfreigabe erst nach Architekturentscheidung.

Kein blindes „alle Tests grün machen“, indem Sollwerte an Fehler angepasst werden. Offene Fachentscheidungen als offene Fälle führen, nicht dauerhaft übersprungene Tests hinter einem grünen Gesamtstatus verstecken.

## 7. Automatisierung und Abnahmekriterien

- Jeder Pull Request: .NET-10-Restore/Build, Unit/PDF/Headless-Tests und Kern-DB-Integration unter Linux. Test-Solution direkt ausführen, nicht die mobile Gesamtsolution.
- Fehlerberichte pro Test (TRX oder kompatibles Format), Coverage-Artefakt und ausschließlich synthetische PDF-Diagnosedateien bei Fehlern. Keine Datenbank-Zugangsdaten loggen.
- Separater Android-Build-Job mit SDK/JDK; Test-/Build-Caches an Pakete/SDK binden. Bestehende Skia-16-KB-Warnungen transparent als offene Plattformgrenze führen.
- Vor Release: vollständige DB-Integration, Linux-Publish-Smoke sowie native Datei-/Viewer-Tests; echte Android-Ausführung auf unterstützten Geräten/API-Levels.
- iOS-Job nur auf geeignetem Mac/Xcode; aktuell als nicht verfügbar dokumentieren. Ein fehlender Mac darf weder Linux-Tests verhindern noch als erfolgreicher iOS-Test erscheinen.
- Zielbudgets, zunächst messen: schnelle Unit/PDF/Headless-Suite etwa 2 Minuten, DB-Suite etwa 5 Minuten ohne erstmaligen Image-Download. Performance-Stichproben mit 1.000/10.000 synthetischen Zeilen; feste Laufzeitgrenzen erst anhand eines definierten Referenzsystems.
- Keine pauschale 100-%-Coverage-Vorgabe. Alle identifizierten P0-Verhaltenszweige, alle 14 Datenbankoperationen mit passenden Positiv-/Fehlerfällen und alle fünf Exportwege sowie die neuen Einstellungen E01–E10 müssen abgedeckt sein. Coverage zeigt Lücken, ersetzt keine fachlichen Assertions.
- Kleine Mutation-Stichprobe für Geldberechnung und Filter: absichtlich veränderte Faktoren, Rundung oder AND/OR müssen Tests rot machen. Erst nach stabiler Grundsuite.
- Regressionen müssen einzeln in Rider sichtbar, wiederholbar und unabhängig von Testreihenfolge laufen. Kein echter Datenbankserver, PDF-Viewer oder Drucker für Unit/PDF/Headless erforderlich.

## 8. Bewusste Grenzen

Keine Tests für triviale Model-Getter, Framework-internes Rendering, jede generierte ObservableProperty oder sämtliche Kombinationen aller Betriebssysteme. `Vertragspreis`/`Erhoehung` besitzen derzeit keine verwendete Berechnungslogik; keine Tarifregeln dafür erfinden. Browser- und iOS-Quellprojekte bedeuten nicht, dass diese Ziele funktional freigegeben sind. Die simulierten Responsiveness-Tests und Headless-Tests ersetzen keinen Test der NVIDIA-Treiber, Wayland-Portale oder echten Android-Dateipicker.

Der Plan wurde aus Code und dem erfolgreichen vorhandenen Regressionstestlauf abgeleitet. Er ist **keine Aussage, dass diese 120 Szenarien bereits implementiert oder bestanden sind**. Produktivcode und Git-Commits wurden für die Planung nicht geändert.
