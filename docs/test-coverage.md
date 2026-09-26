# Umsetzung des Testplans

Die korrigierte Einstellungsvorgabe gilt ohne Jahresbindung: neue Rechnungen verwenden ab dem Speichern die aktuellen Faktoren. Die 120 Szenarien des Plans sind fachliche Gruppen, keine Behauptung über 120 einzelne Testmethoden. Parametrisierte Tests führen mehrere Daten-/Fehlervarianten aus. Die vier regulären Testprojekte werden in der Hauptsolution und in der unabhängigen Test-Solution aufgeführt. Die Tests laufen ohne Produktivzugriff.

Nicht jede Plattformprüfung ist auf diesem Linux-Rechner ausführbar. Native Dateipicker/Viewer und reale Android-Geräte bleiben zusätzliche Freigabeschritte. iOS benötigt einen Mac; Browser-Funktionalität benötigt eine Backend-API. Diese Schritte werden nicht als bestandene automatisierte Tests gezählt.

| Plan-ID | Szenario | Nachweis / Status |
|---|---|---|
| R01 | Strom und Wasser berechnen | Automatisiert. `UnitTests/BillingTests.cs` |
| R02 | Alt und Neu nachträglich ändern | Automatisiert. `UnitTests/BillingTests.cs` |
| R03 | Nur Faktor ändern | Automatisiert. `UnitTests/BillingTests.cs` |
| R04 | Art wechseln | Automatisiert. `UnitTests/BillingTests.cs` |
| R05 | Rundungsgrenzen | Automatisiert. `UnitTests/BillingTests.cs` |
| R06 | Negativer Verbrauch und Zählerwechsel | Automatisiert. `UnitTests/BillingTests.cs` |
| R07 | Wasseranzeige | Automatisiert. `UnitTests/BillingTests.cs` |
| R08 | Stromanzeige | Automatisiert. `UnitTests/BillingTests.cs` |
| R09 | Kulturwechsel | Automatisiert. `UnitTests/BillingTests.cs` |
| R10 | Kosten-Gesamtsummen | Automatisiert. `UnitTests/BillingTests.cs` |
| V01 | Neue Rechnung initialisieren | Automatisiert. `UnitTests/WorkflowTests.cs, SearchSettingsExportTests.cs` |
| V02 | Pflichtauswahl fehlt | Automatisiert. `UnitTests/WorkflowTests.cs, SearchSettingsExportTests.cs` |
| V03 | Ungültige Zahlen/Jahre | Automatisiert. `UnitTests/WorkflowTests.cs, SearchSettingsExportTests.cs` |
| V04 | Neue Rechnung speichern | Automatisiert. `UnitTests/WorkflowTests.cs, SearchSettingsExportTests.cs` |
| V05 | Erfolgreich zum nächsten Platz | Automatisiert. `UnitTests/WorkflowTests.cs, SearchSettingsExportTests.cs` |
| V06 | Leere oder veränderte Platzliste | Automatisiert. `UnitTests/WorkflowTests.cs, SearchSettingsExportTests.cs` |
| V07 | Speichern schlägt fehl | Automatisiert. `UnitTests/WorkflowTests.cs, SearchSettingsExportTests.cs` |
| V08 | Speichern und Schließen | Automatisiert. `UnitTests/WorkflowTests.cs, SearchSettingsExportTests.cs` |
| V09 | Rechnung bearbeiten | Automatisiert. `UnitTests/WorkflowTests.cs, SearchSettingsExportTests.cs` |
| V10 | Art einer Rechnung bearbeiten | Automatisiert. `UnitTests/WorkflowTests.cs, SearchSettingsExportTests.cs` |
| V11 | Camperwechsel erfolgreich | Automatisiert. `UnitTests/WorkflowTests.cs, SearchSettingsExportTests.cs` |
| V12 | Camperwechsel schlägt fehl | Automatisiert. `UnitTests/WorkflowTests.cs, SearchSettingsExportTests.cs` |
| V13 | Camperdaten bearbeiten | Automatisiert. `UnitTests/WorkflowTests.cs, SearchSettingsExportTests.cs` |
| V14 | Camperformular abbrechen/validieren | Automatisiert. `UnitTests/WorkflowTests.cs, SearchSettingsExportTests.cs` |
| V15 | Listen nach Speichern aktualisieren | Automatisiert. `UnitTests/WorkflowTests.cs, SearchSettingsExportTests.cs` |
| V16 | Export und Gedruckt-Status | Automatisiert. `UnitTests/WorkflowTests.cs, SearchSettingsExportTests.cs` |
| V17 | Export erfolgreich, Statusspeicherung fehlschlägt | Automatisiert. `UnitTests/WorkflowTests.cs, SearchSettingsExportTests.cs` |
| V18 | Jahresauswahl und Export ohne Daten | Automatisiert. `UnitTests/WorkflowTests.cs, SearchSettingsExportTests.cs` |
| D01 | Aktive Camper lesen | Automatisiert auf MariaDB 11.8.8. `IntegrationTests/DatabaseTests.cs` |
| D02 | Personen und Rechnungsadresse | Automatisiert auf MariaDB 11.8.8. `IntegrationTests/DatabaseTests.cs` |
| D03 | Datenbankwerte abbilden | Automatisiert auf MariaDB 11.8.8. `IntegrationTests/DatabaseTests.cs` |
| D04 | Platznummern lesen | Automatisiert auf MariaDB 11.8.8. `IntegrationTests/DatabaseTests.cs` |
| D05 | Platz-ID auflösen | Automatisiert auf MariaDB 11.8.8. `IntegrationTests/DatabaseTests.cs` |
| D06 | Rechnungsjahre lesen | Automatisiert auf MariaDB 11.8.8. `IntegrationTests/DatabaseTests.cs` |
| D07 | Rechnungen nach Belegerwechsel | Automatisiert auf MariaDB 11.8.8. `IntegrationTests/DatabaseTests.cs` |
| D08 | Rechnungsliste sortieren/abbilden | Automatisiert auf MariaDB 11.8.8. `IntegrationTests/DatabaseTests.cs` |
| D09 | Jahressummen | Automatisiert auf MariaDB 11.8.8. `IntegrationTests/DatabaseTests.cs` |
| D10 | Jahressummen unter echtem SQL-Modus | Automatisiert auf MariaDB 11.8.8. `IntegrationTests/DatabaseTests.cs` |
| D11 | Letzter Zählerstand pro Platz/Art | Automatisiert auf MariaDB 11.8.8. `IntegrationTests/DatabaseTests.cs` |
| D12 | Letzter Zählerstand nach Korrektur | Automatisiert auf MariaDB 11.8.8. `IntegrationTests/DatabaseTests.cs` |
| D13 | Ablesetabelle | Automatisiert auf MariaDB 11.8.8. `IntegrationTests/DatabaseTests.cs` |
| D14 | Leere Datenbank und fehlende Beziehungen | Automatisiert auf MariaDB 11.8.8. `IntegrationTests/DatabaseTests.cs` |
| D15 | Camper auf bisher unbenutztem Platz anlegen | Automatisiert auf MariaDB 11.8.8. `IntegrationTests/DatabaseTests.cs` |
| D16 | Bestehenden Camper ersetzen | Automatisiert auf MariaDB 11.8.8. `IntegrationTests/DatabaseTests.cs` |
| D17 | Rollback je Schreibschritt | Automatisiert auf MariaDB 11.8.8. `IntegrationTests/DatabaseTests.cs` |
| D18 | Camper bearbeiten mit Historie | Automatisiert auf MariaDB 11.8.8. `IntegrationTests/DatabaseTests.cs` |
| D19 | Camper deaktivieren | Automatisiert auf MariaDB 11.8.8. `IntegrationTests/DatabaseTests.cs` |
| D20 | Rechnung einfügen | Automatisiert auf MariaDB 11.8.8. `IntegrationTests/DatabaseTests.cs` |
| D21 | Rechnung aktualisieren | Automatisiert auf MariaDB 11.8.8. `IntegrationTests/DatabaseTests.cs` |
| D22 | Unbekannte Update-ID | Automatisiert auf MariaDB 11.8.8. `IntegrationTests/DatabaseTests.cs` |
| D23 | Gedruckt markieren | Automatisiert auf MariaDB 11.8.8. `IntegrationTests/DatabaseTests.cs` |
| D24 | Gleichzeitiger Camperwechsel | Automatisiert auf MariaDB 11.8.8. `IntegrationTests/DatabaseTests.cs` |
| D25 | Parametrisierung und Textdaten | Automatisiert auf MariaDB 11.8.8. `IntegrationTests/DatabaseTests.cs` |
| D26 | Verbindungsabbruch/Timeout | Automatisiert auf MariaDB 11.8.8. `IntegrationTests/DatabaseTests.cs` |
| S01 | Leere Suche | Automatisiert, einschließlich parametrisierter Suchfälle. `UnitTests/SearchSettingsExportTests.cs` |
| S02 | Ein Begriff über alle Suchfelder | Automatisiert, einschließlich parametrisierter Suchfälle. `UnitTests/SearchSettingsExportTests.cs` |
| S03 | Mehrere Begriffe | Automatisiert, einschließlich parametrisierter Suchfälle. `UnitTests/SearchSettingsExportTests.cs` |
| S04 | Zitierte Wortgruppe | Automatisiert, einschließlich parametrisierter Suchfälle. `UnitTests/SearchSettingsExportTests.cs` |
| S05 | Fehlerhafte Anführungszeichen | Automatisiert, einschließlich parametrisierter Suchfälle. `UnitTests/SearchSettingsExportTests.cs` |
| S06 | Leerraumvarianten | Automatisiert, einschließlich parametrisierter Suchfälle. `UnitTests/SearchSettingsExportTests.cs` |
| S07 | Unvollständige Stammdaten | Automatisiert, einschließlich parametrisierter Suchfälle. `UnitTests/SearchSettingsExportTests.cs` |
| S08 | Unicode | Automatisiert, einschließlich parametrisierter Suchfälle. `UnitTests/SearchSettingsExportTests.cs` |
| S09 | Zahlen und Kultur | Automatisiert, einschließlich parametrisierter Suchfälle. `UnitTests/SearchSettingsExportTests.cs` |
| S10 | Suche nach Reload | Automatisiert, einschließlich parametrisierter Suchfälle. `UnitTests/SearchSettingsExportTests.cs` |
| U01 | Navigationsstapel | Automatisiert mit Avalonia Headless. `UiTests/UiTests.cs; UnitTests/WorkflowTests.cs` |
| U02 | Null-Navigation | Automatisiert mit Avalonia Headless. `UiTests/UiTests.cs; UnitTests/WorkflowTests.cs` |
| U03 | MainView übernimmt Kontext | Automatisiert mit Avalonia Headless. `UiTests/UiTests.cs; UnitTests/WorkflowTests.cs` |
| U04 | Alle DataTemplates | Automatisiert mit Avalonia Headless. `UiTests/UiTests.cs; UnitTests/WorkflowTests.cs` |
| U05 | Numerische TextBox-Bindings | Automatisiert mit Avalonia Headless. `UiTests/UiTests.cs; UnitTests/WorkflowTests.cs` |
| U06 | Fokus bei Rechnungserfassung | Automatisiert mit Avalonia Headless. `UiTests/UiTests.cs; UnitTests/WorkflowTests.cs` |
| U07 | Doppelklick auf Tabellen | Automatisiert mit Avalonia Headless. `UiTests/UiTests.cs; UnitTests/WorkflowTests.cs` |
| U08 | DataGrid-Mehrfachauswahl | Automatisiert mit Avalonia Headless. `UiTests/UiTests.cs; UnitTests/WorkflowTests.cs` |
| U09 | Behavior-Lebenszyklus | Automatisiert mit Avalonia Headless. `UiTests/UiTests.cs; UnitTests/WorkflowTests.cs` |
| U10 | Command-Aktivierung | Automatisiert mit Avalonia Headless. `UiTests/UiTests.cs; UnitTests/WorkflowTests.cs` |
| P01 | Gewählte Datei unverändert | Automatisiert mit echten PDFs. `PdfTests/PdfTests.cs` |
| P02 | Dialogabbruch | Automatisiert mit echten PDFs. `PdfTests/PdfTests.cs` |
| P03 | Fehlender Storage/Launcher | Automatisiert mit echten PDFs. `PdfTests/PdfTests.cs` |
| P04 | Nichtlokale Dateien | Automatisiert mit echten PDFs. `PdfTests/PdfTests.cs` |
| P05 | Schreib-/Abschlussfehler | Automatisiert mit echten PDFs. `PdfTests/PdfTests.cs` |
| P06 | Kostenübersicht Inhalt | Automatisiert mit echten PDFs. `PdfTests/PdfTests.cs` |
| P07 | Rechnungstabelle Inhalt | Automatisiert mit echten PDFs. `PdfTests/PdfTests.cs` |
| P08 | Ablesetabelle Inhalt | Automatisiert mit echten PDFs. `PdfTests/PdfTests.cs` |
| P09 | Einzelrechnung Inhalt | Automatisiert mit echten PDFs. `PdfTests/PdfTests.cs` |
| P10 | Zusammengeführte Rechnungen | Automatisiert mit echten PDFs. `PdfTests/PdfTests.cs` |
| P11 | Gruppenexport | Automatisiert mit echten PDFs. `PdfTests/PdfTests.cs` |
| P12 | Vorhandene Zieldatei | Automatisiert mit echten PDFs. `PdfTests/PdfTests.cs` |
| P13 | Problematische Platznamen | Automatisiert mit echten PDFs. `PdfTests/PdfTests.cs` |
| P14 | Temporäre Dateien bei Erfolg | Automatisiert mit echten PDFs. `PdfTests/PdfTests.cs` |
| P15 | Temporäre Dateien bei Fehler | Automatisiert mit echten PDFs. `PdfTests/PdfTests.cs` |
| P16 | Leere und große Exporte | Automatisiert mit echten PDFs. `PdfTests/PdfTests.cs` |
| P17 | Viewer-Aufruf | Automatisiert mit echten PDFs. `PdfTests/PdfTests.cs` |
| P18 | Layout-Stichproben | PDF-Seitengröße/Inhalt automatisch; vier gerenderte Muster zusätzlich visuell geprüft. `PdfTests/PdfTests.cs` |
| A01 | Verspätete Zählerstandantwort | Automatisiert mit kontrollierter Antwortreihenfolge und Dispatcher. `UnitTests/WorkflowTests.cs, SearchSettingsExportTests.cs; UiTests/UiTests.cs` |
| A02 | Initiales Laden und Fehler | Automatisiert mit kontrollierter Antwortreihenfolge und Dispatcher. `UnitTests/WorkflowTests.cs, SearchSettingsExportTests.cs; UiTests/UiTests.cs` |
| A03 | Konkurrierende Reloads | Automatisiert mit kontrollierter Antwortreihenfolge und Dispatcher. `UnitTests/WorkflowTests.cs, SearchSettingsExportTests.cs; UiTests/UiTests.cs` |
| A04 | Parallele Speicherbefehle | Automatisiert mit kontrollierter Antwortreihenfolge und Dispatcher. `UnitTests/WorkflowTests.cs, SearchSettingsExportTests.cs; UiTests/UiTests.cs` |
| A05 | PDF-Responsiveness | Automatisiert mit kontrollierter Antwortreihenfolge und Dispatcher. `UnitTests/WorkflowTests.cs, SearchSettingsExportTests.cs; UiTests/UiTests.cs` |
| A06 | Thread-Grenzen | Automatisiert mit kontrollierter Antwortreihenfolge und Dispatcher. `UnitTests/WorkflowTests.cs, SearchSettingsExportTests.cs; UiTests/UiTests.cs` |
| A07 | Auswahl während Export ändern | Automatisiert mit kontrollierter Antwortreihenfolge und Dispatcher. `UnitTests/WorkflowTests.cs, SearchSettingsExportTests.cs; UiTests/UiTests.cs` |
| A08 | Navigation während Laden/Export | Automatisiert mit kontrollierter Antwortreihenfolge und Dispatcher. `UnitTests/WorkflowTests.cs, SearchSettingsExportTests.cs; UiTests/UiTests.cs` |
| A09 | Statuskonkurrenz | Automatisiert mit kontrollierter Antwortreihenfolge und Dispatcher. `UnitTests/WorkflowTests.cs, SearchSettingsExportTests.cs; UiTests/UiTests.cs` |
| A10 | Gleichzeitige Exporte | Automatisiert mit kontrollierter Antwortreihenfolge und Dispatcher. `UnitTests/WorkflowTests.cs, SearchSettingsExportTests.cs; UiTests/UiTests.cs` |
| E01 | Einstellungsseite öffnen | Automatisiert. `UnitTests/SearchSettingsExportTests.cs; IntegrationTests/DatabaseTests.cs` |
| E02 | Zentral speichern und erneut laden | Automatisiert. `UnitTests/SearchSettingsExportTests.cs; IntegrationTests/DatabaseTests.cs` |
| E03 | Arten trennen; Faktoren unabhängig vom Jahr verwenden | Automatisiert. `UnitTests/SearchSettingsExportTests.cs; IntegrationTests/DatabaseTests.cs` |
| E04 | Geänderte Faktoren sofort nutzen | Automatisiert. `UnitTests/SearchSettingsExportTests.cs; IntegrationTests/DatabaseTests.cs` |
| E05 | Bestehende Rechnungsfaktoren erhalten | Automatisiert. `UnitTests/SearchSettingsExportTests.cs; IntegrationTests/DatabaseTests.cs` |
| E06 | Neue Rechnung und Artwechsel | Automatisiert. `UnitTests/SearchSettingsExportTests.cs; IntegrationTests/DatabaseTests.cs` |
| E07 | Erstinitialisierung und Schema | Automatisiert. `UnitTests/SearchSettingsExportTests.cs; IntegrationTests/DatabaseTests.cs` |
| E08 | Eingabe und Abbrechen | Automatisiert. `UnitTests/SearchSettingsExportTests.cs; IntegrationTests/DatabaseTests.cs` |
| E09 | Datenbankfehler beim Speichern | Automatisiert. `UnitTests/SearchSettingsExportTests.cs; IntegrationTests/DatabaseTests.cs` |
| E10 | Mehrere Instanzen und langsames Laden | Automatisiert. `UnitTests/SearchSettingsExportTests.cs; IntegrationTests/DatabaseTests.cs` |
| X01 | Linux Desktop Debug/Release | Linux Desktop Debug/Release gebaut. `docs/platform-checks.md; .github/workflows/tests.yml` |
| X02 | Linux echter Desktop | Nativer Linux-Start und Laden mit synthetischer DB; Systemdialog-/Viewer-Prüfung bleibt manuell. `docs/platform-checks.md; .github/workflows/tests.yml` |
| X03 | Desktop-Publish | linux-x64-Publish gebaut und nativ mit synthetischer Testdatenbank gestartet; Laden und sauberes Beenden erfolgreich. `docs/platform-checks.md; .github/workflows/tests.yml` |
| X04 | Android APK | Android-APK gebaut; zwei bekannte XA0141-Warnungen bleiben offen. `docs/platform-checks.md; .github/workflows/tests.yml` |
| X05 | Android echter Lauf | Prüfanleitung vorhanden; ohne angeschlossenes Gerät/Emulator nicht ausgeführt. `docs/platform-checks.md; .github/workflows/tests.yml` |
| X06 | iOS | Prüfanleitung vorhanden; kein Mac/Xcode verfügbar, nicht ausgeführt. `docs/platform-checks.md; .github/workflows/tests.yml` |
| X07 | Browser | Browser-Build geprüft; Web-Funktionsfreigabe erfordert separate Backend-Architektur. `docs/platform-checks.md; .github/workflows/tests.yml` |
| X08 | Test-/Release-Konfiguration | Explizite isolierte DB-Konfiguration, Fixture-Hostprüfung, keine Produktivtests; CI ohne Produktivgeheimnisse. `docs/platform-checks.md; .github/workflows/tests.yml` |

## Grenzen einzelner Prüfungen

- D26 prüft Verbindungsfehler. Rollbacks während Schreibvorgängen werden über echte MariaDB-Triggerfehler in D17/E09 und unbekannte IDs in D23 geprüft; ein unklarer Commit nach Netzwerkverlust lässt sich damit nicht vollständig ausschließen. Es gibt keinen automatischen blinden Schreib-Retry.
- A08 prüft verspätete Antworten nach Verlassen des Formulars. Navigation während einer Speicherung wird blockiert; Hintergrundexporte verwenden unabhängige Werte-Snapshots.
- Geldwerte/Filter wurden zusätzlich mit zwei kurzzeitig eingesetzten Mutationen geprüft: falsche ToEven-Rundung und ODER statt UND lassen Tests fehlschlagen. Beide Änderungen wurden zurückgenommen.
- Keine pauschale 100-%-Coverage-Behauptung. TRX- und Coverage-Dateien sind reproduzierbare Testartefakte; Layout- und Geräteprüfungen bleiben ergänzend notwendig.
