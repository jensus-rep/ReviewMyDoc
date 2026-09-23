# Konzept

Stand 21.09.2026, Version 1. Was ReviewMyDoc ist, für wen, und welche Abläufe es abdeckt.
Die Regeln für Code und Struktur stehen in [Konventionen.md](Konventionen.md), die Ablage der
Daten in [Datenmodell.md](Datenmodell.md).

## Wofür

Eine Person schreibt längere Fachdokumente und lässt einzelne Teile davon von mehreren Leuten
prüfen. Heute geht das per Mail und Dateianhang: der Stand läuft auseinander, niemand weiß, welcher
Abschnitt bei wem liegt, und Rückmeldungen kommen als Prosa zurück, die erst wieder zugeordnet
werden muss.

ReviewMyDoc deckt drei Dinge ab:

1. **Schreiben und gliedern.** Ein Dokument entsteht als Folge von Abschnitten, mit Formatierung
   und auf Wunsch mit Unterstützung einer AI.
2. **Gezielt zum Review geben.** Ein Reviewauftrag nennt eine Person, eine Menge von Abschnitten
   und eine Frist. Er legt fest, ob die Person das ganze Dokument lesen darf oder nur ihren Teil.
3. **Den Stand sehen.** Eine Übersicht beantwortet jederzeit: was ist draußen, was ist zurück, was
   liegt bei mir zur Abnahme, wo steht eine Rückfrage offen.

Nicht Ziel: ein zweites Word, eine allgemeine Dokumentenverwaltung, gleichzeitiges Tippen mehrerer
Leute im selben Absatz, ein Freigabeworkflow mit Rollenmatrix.

## Die vier Begriffe

Das ganze Fachmodell kommt mit vier Begriffen aus. Wer einen fünften einführen will, begründet ihn.

**Dokument.** Titel, Eigentümer, Zustand (Entwurf, im Review, freigegeben), eine geordnete Liste von
Abschnitten und eine laufende Versionsnummer. Das Dokument selbst trägt keinen Text.

**Abschnitt.** Die Einheit, auf die sich alles bezieht: Zuweisung, Kommentar, Abnahme. Er hat eine
stabile Kennung, eine Überschrift und seinen Text als Markdown. Die Kennung bleibt, wenn Abschnitte
umsortiert oder umbenannt werden, damit Rückmeldungen nicht verrutschen. Sichtbar ist der Abschnitt
beim Schreiben kaum: Der Eigentümer sieht einen durchgehenden Text, und die Abschnittsgrenzen sind
ruhige Marken darin. Er entsteht meist dadurch, dass eine markierte Stelle zum Abschnitt gemacht
wird, siehe Dokument anlegen und ausarbeiten.

**Reviewauftrag.** Eine Person (Name und Mailadresse), eine Menge von Abschnittskennungen, eine
Frist, eine Sichtbarkeitsstufe und ein Zustand. Er zeigt immer auf eine feste Dokumentversion, nie
auf „den aktuellen Stand“; siehe Eingefrorener Stand.

**Rückmeldung.** Gehört zu genau einem Abschnitt und zu genau einem Reviewauftrag. Sie ist von einer
von drei Arten: Kommentar, Änderungsvorschlag (mit vorgeschlagenem Text) oder Rückfrage. Sie hat
einen eigenen Zustand: offen, übernommen, abgelehnt, beantwortet.

Die Pipeline ist kein fünfter Begriff, sondern eine Sicht auf diese vier. Sie wird berechnet, nicht
gespeichert, und kann deshalb nicht falsch werden.

## Abläufe

### Dokument anlegen und ausarbeiten

Der Eigentümer legt ein Dokument an und schreibt darin, durchgehend, auf einer einzigen Fläche. Er
gliedert nicht zuerst und füllt dann Kästen: Er schreibt, und die Gliederung entsteht aus dem Text.

**Der Text wird direkt formatiert bearbeitet.** Eine Überschrift sieht aus wie eine Überschrift,
Fettes ist fett, eine Liste ist eingerückt. Markdown bleibt das Speicherformat; seine Zeichen
werden beim Schreiben nicht angezeigt. Es gibt
keine zweite Spalte mit einer Vorschau und keine dauernd sichtbare Werkzeugleiste: Werkzeuge
erscheinen an der Markierung und verschwinden mit ihr. Die Formatierung ist bewusst begrenzt auf
Überschrift, Fett, Kursiv, Listen, Tabelle, Zitat, Link und Code. Was nicht in dieser Liste steht,
gehört nicht in ein Dokument, das von mehreren Leuten geprüft wird.

**Aus der Markierung wird eine Reviewpassage.** Wer eine Stelle markiert, sieht die Werkzeuge und
die Aktionen „Sammeln“ und „Zum Review“. Sammeln legt einen Ausschnitt dauerhaft in einem
Auftragsentwurf ab. „Zum Review“ sammelt ebenfalls und öffnet direkt die Zuweisung. Mehrere
Passagen können gemeinsam einer Person zugewiesen werden. Die Gliederung und der Textfluss
bleiben dabei unverändert. Der Auftrag hält Ausschnitt, Quellabschnitt und Quellstand fest.
Vor dem Erteilen wird geprüft, dass der Quelltext noch passt. Die Gliederungsseite bleibt als
Nebenansicht für Reihenfolge, Umbenennen und Löschen bestehen.

**Ohne JavaScript bleibt das Dokument benutzbar.** Die mitlaufende Formatierung und das Menü an der
Markierung sind Zugaben. Ohne sie zeigt die Seite den Text formatiert an, und jeder Abschnitt
bekommt ein gewöhnliches Formular zum Bearbeiten. Was nicht geht, ist das Teilen an einer
Markierung; dafür gibt es den Weg über die Gliederungsseite.

Die AI arbeitet immer auf genau einem Abschnitt und immer auf Zuruf, nie von selbst: entwerfen,
umformulieren, kürzen, Gliederung vorschlagen, eingegangene Rückmeldungen zu einer Fassung
zusammenführen. Ihr Vorschlag erscheint neben dem Text und wird erst durch Übernehmen zum Inhalt.
Ein Dokument ändert sich nie, ohne dass ein Mensch es ausgelöst hat.

### Review beauftragen

Der Eigentümer wählt Abschnitte, eine Person und eine Frist. Beim Versenden passiert dreierlei:

1. Der aktuelle Stand des Dokuments wird als Version eingefroren.
2. Ein Zugangstoken wird erzeugt, gehasht gespeichert und im Link versendet.
3. Der Auftrag geht in den Zustand versendet.

Die Anwendung zeigt einen persönlichen Link, den der Eigentümer selbst weitergibt. Der Link ist
der Zugang; ein Konto braucht die Person nicht. Es wird keine Mail automatisch verschickt.

### Reviewer arbeitet

Über den Link landet die Person direkt bei ihrem ersten zugewiesenen Abschnitt. Sie sieht, je nach
Sichtbarkeitsstufe:

- **Ganzes Dokument, geführt.** Sie liest alles zum Verständnis, aber die Navigation führt sie zu
  ihren Abschnitten, und nur dort kann sie schreiben.
- **Nur die eigenen Abschnitte.** Die übrigen Abschnitte werden nicht ausgeliefert, nicht bloß
  ausgeblendet. Was der Browser nie bekommt, kann auch niemand aufklappen.

Sie hinterlässt je Abschnitt Kommentare, Änderungsvorschläge oder Rückfragen und meldet am Ende
ihren Teil zurück. Der Auftrag geht auf zurück.

### Rücklauf und Abnahme

Der Eigentümer sieht jede Rückmeldung am Abschnitt, bei Änderungsvorschlägen als Gegenüberstellung
alt gegen neu. Er übernimmt, lehnt ab oder beantwortet eine Rückfrage. Eine beantwortete Rückfrage
geht an den Reviewer zurück, ohne dass ein neuer Auftrag nötig ist. Sind alle Rückmeldungen eines
Auftrags entschieden, nimmt er den Auftrag ab.

Sind alle Aufträge eines Dokuments abgenommen, kann das Dokument freigegeben werden.

### Die Übersicht

Eine Seite, vier Spalten, über alle Dokumente hinweg:

| Spalte | Was darin steht |
| --- | --- |
| Draußen | versendete Aufträge, mit Frist und Restzeit; überfällige zuerst |
| Zurück | Aufträge mit Rückmeldungen, die noch niemand entschieden hat |
| Bei dir | Rückfragen an den Eigentümer und Aufträge, die zur Abnahme bereit sind |
| Erledigt | abgenommene Aufträge der letzten Zeit |

Das ist die Startseite nach der Anmeldung, nicht die Dokumentliste. Der Eigentümer will zuerst
wissen, was auf ihn wartet.

## Zwei Festlegungen, die tragen

**Eingefrorener Stand.** Ein Reviewauftrag zeigt auf die Dokumentversion, die beim Versenden galt.
Sonst ändert sich der Text unter dem Reviewer, während er ihn prüft, und seine Rückmeldung bezieht
sich auf etwas, das es nicht mehr gibt. Weicht die aktuelle Fassung eines Abschnitts von der
begutachteten ab, sagt die Oberfläche das auf beiden Seiten.

**Sichtbarkeit wird serverseitig durchgesetzt.** Die Sichtbarkeitsstufe entscheidet, was der Server
in die Antwort schreibt. Kein Abschnitt verlässt den Server, den der Empfänger nicht sehen darf.

## Offene Entscheidungen

Diese zwei sind noch nicht entschieden und deshalb in keiner Task vorausgesetzt:

1. **Ausgabeformat.** Markdown und HTML fallen ohnehin an, PDF über den Druckdialog des Browsers
   ebenfalls. Ob es zusätzlich einen echten .docx-Export braucht, hängt daran, was am Ende mit dem
   Dokument passiert.
2. **Mehrere Eigentümer.** Der erste Wurf hat genau einen. Das Datenmodell trägt trotzdem von Anfang
   an eine Eigentümerkennung, damit ein späterer zweiter Eigentümer keine Migration auslöst.

Entschieden am 22.09.2026 und deshalb keine offene Frage mehr: **Der Reviewlink geht nicht per
Mail hinaus.** Die Anwendung zeigt ihn nach dem Erteilen des Auftrags, und der Eigentümer verschickt
ihn selbst. Damit ist die Anwendung ohne eingerichtetes Postfach vollständig benutzbar, es gibt
keine Zustellprobleme zu deuten und keinen weiteren Betriebsschlüssel für den, der sie später
übernimmt. SMTP bleibt eine spätere Möglichkeit und darf den Entwurf nicht prägen.

## Nicht im ersten Wurf

Gleichzeitiges Bearbeiten durch mehrere Personen, Kommentare auf Wortebene statt auf
Abschnittsebene, Benachrichtigungen außer der Einladungsmail, Vorlagen und Textbausteine,
Volltextsuche über alle Dokumente, mobile Bearbeitung (Lesen und Kommentieren auf dem Telefon
dagegen schon).
