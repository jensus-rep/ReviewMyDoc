# Zuständigkeiten im MVP

Die Dienste sind nach fachlichen Änderungen aufgeteilt. Eine Zuständigkeit bedeutet hier
einen klaren Pflegebereich, keine zusätzliche Benutzerrolle. AI gehört nicht zum MVP.

| Pflegebereich | Verantwortliche Klassen | Grenze |
| --- | --- | --- |
| Dokumentzugang | `DocumentService` | Stabile Fassade, Anlegen, Lesen, Umbenennen |
| Gliederung | `DocumentOutlineService` | Hinzufügen, Umbenennen, Reihenfolge, Entfernen |
| Abschnittsteilung | `DocumentSplitService` | Teilung mit Kompensation unvollständiger Schreibvorgänge |
| Versionierung | `DocumentVersionService` | Unveränderliche Fassungen und Vergleich |
| Gemeinsame Schreibregeln | `DocumentOperations` | ETag-Prüfung, Freigabesperre, bedingtes Schreiben |
| Dokumenttext | `DocumentEditingService` | Abschnittstext und Text-ETag |
| Reviewzugang | `ReviewService` | Sammlung, Erteilen, Sichtbarkeit, Zugang, Rückgabe |
| Review-Sets | `ReviewCollectionService` | Benannte leere Sets anlegen, Sammlung abschließen und wieder öffnen |
| Rückmeldungsentscheidung | `ReviewDecisionService` | Bearbeiten, Antworten, Übernehmen, Ablehnen, Abnehmen, Widerrufen |
| Dokumentfreigabe | `DocumentApprovalService` | Alle Aufträge prüfen, freigeben, ausdrücklich wieder öffnen |
| Übersicht | `ReviewPipeline` | Vier Spalten, Fristen über `TimeProvider`, 30 Tage für Erledigt |
| Speicherung | `DocumentStore`, `ReviewStore` | Pfade und JSON, keine Darstellung |
| Darstellung | Razor PageModels und Bausteine | Eingaben, Validierung, sichere Ausgabe |

Die internen Dokumentdienste werden über die bestehende Fassade aufgerufen. Sie erhalten
denselben Store und dieselbe Uhr. Dadurch bleiben bestehende Aufrufer und Vertragstests
stabil, während Änderungen je Fachbereich in getrennten Dateien erfolgen. Die gemeinsame
Basisklasse enthält ausschließlich Speicher- und Konfliktregeln, keine zusätzlichen Abläufe.
Neue Funktionen werden dem zuständigen Dienst zugeordnet, nicht der Fassade hinzugefügt.

Reviewentscheidungen verwenden dasselbe Auftrags-ETag. Eine Textübernahme betrifft zwei
Einträge; ihr Zwischenzustand `Applying` macht die Operation nach einem abgebrochenen Aufruf
wiederaufnehmbar. Solche internen Arbeitsdaten verlassen die Eigentümergrenze nicht.

Für spätere AI-Unterstützung bleibt die Grenze vorgesehen: anbieterunabhängige Schnittstelle
in Core, Anbieterimplementierungen in Infrastructure, ausdrückliche Übernahme in der Webschicht.
Im MVP gibt es weder Anbieteraufrufe noch einen erforderlichen API-Schlüssel.

## Ablage und Dateigrößen

Die tatsächliche Ordnerstruktur steht in [Konventionen.md](Konventionen.md#struktur).
Dokumentmodelle, Änderungsoperationen, Versionierung und Persistenz haben eigene Unterordner.
Bei Reviews liegen die Entscheidungen, Freigabe und Pipelineprojektion in `Operations/`;
die kleine gemeinsame Datenstruktur, Fassade und Speicherung bleiben direkt beim Feature.
Ein neuer Unterordner soll einen Pflegebereich zusammenfassen, nicht nur eine einzelne Datei verstecken.

Die bisherigen 1.159 Zeilen der Dokumentdienst-Tests sind nach Dokumentzugang, Gliederung,
Teilung, Nebenläufigkeit und Versionierung auf fünf Testklassen verteilt. `DocumentServiceTestBase`
enthält ausschließlich Aufbau und gemeinsame Assertions. Jeder Test erhält weiterhin sein
eigenes Verzeichnis; es gibt keinen gemeinsam veränderlichen Testzustand.
Webtests sind entsprechend ihrer Zuständigkeit unter `Documents`, `Reviews`, `Security`,
`Components` und `Support` abgelegt.

Die bisherigen 1.033 Zeilen von `site.css` sind in Anwendungsrahmen und Dateien unter
`wwwroot/css/pages/` aufgeteilt: Gliederungsformular, Dokumentansicht, Schreibbereich, Review
und Pipeline. Breakpoints liegen bei ihren Ansichten, Druckregeln beim Schreibbereich.
`_ApplicationStyles.cshtml` lädt diese Dateien in beiden Layouts mit Versionskennungen.
Gemeinsame Schreib- und Reviewlayouts gehören zu `workspace.css`; Rückmeldungen und
Versionsvergleich zu `review.css`. Der Test auf Design-Tokens erfasst alle CSS-Unterordner.

Bewusst zusammengehalten werden `Document` als Grenze der Aggregatinvarianten,
`DirectoryObjectStore` mit Sperren und atomaren Schreibregeln sowie `ObjectStoreContractTests`
als gemeinsamer Vertrag beider Speicheranbieter. Ihre Größe entsteht auch durch Erläuterungen
und Vertragsfälle. Eine rein mechanische Teilung würde diese Zusammenhänge schwerer prüfbar machen.
Die große `markdown-surface`-Komponente stammt aus Atelier und darf laut Herkunftsregel nur
dort geändert werden. Neue Fachabläufe werden nicht in diese bestehenden Dateien angehängt.
