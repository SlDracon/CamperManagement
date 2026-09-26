# Plattformprüfungen

[Zur Dokumentationsübersicht](../README.md)

| Ziel | Automatisch prüfbar | Zusätzliche Freigabe |
|---|---|---|
| Linux | Debug/Release, linux-x64-Publish, Unit/PDF/MariaDB/Headless | Native Dateidialoge unter den eingesetzten Wayland-/X11-Portalen und PDF-Viewer prüfen. |
| Android | Debug-APK mit .NET-10-Workload, JDK 21 und Android SDK | Gerät/Emulator: Formular, Tastatur, `content:`-Speicherziel, Abbruch, Offlinezustand und PDF-Viewer. |
| iOS | Auf Linux nicht ausführbar | Mac mit passendem Xcode/SDK erforderlich. Build und Simulator-Smoke dort durchführen. |
| Browser | Quellprojekt kann gebaut werden | Kein funktionsfähiger Datenbankclient: Browser können diese direkte MySQL-Verbindung nicht verwenden. Webbetrieb benötigt eine Backend-API; ein erfolgreicher Build ist keine Web-Freigabe. |

Android verwendet SkiaSharp.NativeAssets.Android 3.119.4. Der saubere Android-Release-Build meldet keine XA0141-Warnung mehr; alle 24 ARM64-/x64-Nativbibliotheken bestehen die 16-KB-Prüfung. Der Browser-Build meldet weiterhin nicht eingebundene native WASM-Referenzen. Die Testsuite behebt keinen NVIDIA-Treiberkonflikt des Systems.

Manuelle Linux-Prüfung: Anwendung starten, Einstellungen öffnen, aktuelle Faktoren ohne Jahresauswahl anzeigen, ungültige Eingaben prüfen; Rechnung als PDF unter geändertem Unicode-Dateinamen speichern; bei Abbruch darf der Status unverändert bleiben; Viewer öffnen; Fenster während eines größeren Exports bedienen. Dabei ausschließlich eine Testdatenbank verwenden. Ein produktiver Schreibtest ist nicht erforderlich.

Ausgeführt: nativer Linux-Start sowohl des Debug-Builds als auch des veröffentlichten Release-Builds. Der explizite `--smoke-test` prüfte Initialisierung und Datenladen mit einer synthetischen lokalen Testdatenbank und beendete beide Läufe erfolgreich mit Exitcode 0. Die Testdatenbank wurde anschließend entfernt und der ausschließlich für diese Prüfungen gestartete Server beendet.


## GitHub-Releases (Ergänzung)

Die CI veröffentlicht Desktop-Projekte self-contained für `linux-x64` und `win-x64` und baut Android in Debug. Versions-Tags durchlaufen diese Prüfungen erneut und erzeugen zusätzlich das signierte Android-Release-APK. Lokaler Release-Publish für alle drei Ziele sowie der native Linux-Start wurden geprüft. Unter Windows baut und testet ein eigener Windows-Runner; ein interaktiver Windows- oder Android-Gerätetest wird dadurch nicht ersetzt.

Android-Release verwendet derzeit kein Trimming und keine AOT-Kompilierung, damit reflektionsabhängige Funktionen erhalten bleiben. Das APK ist dadurch größer. System-Datei- und Ordnerdialoge ersetzen pauschale externe Speicherberechtigungen; das Manifest benötigt für die Datenbank lediglich INTERNET. Signierung, Versionsnummern und Installation sind in [CI/CD und Releases](releases.md) dokumentiert.

## Qualitätsprüfung vom 27.09.2026

- 222 Anwendungstests und sieben Build-Skripttests erfolgreich; GitHub-Workflows mit actionlint geprüft.
- Linux-/Windows-Publish erfolgreich; nativer Linux-Start mit synthetischer Datenbank, Datenladen und Beenden: Exitcode 0. Ein interaktiver Windows-Test wurde hier nicht durchgeführt.
- Android Release: sauberer Neubau ohne Warnungen/Fehler, 24 native 64-Bit-Bibliotheken und APK-ZIP-Ausrichtung geprüft.
- Android-15-Emulator (`google_apis_ps16k`, x86_64), nachgewiesen `PAGE_SIZE=16384`: App-Start, Verbindung über Einrichtungsformular, Camper-/Rechnungslisten, Hoch-/Querformat, Hintergrund/Rückkehr, PDF-Dateiauswahl und Ordnerauswahl mit synthetischen Daten geprüft. Ablesetabelle und gruppierte Rechnung wurden gespeichert; Druckstatus wurde erst danach gesetzt.
- PDF-Viewer-Start erfolgreich geprüft. Dabei wurde eine zuvor fehlende gezielte `application/pdf`-Abfrage im Android-Manifest ergänzt: Avalonia 11 prüft vor dem Öffnen die sichtbaren Viewer. Ein physisches ARM64-Gerät bleibt eine zusätzliche manuelle Prüfung.
- Screenshotvergleich in 1100 und 390 Pixel Breite; helles Layout, begrenzte Tabellen und umbrechende Toolbar bleiben erhalten. Automation-Namen und Tastaturzugriff sind automatisiert geprüft; ein vollständiger Screenreader-Test steht aus.

Es wurden keine produktiven Datenbankänderungen ausgeführt. Die bisherige Kennwortspeicherung und das Datenbankpasswort wurden auf ausdrücklichen Wunsch nicht geändert. Der Avalonia-Dokumentations-MCP war durch HTTP 403 nicht erreichbar; die Umsetzung stützt sich auf den Code und die [offiziellen Avalonia-Empfehlungen](https://docs.avaloniaui.net/docs/app-development/dependency-injection) sowie die [Android-Dokumentation](https://developer.android.com/guide/practices/page-sizes).
