# GitHub Actions und Releases

[Zur Übersicht](../README.md)

## Automatische Prüfungen

`Build and test` läuft bei Branch-Pushes, Pull Requests und manuell über **Actions**:

- Unit-, Datenbank-, PDF- und Oberflächentests gegen eine eigene MariaDB 11.8.8.
- Self-contained Desktop-Publish für Linux x64 und Windows x64; zusätzliche Unit-Tests unter Windows.
- Android-Debug-Build ohne produktiven Signierschlüssel.
- Prüfung der Versionsregeln; TRX- und Coverage-Ergebnisse als Artefakt für 14 Tage.

Die Jobs verwenden .NET SDK 10.0.112. Es wird keine Verbindung zur produktiven Datenbank benötigt. Pull Requests erhalten keine Android-Release-Secrets. Fehler in einem erforderlichen Test oder Build verhindern die Veröffentlichung.

## Android-Signierschlüssel einmalig hinterlegen

Für dieses Projekt wurde ein dauerhafter Schlüssel erstellt. Auf dem Entwicklungsrechner liegen `campermanagement.p12` und `android-signing.json` im privaten, von Git ausgeschlossenen Verzeichnis `.local/signing/`. Dieses Verzeichnis sicher außerhalb des Rechners sichern. Es gehört weder in Git noch in Release-Dateien. Der Verlust des Schlüssels verhindert Updates bestehender Installationen.

Unter **Settings → Secrets and variables → Actions → Repository secrets** werden benötigt:

| Secret | Inhalt |
|---|---|
| `ANDROID_KEYSTORE_BASE64` | Base64-Inhalt der PKCS12-Datei, ohne Zeilenumbrüche |
| `ANDROID_KEY_ALIAS` | Alias aus `android-signing.json` |
| `ANDROID_KEYSTORE_PASSWORD` | `store_password` aus `android-signing.json` |
| `ANDROID_KEY_PASSWORD` | `key_password` aus `android-signing.json` |

Mit angemeldeter GitHub CLI kann `python3 build/upload_android_secrets.py` die vier Werte direkt aus den lokalen Dateien übertragen, ohne sie auszugeben. Der Upload überschreibt gleichnamige Repository-Secrets. Er ist ausschließlich für das Repository `SlDracon/CamperManagement` vorgesehen.

Die Pipeline legt den Schlüssel nur temporär an, übergibt Passwörter über Umgebungsvariablen, prüft die fertige APK-Signatur und entfernt die Schlüsseldatei anschließend. Fehlende Secrets führen zu einem klaren Fehler; ein Debug-Schlüssel wird nicht als Ersatz verwendet.

## Release erzeugen

Änderungen zuerst committen und pushen. Dann einen neuen, aufsteigenden Versions-Tag setzen:

```bash
git tag -a v1.0.0 -m "CamperManagement 1.0.0"
git push origin v1.0.0
```

`v1.0.0` ist ein Beispiel; für weitere Releases jeweils eine höhere, bisher unbenutzte Version wählen. Erlaubt ist ausschließlich `vMAJOR.MINOR.PATCH`, ohne führende Nullen oder Vorabversionssuffix. Grenzen: MAJOR 0–2099, MINOR/PATCH 0–999; `v0.0.0` ist gesperrt. Der Android-Versioncode wird als `MAJOR × 1.000.000 + MINOR × 1.000 + PATCH` gebildet. Dadurch erhalten aufsteigende Versionen auch auf Android einen höheren Versioncode.

Der Workflow **Release** führt zuerst sämtliche Prüfungen aus. Danach erstellt er:

- `CamperManagement-VERSION-linux-x64.tar.gz` mit enthaltener .NET-Laufzeit und ausführbarer Startdatei.
- `CamperManagement-VERSION-win-x64.zip` mit enthaltener .NET-Laufzeit.
- `CamperManagement-VERSION-android.apk`, signiert mit dem dauerhaften Schlüssel.
- `SHA256SUMS.txt` mit Prüfsummen dieser drei Dateien.

Die Dateien werden zunächst an einen Release-Entwurf angehängt. Erst wenn alle Uploads erfolgreich sind, wird er veröffentlicht. Bei einem Fehler kann der Lauf wiederholt werden: Ein vorhandener Entwurf wird vervollständigt. Bereits veröffentlichte Releases werden nicht überschrieben. Ein manueller Start des Release-Workflows muss einen vorhandenen Versions-Tag auswählen; ein Branch wird abgelehnt.

Nur der abschließende Veröffentlichungsjob erhält Schreibrechte auf Repository-Inhalte. Build-Jobs arbeiten mit Leserechten. Ein Release-Tag muss daher auf vertrauenswürdigen, geprüften Code zeigen.

## Installation und Grenzen

Desktop-Archive vollständig entpacken und `CamperManagement.Desktop` beziehungsweise `CamperManagement.Desktop.exe` starten. Eine zusätzliche .NET-Installation ist nicht nötig. Linux benötigt die Systembibliotheken einer üblichen grafischen Distribution. Die Pakete sind portable Archive, keine Installationsprogramme; Windows-Dateien sind nicht mit Authenticode signiert.

Beim ersten Start die lokale [Datenbankverbindung](einrichtung.md#datenbankverbindung) einrichten. Release-Pakete enthalten keine produktiven Zugangsdaten. Alte Zugangsdaten in früheren Git-Commits werden durch diese Änderung nicht aus der Historie entfernt und müssen administrativ gewechselt werden.

Das Android-APK ist für direkte Installation vorgesehen, nicht als Google-Play-Bundle. Die Application-ID bleibt unverändert. Eine zuvor mit dem Debug-Schlüssel installierte App lässt sich nicht durch diesen neuen Release-Schlüssel aktualisieren; sie muss gegebenenfalls zuerst deinstalliert und anschließend neu eingerichtet werden. Gerätestarts und 16-KB-Speicherseiten sind weiterhin manuell zu prüfen; siehe [Plattformstatus](platform-checks.md).

Offizielle Referenzen: [GitHub-Release-CLI](https://cli.github.com/manual/gh_release_create), [.NET-Android-Signierung](https://learn.microsoft.com/en-us/dotnet/maui/android/deployment/publish-cli?view=net-maui-10.0), [Artefakte hochladen](https://github.com/actions/upload-artifact) und [herunterladen](https://github.com/actions/download-artifact).

## Android: 16-KB-Prüfung

Das Android-Projekt überschreibt SkiaSharp und dessen Android-NativeAssets gezielt mit Version 3.119.4. Desktop verwendet weiter die Avalonia-Abhängigkeiten. `build/check_android_alignment.py` prüft alle ARM64-/x64-Bibliotheken, ihre LOAD-Segmente, problematische RELRO-Überlappungen und die Ausrichtung unkomprimierter APK-Einträge. Die Prüfung läuft im normalen Android-CI-Build und vor Veröffentlichung eines signierten APKs; der Release-Lauf führt zusätzlich `zipalign -c -P 16 4` aus. Ein fehlgeschlagener Check verhindert die Veröffentlichung.

Für einen lokalen Emulator-Test ein offizielles `google_apis_ps16k`-Abbild verwenden und `adb shell getconf PAGE_SIZE` prüfen (Ergebnis `16384`). Regulär mit `adb install --no-incremental app.apk` installieren. Auf dem geprüften Android-15-16-KB-Emulator führte eine inkrementelle Installation zu einem nativen Ladefehler, die reguläre Installation funktionierte. Bei Änderungen zwischen Desktop-/Android-Builds und fehlerhaften JNI-Callbacks zuerst `dotnet clean CamperManagement.Android -c Release` ausführen und Android separat neu bauen. Der saubere Release-Build wurde ohne Warnungen geprüft.

Das Manifest enthält eine auf `ACTION_VIEW` und `application/pdf` begrenzte `<queries>`-Deklaration. Sie ermöglicht Avalonias Prüfung auf einen PDF-Viewer unter Android 11 und neuer; sie gewährt keinen allgemeinen Speicherzugriff und benötigt keine `QUERY_ALL_PACKAGES`-Berechtigung. Speichern und anschließendes Öffnen wurden im 16-KB-Emulator erfolgreich getestet.
