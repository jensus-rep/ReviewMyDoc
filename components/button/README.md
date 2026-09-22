Herkunft: Atelier components/button, Stand 22.09.2026, Commit abe762535585569f69b7aa81e594c46b09be9681

# Button

Version 0.1.0. Die eine interaktive Fläche einer Seite, in zwei Varianten: primär in Akzentblau,
sekundär als Textlink mit Haarlinienrahmen. Dazu der deaktivierte Zustand. Nur CSS, keine
Abhängigkeit, kein Framework. Alle Werte kommen aus
[tokens](../tokens/), Grundlage ist [docs/Design/designsprache.md](../../docs/Design/designsprache.md).

Die Klassen funktionieren auf `<button>` und auf `<a>`. Ein Element darf gleichzeitig die Klassen
dieses Bausteins und die des Bausteins [busy-button](../busy-button/) tragen; die beiden kennen
einander nicht.

## Einbinden

```html
<link rel="stylesheet" href="/components/tokens/tokens.css">
<link rel="stylesheet" href="/components/button/button.css">

<button class="button button--primary" type="submit">Erstgespräch</button>
<a class="button button--secondary" href="/leistungen">Leistungen</a>
```

## Klassen

| Klasse               | Wirkung                                                              |
| -------------------- | -------------------------------------------------------------------- |
| `button`             | Grundform: Abstände, Radius, Schrift, Fokusrahmen, Übergang          |
| `button--primary`    | gefüllte Fläche in Akzentblau, Beschriftung in Grundfarbe            |
| `button--secondary`  | Textlink in Akzentblau mit Haarlinienrahmen, ohne Fläche             |

Deaktiviert wird nicht über eine Klasse, sondern über den Zustand des Elements: am `<button>` das
Attribut `disabled`, am `<a>` `aria-disabled="true"` ohne `href`, damit der Link weder anklickbar
noch mit der Tastatur erreichbar ist.

```html
<button class="button button--primary" type="submit" disabled>Erstgespräch</button>
<a class="button button--secondary" aria-disabled="true">Noch nicht verfügbar</a>
```

## Zusammen mit busy-button

```html
<link rel="stylesheet" href="/components/button/button.css">
<link rel="stylesheet" href="/components/busy-button/busy-button.css">

<button class="button button--primary busy-button" type="submit">Absenden</button>
<button class="button button--secondary busy-button busy-button--quiet" type="submit">Prüfen</button>
```

Die Seite kombiniert die beiden Bausteine, keiner verweist auf den anderen. Damit das zusammenpasst,
hält sich `button.css` an zwei Regeln:

- kein Pseudoelement. `::after` gehört busy-button, dort sitzt der Spinner.
- der Fokus ist ein `outline`, kein `box-shadow`, der von einem Überlauf abgeschnitten würde.

Für die sekundäre Variante kommt `busy-button--quiet` dazu, weil der Spinner sonst weiß auf hellem
Grund stünde.

## CSS-Variablen

Standardwerte in `:where(.button)`, jede Seite kann sie ohne Spezifitätskampf überschreiben.

| Variable                    | Standard                          |
| --------------------------- | --------------------------------- |
| `--button-foreground`       | `var(--ink)`, primär `var(--bg)`, sekundär `var(--blue)` |
| `--button-background`       | `transparent`, primär `var(--blue)` |
| `--button-border-color`     | `transparent`, primär `var(--blue)`, sekundär `var(--hairline)` |
| `--button-radius`           | `var(--radius-button)`            |
| `--button-padding-block`    | `var(--space-3)`                  |
| `--button-padding-inline`   | `var(--space-5)`                  |
| `--button-font-size`        | `var(--font-size-text-web)`       |
| `--button-font-weight`      | `var(--font-weight-title)`        |
| `--button-gap`              | `var(--space-2)`                  |
| `--button-focus-color`      | `var(--blue)`                     |
| `--button-focus-width`      | `calc(var(--hairline-width) * 2)` |
| `--button-focus-offset`     | `var(--space-1)`                  |
| `--button-hover-mix`        | `12%`                             |
| `--button-active-mix`       | `22%`                             |
| `--button-disabled-opacity` | `0.4`                             |
| `--button-duration`         | `var(--motion-duration)`          |
| `--button-ease`             | `var(--motion-ease)`              |

In der Anwendung, nicht auf der Website, ist die kleinere Schriftgröße richtig:

```css
.app .button { --button-font-size: var(--font-size-text-app); }
```

## Hell und dunkel, Bewegung, Zeigerzustände

Der Baustein hat keine eigene Regel für den dunklen Modus: er liest `--blue`, `--ink`, `--bg` und
`--hairline`, und die Tokens setzen dort die Werte des Modus. Zeigen und Drücken mischen die Fläche
über `color-mix` in Richtung `--ink`, was im hellen Modus dunkler und im dunklen Modus heller wird,
ohne einen zweiten Satz Werte. `:hover` steht in `@media (hover: hover)`, damit ein Fingertipp nicht
den Zeigerzustand hängen lässt. Die Dauer des Übergangs ist `--motion-duration`; bei „Bewegung
reduzieren“ steht sie auf `0ms`, der Baustein braucht dafür keine eigene Regel.

## Barrierefreiheit

- Fokus sichtbar über `:focus-visible` in Akzentblau, mit Abstand zum Rahmen, in beiden Modi.
- Die sekundäre Variante trägt einen Rahmen. Damit ist die Farbe nicht das einzige Merkmal, das sie
  als Schaltfläche erkennbar macht.
- Die Größe kommt aus Abstand und Schriftgröße, nicht aus einer festen Höhe, und wächst mit der
  Schriftgröße des Systems mit.
- **Offener Punkt, gehört zur Kontrastfrage aus dem Tokens-Task.** Im hellen Modus hat Weiß auf
  `#0a7aff` 4,01:1 und Blau auf Weiß ebenfalls 4,01:1. Das erfüllt AA für große Schrift, nicht für
  Fließtext. Der dunkle Modus ist mit 5,76:1 unkritisch. Dieser Baustein ändert die Designsprache
  nicht; entscheidet Jens sich für das dunklere Blau aus
  [tokens/README.md](../tokens/README.md) (`#0a68db`, 5,23:1), erfüllen beide Varianten AA, ohne dass
  hier eine Zeile geändert werden muss.

## Demo

`demo.html` lokal im Browser öffnen. Sie zeigt beide Varianten auf beiden Gründen, als Schaltfläche
und als Link, deaktiviert, zusammen mit busy-button, und hat einen Umschalter für System, Hell und
Dunkel. Unter `/components/` liefert die Anwendung keine `.html`-Dateien aus, die Demo wird also aus
dem Repository geöffnet, nicht über die Anwendung.
