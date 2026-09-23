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
