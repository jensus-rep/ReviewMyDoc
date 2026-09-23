# Konventionen

Stand 22.09.2026, Version 1. Verbindliche Regeln für Code, Struktur, Tests und Zusammenarbeit in
ReviewMyDoc. Sie gelten für Jens, Claude und jeden Subagenten, der einen TaskPilot-Task bearbeitet.
Was hier steht, wird in TaskPilot nicht wiederholt, sondern referenziert.

Das Regelwerk ist bewusst nah an dem des Projekts Atelier, damit Bausteine und Gewohnheiten in beide
Richtungen wandern können. Die Abweichungen sind einzeln begründet.

## Stack

.NET 10 mit ASP.NET Core Razor Pages, C# mit Nullable-Referenztypen und Warnungen als Fehler.
Serverseitig gerendert. Azure Blob Storage über `Azure.Storage.Blobs` mit Managed Identity, lokal
ein Verzeichnis. Hosting als Azure Web App (Linux) mit GitHub Actions. Browser-Logik als TypeScript
in den Bausteinen unter `components/`, mit esbuild zu ES-Modulen gebaut, ohne Framework, ohne
UI-Bibliothek, ohne CSS-Framework.

**Abweichung von Atelier: keine Datenbank und kein ASP.NET Core Identity.** Begründung in
[Datenmodell.md](Datenmodell.md). Statt Identity:

- Der Eigentümer meldet sich mit einem Passwort an, dessen Hash (`PasswordHasher<T>` aus
  `Microsoft.AspNetCore.Identity`, ohne den Rest von Identity) als App Setting liegt. Danach eine
  Cookie-Authentifizierung mit der Rolle `owner`.
- Reviewer melden sich gar nicht an. Sie kommen über `/review/{token}`; der Server prüft den
  Tokenhash, setzt eine auf diesen Reviewauftrag begrenzte Link-Session als Cookie und leitet auf
  die Reviewansicht. Das Muster entspricht `/seite/{token}` in Atelier.
- Antiforgery, Data Protection (Schlüssel im Blob Storage), Ratenbegrenzung und Security-Header wie
  in Atelier, ohne Abstriche.

AI gehört nach der MVP-Entscheidung vom 23.09.2026 nicht zum aktuellen Lieferumfang. Für die spätere
Anbindung ist eine Schnittstelle in Core mit Implementierungen in Infrastructure vorgesehen,
Anthropic und OpenAI, ausgewählt über `Ai:Provider`. Die Schnittstelle kennt nur Textaufgaben, keine
Anbieterbegriffe. Kein Schlüssel verlässt den Server; der Browser spricht ausschließlich mit den
eigenen Endpunkten.

## Sprache

Dokumentation, Oberfläche, Commit-Rümpfe und TaskPilot-Einträge auf Deutsch, ohne Gedankenstriche
und mit echten Umlauten, nie mit Ersatzschreibweisen wie „Qualitaet“. Der Commit-Betreff ist die
Ausnahme und steht auf Englisch, siehe Git und Ablauf.

Bezeichner im Code, Kommentare und XML-Dokumentation auf Englisch. Zustandswerte
sind englische Bezeichner im Code (`Sent`, `Returned`) und deutsche Beschriftungen in der
Oberfläche; die Zuordnung steht an genau einer Stelle.

Blobpfade und JSON-Felder auf Englisch in camelCase, wie in [Datenmodell.md](Datenmodell.md)
festgelegt. Wer einen Pfad oder ein Feld ändert, ändert zuerst dort.

## Struktur

```
ReviewMyDoc.sln
Directory.Build.props          Nullable, TreatWarningsAsErrors, LangVersion, gemeinsame Einstellungen
src/ReviewMyDoc.Web/           ASP.NET Core Razor Pages
  Program.cs                   Komposition: Dienste, Middleware, Security-Header, /components/
  Pages/                       Übersicht (Startseite nach Anmeldung), Anmeldung, Dokumentliste
  Pages/Dokumente/             Dokument bearbeiten, gliedern, Reviewauftrag erteilen, Rücklauf
  Pages/Review/                Reviewansicht unter /review/{token}, eigener Rahmen ohne Navigation
  Pages/Shared/                _Layout.cshtml, _ReviewLayout.cshtml
  Pages/Shared/Ui/             Razor-Partials der Bausteine, Modell je Partial als record daneben
  Security/                    Passwortanmeldung, Link-Session, Ratenbegrenzung, Header, Data Protection
  wwwroot/css/site.css         Gemeinsamer Anwendungsrahmen und Formulare
  wwwroot/css/pages/           Stile je Ansicht, einschließlich ihrer Breakpoints
  wwwroot/images/              Bilder; keine Kopien aus components/
  appsettings.json             alle Schlüssel mit Entwicklungswerten, keine Secrets
src/ReviewMyDoc.Core/          Fachlichkeit; kennt weder ASP.NET noch Azure noch einen AI-Anbieter
  Documents/                   Fassade DocumentService, Ergebnis und Speichervertrag
    Models/                    Aggregat, Abschnitte, Kennungen und Zustände
    Operations/                Text, Gliederung, Teilung und gemeinsame Schreibregeln
    Versions/                  Unveränderliche Fassungen, Vergleich und Versionsdienst
    Persistence/               Store, Pfade und JSON-Abbildung
  Reviews/                     Auftrag und Rückmeldungen, Fassade und Store
    Operations/                Entscheidungen, Dokumentfreigabe und Pipelineprojektion
  Storage/                     IObjectStore: lesen, schreiben mit ETag, auflisten, anhängen
src/ReviewMyDoc.Infrastructure/
  Storage/                     BlobObjectStore (Azure), DirectoryObjectStore (lokal)
tests/ReviewMyDoc.Tests/       xUnit: Unit-Tests für Core, Integrationstests mit WebApplicationFactory
  Documents/Operations/        Testklassen je Dokumentoperation
  Documents/Support/           Gemeinsamer Aufbau mit echtem Verzeichnisspeicher
  Web/                         Documents, Reviews, Security, Components und Support
components/                    framework-freie Bausteine, Regeln in components/README.md
docs/                          Konzept.md, Datenmodell.md, Konventionen.md, Betrieb.md
```

`ReviewMyDoc.Web` liefert den Repo-Ordner `components/` unter `/components/` aus, wie in Atelier,
damit Tokens und Bausteine genau einmal existieren.

Unterordner ordnen die Pflegebereiche innerhalb eines Features. Die öffentlichen Namespaces
bleiben auf Feature-Ebene (`ReviewMyDoc.Core.Documents`, `ReviewMyDoc.Core.Reviews`), damit
eine Änderung der Ablage keine Änderung aller Aufrufer erfordert. Webtests verwenden weiterhin
`ReviewMyDoc.Tests.Web`. Razor-Pages bleiben wegen ihrer Routen an ihren bestehenden Orten.
AI- und Mailordner werden erst mit einer tatsächlichen Implementierung angelegt.

Dateien ab etwa 400 Zeilen werden auf mehrere Zuständigkeiten geprüft. Die Zeilenzahl ist ein
Prüfanlass, kein starres Limit: zusammenhängende Invarianten und Speicherverträge bleiben
zusammen. Aufteilungen folgen Fachoperationen und werden durch bestehende Tests abgesichert.
Aktuelle Entscheidungen und Pflegegrenzen stehen in [Architektur.md](Architektur.md).

## Bausteine und ihre Herkunft

Jedes wiederkehrende Element der Oberfläche ist ein Baustein unter `components/`: eigener Ordner,
eigene CSS-Datei, TypeScript nur wenn nötig, README, `demo.html`, keine Abhängigkeit zu anderen
Bausteinen, kein Framework. Es gelten die Regeln aus `components/README.md` in Atelier: BEM mit
Komponentenpräfix, Einstellbares als CSS-Variablen mit Präfix in `:where(…)`, Bewegung reduzieren
immer berücksichtigt, Versionsnummer im Kopf der Datei und in der README.

Zwei Wege hinein, und die README jedes Bausteins nennt in ihrer ersten Zeile welchen:

- **Aus Atelier kopiert.** Zeile `Herkunft: Atelier components/<name>, Stand <Datum>, Commit <sha>`.
  Geändert wird dann in Atelier und neu kopiert, nicht hier gepflegt. Nur so bleibt der Rückweg
  offen.
- **Hier entstanden.** Zeile `Herkunft: ReviewMyDoc`. Ist der Baustein auch außerhalb dieses
  Projekts sinnvoll, wird er nach Fertigstellung nach Atelier zurückgegeben und die Zeile
  umgeschrieben.

Ein Baustein wird erst in eine Seite eingebaut, wenn seine `demo.html` steht. TypeScript in
Bausteinen: `strict`, keine Abhängigkeiten außer der DOM-Typisierung, Rechenlogik in eigenen
Funktionen ohne DOM-Zugriff, damit sie mit `node --test` ohne Browser prüfbar ist.

Design-Tokens kommen aus Atelier und sind die einzige Quelle für Schrift, Abstände, Radien und
Bewegung. Die Farben dieser Anwendung stehen nicht darin: Der hier entstandene Baustein `theme`
schreibt die sieben Farbrollen der Tokens neu und trägt das Erscheinungsbild von ReviewMyDoc: ein
reduziertes System aus kühlem Weiß, Anthrazit und Dunkelblau mit tonalen blauen Abstufungen für
Zustände. Er wird unmittelbar nach den Tokens geladen und vor allen übrigen Bausteinen. Kein
Baustein und keine Seite verwendet Rohwerte; wer eine Farbe braucht, nimmt eine Rolle oder eine
Farbrolle, und Farbe ist nie das einzige Merkmal eines Zustands.

## Code

- Jede Datei beginnt mit einem Kommentar, der in ein bis drei Sätzen sagt, was sie tut und warum es
  sie gibt. Öffentliche Typen und Methoden tragen XML-Dokumentation. Kommentare erklären das Warum,
  nicht das Was. Kein auskommentierter Code.
- Ein `TODO` nennt immer die TaskPilot-Task-ID, sonst wird er nicht angelegt.
- Fachlogik liegt in `ReviewMyDoc.Core` hinter Schnittstellen und kennt kein ASP.NET, kein Azure und
  keinen AI-Anbieter. `ReviewMyDoc.Infrastructure` implementiert die Schnittstellen. Razor Pages
  sind dünn: Eingabe binden und prüfen, Dienst aufrufen, Ergebnis rendern.
- Ablagezugriff nur über `IObjectStore`. Keine Seite und kein Dienst außerhalb von
  `Infrastructure/Storage` kennt einen Blobpfad als Zeichenkette; Pfade baut genau eine Stelle je
  Aggregat. Jede Änderung an Pfaden oder JSON-Feldern ist zuerst eine Änderung in
  `docs/Datenmodell.md`.
- Jeder Schreibvorgang auf einen bestehenden Blob geht mit `If-Match`. Ein Konflikt wird der
  Oberfläche gemeldet, nie stillschweigend überschrieben.
- Konfiguration nur über `IOptions<T>` aus `appsettings.json`, Umgebungsvariablen und, lokal,
  `dotnet user-secrets`. Jeder Schlüssel steht in `appsettings.json` mit Entwicklungswert und ist in
  `docs/Betrieb.md` beschrieben. Keine Secrets im Repo. In Azure dieselben Schlüssel als App
  Settings mit `__` als Trenner.
- Sicherheit: Antiforgery in jedem Formular, Ratenbegrenzung an Anmeldung, Tokeneinlösung und
  AI-Endpunkten, Security-Header zentral in einer Middleware, Data-Protection-Schlüssel im Blob
  Storage, Uploads mit Größen- und Typprüfung. Ein abgelehnter Aufruf antwortet als 429 mit
  `Retry-After` in seinem Rahmen, nie als leere Seite.
- Sichtbarkeit ist eine Frage des Dienstes, nicht der Ansicht: Was ein Empfänger nicht sehen darf,
  steht nicht im Modell, das die Seite rendert. Eine Prüfung allein im Markup gilt als Fehler.
- Keine personenbezogenen Daten in Logs, keine Tokens in Logs, Clientadressen nur gehasht.

## Tests

- Befehle: `dotnet build -warnaserror`, `dotnet format --verify-no-changes`, `dotnet test`,
  `npm run typecheck`, `npm test`, `npm run build` mit anschließendem
  `git diff --exit-code components/`. Alle laufen im Build-Job der Pipeline; ohne grün kein
  Deployment.
- Jede Razor Page hat mindestens einen Integrationstest über `WebApplicationFactory`: Statuscode,
  zentraler Inhalt, Header. Jeder Dienst in Core hat Unit-Tests für den Erfolgsfall und die im Task
  genannten Fehlerfälle.
- Jede Zusicherung aus `docs/Datenmodell.md` hat mindestens einen Test, der ihre Verletzung
  versucht. Besonders: ETag-Konflikt, Schreiben auf eine bestehende Version, Rückmeldung auf einen
  nicht zugewiesenen Abschnitt, Abnahme mit offener Rückmeldung.
- Für die Sichtbarkeitsstufen gibt es Tests, die die Antwort des Servers auf nicht zugewiesene
  Abschnitte durchsuchen. Ein Test, der nur prüft, dass etwas nicht angezeigt wird, genügt nicht.
- Tests laufen gegen `DirectoryObjectStore` in einem temporären Verzeichnis je Lauf, damit parallele
  Läufe aus verschiedenen Worktrees sich nicht stören. Ein zusätzlicher Test gegen echtes Blob
  Storage läuft nur mit gesetzter Verbindung, sonst übersprungen mit Grund, nicht rot.
- AI und Mail haben Ersatzimplementierungen; Tests verwenden nur diese. Kein Test ruft einen
  externen Anbieter.
- **Ein lokaler Server, ein Port.** Die Anwendung läuft lokal ausschließlich unter
  `http://localhost:5071`. Browserprüfungen und Vorschauen verwenden diese eine Instanz. Eine
  Vorschau, die nicht den Stand des Arbeitsbaums zeigt, gilt als nicht geprüft.
  Der Port ist nicht beliebig: Browser führen eine feste Sperrliste, und 5060 und 5061 stehen darauf,
  weil sie für SIP vergeben sind. Chrome antwortet dort mit `ERR_UNSAFE_PORT`, ohne dass die
  Anwendung etwas davon merkt. Wer den Port ändert, prüft ihn vorher gegen diese Liste.

## Git und Ablauf

- Branch `main` deployt. Gearbeitet wird pro TaskPilot-Task in einem eigenen Branch oder
  Git-Worktree, benannt `task/<tsk-id>-<kurzname>`.
- Kleine Commits in der Conventional-Commit-Form, wie im Projekt aiqa:

  ```text
  type(scope): imperative summary
  ```

  Typ und Bereich klein und auf Englisch, der Bereich so eng wie sinnvoll. Übliche Typen sind
  `feat`, `fix`, `docs`, `test`, `style`, `perf` und `chore`. Beispiel:
  `feat(review-link): redeem a review token once`.
- Der Rumpf ist deutsch mit echten Umlauten und sagt, was sich ändert und warum, nicht was der Diff
  ohnehin zeigt. Er nennt bewusste Abweichungen, verworfene Wege und offene Punkte, dazu die
  TaskPilot-Task-ID in einer eigenen Zeile.
- Definition of Done je Task: Akzeptanz aus der Task-Beschreibung erfüllt, alle Prüfbefehle grün,
  betroffene Doku aktualisiert (`docs/Datenmodell.md`, `docs/Betrieb.md`, README des Bausteins),
  TaskPilot-Task mit Commit, `implementation_summary` und `verification` gemeldet.

## Parallelarbeit mit Subagenten

- Ein Subagent bearbeitet genau einen Task in einem eigenen Worktree und ändert nur die Dateien, die
  der Task nennt oder die für dessen Ergebnis zwingend nötig sind.
- Vor dem Start liest er den Task, den Story-Kontext und die erledigten Vorgänger in TaskPilot sowie
  diese Datei, `docs/Konzept.md` und `docs/Datenmodell.md`. Er erfindet keine zusätzlichen Features.
- Er meldet Beginn (`in_arbeit`) und Ende (`erledigt` mit Commit, Zusammenfassung, Verifikation,
  offenen Punkten) in TaskPilot. Abweichungen von der Vorgabe meldet er als
  `specification_deviation`, nicht stillschweigend.
- Die Integration in `main` macht Jens oder Claude: Diff lesen, Tests selbst laufen lassen, dann
  mergen. Der Abschlussbericht eines Subagenten ist eine Behauptung, kein Nachweis.
- Tasks, die denselben Bereich berühren (`Program.cs`, `appsettings.json`, gemeinsame Layouts),
  laufen nacheinander, nicht parallel. Die Abhängigkeiten in TaskPilot sind dafür die verbindliche
  Quelle.
- Niemand beendet Prozesse nach Name. `taskkill /IM dotnet.exe` und Vergleichbares trifft jeden
  Worktree auf der Maschine und jedes andere Projekt dazu, nicht nur den eigenen Lauf. Wer einen
  Server startet, merkt sich dessen Prozesskennung und beendet genau diese.

## Übergabe

Das Repository geht am Ende an den Auftraggeber, öffentlich und zum Weiterbauen. Daraus folgen zwei
Pflichten, die jeder Task mitträgt: Die README beschreibt den Start in wenigen Schritten und bleibt
wahr, und `docs/Betrieb.md` beschreibt jeden Konfigurationsschlüssel und jeden Schritt des
Deployments so, dass jemand ohne Kenntnis dieses Projekts ihn ausführen kann.
