Herkunft: ReviewMyDoc

# Document editor 1.2.0

Eine direkt formatierte Schreibfläche mit nativer Textauswahl, Rückgängig und einem
kontextuellen Menü. Überschriften und Listen werden in ihrer tatsächlichen Darstellung
bearbeitet. Der Baustein ersetzt die Markdown-Schicht für den dokumentorientierten Workflow.

`document-editor.ts` koordiniert Auswahl, Speichern und Sammeln über die Formularhandler
der Dokumentenseite. `markdown.ts` verantwortet die Serialisierung, `review-collection.ts`
die Sammlungen und ihre Badges und `review-access.ts` die Einladungslinks. Der Build bündelt
diese internen Module im versionierten Einstiegspunkt. `demo.html` zeigt die Oberfläche ohne Server.
Das gespeicherte Format bleibt Markdown; ausgeliefertes HTML kommt ausschließlich aus dem
bereinigenden Serverrenderer. Eingefügtes HTML wird auf Textelemente reduziert, Bilder und
aktive Inhalte werden entfernt. Ohne JavaScript ist der Text bereits formatiert lesbar;
das Textformular und die Sammlung ganzer Abschnitte bleiben verfügbar. Konfliktantworten behalten
die Eingabe im Formular. Freigegebene Dokumente aktivieren den Editor erst nach Wiederöffnung.

Änderungen werden nach einer Schreibpause gespeichert. Speichervorgänge laufen nacheinander,
tragen das gelesene ETag und melden Konflikte, ohne die lokale Eingabe zu ersetzen. Vor dem
Sammeln wird gespeichert; bei einem Fehler bleibt die Auswahl erhalten. Beim Verlassen mit
ungespeicherten Änderungen warnt der Browser. Gesammelte Passagen liegen auf dem Server.

Review-Sets werden zuerst mit einem Namen angelegt und sofort gespeichert, auch ohne Passagen.
Die Auswahlleiste an einer Markierung enthält die offenen Sets und „Zum Set hinzufügen“.
Eine Markierung kann auch auf die Ablagefläche oder einen Set-Badge gezogen werden. Dabei wird
der Text kopiert. Klick und Tastatur bleiben als Alternative verfügbar. Nur „Person zuweisen“
öffnet die Zuweisungsseite. Während des Sammelns bleibt die Zielsammlung gesperrt; ein
Speicherfehler lässt Dokument und Auswahl erhalten.

Rechts stehen zunächst nur Setnamen und Anzahl. „Passagen und Aktionen“ zeigt die Details.
„Sammlung abschließen“ entfernt das Set aus der Markierungsauswahl, erhält aber seine Inhalte
für die spätere Zuweisung. Abgeschlossene Sammlungen und bereits zugewiesene Aufträge sind in
separaten aufklappbaren Bereichen erreichbar. Vor der Zuweisung lässt sich eine Sammlung wieder
öffnen. Ohne JavaScript stehen dieselben Anlage- und Abschlussformulare sowie die Auswahl eines
Sets für einen ganzen Abschnitt bereit. Bestehende Aufträge bleiben lesbar.

`npm run typecheck`, `npm test` und `npm run build` prüfen und bauen den Baustein.
