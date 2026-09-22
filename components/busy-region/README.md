Herkunft: Atelier components/busy-region, Stand 22.09.2026, Commit abe762535585569f69b7aa81e594c46b09be9681

# Busy Region

Version 0.1.1. Ein Bereich mit Platzhalter und Inhalt. Solange `aria-busy="true"` gesetzt ist,
steht der Platzhalter da, sonst der Inhalt. Nur CSS. Alle Werte kommen aus [tokens](../tokens/).

Änderung 0.1.1 (16.09.2026): Der Weg, den der Inhalt beim Einblenden zurücklegt, kommt aus
`--space-1` statt aus einem Rohwert, die Dauer des Einblendens aus `--motion-duration`. Beide
stehen mit den übrigen Werten als Baustein-Variablen in `:where(.busy-region)`.

- Der Platzhalter blendet erst nach 300 ms ein. Lädt es schneller, bleibt der Bereich kurz leer,
  und es blitzt kein Skelett auf.
- Der Inhalt blendet beim Erscheinen weich ein und rückt dabei um `--space-1` nach oben.

```html
<link rel="stylesheet" href="/components/tokens/tokens.css">
<link rel="stylesheet" href="/components/busy-region/busy-region.css">

<div class="busy-region" aria-busy="true">
  <div class="busy-region__placeholder" aria-hidden="true">
    … zum Beispiel ein Skeleton oder ein Spinner …
  </div>
  <div class="busy-region__content">
    … Inhalt …
  </div>
</div>
```

```js
region.setAttribute('aria-busy', 'true');   // Laden startet
region.removeAttribute('aria-busy');        // Daten sind da
```

Was im Platzhalter steht, entscheidet die Seite. Die Komponente hat keine Abhängigkeit zu
`skeleton` oder `spinner`, beide passen aber hinein.

## CSS-Variablen

Standardwerte in `:where(.busy-region)`, jede Seite kann sie ohne Spezifitätskampf überschreiben.

| Variable                         | Standard                   | Wofür                                     |
| -------------------------------- | -------------------------- | ----------------------------------------- |
| `--busy-region-delay`            | `300ms`                    | ab dieser Wartezeit erscheint der Platzhalter |
| `--busy-region-fade-duration`    | `var(--motion-duration)`   | Einblenden des Platzhalters               |
| `--busy-region-settle-duration`  | `320ms`                    | Einblenden des Inhalts                    |
| `--busy-region-rise`             | `var(--space-1)`           | Weg, den der Inhalt dabei nach oben rückt |

## Hell und dunkel

Der Baustein setzt keine Farbe und keine Fläche, nur Sichtbarkeit und Bewegung. Er ist damit in
beiden Modi richtig, ohne eine eigene Regel. Farbe bringt mit, was in Platzhalter und Inhalt steht.

## Begründete Ausnahmen von den Tokens

- `--busy-region-delay` (300 ms) ist ein Rohwert. Der Wert ist eine Schwelle für das Auge, keine
  Dauer einer Bewegung; die Tokens kennen keine Schwelle dieser Art.
- `--busy-region-settle-duration` (320 ms) ist ein Rohwert, weil zwischen `--motion-duration`
  (200 ms) und `--motion-duration-story` (460 ms) kein Token liegt. Ein Token dafür wurde nicht
  erfunden, der Bedarf ist gemeldet.

Bei „Bewegung reduzieren“ blendet der Inhalt nur ein, ohne Bewegung.

Demo: `demo.html` im Browser öffnen, mit Umschalter für System, Hell und Dunkel.
