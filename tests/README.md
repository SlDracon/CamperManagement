# Automatisierte Tests

[Zur Dokumentationsübersicht](../README.md)

Die regulären xUnit-2-Tests sind in Rider einzeln auffindbar. `CamperManagement.Tests.sln` enthält nur die gemeinsame Anwendung und vier Testprojekte; mobile Workloads und ein Mac sind für die Tests nicht erforderlich. Das Avalonia-Headless-Paket passt zur bestehenden Avalonia-11-Version.

```bash
dotnet test tests/CamperManagement.UnitTests
dotnet test tests/CamperManagement.PdfTests
dotnet test tests/CamperManagement.UiTests
```

Die Integrationstests benötigen eine **isolierte MariaDB 11.8.8**. Sie erzeugen pro Test eine eigene Datenbank `camper_test_<UUID>` und entfernen ausschließlich diese. Ohne expliziten Endpunkt schlagen sie mit einer Erklärung fehl; es gibt keinen Fallback auf TrueNAS. Der Testbenutzer benötigt CREATE/DROP DATABASE, Trigger-Rechte und für die SQL-Modus-Prüfung SUPER auf dieser ausschließlich für Tests verwendeten Instanz. Andere Datenbanken dürfen dort nicht betrieben werden.

```bash
docker run --rm --name camper-test-db -p 127.0.0.1:33307:3306 \
  -e MARIADB_ROOT_PASSWORD=isolated-test-password mariadb:11.8.8
# In einer zweiten Shell nach erfolgreicher Datenbankinitialisierung:
export CAMPER_TEST_DB='Server=127.0.0.1;Port=33307;User ID=root;Password=isolated-test-password'
dotnet test CamperManagement.Tests.sln --logger trx --collect:'XPlat Code Coverage'
```

Alternativ kann ein lokal gestarteter MariaDB-11.8.8-Server mit eigenen Test-Datendateien verwendet werden. Erlaubte Testhosts: `127.0.0.1`, `localhost`, `mariadb`; der Connection-String darf keinen Datenbanknamen enthalten. Alle Fixtures enthalten erfundene Personen. Produktive Sicherungen und Zugangsdaten gehören nicht in Testartefakte.

PDF-Tests prüfen echte PDF-Texte, Seiten, Dateihandles, Abbruch und Fehler beim Schreiben/Schließen. Die UI-Suite prüft den echten Avalonia-Dispatcher bei absichtlich verzögerten Schreibzugriffen. Sie ersetzt keinen Test echter System-Dateidialoge oder Gerätetreiber. `CAMPER_TEST_ARTIFACTS` kann auf einen lokalen Ordner gesetzt werden, um synthetische Layoutmuster zu behalten. Die eingebetteten Noto-Schriften stehen unter der beigefügten SIL Open Font License.

Die ältere `CamperManagement.RegressionTests`-Konsolenprüfung bleibt als zusätzlicher Diagnoseaufruf erhalten. Die regulären Suiten decken ihre Fälle einzeln ab und erweitern sie.

Die CI startet eine eigene MariaDB-Instanz, führt alle vier Suiten aus und baut Linux sowie Android. iOS und echte Android-Geräte werden dadurch nicht als geprüft ausgegeben. Details: [Testabdeckung](../docs/test-coverage.md) und [Plattformprüfungen](../docs/platform-checks.md).
