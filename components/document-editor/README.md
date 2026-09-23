Herkunft: ReviewMyDoc

# Document editor 1.0.0

Eine direkt formatierte Schreibfläche mit nativer Textauswahl, Rückgängig und einem
kontextuellen Menü. Überschriften und Listen werden in ihrer tatsächlichen Darstellung
bearbeitet. Der Baustein ersetzt die Markdown-Schicht für den dokumentorientierten Workflow.

`document-editor.ts` enthält die Markdownserialisierung sowie Speichern und Sammeln über
die Formularhandler der Dokumentenseite. `demo.html` zeigt die Schreibfläche ohne Server.
Das gespeicherte Format bleibt Markdown; ausgeliefertes HTML kommt ausschließlich aus dem
bereinigenden Serverrenderer. Eingefügtes HTML wird auf Textelemente reduziert, Bilder und
aktive Inhalte werden entfernt. Ohne JavaScript ist der Text bereits formatiert lesbar;
das Textformular und die Sammlung ganzer Abschnitte bleiben verfügbar. Konfliktantworten behalten
die Eingabe im Formular. Freigegebene Dokumente aktivieren den Editor erst nach Wiederöffnung.

Änderungen werden nach einer Schreibpause gespeichert. Speichervorgänge laufen nacheinander,
tragen das gelesene ETag und melden Konflikte, ohne die lokale Eingabe zu ersetzen. Vor dem
Sammeln wird gespeichert; bei einem Fehler bleibt die Auswahl erhalten. Beim Verlassen mit
ungespeicherten Änderungen warnt der Browser. Gesammelte Passagen liegen auf dem Server.

`npm run typecheck`, `npm test` und `npm run build` prüfen und bauen den Baustein.
