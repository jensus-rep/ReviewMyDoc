Herkunft: Atelier components/markdown-surface, Stand 23.09.2026, Commit cb6eeff4cd2565e2231afc1369c7c7457919ac16

# Markdown surface 1.0.0

Eine Schreibfläche für Markdown: ein Textfeld, dahinter eine zweite Schicht, die denselben Text
ausgezeichnet zeigt — zeichengenau deckungsgleich. Werkzeuge gibt es nur an der Markierung; ohne
Markierung ist keine Leiste zu sehen. Die Markdown-Zeichen bleiben sichtbar, nur zurückgenommen.

Der Baustein kennt keine Anwendung und keinen Server. Er bekommt Text, meldet Text zurück und
meldet, wenn jemand den zusätzlichen Eintrag des Menüs gewählt hat, mit Anfang und Ende der
Markierung.

Er ist der Nachfolger von [markdown-editor](../markdown-editor/README.md). Dessen Rechenkern — die
acht Formatierungen, die Zeilenregeln, die kleinste Ersetzung — steckt hier als Kopie, weil ein
Baustein in diesem Ordner nicht von einem anderen abhängt.

## Warum nichts größer wird

Die Schicht ist deckungsgleich, solange jedes Zeichen seine Laufweite behält. Das ist keine
Feinheit, sondern die ganze Konstruktion: Der Cursor, die Markierung und die Zeilenumbrüche kommen
vom Textfeld, die Farbe von der Schicht dahinter. Verschiebt sich ein Zeichen um einen halben Punkt,
steht der Cursor an der falschen Stelle.

Ein Textfeld trägt genau eine Schrift für seinen ganzen Inhalt. Eine Überschrift, die wirklich
größer würde, eine Liste, die wirklich einrückte, Code in einer wirklich festen Schrift — jedes
davon verschöbe die Schicht gegen das Feld. Es gäbe nur zwei Auswege, und beide sind hier
ausgeschlossen: `contenteditable`, womit Eingabe, Einfügen aus Word, Rückgängig und Tastaturbedienung
nachgebaut werden müssten, oder ein Feld je Absatz, womit Rückgängig, „Alles markieren“ und das
Einfügen eines langen Textes am Absatzrand zerbrächen.

Deshalb zeichnet dieser Baustein nur aus, was ein Zeichen an seinem Platz lässt: Farbe, Deckkraft,
Hintergrund. Fettes bekommt sein Gewicht über einen Schatten links und rechts des Zeichens, der die
Laufweite nicht anrührt. Was bleibt, ist eine Fläche, die wie ausgezeichneter Text aussieht, ohne
eine Vorschau zu sein — eine Überschrift ist an ihrem kräftigen Ton und ihren blassen Rauten zu
erkennen, nicht an ihrer Größe.

Wer das gerenderte Dokument sehen will, sieht es an der Stelle, an der es zählt: beim Server, in
derselben Umsetzung, die auch der spätere Leser bekommt.

## Dateien

| Datei | Inhalt |
| --- | --- |
| `markdown-surface.ts` | Rechenlogik, Auszeichnung und Anbindung an das Feld, Quelle von `markdown-surface.js` |
| `markdown-surface.css` | Feld, Schicht und Menü |
| `markdown-surface.test.ts` | Tests der Rechenlogik mit `node --test`, ohne Browser |
| `demo.html` | die Demo, direkt im Browser zu öffnen |

Die `.js`-Datei erzeugt `npm run build`, sie ist eingecheckt. Quelle ist immer die `.ts`-Datei.

## Einbinden

```html
<link rel="stylesheet" href="/components/tokens/tokens.css">
<link rel="stylesheet" href="/components/markdown-surface/markdown-surface.css">
<script type="module" src="/components/markdown-surface/markdown-surface.js"></script>
```

Das Markup steht in `demo.html`. Verbindlich sind diese Datenattribute:

| Attribut | Wo |
| --- | --- |
| `data-markdown-surface` | am Wurzelelement |
| `data-markdown-surface-input` | am `textarea` |
| `data-markdown-surface-layer` | an der Schicht dahinter, im Markup leer und `aria-hidden` |
| `data-markdown-surface-menu` | am Menü, das im Markup `hidden` ist |
| `data-markdown-format` | an einer Schaltfläche des Menüs, Wert eine der acht Formatierungen |
| `data-markdown-surface-action` | an einem weiteren Eintrag, Wert bestimmt die Seite |

Instanzen im Markup starten von selbst. Wer den Griff braucht, ruft `initMarkdownSurface(element)`;
ein zweiter Aufruf auf demselben Element liefert denselben Griff.

```js
import { ACTION_EVENT, CHANGE_EVENT, initMarkdownSurface } from '/components/markdown-surface/markdown-surface.js';

const flaeche = initMarkdownSurface(document.querySelector('[data-markdown-surface]'));

element.addEventListener(CHANGE_EVENT, (event) => speichern(event.detail.text));
element.addEventListener(ACTION_EVENT, (event) => {
  const { action, text, selectionStart, selectionEnd } = event.detail;
  // action ist der Wert von data-markdown-surface-action, etwa 'review'.
});
```

Der Griff kann: `getText()`, `setText(text)`, `getSelection()`, `format(name)`, `refresh()` und
`destroy()`. `CHANGE_EVENT` meldet jede Änderung sofort; wann gespeichert wird, entscheidet die
Seite, nicht der Baustein.

## Verhalten

- **Das Menü erscheint an der Markierung** und verschwindet mit ihr. Es steht mittig über der
  Markierung und rutscht darunter, wenn darüber kein Platz ist; es bleibt immer innerhalb der
  Fläche. Eine Markierung über mehrere Zeilen wird nach ihrer ersten Zeile bemessen, damit das Menü
  dort steht, wo die Markierung anfing.
- **Der Druck im Menü nimmt dem Feld nicht den Fokus.** Ein Textfeld ohne Fokus verliert seine
  Markierung, und die Formatierung hätte nichts mehr, worauf sie wirkt.
- **Die Markierung bleibt die des Browsers**, nur durchscheinend, damit der ausgezeichnete Text
  hindurchkommt. Die Schicht markiert dieselbe Stelle noch einmal, zeichnet dort aber nichts: dieses
  Stück ist nur zum Messen da und sagt dem Menü, wo es hingehört.
- **Die acht Formatierungen** verhalten sich wie im Vorgänger: Sie schalten um, lassen Leerzeichen
  am Rand der Auswahl außen, nehmen bei zeilenweisen Formatierungen jede berührte Zeile mit und
  ersetzen immer nur das Stück, das sich wirklich ändert, damit Strg+Z Schritt für Schritt
  zurückkommt. Die Einzelheiten stehen in der [README des Vorgängers](../markdown-editor/README.md).
- **Ein offen gebliebener Marker ist keine Auszeichnung.** `**angefangen` bleibt gewöhnlicher Text,
  genau so, wie Markdown es liest.
- **Im eingezäunten Codeblock wird nichts ausgezeichnet.** Ein `**` darin bleibt, was es ist.
- **Tastatur.** Strg/Cmd+B, Strg/Cmd+I und Strg/Cmd+K für Fett, Kursiv und Link. Der Zeilenumbruch
  führt eine Liste fort. Esc schließt das Menü. Das Menü ist eine Station in der Tabulatorfolge;
  innerhalb wechseln die Pfeiltasten, Pos1 und Ende.

## Ohne JavaScript

Die Schicht ist im Markup leer und das Menü `hidden`. Sichtbar wird die Schicht erst, wenn das
Skript `is-ready` auf das Wurzelelement setzt; bis dahin trägt das Feld seine eigene Tinte. Ohne
JavaScript bleibt also das Textfeld, und es ist voll benutzbar: ein `textarea` in einem Formular, das
der Server entgegennimmt. Nur das Auszeichnen und das Menü fehlen.

## Variablen

`--markdown-surface-height`, `--markdown-surface-padding`, `--markdown-surface-radius`,
`--markdown-surface-font`, `--markdown-surface-font-size`, `--markdown-surface-line-height`,
`--markdown-surface-marker-opacity` (wie blass die Markdown-Zeichen stehen),
`--markdown-surface-weight` (wie stark der Schatten Fettes vortäuscht). Alles andere kommt aus den
Tokens.

`--markdown-surface-menu-x` und `--markdown-surface-menu-y` setzt der Baustein selbst; sie sind die
Stelle, an der das Menü steht, und keine Einstellung.

Schrift, Größe, Zeilenhöhe und Innenabstand werden an **einer** Stelle gesetzt und von Feld und
Schicht gemeinsam gelesen. Wer eines davon ändert, ändert beide; wer es an Feld oder Schicht einzeln
überschreibt, bricht die Deckungsgleichheit.

## Prüfen

```
node --test --experimental-strip-types components/markdown-surface/markdown-surface.test.ts
```

Geprüft wird ohne Browser. Die erste Hälfte ist der mitkopierte Test des Rechenkerns, damit die
Kopie hier bewiesen bleibt. Die zweite prüft, was dieser Baustein hinzufügt: dass die ausgezeichneten
Stücke lückenlos aneinanderstoßen und zusammengesetzt wieder genau den Eingabetext ergeben — das ist
die Bedingung, auf der die Deckungsgleichheit ruht —, dass jede Auszeichnung ihre Stücke richtig
schneidet, dass ein offener Marker keine ist, dass die Markierung die Stücke teilt, die sie berührt,
und wo das Menü landet, auch am Rand, in der ersten Zeile und wenn es breiter ist als die Fläche.
