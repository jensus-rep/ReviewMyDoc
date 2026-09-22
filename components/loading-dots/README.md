Herkunft: Atelier components/loading-dots, Stand 22.09.2026, Commit abe762535585569f69b7aa81e594c46b09be9681

# Loading Dots

Version 0.1.1. Drei kleine Punkte, die nacheinander aufleuchten. Im Fließtext, etwa bei
„Wird gespeichert“. Übernimmt die Textfarbe. Nur CSS. Alle Werte kommen aus
[tokens](../tokens/).

Änderung 0.1.1 (16.09.2026): Größe und Abstand der Punkte kommen aus den Design-Tokens statt aus
Rohwerten, Geschwindigkeit und Deckkraft stehen als Baustein-Variablen in `:where(.loading-dots)`,
damit eine Seite sie überschreiben kann.

```html
<link rel="stylesheet" href="/components/tokens/tokens.css">
<link rel="stylesheet" href="/components/loading-dots/loading-dots.css">

Wird gespeichert
<span class="loading-dots" role="status" aria-label="Wird gespeichert">
  <i class="loading-dots__dot"></i><i class="loading-dots__dot"></i><i class="loading-dots__dot"></i>
</span>
```

## CSS-Variablen

Standardwerte in `:where(.loading-dots)`, jede Seite kann sie ohne Spezifitätskampf überschreiben.

| Variable                        | Standard             | Wofür                                  |
| ------------------------------- | -------------------- | -------------------------------------- |
| `--loading-dots-size`           | `var(--space-1)`     | Durchmesser eines Punktes              |
| `--loading-dots-gap`            | `var(--space-1)`     | Abstand zwischen den Punkten           |
| `--loading-dots-color`          | `currentColor`       | Farbe der Punkte                       |
| `--loading-dots-speed`          | `1.2s`               | ein Durchlauf der drei Punkte          |
| `--loading-dots-step`           | `0.15s`              | Versatz von Punkt zu Punkt             |
| `--loading-dots-opacity`        | `0.25`               | Deckkraft im Ruhezustand               |
| `--loading-dots-opacity-on`     | `1`                  | Deckkraft im Aufleuchten               |
| `--loading-dots-opacity-still`  | `0.6`                | Deckkraft bei „Bewegung reduzieren“    |

## Hell und dunkel

Der Baustein hat keine eigene Regel für den dunklen Modus und braucht keine: die Punkte stehen in
`currentColor` und gehören damit dem Satz, in dem sie stehen. Wechselt dessen Farbe mit dem Modus,
wechseln die Punkte mit.

## Begründete Ausnahmen von den Tokens

- `--loading-dots-speed` und `--loading-dots-step` sind Rohwerte. Die Tokens tragen die Dauer von
  Zustandswechseln (`--motion-duration` 200 ms, `--motion-duration-story` 460 ms), nicht die
  Geschwindigkeit einer Schleife, die weiterläuft. Ein Token dafür wurde nicht erfunden.
- Die drei Deckkraftwerte sind Rohwerte, weil die Tokens keine Deckkraftstufen kennen.

Bei „Bewegung reduzieren“ stehen die Punkte still und tragen `--loading-dots-opacity-still`.

Demo: `demo.html` im Browser öffnen, mit Umschalter für System, Hell und Dunkel.
