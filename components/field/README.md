Herkunft: Atelier components/field, Stand 22.09.2026, Commit abe762535585569f69b7aa81e594c46b09be9681

# Field

Version 0.2.0. Ein Formularfeld mit Beschriftung, optionalem Hilfetext und optionalem Fehlertext,
für Text, E-Mail, Passwort, mehrzeiligen Text, Datei und eine geschlossene Auswahl. Nur CSS, keine
Abhängigkeit, kein Framework. Alle Werte kommen aus [tokens](../tokens/), Grundlage ist
[docs/Design/designsprache.md](../../docs/Design/designsprache.md).

Der Baustein gestaltet die Teile und gibt ihnen ihren Platz. Die Verbindung zwischen Feld, Hilfetext
und Fehlertext ist Sache des Markups: `for` und `id` gehören zusammen, `aria-describedby` nennt die
IDs von Hilfe- und Fehlertext, und ein fehlerhaftes Feld trägt `aria-invalid="true"`.

## Einbinden

```html
<link rel="stylesheet" href="/components/tokens/tokens.css">
<link rel="stylesheet" href="/components/field/field.css">

<div class="field">
  <label class="field__label" for="mail">E-Mail</label>
  <input class="field__input" id="mail" name="mail" type="email" autocomplete="email"
         aria-describedby="mail-hint">
  <p class="field__hint" id="mail-hint">Nur für die Antwort auf Ihre Anfrage.</p>
</div>
```

Mit Fehler:

```html
<div class="field field--invalid">
  <label class="field__label" for="mail">E-Mail</label>
  <input class="field__input" id="mail" name="mail" type="email" aria-invalid="true"
         aria-describedby="mail-hint mail-error">
  <p class="field__hint" id="mail-hint">Nur für die Antwort auf Ihre Anfrage.</p>
  <p class="field__error" id="mail-error">Diese E-Mail-Adresse ist unvollständig.</p>
</div>
```

## Klassen

| Klasse            | Wofür                                                                  |
| ----------------- | ---------------------------------------------------------------------- |
| `field`           | Fassung: setzt Beschriftung, Feld, Hilfe- und Fehlertext untereinander |
| `field__label`    | Beschriftung, Sekundärgrau in Beschriftungsgröße                       |
| `field__input`    | das Bedienelement, auch auf `textarea` und `input[type=file]`          |
| `field__hint`     | Hilfetext unter dem Feld                                               |
| `field__error`    | Fehlertext: roter Punkt, Satz in Textfarbe                             |
| `field--invalid`  | an der Fassung, färbt den Rahmen des Feldes rot                        |

## Die sechs Arten

Alle sechs tragen `field__input`; den Unterschied macht das Element beziehungsweise `type`.

```html
<input class="field__input" type="text">
<input class="field__input" type="email" inputmode="email">
<input class="field__input" type="password" autocomplete="current-password">
<textarea class="field__input" rows="5"></textarea>
<input class="field__input" type="file" accept=".pdf,.png,.jpg">
<select class="field__input">
  <option value="angebot">Angebot</option>
  <option value="bericht">Bericht</option>
</select>
```

Die Auswahl ist für eine kurze, geschlossene Liste gedacht, etwa die Art einer Kundenseite. Der Pfeil
ist aus zwei Verläufen in `currentcolor` gezeichnet, kein Bild und kein Skript, deshalb folgt er dem
hellen und dem dunklen Modus von selbst.

Die `textarea` wächst nur nach unten (`resize: vertical`), damit sie die Textspalte nicht sprengt;
ihre Höhe kommt aus `rows`, nicht aus CSS. Beim Dateifeld bekommt die Schaltfläche des Systems über
`::file-selector-button` den ruhigen Rahmen des Sekundärstils, ohne dass dieser Baustein den
Baustein button kennt.

## CSS-Variablen

Standardwerte in `:where(.field)`, jede Seite kann sie ohne Spezifitätskampf überschreiben.

| Variable                       | Standard                          |
| ------------------------------ | --------------------------------- |
| `--field-gap`                  | `var(--space-2)`                  |
| `--field-max-width`            | `var(--measure-text)`             |
| `--field-label-color`          | `var(--ink-2)`                    |
| `--field-label-size`           | `var(--font-size-label-web)`      |
| `--field-input-color`          | `var(--ink)`                      |
| `--field-input-background`     | `var(--bg)`                       |
| `--field-input-border-color`   | `var(--hairline)`, ungültig `var(--red)` |
| `--field-input-radius`         | `var(--radius-s)`                 |
| `--field-input-padding-block`  | `var(--space-3)`                  |
| `--field-input-padding-inline` | `var(--space-3)`                  |
| `--field-input-size`           | `var(--font-size-text-web)`       |
| `--field-hint-color`           | `var(--ink-2)`                    |
| `--field-hint-size`            | `var(--font-size-label-web)`      |
| `--field-error-color`          | `var(--ink)`                      |
| `--field-error-size`           | `var(--font-size-label-web)`      |
| `--field-error-dot-color`      | `var(--red)`                      |
| `--field-error-dot-size`       | `var(--space-2)`                  |
| `--field-focus-color`          | `var(--blue)`                     |
| `--field-focus-width`          | `calc(var(--hairline-width) * 2)` |
| `--field-focus-offset`         | `var(--space-1)`                  |
| `--field-duration`             | `var(--motion-duration)`          |
| `--field-ease`                 | `var(--motion-ease)`              |

In der Anwendung sind die kleineren Schriftgrößen richtig:

```css
.app .field { --field-input-size: var(--font-size-text-app); --field-label-size: var(--font-size-label-app); }
```

## Hell und dunkel

Keine eigene Regel für den dunklen Modus: der Baustein liest `--bg`, `--ink`, `--ink-2`,
`--hairline`, `--blue` und `--red`, die Tokens setzen dort die Werte des Modus. `color-scheme` aus
den Tokens sorgt dafür, dass auch die Teile, die der Browser selbst zeichnet, zum Modus passen.

## Barrierefreiheit

- `for` am Label und `id` am Feld gehören zusammen; ein Klick auf die Beschriftung setzt den Fokus.
- `aria-describedby` nennt Hilfetext und Fehlertext, beide durch Leerzeichen getrennt und in dieser
  Reihenfolge, damit der Screenreader zuerst die Hilfe und dann den Fehler liest.
- Ein fehlerhaftes Feld trägt `aria-invalid="true"`, die Fassung `field--invalid`.
- Der Fehlertext steht in Textfarbe, rot ist nur der kleine Punkt davor. Damit ist Farbe nie das
  einzige Merkmal, und der Satz erfüllt im hellen Modus AA (16,83:1), was roter Text mit 3,55:1
  nicht täte. Der rote Rahmen ist kein Text und muss nur 3:1 erreichen, was er in beiden Modi tut.
- Der Fokus ist über `:focus-visible` sichtbar. Textfelder erfüllen `:focus-visible` auch beim Klick,
  ein Feld, in dem geschrieben wird, ist also immer markiert.
- Die Schriftgröße im Feld ist `--font-size-text-web` (18px). Über 16px zoomt Safari auf dem iPhone
  beim Fokus nicht in die Seite.

## Formulare in Razor Pages

Das Partial `src/Atelier.Web/Pages/Shared/Ui/_Field.cshtml` rendert diesen Baustein. Es schreibt
`name` und `id` selbst und arbeitet damit mit der Modellbindung von Razor Pages. Der
Antiforgery-Token kommt vom Form-Tag-Helper des umgebenden `<form method="post">`, nicht von diesem
Baustein.

## Demo

`demo.html` lokal im Browser öffnen. Sie zeigt die sechs Arten, den Fehlerfall, ein deaktiviertes
Feld, beide Gründe und hat einen Umschalter für System, Hell und Dunkel. Unter `/components/`
liefert die Anwendung keine `.html`-Dateien aus, die Demo wird also aus dem Repository geöffnet,
nicht über die Anwendung.
