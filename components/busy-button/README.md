Herkunft: Atelier components/busy-button, Stand 22.09.2026, Commit abe762535585569f69b7aa81e594c46b09be9681

# Busy Button

Version 0.1.1. Solange eine Aktion läuft, weicht die Beschriftung einem kleinen Spinner. Die
Schaltfläche behält ihre Breite und nimmt keine weiteren Klicks an. Nur CSS, gesteuert über
`aria-busy`. Farbe und Strichbreite kommen aus [tokens](../tokens/).

Änderung 0.1.1 (16.09.2026): Die Rohwerte aus der Zeit vor den Design-Tokens sind ersetzt. Die
weiße Hexfarbe `#ffffff` ist `var(--bg)`, die blaue `#0a7aff` ist `var(--blue)`, die Strichbreite
`1.5px` ist `calc(var(--hairline-width) * 1.5)`. Damit stimmt der dunkle Modus, der vorher einen
weißen Spinner und ein festes Hellblau zeigte.

```html
<link rel="stylesheet" href="/components/tokens/tokens.css">
<link rel="stylesheet" href="/components/busy-button/busy-button.css">

<button class="busy-button" type="button">Freigeben</button>
```

```js
button.setAttribute('aria-busy', 'true');   // Aktion startet
button.removeAttribute('aria-busy');        // Aktion beendet
```

Die Klasse gibt keine Gestaltung für die Schaltfläche selbst vor. Farbe, Radius und Abstand kommen
von der Seite, etwa vom Baustein [button](../button/). Ein Element darf die Klassen beider
Bausteine tragen; die beiden kennen einander nicht.

| Klasse                 | Wirkung                                                |
| ---------------------- | ------------------------------------------------------ |
| `busy-button--quiet`   | Spinner in Blau, für Textschaltflächen ohne Fläche     |

## CSS-Variablen

Standardwerte in `:where(.busy-button)`, jede Seite kann sie ohne Spezifitätskampf überschreiben.

| Variable                      | Standard                             | Wofür                                |
| ----------------------------- | ------------------------------------ | ------------------------------------ |
| `--busy-button-spinner`       | `var(--bg)`, quiet `var(--blue)`     | Farbe des Rings                      |
| `--busy-button-size`          | `14px`                               | Durchmesser des Rings                |
| `--busy-button-stroke`        | `calc(var(--hairline-width) * 1.5)`  | Strichbreite des Rings               |
| `--busy-button-track-mix`     | `35%`                                | Deckkraft des ruhenden Ringteils     |
| `--busy-button-speed`         | `0.9s`                               | eine Umdrehung                       |

## Hell und dunkel

Der Baustein hat keine eigene Regel für den dunklen Modus: er liest `--bg` und `--blue`, und die
Tokens setzen dort die Werte des Modus. Auf einer gefüllten Schaltfläche trägt der Spinner die
Grundfarbe, also genau die Farbe, die die Beschriftung hatte, für die er einsteht: hell Weiß auf
Blau, dunkel Schwarz auf Blau. Das ist derselbe Wert, den der Baustein
[button](../button/) als `--button-foreground` der primären Variante setzt.

Auf einer Textschaltfläche gibt es keine gefüllte Fläche, deren Beschriftungsfarbe der Spinner
übernehmen könnte; dort setzt `busy-button--quiet` ihn auf `--blue`.

## Begründete Ausnahmen von den Tokens

- `--busy-button-size` (14 px) ist ein Rohwert. Für den Durchmesser eines Rings gibt es kein Token;
  die Tokens tragen Abstände, Radien und Schriftgrößen. Der Wert ist eine Baustein-Variable, eine
  Seite kann ihn also überschreiben, etwa auf die Schriftgröße der Anwendung.
- `--busy-button-speed` (0.9s) ist ein Rohwert. Die Tokens tragen die Dauer von Zustandswechseln
  (`--motion-duration` 200 ms), nicht die Geschwindigkeit einer Schleife, die weiterläuft. Ein
  Token dafür wurde nicht erfunden.

Bei „Bewegung reduzieren“ dreht der Ring halb so schnell, abgeleitet aus `--busy-button-speed`.

Demo: `demo.html` im Browser öffnen, mit Umschalter für System, Hell und Dunkel.
