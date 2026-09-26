# Plattformprüfungen

| Ziel | Automatisch prüfbar | Zusätzliche Freigabe |
|---|---|---|
| Linux | Debug/Release, linux-x64-Publish, Unit/PDF/MariaDB/Headless | Native Dateidialoge unter den eingesetzten Wayland-/X11-Portalen und PDF-Viewer prüfen. |
| Android | Debug-APK mit .NET-10-Workload, JDK 21 und Android SDK | Gerät/Emulator: Formular, Tastatur, `content:`-Speicherziel, Abbruch, Offlinezustand und PDF-Viewer. |
| iOS | Auf Linux nicht ausführbar | Mac mit passendem Xcode/SDK erforderlich. Build und Simulator-Smoke dort durchführen. |
| Browser | Quellprojekt kann gebaut werden | Kein funktionsfähiger Datenbankclient: Browser können diese direkte MySQL-Verbindung nicht verwenden. Webbetrieb benötigt eine Backend-API; ein erfolgreicher Build ist keine Web-Freigabe. |

Aktuell bekannte Abhängigkeiten: SkiaSharp.NativeAssets.Android 2.88.9 meldet XA0141 wegen 16-KB-Speicherseiten. Der Browser-Build meldet nicht eingebundene native WASM-Referenzen. Diese Warnungen sind nicht unterdrückt. Die neue Testsuite behebt weder den NVIDIA-Treiberkonflikt des Systems noch diese Plattformgrenzen.

Manuelle Linux-Prüfung: Anwendung starten, Einstellungen öffnen, aktuelle Faktoren ohne Jahresauswahl anzeigen, ungültige Eingaben prüfen; Rechnung als PDF unter geändertem Unicode-Dateinamen speichern; bei Abbruch darf der Status unverändert bleiben; Viewer öffnen; Fenster während eines größeren Exports bedienen. Dabei ausschließlich eine Testdatenbank verwenden. Ein produktiver Schreibtest ist nicht erforderlich.

Ausgeführt: nativer Linux-Start sowohl des Debug-Builds als auch des veröffentlichten Release-Builds. Der explizite `--smoke-test` prüfte Initialisierung und Datenladen mit einer synthetischen lokalen Testdatenbank und beendete beide Läufe erfolgreich mit Exitcode 0. Die Testdatenbank wurde anschließend entfernt und der ausschließlich für diese Prüfungen gestartete Server beendet.
