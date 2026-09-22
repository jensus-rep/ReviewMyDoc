Herkunft: Atelier components/rows, Stand 22.09.2026, Commit abe762535585569f69b7aa81e594c46b09be9681

# Rows

Version 0.1.0. Eine Liste gleichartiger Einträge, getrennt durch Haarlinien statt durch Karten oder
Kacheln, wie Regel 6 der Designsprache es verlangt. Jeder Eintrag trägt eine optionale Nummer, einen
Titel, einen optionalen Text und eine optionale Aktion rechts. Nur CSS, keine Abhängigkeit, kein
Framework. Alle Werte kommen aus [tokens](../tokens/), Grundlage ist
[docs/Design/designsprache.md](../../docs/Design/designsprache.md).

## Einbinden

```html
<link rel="stylesheet" href="/components/tokens/tokens.css">
<link rel="stylesheet" href="/components/rows/rows.css">

<ol class="rows">
  <li class="rows__item">
    <span class="rows__number">01</span>
    <h3 class="rows__title">Prozess- und Systemanalyse</h3>
    <p class="rows__text">Sichtbar machen, wie die Arbeit heute wirklich läuft.</p>
    <a class="rows__action" href="/leistungen">Ansehen</a>
  </li>
</ol>
```

`ol` für Schritte mit Nummer, `ul` für Einträge ohne Reihenfolge. Der Titel darf jedes Element sein,
das in die Gliederung der Seite passt (`h3`, `span`, `div`); der Baustein setzt nur Größe, Gewicht
und Farbe.

## Klassen

| Klasse          | Wofür                                                                     |
| --------------- | ------------------------------------------------------------------------- |
| `rows`          | die Liste: ohne Aufzählungszeichen, Haarlinie unter dem letzten Eintrag   |
| `rows__item`    | ein Eintrag: Haarlinie darüber, Raster aus Nummer, Inhalt und Aktion      |
| `rows__number`  | optionale Nummer, rechtsbündig, Sekundärgrau, `tabular-nums`              |
| `rows__title`   | Titel des Eintrags, Textfarbe im Titelgewicht                             |
| `rows__text`    | optionaler Text darunter, Sekundärgrau, höchstens `--measure-text` breit  |
| `rows__action`  | optionale Aktion; die Klasse sitzt auf dem `a` oder `button` selbst       |

Jeder Teil außer dem Titel kann fehlen. Die Abstände sitzen an den Teilen selbst, nicht als `gap` am
Raster, damit ein fehlender Teil keine leere Lücke hinterlässt.

`rows__action` gehört auf das Bedienelement selbst, nicht auf eine Hülle darum. So kann der Baustein
seinen Fokusrahmen setzen, ohne fremde Kindelemente zu gestalten, und so entsteht keine Abhängigkeit
zum Baustein button. Wer dort eine Schaltfläche mit Fläche braucht, nimmt statt `rows__action` den
Baustein button; die Seite kombiniert beides, die Bausteine kennen einander nicht.

## Responsiv

Gemessen wird die Liste selbst, nicht das Fenster: `.rows` ist über `container-type: inline-size`
ihr eigener Container, die Umschaltung steht in `@container`. Das ist der Unterschied, der zählt,
denn dieselbe Liste steht einmal in der ganzen Textspalte und einmal in einer schmalen Spalte daneben.

Ist die Liste schmaler als 26 em (rund 416 Pixel bei Standardschriftgröße), steht die Aktion unter
dem Text, darüber rechts neben dem Inhalt und senkrecht mittig. Die Nummer behält ihre Spalte in
beiden Fällen. Der Umbruchpunkt steht in `em`, nicht in Pixeln, damit er mit der Schriftgröße des
Lesers mitwandert. Ein Browser ohne Container-Queries bleibt überall beim gestapelten Layout, also
bei der unschädlichen Hälfte. Geprüft ab 360 Pixel Fensterbreite.

## CSS-Variablen

Standardwerte in `:where(.rows)`, jede Seite kann sie ohne Spezifitätskampf überschreiben.

| Variable                | Standard                          |
| ----------------------- | --------------------------------- |
| `--rows-hairline-color` | `var(--hairline)`                 |
| `--rows-padding-block`  | `var(--space-4)`                  |
| `--rows-number-color`   | `var(--ink-2)`                    |
| `--rows-number-size`    | `var(--font-size-label-web)`      |
| `--rows-number-gap`     | `var(--space-4)`                  |
| `--rows-title-color`    | `var(--ink)`                      |
| `--rows-title-size`     | `var(--font-size-text-web)`       |
| `--rows-text-color`     | `var(--ink-2)`                    |
| `--rows-text-size`      | `var(--font-size-text-web)`       |
| `--rows-text-gap`       | `var(--space-1)`                  |
| `--rows-action-color`   | `var(--blue)`                     |
| `--rows-action-size`    | `var(--font-size-label-web)`      |
| `--rows-action-gap`     | `var(--space-5)`                  |
| `--rows-focus-color`    | `var(--blue)`                     |
| `--rows-focus-width`    | `calc(var(--hairline-width) * 2)` |
| `--rows-focus-offset`   | `var(--space-1)`                  |

## Hell und dunkel, Bewegung

Keine eigene Regel für den dunklen Modus: der Baustein liest `--ink`, `--ink-2`, `--hairline` und
`--blue`, die Tokens setzen dort die Werte des Modus. Der Baustein bewegt nichts, also gibt es auch
nichts, was „Bewegung reduzieren“ abschalten müsste.

## Barrierefreiheit

- Die Liste ist eine echte Liste (`ul` oder `ol`), damit ein Screenreader die Anzahl der Einträge
  nennt. Bei einer Liste mit Nummern übernimmt `ol` die Reihenfolge, die Nummer im Text bleibt die
  sichtbare Form.
- Anklickbar ist nur die Aktion, nicht die ganze Zeile. Damit folgt die Reihenfolge der Tastatur der
  des Textes, und es entsteht kein Link ohne erkennbaren Namen.
- Die Aktion ist unterstrichen. Blau erreicht im hellen Modus nur AA für große Schrift (4,01:1), die
  Unterstreichung ist deshalb das zweite Merkmal, wie es
  [tokens/README.md](../tokens/README.md) bis zur Entscheidung von Jens verlangt.
- Fokus sichtbar über `:focus-visible` in Akzentblau, in beiden Modi.

## Demo

`demo.html` lokal im Browser öffnen. Sie zeigt eine Liste mit Nummer, Text und Aktion, eine mit
fehlenden Teilen, die schmale Fassung auf Sekundärgrund und hat einen Umschalter für System, Hell
und Dunkel. Unter `/components/` liefert die Anwendung keine `.html`-Dateien aus, die Demo wird also
aus dem Repository geöffnet, nicht über die Anwendung.
