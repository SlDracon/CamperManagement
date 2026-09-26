# Plattformprüfungen

[Zur Dokumentationsübersicht](../README.md)

| Ziel | Automatisch prüfbar | Zusätzliche Freigabe |
|---|---|---|
| Linux | Debug/Release, linux-x64-Publish, Unit/PDF/MariaDB/Headless | Native Dateidialoge unter den eingesetzten Wayland-/X11-Portalen und PDF-Viewer prüfen. |
| Android | Debug-APK mit .NET-10-Workload, JDK 21 und Android SDK | Gerät/Emulator: Formular, Tastatur, `content:`-Speicherziel, Abbruch, Offlinezustand und PDF-Viewer. |
| iOS | Auf Linux nicht ausführbar | Mac mit passendem Xcode/SDK erforderlich. Build und Simulator-Smoke dort durchführen. |
| Browser | Quellprojekt kann gebaut werden | Kein funktionsfähiger Datenbankclient: Browser können diese direkte MySQL-Verbindung nicht verwenden. Webbetrieb benötigt eine Backend-API; ein erfolgreicher Build ist keine Web-Freigabe. |

Aktuell bekannte Abhängigkeiten: SkiaSharp.NativeAssets.Android 2.88.9 meldet XA0141 wegen 16-KB-Speicherseiten. Der Browser-Build meldet nicht eingebundene native WASM-Referenzen. Diese Warnungen sind nicht unterdrückt. Die neue Testsuite behebt weder den NVIDIA-Treiberkonflikt des Systems noch diese Plattformgrenzen.

Manuelle Linux-Prüfung: Anwendung starten, Einstellungen öffnen, aktuelle Faktoren ohne Jahresauswahl anzeigen, ungültige Eingaben prüfen; Rechnung als PDF unter geändertem Unicode-Dateinamen speichern; bei Abbruch darf der Status unverändert bleiben; Viewer öffnen; Fenster während eines größeren Exports bedienen. Dabei ausschließlich eine Testdatenbank verwenden. Ein produktiver Schreibtest ist nicht erforderlich.

Ausgeführt: nativer Linux-Start sowohl des Debug-Builds als auch des veröffentlichten Release-Builds. Der explizite `--smoke-test` prüfte Initialisierung und Datenladen mit einer synthetischen lokalen Testdatenbank und beendete beide Läufe erfolgreich mit Exitcode 0. Die Testdatenbank wurde anschließend entfernt und der ausschließlich für diese Prüfungen gestartete Server beendet.


## GitHub-Releases (Ergänzung)

Die CI veröffentlicht Desktop-Projekte self-contained für `linux-x64` und `win-x64` und baut Android in Debug. Versions-Tags durchlaufen diese Prüfungen erneut und erzeugen zusätzlich das signierte Android-Release-APK. Lokaler Release-Publish für alle drei Ziele sowie der native Linux-Start wurden geprüft. Unter Windows baut und testet ein eigener Windows-Runner; ein interaktiver Windows- oder Android-Gerätetest wird dadurch nicht ersetzt.

Android-Release verwendet derzeit kein Trimming und keine AOT-Kompilierung, damit reflektionsabhängige Funktionen erhalten bleiben. Das APK ist dadurch größer. Die bekannten SkiaSharp-XA0141-Warnungen bleiben bestehen. Signierung, Versionsnummern und Installation sind in [CI/CD und Releases](releases.md) dokumentiert.
