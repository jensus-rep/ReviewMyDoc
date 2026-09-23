<!--
  Einstieg in das Repository: was ReviewMyDoc ist, wie die Anwendung lokal startet und
  welcher Befehl was prüft. Alles Weitere steht in docs/.
-->

# ReviewMyDoc

ReviewMyDoc ist eine Webanwendung, mit der eine Person längere Fachdokumente schreibt, sie in
Abschnitte gliedert und gezielt einzelnen Personen zum Review gibt. Ein Reviewauftrag legt fest,
welche Abschnitte eine Person sieht und bis wann sie antworten soll; die Person braucht dafür kein
eigenes Konto, sondern bekommt einen Link. Eine Übersicht zeigt jederzeit, was draußen ist, was
zurück ist und was auf eine Entscheidung des Eigentümers wartet. Die Anwendung ist serverseitig
gerendert mit ASP.NET Core Razor Pages auf .NET 10, ohne relationale Datenbank; die Ablage liegt als
Blob, lokal in einem Verzeichnis und in Azure als Azure Blob Storage. Das verbindliche Regelwerk für
Code, Struktur, Tests und Zusammenarbeit ist [docs/Konventionen.md](docs/Konventionen.md).

## Start in fünf Schritten

1. **Werkzeuge prüfen.** `dotnet --version` muss 10.0 melden, `node --version` mindestens 22.
2. **.NET holen und bauen.** `dotnet restore` und `dotnet build`
3. **Frontend-Werkzeuge holen.** `npm install`
4. **Starten.** `dotnet run --project src/ReviewMyDoc.Web` und die Adresse
   <http://localhost:5071> öffnen. Wichtig: Es gibt nur das Startprofil `http`, die Anwendung läuft
   lokal ausschließlich unter dieser einen Adresse, ein https-Profil ist nicht eingerichtet. Es
   erscheint die Anmeldung; das Passwort der Entwicklung ist `entwicklung`. Danach trägt die
   Startseite die Überschrift ReviewMyDoc. Ein eigenes Passwort erzeugt man mit
   `dotnet run --project src/ReviewMyDoc.Web -- passwort-hash`, siehe
   [docs/Betrieb.md](docs/Betrieb.md).
5. **Prüfen.** `dotnet test`, `npm run typecheck`, `npm test`

## Prüfbefehle

Die Dokumentenansicht ist eine direkt bearbeitbare Schreibfläche. Änderungen werden nach einer
kurzen Schreibpause gespeichert. Markierten Text kann man sammeln oder direkt zum Review geben.
Eine Sammlung wird einer Person mit Frist zugewiesen; der erzeugte Link wird anschließend selbst
geteilt. Die Person sieht ausschließlich die Ausschnitte, gibt Kommentare ab und sendet das Review
zurück. Die Startseite zeigt die Pipeline; dort lassen sich Rückmeldungen bearbeiten und Aufträge
abschließen. Gliederung und Markdownformulare bleiben als Nebenansicht beziehungsweise als
Fallback ohne JavaScript erreichbar. Word-Dateiimport und automatische Übernahme von
Änderungsvorschlägen gehören nicht zu diesem Ablauf.

Diese Befehle laufen im Build-Job der Pipeline (`.github/workflows/build.yml`) bei jedem Push und
jedem Pull Request; ohne grün kein Deployment. Alle laufen vom Repositorywurzelverzeichnis aus.

| Befehl                               | Prüft                                                                                          |
| ------------------------------------ | ----------------------------------------------------------------------------------------------- |
| `dotnet build -warnaserror`          | alle vier Projekte übersetzen, jede Warnung zählt als Fehler                                     |
| `dotnet format --verify-no-changes`  | Formatierung und C#-Stil nach `.editorconfig`, ohne etwas zu ändern                              |
| `dotnet test`                        | xUnit-Tests in `tests/ReviewMyDoc.Tests`: die Projektgrenzen, den Vertrag des Objektspeichers gegen beide Ablagen und die Auswahl zwischen ihnen. Ohne konfiguriertes Blob Storage werden dessen Tests mit Grund übersprungen, sie sind dann nicht rot |
| `npm run typecheck`                  | `tsc --noEmit` über `components/**/*.ts` und das Build-Skript unter `scripts/`                  |
| `npm test`                           | `node --test` über die Bausteintests: `components/**/*.test.mjs` und `components/**/*.test.ts`  |
| `npm run build`                      | esbuild erzeugt aus jeder `components/<name>/<name>.ts` das ES-Modul `<name>.js` daneben         |
| `git diff --exit-code components/`   | nach `npm run build`: die eingecheckten `.js`-Dateien passen zum TypeScript-Quelltext            |

Zwei Eigenheiten der Umgebung, damit die Befehle überall gleich laufen:

- Die Muster stehen in Anführungszeichen (`node --test "components/**/*.test.mjs"`). Unter Windows
  löst die Shell sie sonst selbst auf, und Node bekommt kein Muster zu sehen.
- `npm test` läuft mit `--experimental-strip-types`, weil Node TypeScript-Testdateien sonst nicht
  ausführt.

## Struktur

```
ReviewMyDoc.sln                  Projektmappe mit vier Projekten
Directory.Build.props            gemeinsame Einstellungen (.NET 10, Nullable, Warnungen als Fehler)
src/ReviewMyDoc.Web/              Razor Pages, Program.cs, wwwroot, Properties/launchSettings.json
src/ReviewMyDoc.Core/              Fachlogik hinter Schnittstellen, ohne ASP.NET, Azure oder AI-Anbieter
src/ReviewMyDoc.Infrastructure/    Implementierungen der Schnittstellen aus Core (Ablage, AI, Mail)
tests/ReviewMyDoc.Tests/           xUnit: Unit- und Vertragstests, später Integrationstests über WebApplicationFactory
components/                        framework-freie Bausteine, ausgeliefert unter /components/, Regeln in components/README.md
scripts/build-components.mjs       Frontend-Build (esbuild)
docs/                              Konzept, Konventionen, Datenmodell, Betrieb
```

Der vollständige Zielaufbau mit allen künftigen Ordnern und ihrer Begründung steht in
[docs/Konventionen.md](docs/Konventionen.md), Abschnitt Struktur.

## Weiterführende Dokumentation

- [docs/Konzept.md](docs/Konzept.md): hier steht, was ReviewMyDoc fachlich ist, welche vier
  Begriffe das Modell trägt und welche Abläufe es abdeckt. Zum Lesen, bevor man an der Fachlichkeit
  etwas ändert.
- [docs/Konventionen.md](docs/Konventionen.md): hier stehen die verbindlichen Regeln für Stack,
  Struktur, Code, Tests und Git. Zum Nachschlagen vor jeder Änderung am Code.
- [docs/Datenmodell.md](docs/Datenmodell.md): hier steht, wie die Daten als Blobs abgelegt sind,
  unter welchen Pfaden und mit welchen Zusicherungen. Zum Lesen, bevor man einen Pfad oder ein
  JSON-Feld ändert.
- [docs/Betrieb.md](docs/Betrieb.md): hier steht jeder Konfigurationsschlüssel mit seiner Bedeutung,
  wo die Daten liegen und was in Azure eingerichtet sein muss. Zum Nachschlagen beim Einrichten und
  beim Veröffentlichen. Für den rein lokalen Start braucht man daraus nichts.
