Herkunft: Atelier components/skeleton, Stand 22.09.2026, Commit abe762535585569f69b7aa81e594c46b09be9681

# Skeleton

Version 0.1.1. Graue Blöcke in der Form des Inhalts, der gleich kommt. Die Seite springt nicht,
wenn die Daten da sind. Nur CSS. Farbe, Radius und die Abstände auf dem 4-px-Raster kommen aus
[tokens](../tokens/).

Änderung 0.1.1 (16.09.2026): Die Rohwerte aus der Zeit vor den Design-Tokens sind ersetzt. Die
Füllfarbe `#ececee` ist `var(--hairline)`, die Seitenfläche `#ffffff` ist `var(--bg)`, der Glanz
`rgba(255, 255, 255, 0.65)` ist aus `--bg` gemischt, der Radius `4px` ist die Hälfte von
`--radius-s`, der Rahmen ist `--hairline-width`, der Schatten der Seitenvariante ist aus `--ink`
gemischt. Alle bisher fest in den Regeln stehenden Maße stehen jetzt als Baustein-Variablen in
`:where(.skeleton)`. Damit stimmt der dunkle Modus, der vorher hellgraue Blöcke mit weißem Glanz
auf einer weißen Seite zeigte.

```html
<link rel="stylesheet" href="/components/tokens/tokens.css">
<link rel="stylesheet" href="/components/skeleton/skeleton.css">

<div class="skeleton" aria-hidden="true">
  <span class="skeleton__block skeleton__block--title"></span>
  <span class="skeleton__space"></span>
  <div class="skeleton__row"><span class="skeleton__block"></span><span class="skeleton__block"></span></div>
  <div class="skeleton__row"><span class="skeleton__block skeleton__block--mid"></span><span class="skeleton__block"></span></div>
</div>
```

Das Skelett ist Dekoration und bekommt `aria-hidden="true"`. Den Ladezustand meldet der
umgebende Bereich, zum Beispiel mit `aria-busy="true"` (siehe Komponente `busy-region`).

| Klasse                         | Wirkung                                            |
| ------------------------------ | -------------------------------------------------- |
| `skeleton__block`              | Textzeile, 10 px hoch, volle Breite                |
| `skeleton__block--title`       | Titel, 18 px hoch, 45 %                            |
| `skeleton__block--figure`      | große Zahl, 30 px hoch                             |
| `skeleton__block--short` / `--mid` | 40 % / 70 % Breite                             |
| `skeleton__block--circle`      | Kreis, `--space-6`                                 |
| `skeleton__row`                | Listenzeile: Beschriftung links, Wert rechts       |
| `skeleton__space`              | Abstand von `--space-2`                            |
| `skeleton--pulse`              | Pulsieren statt Glanz, versetzt mit `style="--i: 2"` |
| `skeleton--page`               | Dokumentseite mit Textzeilen, für den Dokumentbereich |

## CSS-Variablen

Standardwerte in `:where(.skeleton)`, jede Seite kann sie ohne Spezifitätskampf überschreiben.

| Variable                        | Standard                                       | Wofür                      |
| ------------------------------- | ---------------------------------------------- | -------------------------- |
| `--skeleton-fill`               | `var(--hairline)`                              | Fläche eines Blocks        |
| `--skeleton-shine`              | `color-mix(in srgb, var(--bg) 65%, transparent)` | Glanz, der darüberläuft  |
| `--skeleton-radius`             | `calc(var(--radius-s) / 2)`                    | Radius eines Blocks        |
| `--skeleton-gap`                | `10px`                                         | Abstand zwischen Blöcken   |
| `--skeleton-block-height`       | `10px`                                         | Höhe einer Textzeile       |
| `--skeleton-title-height`       | `18px`                                         | Höhe eines Titels          |
| `--skeleton-figure-height`      | `30px`                                         | Höhe einer großen Zahl     |
| `--skeleton-figure-width`       | `36px`                                         | Breite einer großen Zahl   |
| `--skeleton-circle-size`        | `var(--space-6)`                               | Durchmesser des Kreises    |
| `--skeleton-row-gap`            | `var(--space-4)`                               | Abstand in einer Zeile     |
| `--skeleton-row-padding-block`  | `var(--space-2)`                               | Höhe einer Zeile           |
| `--skeleton-row-value-width`    | `var(--space-9)`                               | Breite des Werts rechts    |
| `--skeleton-space-height`       | `var(--space-2)`                               | Höhe von `skeleton__space` |
| `--skeleton-sheen-speed`        | `1.8s`                                         | ein Durchlauf des Glanzes  |
| `--skeleton-pulse-speed`        | `1.6s`                                         | ein Takt des Pulsierens    |
| `--skeleton-pulse-step`         | `90ms`                                         | Versatz je `--i`           |
| `--skeleton-pulse-opacity`      | `0.45`                                         | Deckkraft im Pulsieren     |
| `--skeleton-page`               | `var(--bg)`                                    | Fläche der Seitenvariante  |
| `--skeleton-page-gap`           | `9px`                                          | Zeilenabstand im Modell    |
| `--skeleton-page-padding-block` | `36px`                                         | Rand oben und unten        |
| `--skeleton-page-padding-inline`| `38px`                                         | Rand links und rechts      |
| `--skeleton-page-block-height`  | `6px`                                          | Höhe einer Zeile im Modell |
| `--skeleton-page-title-height`  | `9px`                                          | Höhe des Titels im Modell  |
| `--skeleton-page-shadow`        | aus `--hairline-width` und `--ink` gemischt    | Schatten der Seite         |

Die prozentualen Breiten der Modifikatoren (45 %, 40 %, 70 %, 50 %) stehen weiter in den Regeln:
sie machen aus, was der Modifikator ist, und sind kein einstellbarer Wert.

## Hell und dunkel

Der Baustein hat keine eigene Regel für den dunklen Modus: er liest `--hairline`, `--bg` und
`--ink`, und die Tokens setzen dort die Werte des Modus. Zwei Stellen sind dabei gemischt statt
gesetzt, damit sie in beiden Modi in die richtige Richtung zeigen:

- Der Glanz ist die Grundfarbe mit 65 % Deckkraft. Im hellen Modus ist das der bisherige weiße
  Streifen, im dunklen Modus ein dunkler Streifen. Vorher lief ein weißer Streifen mit 65 % über
  einen dunklen Block, was auf schwarzem Grund aufblitzte.
- Der Schatten der Seitenvariante ist die Textfarbe mit 4 % Deckkraft. Im hellen Modus ist das der
  bisherige schwarze Schatten, im dunklen Modus ein sehr schwacher heller Saum, weil ein schwarzer
  Schatten auf schwarzem Grund nichts zeigt.

## Begründete Ausnahmen von den Tokens

- `--skeleton-gap` (10 px), `--skeleton-block-height` (10 px), `--skeleton-title-height` (18 px),
  `--skeleton-figure-height` (30 px) und `--skeleton-figure-width` (36 px) sind Rohwerte. Sie sind
  die Eigengeometrie der Blöcke und liegen aus der Zeit vor den Tokens neben dem 4-px-Raster. Auf
  das Raster gezogen würde sich das Bild ändern; das ist eine Entscheidung der Designsprache und
  nicht die dieses Bausteins. Der Punkt ist gemeldet. Die Werte stehen als Baustein-Variablen, eine
  Seite kann sie also überschreiben.
- Die Maße der Seitenvariante (`--skeleton-page-gap` 9 px, Ränder 36 px und 38 px, Zeilenhöhe 6 px,
  Titelhöhe 9 px) sind Rohwerte. Die Variante ist ein Maßstabsmodell einer Dokumentseite; ihre Maße
  sind bewusst kleiner als jede Stufe des Rasters. Der Radius dieser Zeilen ist die halbe
  Zeilenhöhe, damit sie wie vorher rund enden.
- `--skeleton-sheen-speed`, `--skeleton-pulse-speed` und `--skeleton-pulse-step` sind Rohwerte. Die
  Tokens tragen die Dauer von Zustandswechseln, nicht die Geschwindigkeit einer Schleife, die
  weiterläuft. `--skeleton-pulse-opacity` ist ein Rohwert, weil die Tokens keine Deckkraftstufen
  kennen.

Bei „Bewegung reduzieren“ stehen die Blöcke still, ohne Glanz und ohne Pulsieren.

Demo: `demo.html` im Browser öffnen, mit Umschalter für System, Hell und Dunkel.
