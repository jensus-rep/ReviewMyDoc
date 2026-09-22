Herkunft: Atelier components/progress-line, Stand 22.09.2026, Commit abe762535585569f69b7aa81e594c46b09be9681

# Progress Line

Version 0.1.1. Eine Linie aus zwei Haarlinien an der Oberkante eines Fensters oder Bereichs. Sie
verdeckt keinen Inhalt. Ohne Fortschrittswert läuft ein Abschnitt durch, mit Wert füllt sie sich.
Nur CSS. Farbe und Höhe kommen aus [tokens](../tokens/).

Änderung 0.1.1 (16.09.2026): Die Rohwerte aus der Zeit vor den Design-Tokens sind ersetzt. Die
Linienfarbe `#0a7aff` ist `var(--blue)`, die Spurfarbe `#ececee` ist `var(--hairline)`, die Höhe
`2px` ist `calc(var(--hairline-width) * 2)` und der Radius `1px` ist `var(--hairline-width)`. Damit
stimmt der dunkle Modus, der vorher eine hellgraue Spur auf schwarzem Grund zeigte.

```html
<link rel="stylesheet" href="/components/tokens/tokens.css">
<link rel="stylesheet" href="/components/progress-line/progress-line.css">

<!-- unbestimmt -->
<span class="progress-line" role="status" aria-label="Wird geladen"></span>

<!-- mit Fortschritt, Wert von 0 bis 1 -->
<span class="progress-line progress-line--determinate" role="progressbar"
      aria-valuemin="0" aria-valuemax="100" aria-valuenow="40"
      style="--progress-line-value: 0.4"></span>
```

| Klasse                         | Wirkung                                        |
| ------------------------------ | ---------------------------------------------- |
| `progress-line--determinate`   | zeigt `--progress-line-value` als Füllstand    |
| `progress-line--window`        | fest am oberen Fensterrand, etwa beim Seitenwechsel |

## CSS-Variablen

Standardwerte in `:where(.progress-line)`, jede Seite kann sie ohne Spezifitätskampf überschreiben.

| Variable                          | Standard                            | Wofür                              |
| --------------------------------- | ----------------------------------- | ---------------------------------- |
| `--progress-line-height`          | `calc(var(--hairline-width) * 2)`   | Höhe der Linie                     |
| `--progress-line-radius`          | `var(--hairline-width)`             | Radius der laufenden Linie         |
| `--progress-line-color`           | `var(--blue)`                       | Farbe der laufenden Linie          |
| `--progress-line-track`           | `var(--hairline)`                   | Farbe der Spur dahinter            |
| `--progress-line-value`           | `0`                                 | Füllstand von 0 bis 1              |
| `--progress-line-section`         | `40%`                               | Breite des laufenden Abschnitts    |
| `--progress-line-speed`           | `1.4s`                              | ein Durchlauf des Abschnitts       |
| `--progress-line-pulse-speed`     | `1.6s`                              | ein Takt des Pulsierens            |
| `--progress-line-pulse-opacity`   | `0.35`                              | Deckkraft im Pulsieren             |
| `--progress-line-slide-ease`      | `cubic-bezier(0.4, 0, 0.2, 1)`      | Verlauf des Durchlaufs             |
| `--progress-line-value-duration`  | `300ms`                             | Dauer, bis der Füllstand folgt     |
| `--progress-line-layer`           | `1000`                              | `z-index` der Fenstervariante      |

## Hell und dunkel

Der Baustein hat keine eigene Regel für den dunklen Modus: er liest `--blue` und `--hairline`, und
die Tokens setzen dort die Werte des Modus.

## Begründete Ausnahmen von den Tokens

- `--progress-line-speed` (1.4s) und `--progress-line-pulse-speed` (1.6s) sind Rohwerte. Die Tokens
  tragen die Dauer von Zustandswechseln (`--motion-duration` 200 ms), nicht die Geschwindigkeit
  einer Schleife, die weiterläuft. Ein Token dafür wurde nicht erfunden.
- `--progress-line-value-duration` (300 ms) ist ein Rohwert, weil zwischen `--motion-duration`
  (200 ms) und `--motion-duration-story` (460 ms) kein Token liegt. Der Bedarf ist gemeldet.
- `--progress-line-slide-ease` ist ein eigener Verlauf, nicht `--motion-ease`. Der Abschnitt
  überquert die ganze Breite und soll an keinem Ende bremsen; `--motion-ease` läuft dagegen aus.
  Der Füllstand der bestimmten Variante nutzt `--motion-ease`, weil er ein Zustandswechsel ist.

Bei „Bewegung reduzieren“ pulsiert die unbestimmte Linie über die volle Breite, statt zu laufen,
und der Füllstand springt ohne Übergang.

Demo: `demo.html` im Browser öffnen, mit Umschalter für System, Hell und Dunkel. Sie zeigt die
unbestimmte Linie auf Grund und auf Sekundärgrund.
