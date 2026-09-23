# Datenmodell

Stand 22.09.2026, Version 1. Wie die Daten liegen, was zusammen geschrieben wird und welche
Zusicherungen gelten. Es gibt keine relationale Datenbank; alles liegt als Blob. Das Fachliche
dahinter steht in [Konzept.md](Konzept.md).

## Warum ohne Datenbank

Die Fachlichkeit kennt keine Abfrage über alles. Jede Frage der Anwendung beginnt entweder bei einem
Dokument („welche Aufträge hat dieses Dokument“) oder bei der Übersicht, die über eine überschaubare
Menge Dokumente läuft. Dafür genügt das Auflisten nach Präfix, das Blob Storage selbst beherrscht.
Eine Azure SQL Database würde monatlich Geld kosten, Migrationen erzwingen und die Übergabe an einen
Fachfremden erschweren, ohne eine einzige dieser Fragen besser zu beantworten.

Die Grenze ist benannt: Jenseits einiger hundert Dokumente wird die Übersicht langsam, weil sie je
Dokument auflisten muss. Dann kommt ein Projektions-Blob dazu, der aus den Ereignissen neu gebaut
werden kann. Vorher nicht, denn ein Index, den niemand prüft, läuft auseinander.

## Ablage

Ein Container, Präfixe statt Tabellen. Kennungen sind URL-sichere Zufallswerte, keine laufenden
Nummern, und sie bestehen ausschließlich aus Kleinbuchstaben, Ziffern und Unterstrich.

Die Kleinschreibung ist keine Kosmetik. Blobnamen unterscheiden Groß- und Kleinschreibung, ein
Windows-Dateisystem nicht: zwei Kennungen, die sich nur darin unterscheiden, wären in Azure zwei
Einträge und lokal einer. Die Alternative wäre, die Schreibweise in den Dateinamen zu kodieren, und
das kostet genau die Lesbarkeit von Hand, die weiter oben als Grund dafür steht, einen Abschnitt als
schlichte `.md`-Datei abzulegen. Der Objektspeicher weist deshalb jeden Pfad mit einem Großbuchstaben
zurück, statt die beiden Implementierungen still auseinanderlaufen zu lassen.

```
documents/{documentId}/document.json              Metadaten und Reihenfolge der Abschnitte
documents/{documentId}/sections/{sectionId}.md    Text eines Abschnitts, Markdown
documents/{documentId}/versions/{version}.json    eingefrorener Stand samt Abschnittstexten
documents/{documentId}/reviews/{reviewId}.json    ein Reviewauftrag
documents/{documentId}/feedback/{feedbackId}.json eine Rückmeldung
documents/{documentId}/audit.log                  Append-Blob, eine JSON-Zeile je Ereignis
```

Warum der Text nicht in `document.json` steht: Der Eigentümer bearbeitet einen Abschnitt, während
ein anderer Vorgang die Reihenfolge ändert oder einen Auftrag anlegt. Getrennte Blobs heißen
getrennte Schreibkonflikte. Und ein Abschnitt als reine `.md`-Datei lässt sich lesen, sichern und
notfalls von Hand retten, ohne die Anwendung.

Warum jede Rückmeldung ein eigener Blob ist: Mehrere Reviewer schreiben gleichzeitig. Ein
gemeinsamer Blob je Dokument wäre die einzige echte Konfliktstelle des ganzen Systems.

## Die Dateien im Einzelnen

### document.json

```json
{
  "id": "d7kq2fr",
  "ownerId": "owner",
  "title": "Gutachten Musterstraße",
  "state": "InReview",
  "version": 4,
  "sections": [
    { "id": "s_1a2b", "heading": "Ausgangslage", "order": 1, "updatedAt": "2026-09-22T08:14:00Z" }
  ],
  "createdAt": "2026-09-19T10:00:00Z",
  "updatedAt": "2026-09-22T08:14:00Z"
}
```

`state`: `Draft`, `InReview`, `Approved`. Die Überschrift steht hier und nicht in der `.md`, damit
die Gliederung ohne Laden aller Abschnitte darstellbar ist. `version` zählt nur hoch, wenn ein Stand
eingefroren wird, nicht bei jedem Tastendruck.

### versions/{version}.json

```json
{
  "documentId": "d7kq2fr",
  "version": 4,
  "title": "Gutachten Musterstraße",
  "sections": [
    { "id": "s_1a2b", "heading": "Ausgangslage", "order": 1, "text": "Der Auftraggeber hat …" }
  ],
  "frozenAt": "2026-09-22T08:14:00Z"
}
```

Der vollständige Stand zum Zeitpunkt des Einfrierens, samt Abschnittstexten. Er wird geschrieben und
nie wieder geändert. Er ist die Fassung, die ein Reviewer sieht, und die Grundlage jeder
Gegenüberstellung alt gegen neu. `documentId` steht auch hier, obwohl der Pfad das Dokument schon
nennt, aus demselben Grund wie `id` in `document.json`: die Datei bleibt für sich lesbar, auch aus
dem Zusammenhang gerissen. `title` und die Überschriften sind die Werte, die zum Zeitpunkt des
Einfrierens galten, nicht die aktuellen.

### reviews/{reviewId}.json

```json
{
  "id": "r_9xq",
  "documentId": "d7kq2fr",
  "documentVersion": 4,
  "reviewerName": "Anna Berg",
  "reviewerEmail": "anna@example.org",
  "sectionIds": ["s_1a2b", "s_3c4d"],
  "visibility": "WholeDocument",
  "state": "Sent",
  "dueAt": "2026-09-29T12:00:00Z",
  "tokenHash": "…",
  "tokenExpiresAt": "2026-10-13T12:00:00Z",
  "sentAt": "2026-09-22T08:20:00Z",
  "openedAt": null,
  "returnedAt": null,
  "acceptedAt": null
}
```

`visibility`: `WholeDocument` (ganzes Dokument lesbar, geführt zum eigenen Teil) oder
`AssignedSectionsOnly`. `state`: `Sent`, `Opened`, `Returned`, `Accepted`, `Revoked`.

Das Token steht nur als Hash in der Datei. Der Klartext existiert einmal, im Link. Ist er verloren,
wird ein neuer erzeugt; der alte gilt dann nicht mehr.

### feedback/{feedbackId}.json

```json
{
  "id": "f_4tz",
  "documentId": "d7kq2fr",
  "reviewId": "r_9xq",
  "sectionId": "s_1a2b",
  "documentVersion": 4,
  "kind": "SuggestedChange",
  "comment": "Der zweite Absatz nennt die Frist doppelt.",
  "suggestedText": "…",
  "state": "Open",
  "createdAt": "2026-09-23T09:00:00Z",
  "resolvedAt": null,
  "answer": null
}
```

`kind`: `Comment`, `SuggestedChange`, `Question`. `state`: `Open`, `Accepted`, `Rejected`,
`Answered`.

### audit.log

Ein Append-Blob, eine JSON-Zeile je Ereignis: Zeitpunkt, Art, handelnde Rolle, betroffene Kennung.
Er beantwortet „wer hat wann was ausgelöst“, ohne dass die Aggregate eine Historie mitschleppen.
Keine personenbezogenen Daten außer der ohnehin gespeicherten Reviewerkennung, Clientadressen nur
gehasht.

## Zusicherungen

1. **Optimistisches Schreiben.** Jeder Schreibvorgang auf einen bestehenden Blob läuft mit
   `If-Match` gegen das ETag, das beim Lesen galt. Passt es nicht, ist das ein Konflikt, der der
   Oberfläche gemeldet wird („inzwischen geändert“), nie ein stilles Überschreiben.
   Das Löschen fällt nicht darunter und trägt keine Bedingung: Ob ein Abschnitt zum Dokument gehört,
   entscheidet `document.json`, und das wird unter dieser Zusicherung geschrieben. Den verwaisten
   Textblob danach zu entfernen ist Aufräumarbeit, kein Wettlauf, und zweimal löschen bleibt
   folgenlos.
2. **Versionen sind unveränderlich.** `versions/{n}.json` wird einmal geschrieben. Ein Schreibversuch
   auf eine bestehende Version ist ein Fehler, kein Überschreiben.
3. **Rückmeldung nur auf zugewiesene Abschnitte.** Der Dienst prüft gegen `sectionIds` des Auftrags,
   nicht gegen das, was der Browser schickt.
4. **Ein Auftrag ist erst abnehmbar, wenn jede Rückmeldung entschieden ist.** Kein `Open` mehr.
5. **Ein Dokument ist erst freigebbar, wenn jeder Auftrag abgenommen oder widerrufen ist.**
6. **Abschnittskennungen sterben nicht.** Wird ein Abschnitt gelöscht, für den Rückmeldungen
   existieren, bleiben diese lesbar und werden als verwaist gekennzeichnet.
7. **Kein Token im Klartext**, weder in Blobs noch in Logs.

## Lokal und in Azure

### Ergänzung: Markierungen sammeln und gemeinsam prüfen (23.09.2026)

Benannte Review-Sets werden bereits ohne Passagen als `Draft` gespeichert. `setName` enthält
ihren Namen (1 bis 80 Zeichen, Standard für ältere Einträge: `Review-Set`).
`collectionClosedAt` ist zunächst `null`; ein Zeitpunkt schließt die Sammlung für neue Passagen.
Das ist keine Reviewabnahme: Ein geschlossenes Set bleibt zuweisbar und kann vor der Zuweisung
wieder zum Sammeln geöffnet werden. Die Markierungsauswahl enthält ausschließlich `Draft`
mit `collectionClosedAt == null`. Zuweisung und Rückmeldung behalten ihre bisherigen Zustände.
Anlegen, Schließen, Öffnen und Hinzufügen prüfen Dokumentfreigabe beziehungsweise Auftrags-ETag;
ein altes Browserfenster kann einem geschlossenen Set keine Passagen mehr hinzufügen.

MVP-Erweiterung: Aufträge tragen `visibility` (`AssignedSectionsOnly` als Standard oder
`WholeDocument`) und `acceptedAt`. Nur bei `WholeDocument` liefert der Dienst die eingefrorene
Dokumentversion zusätzlich aus. Rückmeldungen bleiben kompatibel eingebettet: `feedbackKind`
(`Comment`, `Suggestion`, `Question`), `proposedMarkdown`, `answer`, `decision`, `feedbackAt`
und `decidedAt` ergänzen `feedback` und `resolved`. Vorschläge ersetzen ausschließlich den
eindeutig gefundenen Ausschnitt im aktuellen Abschnitt, mit dessen beim Laden gesehenem ETag.
Bei mehrdeutiger Stelle wird nichts geschrieben. `Applying` plus `applicationText`,
`applicationETag` und `applicationHash` hält einen begonnenen Übernahmevorgang wiederaufnehmbar
fest. Solange er offen ist, kann der Auftrag weder entschieden noch abgenommen werden.
Nach erfolgreicher Übernahme wird die Entscheidung endgültig; Arbeitsdaten werden entfernt.
Nach Abnahme, Widerruf oder Ablauf ist jeder Empfängerzugriff gesperrt, auch bestehende Cookies.
Freigabe erfordert mindestens einen abgenommenen Auftrag und keine offenen Aufträge oder
Sammlungen. Änderungen an freigegebenen Dokumenten erfordern vorher explizites Wiederöffnen.

Die Schreibansicht bearbeitet die vorhandenen Abschnittstexte direkt; jeder Text wird mit seinem
eigenen ETag gespeichert. Markierungen teilen den Text nicht mehr automatisch auf. Stattdessen
enthält ein Reviewauftrag `passages`: ausgewählte Markdownausschnitte mit eigener Kennung,
`sectionId` und `sourceHash` des zugehörigen Abschnittstextes. Das sind Werte im Auftrag, keine
zusätzlichen Aggregate. So bleiben Absatzfluss, Abschnittskennungen und vorhandene Rückmeldungen
beim Sammeln unverändert.

Ein Auftrag beginnt als `Draft` und dient als dauerhaft gespeicherte Sammlung. Beim Erteilen werden
die Quelltexte gegen ihre Hashes geprüft und die Dokumentversion eingefroren. Veränderte Quellen
müssen erneut markiert werden. Standardmäßig erhält der Empfänger nur die gespeicherten Ausschnitte;
mit `WholeDocument` zusätzlich die ganze eingefrorene Fassung, niemals den aktuellen Quelltext.
Name, optionale Mailadresse und Frist werden beim Erteilen ergänzt. Der Link wird selbst geteilt;
es wird keine Mail verschickt. Das Zugangstoken steht im URL-Fragment, wird per POST eingelöst
und durch eine geschützte, auf genau diesen Auftrag begrenzte Sitzung ersetzt. Gespeichert wird
weiterhin nur sein Hash; Abnahme, Widerruf und Ablauf werden bei jedem Zugriff geprüft.

Für diesen ersten vollständigen Ablauf liegen die Kommentare als `feedback` direkt am jeweiligen
Ausschnitt im Auftrag, zusammen mit `resolved`. Genau eine Person gibt den Auftrag gesammelt
zurück; danach entscheidet der Eigentümer die Rückmeldungen. Das gemeinsame ETag schützt auch
diese Übergabe. Separate Feedbackdateien bleiben eine spätere Erweiterung; die zusätzlichen
Rückmeldungsarten verwenden die oben dokumentierten Felder im selben Auftrag.
Die Zustände sind `Draft`, `Sent`, `Returned`, `Accepted`, `Revoked`. Abnahme ist erst möglich,
wenn alle vorhandenen Kommentare entschieden sind. Die Pipeline wird aus diesen Aufträgen
berechnet; sie speichert keine zusätzliche Zustandskopie.

Erwartete Ausgänge sind Ergebniswerte, keine Ausnahmen: ein fehlender Eintrag und eine nicht
erfüllte Bedingung sind normale Antworten, die die Oberfläche in eine Meldung übersetzt. Alles, was
niemand einplanen kann, also eine abgerissene Verbindung, ein fehlendes Recht, eine kaputte
Konfiguration, kommt als `ObjectStoreException` aus `ReviewMyDoc.Core.Storage`. Keine
Implementierung lässt dabei einen Typ ihrer Ablage nach außen; die ursprüngliche Ausnahme hängt
innen dran, damit das Log sie behält.

Dieselbe Schnittstelle, zwei Implementierungen: in Azure `Azure.Storage.Blobs` mit Managed Identity,
lokal ein Verzeichnis unter `App_Data/`, das dieselben Pfade und dieselbe ETag-Semantik nachbildet
(ETag als Hash des Inhalts). Tests laufen gegen die lokale Implementierung; ein zusätzlicher Test
gegen echtes Blob Storage läuft nur mit gesetzter Verbindung.
