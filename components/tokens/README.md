Herkunft: Atelier components/tokens, Stand 22.09.2026, Commit abe762535585569f69b7aa81e594c46b09be9681

# Tokens

Basistokens, Website-Tokens und eine ergänzende Palette für den Admin-Arbeitsbereich. Kein Baustein und keine Seite schreibt eigene Farbwerte.

| Datei         | Version | Für                         | Grundlage                                                                  |
| ------------- | ------- | --------------------------- | -------------------------------------------------------------------------- |
| `tokens.css`  | 1.0.0   | Admin und Kundenanwendungen | [designsprache.md](../../docs/Design/designsprache.md), Version 1.1        |
| `website.css` | 2.2.0   | öffentliche Website         | [Hero Richtung B, Fassung 2](../../docs/Design/mockups/hero/richtung-b/)   |
| `admin.css` | 1.4.0 | Silber und Graphit; matte Verwaltung, großzügige Dokumenthöhe, glänzende Artefaktflächen | [Admin-Arbeitsbereich](../../docs/Design/admin-workspace.md) |

Die Website-Tokens stehen in einer eigenen Datei. Kundenanwendungen behalten die Basistokens; Admin und Artefaktfolge ergänzen sie durch Silber und Graphit aus `admin.css`. Die Verwaltung verwendet matte Flächen und kompakte Größen unter `.admin-frame`; Lichtverläufe und Schatten bleiben für die Artefaktfolge verfügbar. Beide laden die Website-Schrift nicht. Beschrieben ist die Website
im Abschnitt [Website 2.1](#website-21); alle Abschnitte davor gelten für `tokens.css`.

Version 1.0.0 von `tokens.css`. Alle Werte der Designsprache 1.1 als CSS-Variablen: Farben in hell
und dunkel, Schrift, Abstände, Radien, Haarlinie, Schatten und Bewegung. `tokens.css` ist die einzige
Quelle dieser Werte für Admin und Kundenanwendungen. Grundlage ist
[docs/Design/designsprache.md](../../docs/Design/designsprache.md), Version 1.1.

## Einbinden

Als erstes Stylesheet der Seite, vor allen Bausteinen.

```html
<link rel="stylesheet" href="/components/tokens/tokens.css">
```

Danach stehen die Variablen auf `:root` und gelten überall. Anders als bei den übrigen Bausteinen
stehen die Standardwerte nicht in `:where(…)`, weil Tokens für die ganze Seite gelten und nicht zu
einem Element gehören. Ein Bereich kann sie lokal überschreiben, etwa
`.dialog { --shadow-float: …; }`, was ohne Spezifitätskampf funktioniert, weil `:root` weniger
spezifisch ist als jeder Klassenselektor.

```css
.hinweis {
  color: var(--ink-2);
  font-size: var(--font-size-label-web);
  border-top: var(--hairline-width) solid var(--hairline);
  padding-top: var(--space-4);
}
```

## Hell und dunkel

Hell ist der Ausgangswert. Dunkel kommt auf zwei Wegen, mit denselben Werten:

- vom System über `@media (prefers-color-scheme: dark)`, solange die Seite nichts erzwingt
- von der Seite über `data-theme="dark"` am `<html>`-Element

`data-theme="light"` erzwingt hell, auch wenn das System dunkel steht. Ohne Attribut gilt das System.
`color-scheme` wird mitgesetzt, damit Formularfelder und Rollbalken zum Modus passen.

```html
<html lang="de" data-theme="dark">
```

## Farben

| Variable            | Kurzname     | Rolle              | Hell      | Dunkel    |
| ------------------- | ------------ | ------------------ | --------- | --------- |
| `--color-bg`        | `--bg`       | Grund              | `#ffffff` | `#000000` |
| `--color-bg-2`      | `--bg-2`     | Sekundärgrund      | `#f5f5f7` | `#1c1c1e` |
| `--color-hairline`  | `--hairline` | Haarlinie          | `#e5e5ea` | `#2c2c2e` |
| `--color-ink`       | `--ink`      | Text               | `#1d1d1f` | `#f5f5f7` |
| `--color-ink-2`     | `--ink-2`    | Sekundärtext       | `#6e6e73` | `#98989d` |
| `--color-blue`      | `--blue`     | Akzent, interaktiv | `#0a7aff` | `#0a84ff` |
| `--color-red`       | `--red`      | Kritisch           | `#ff3b30` | `#ff453a` |

Die Kurznamen sind Aliasse auf dieselben Werte (`--ink: var(--color-ink)`). Bausteine dürfen beide
Schreibweisen verwenden, der Baustein `process-graph` nutzt die kurzen.

## Schrift

`--font-sans` ist die Systemschrift aus der Designsprache. Keine Webfonts.
Je Rolle gibt es eine Größe für die Anwendung und eine für die Website. Die Designsprache nennt
Bereiche, hier steht je Rolle ein Wert daraus.

| Rolle                  | Anwendung                             | Website                                | Bereich laut Designsprache |
| ---------------------- | ------------------------------------- | -------------------------------------- | -------------------------- |
| Seitentitel            | `--font-size-page-title-app` 22px     | `--font-size-page-title-web` 44px      | 20 bis 22 / 40 bis 52      |
| Abschnittstitel        | `--font-size-section-title-app` 15px  | `--font-size-section-title-web` 24px   | 15 / 22 bis 26             |
| Text                   | `--font-size-text-app` 14px           | `--font-size-text-web` 18px            | 13 bis 14 / 17 bis 19      |
| Sekundär, Beschriftung | `--font-size-label-app` 12px          | `--font-size-label-web` 14px           | 12 / 14 bis 15             |

Dazu `--font-weight-text` (400), `--font-weight-title` (500), `--line-height-text` (1.55),
`--line-height-title` (1.15), `--letter-spacing-page-title-web` (−0.02em) und `--measure-text`
(62ch) für die maximale Breite einer Textspalte.

Die Hilfsklasse `.tabular-nums` setzt `font-variant-numeric: tabular-nums` für Zahlen in Listen
und Tabellen.

## Abstände

Zwölf Stufen auf dem 4-px-Raster: `--space-1` 4px, `--space-2` 8px, `--space-3` 12px,
`--space-4` 16px, `--space-5` 20px, `--space-6` 24px, `--space-7` 32px, `--space-8` 40px,
`--space-9` 48px, `--space-10` 64px, `--space-11` 80px, `--space-12` 96px.

Bis Stufe 6 in Schritten von 4px für Abstände innerhalb eines Elements, darüber in größeren
Sprüngen für Abstände zwischen Abschnitten.

## Geometrie, Haarlinie, Schatten

| Variable          | Wert                                | Wofür                                          |
| ----------------- | ----------------------------------- | ---------------------------------------------- |
| `--radius-s`      | `8px`                               | Karten, Felder, Bildausschnitte                |
| `--radius-m`      | `12px`                              | größere Flächen, Dialoge                       |
| `--radius-button` | `7px`                               | Schaltflächen                                  |
| `--hairline-width`| `1px`                               | Breite jeder trennenden Linie                  |
| `--shadow-float`  | `0 24px 60px rgba(0, 0, 0, 0.10)`   | nur schwebende Ebenen, dunkel mit Deckkraft 0.60 |

Schatten nur für Dialog, Menü und die vordere Karte eines Stapels. Karten im Fluss trennen sich
durch Haarlinie und Abstand.

## Bewegung

| Variable                   | Wert                                | Wofür                                      |
| -------------------------- | ----------------------------------- | ------------------------------------------ |
| `--motion-duration`        | `200ms`                             | Zustandswechsel, Ein- und Ausblenden       |
| `--motion-duration-story`  | `460ms`                             | längere Wechsel, etwa ein Kartenstapel     |
| `--motion-ease`            | `cubic-bezier(0.22, 0.61, 0.36, 1)` | Verlauf ohne Nachfedern                    |

Beide Dauern liegen unter einer halben Sekunde. Bei `prefers-reduced-motion: reduce` stehen beide
auf `0ms`. Wer seine Übergänge über diese Variablen steuert, berücksichtigt „Bewegung reduzieren“
damit automatisch.

## Kontraste

Berechnet nach WCAG 2.1: relative Leuchtdichte je Kanal mit
`c/12.92` beziehungsweise `((c+0.055)/1.055)^2.4`, Gewichtung 0.2126 / 0.7152 / 0.0722, Verhältnis
`(L_hell + 0.05) / (L_dunkel + 0.05)`. Schwellen: 4,5:1 für Fließtext, 3:1 für große Schrift
(ab 24px oder ab 18,66px in Gewicht 700).

Hell:

| Textfarbe             | auf Grund `#ffffff` | auf Sekundärgrund `#f5f5f7` | AA                        |
| --------------------- | ------------------- | --------------------------- | ------------------------- |
| Text `#1d1d1f`        | 16,83:1             | 15,46:1                     | ja                        |
| Sekundärtext `#6e6e73`| 5,07:1              | 4,66:1                      | ja                        |
| Akzent `#0a7aff`      | 4,01:1              | 3,68:1                      | **nur große Schrift**     |
| Kritisch `#ff3b30`    | 3,55:1              | 3,26:1                      | **nur große Schrift**     |

Dunkel:

| Textfarbe             | auf Grund `#000000` | auf Sekundärgrund `#1c1c1e` | AA  |
| --------------------- | ------------------- | --------------------------- | --- |
| Text `#f5f5f7`        | 19,29:1             | 15,63:1                     | ja  |
| Sekundärtext `#98989d`| 7,31:1              | 5,93:1                      | ja  |
| Akzent `#0a84ff`      | 5,76:1              | 4,66:1                      | ja  |
| Kritisch `#ff453a`    | 6,16:1              | 4,99:1                      | ja  |

Haarlinien sind keine Textfarben und liegen bewusst niedrig: `#e5e5ea` auf Weiß 1,26:1,
`#2c2c2e` auf Schwarz 1,51:1.

**Offener Punkt für Jens.** Akzentblau `#0a7aff` und Kritischrot `#ff3b30` erreichen im hellen
Modus nur AA für große Schrift, nicht für Fließtext. Die Werte stehen so in der Designsprache und
wurden hier nicht verändert. Bis zur Entscheidung gilt für den hellen Modus:

- Links und kritische Hinweise in Fließtextgröße brauchen ein zweites Merkmal, etwa Unterstreichung
  beim Link und Text neben dem roten Punkt, damit Farbe nicht die einzige Information ist.
- Rot nur als kleiner Punkt verwenden, wie in Regel 2 der Designsprache, nicht als Textfarbe für
  ganze Sätze.
- Wenn Fließtext in Blau oder Rot AA erfüllen soll, braucht es dunklere Werte nur für den hellen
  Modus, etwa Blau `#0a68db` (5,23:1 auf Weiß, 4,80:1 auf dem Sekundärgrund) und Rot `#d12b21`
  (5,15:1 und 4,73:1). Das ändert die Designsprache und ist deshalb nichts, was dieser Baustein
  entscheidet.

## Website 2.1

Version 2.2.0 von `website.css`. Die Werte des freigegebenen Heros Richtung B „Raum und Licht“,
Fassung 2 vom 17.09.2026 ([index.html](../../docs/Design/mockups/hero/richtung-b/index.html) und
[README.md](../../docs/Design/mockups/hero/richtung-b/README.md)). Die Designsprache 2.1 wird daraus
abgeleitet; bei einer Abweichung gilt das Mockup, bis die Designsprache nachgezogen ist.

Jeder Name beginnt mit `--website-`. Kein Name aus `tokens.css` wird hier gesetzt, das prüft der
Test. Das Anwendungsblau aus Version 1.1 bleibt damit unberührt; die Website hat kein Blau, ihr
einziger Akzent ist das Licht.

### Einbinden

```html
<link rel="stylesheet" href="/components/tokens/website.css">
```

Die Datei steht für sich und braucht `tokens.css` nicht. Braucht eine Seite der Website Bausteine,
die auf den Kurznamen der Version 1.1 stehen (`--bg`, `--ink`), bindet sie beide Dateien ein und
zeigt die Kurznamen im Seitenstil auf die Website-Werte, etwa `--bg: var(--website-color-room)`.
Hell und dunkel schalten wie bei `tokens.css`: System über `prefers-color-scheme`, erzwungen über
`data-theme="light"` oder `data-theme="dark"` am `<html>`-Element. Die Bühne der Prozesskarte liegt
auf `--website-color-room` und folgt damit dem Modus.

### Farben

| Variable                      | Rolle im Mockup   | Aufgabe                                                        | Hell                     | Dunkel                 |
| ----------------------------- | ----------------- | -------------------------------------------------------------- | ------------------------ | ---------------------- |
| `--website-color-room`        | Raum              | Seitengrund, Kopfleiste und Bühne                              | `#eeebe4`                | `#0c0c0b`              |
| `--website-color-sheet`       | Blatt             | Blatt der Prozesskarte, Grund der Knoten                       | `#fcfbf8`                | `#191917`              |
| `--website-color-surface`     | Fläche            | Rahmen „Eine Anwendung“ und die Knoten darin                   | `#f1efe8`                | `#21211e`              |
| `--website-color-ink`         | Tusche            | Text, gezeichnete Linien und Symbole                           | `#171613`                | `#ecebe4`              |
| `--website-color-ink-2`       | Tusche 2          | Sekundärtext, Beschriftungen, Rahmen des Schriftfelds          | `#5a5750`                | `#a19e95`              |
| `--website-color-line`        | Linie             | Haarlinie, nur Grafik, nie Text                                | `#d2cec4`                | `#35342f`              |
| `--website-color-light-line`  | Licht, Linie      | tragender Weg; hell ist das die Tusche, dunkel das Gelb        | `#171613`                | `#f3cd5a`              |
| `--website-color-light-band`  | Licht, Band       | hell das gelbe Band unter der Tuschelinie, dunkel ein Schein   | `#f1c94f`                | `#3a3219`              |
| `--website-color-light-dot`   | Licht, Punkt      | der laufende Vorgang auf seinem Weg                            | `#171613`                | `#f3cd5a`              |
| `--website-color-button`      | Knopf, Grund      | Grund des einen Knopfs „Erstgespräch vereinbaren“              | `#171613`                | `#ecebe4`              |
| `--website-color-button-ink`  | Knopf, Schrift    | Schrift und Pfeil auf dem Knopf                                | `#fcfbf8`                | `#0c0c0b`              |

Gelb ist der einzige Akzent: im Dunklen die Linie selbst, im Hellen ein Band unter der Tuschelinie,
wie ein Textmarker auf einem Plan. Es markiert nur tragende Wege und den laufenden Vorgang, nie Text
und nie Flächen.

### Schatten

| Variable                     | Hell                       | Dunkel               | Aufgabe                                             |
| ---------------------------- | -------------------------- | -------------------- | --------------------------------------------------- |
| `--website-shadow-color`     | `rgba(70, 56, 30, 0.085)`  | `rgba(0, 0, 0, 0.55)` | Farbe der harten Schattenfläche                    |
| `--website-shadow-offset-x`  | `0.16`                     | gleich               | Versatz nach rechts je Einheit Höhe, ohne Einheit   |
| `--website-shadow-offset-y`  | `0.52`                     | gleich               | Versatz nach unten je Einheit Höhe, ohne Einheit    |

Schatten sind harte, versetzte Flächen ohne Unschärfe und ohne Filter: Versatz gleich Höhe mal
Lichtrichtung, das Licht fällt von hinten links. Ein Element, das um `--hoehe` über seinem Grund
liegt, bekommt etwa
`box-shadow: calc(var(--hoehe) * var(--website-shadow-offset-x)) calc(var(--hoehe) * var(--website-shadow-offset-y)) 0 var(--website-shadow-color)`.
Einen weichen Schatten wie `--shadow-float` gibt es auf der Website nicht.

### Schrift

Eine Familie: D-DIN von Datto als umbenannte Teilmenge „P51 Plan“, drei Schnitte, `font-display: swap`.
Nur 400 und 700 existieren; ein anderes Gewicht fällt auf den nächsten dieser Schnitte zurück.

| Variable                       | Wert                                                                                   |
| ------------------------------ | -------------------------------------------------------------------------------------- |
| `--website-font-family`        | `"P51 Plan", Bahnschrift, "DIN Alternate", "Arial Narrow", sans-serif`                 |
| `--website-font-family-map`    | `"P51 Plan Condensed", "P51 Plan", Bahnschrift, "Arial Narrow", sans-serif`            |
| `--website-font-weight-regular`| `400`                                                                                  |
| `--website-font-weight-bold`   | `700`                                                                                  |

`--website-font-family` gilt für Titel, Sätze, Fließtext, Schriftfeld, Schritte, Rollen und
Anmerkungen der Karte, `--website-font-family-map` für Werkzeuge und Kantenbeschriftungen der Karte.
Die Systemschriften dahinter sind nur Rückfall, solange die Dateien laden.

Skala. Unter 760 px Fensterbreite gilt die rechte Spalte; sie steht in `website.css` in einem Block
`@media (max-width: 759px)`, weil eine Variable nicht in einer Medienabfrage stehen kann. Ein leerer
Wert in der rechten Spalte heißt: gleich wie breit.

| Variable                                | Aufgabe                                         | Schnitt           | Breit                        | Schmal                      |
| --------------------------------------- | ----------------------------------------------- | ----------------- | ---------------------------- | --------------------------- |
| `--website-font-size-title`             | Titel der Seite (h1)                            | 700               | `clamp(36px, 3.7vw, 56px)`   | `clamp(32px, 9.4vw, 40px)`  |
| `--website-line-height-title`           | Zeilenhöhe Titel                                |                   | `1.02`                       |                             |
| `--website-letter-spacing-title`        | Laufweite Titel                                 |                   | `-0.018em`                   |                             |
| `--website-font-size-statement`         | Satz eines Blatts, Schlussaussage               | 400               | `clamp(27px, 2.6vw, 42px)`   | `clamp(23px, 6.6vw, 28px)`  |
| `--website-line-height-statement`       | Zeilenhöhe Satz                                 |                   | `1.08`                       | `1.1`                       |
| `--website-letter-spacing-statement`    | Laufweite Satz                                  |                   | `-0.012em`                   |                             |
| `--website-font-size-lead`              | Lead unter dem Titel                            | 400               | `17.5px`                     | `16px`                      |
| `--website-font-size-text`              | Fließtext, Beschriftung und Eingabe der Felder  | 400               | `17px`                       |                             |
| `--website-line-height-text`            | Zeilenhöhe Fließtext, Lead, Zusatz              |                   | `1.55`                       |                             |
| `--website-font-size-call`              | Aufforderung unter der Schlussaussage           | 400               | `17px`                       | `15.5px`                    |
| `--website-font-size-addition`          | Zusatz unter dem Satz, in Tusche 2              | 400               | `16px`                       | `15px`                      |
| `--website-font-size-item`              | Stückliste der Leistungen                       | 700               | `17px`                       | `15.5px`                    |
| `--website-font-size-button`            | Knopf                                           | 700               | `17px`                       |                             |
| `--website-font-size-small`             | Verweis „Erstgespräch“ in der Kopfleiste, Hinweis unter dem Knopf | 400 | `14.5px`             | `14px`                      |
| `--website-font-size-brand`             | Marke „Process 51“                              | 700               | `21px`                       | `19px`                      |
| `--website-font-size-label`             | Beschriftung in Versalien, etwa „Blatt 1 · …“   | 700               | `11.5px`                     | `10.5px`                    |
| `--website-letter-spacing-label`        | Laufweite der Beschriftung                      |                   | `0.14em`                     |                             |
| `--website-font-size-title-block-value` | Schriftfeld, Wert                               | 400               | `14px`                       | `12.5px`                    |
| `--website-font-size-title-block-field` | Schriftfeld, Feldname in Versalien              | 700               | `9.5px`                      | `8.5px`                     |
| `--website-font-size-map-step`          | Karte: Schritt, nie kleiner, der Kasten wächst  | 700               | `16px`                       | `12px`                      |
| `--website-font-size-map-tool`          | Karte: Werkzeug und Kantenbeschriftung          | Condensed 400     | `16px`                       | `12px`                      |
| `--website-font-size-map-role`          | Karte: Rolle auf dem Blatt, Versalien           | 700               | `12.5px`                     | `10px`                      |
| `--website-font-size-map-note`          | Karte: Anmerkung wie „Umweg“, Versalien         | 700               | `11.5px`                     | `9px`                       |
| `--website-letter-spacing-map-caps`     | Laufweite der Versalien in der Karte            |                   | `0.12em`                     |                             |

Die Größen der Karte stehen im Mockup als feste Pixel im Skript; die Karte liest sie künftig hier.

Änderung 2.2.0 (17.09.2026, tsk_gg9wFMMX6Q): `--website-font-size-text` bleibt auf dem Telefon bei
17 px, wie die Designsprache 2.1 für die Rolle Text verlangt. Die 15,5 px gehören der Aufforderung
unter der Schlussaussage und stehen jetzt in `--website-font-size-call`.

### Seitenrahmen

Maße aus Regel 6 der Designsprache 2.1, seit 2.2.0.

| Variable                         | Aufgabe                                                        | Breit                        | Schmal                |
| -------------------------------- | -------------------------------------------------------------- | ---------------------------- | --------------------- |
| `--website-frame-edge`           | Rand links und rechts von Kopfleiste, Inhalt und Fuß           | `56px`                       | `18px`                |
| `--website-frame-header-height`  | Höhe der angehefteten Kopfleiste                               | `64px`                       | `56px`                |
| `--website-frame-text-column`    | Textspalte neben der Karte oder neben dem Kontaktformular      | `clamp(300px, 29vw, 430px)`  | `calc(100vw - 36px)`  |
| `--website-radius`               | Ecken von Schaltfläche, Feld und Meldung                       | `3px`                        |                       |

### Dateien der Schrift

| Datei                                  | Familie, Gewicht           | Größe          | SHA-256, erste 16 Zeichen |
| -------------------------------------- | -------------------------- | -------------- | ------------------------- |
| `fonts/p51-plan-400.woff2`             | „P51 Plan“ 400             | 18 720 Bytes   | `f8aa844ecc6c0a88`        |
| `fonts/p51-plan-700.woff2`             | „P51 Plan“ 700             | 18 680 Bytes   | `e4e76e199a9caefc`        |
| `fonts/p51-plan-condensed-400.woff2`   | „P51 Plan Condensed“ 400   | 18 392 Bytes   | `330e74a8dfdb8e28`        |
| `fonts/OFL.txt`                        | Lizenztext                 |                |                           |

Herkunft: Die drei Dateien sind die data-URIs aus
[index.html](../../docs/Design/mockups/hero/richtung-b/index.html) des Mockups, base64-dekodiert und
Byte für Byte unverändert abgelegt. Sie waren dort bereits woff2 (`font/woff2`, Signatur `wOF2`,
CFF-Umrisse); eine Umwandlung war nicht nötig und es wurde keine externe Quelle verwendet. Die
Teilmenge (Latin-1, Anführungszeichen, Pfeile, 198 Zeichen, 201 Glyphen) stammt aus Richtung A und
enthält laut Mockup alle Zeichen der Seite und der Kartendaten. Geprüft mit fontTools 4.53.1:
Familienname „P51 Plan“ beziehungsweise „P51 Plan Condensed“, der Copyright-Eintrag von Datto und der
Lizenzhinweis im Namenseintrag 13 sind erhalten.

Lizenz: SIL Open Font License 1.1, Text in [fonts/OFL.txt](fonts/OFL.txt), eine unveränderte Kopie
von `OFL-D-DIN.txt` des Mockups. Die Teilmenge gilt als geänderte Fassung und trägt deshalb nicht den
reservierten Namen „D-DIN“; sie wird nicht für sich verkauft und bleibt unter der OFL. Die Lizenz
liegt jeder Kopie bei, im Repository und beim Veröffentlichen, weil der ganze Ordner `components/`
mitkopiert wird. Eine neue Teilmenge entsteht aus den Originaldateien von Datto mit
`pyftsubset` und muss wieder umbenannt werden.

### Bewegung der Kamera

Die Kamerafahrt hat keine Dauer. Jedes Bild ist eine Funktion der Scrollposition, ohne Zeitachse
und ohne Autoplay; zurückscrollen ergibt dasselbe Bild. Die Tokens sind deshalb Strecken in
Bildschirmhöhen ab Beginn der Bühne, als Zahl ohne Einheit. Die Hero-Sequenz liest sie mit
`getComputedStyle(document.documentElement).getPropertyValue(…)`. Zwischen zwei Punkten wird mit
`v² · (3 − 2v)` geglättet.

| Variable                              | Wert   | Aufgabe                                                            |
| ------------------------------------- | ------ | ------------------------------------------------------------------ |
| `--website-camera-sheet-1-end`        | `1.8`  | Ende von Blatt 1: Explosion, Kamera fährt heran                    |
| `--website-camera-sheet-2-start`      | `2.5`  | Stapel hat sich zur Hälfte gesenkt, Blatt 2 steht                  |
| `--website-camera-sheet-2-end`        | `3.35` | Ende von Blatt 2, die Blätter beginnen zu landen                   |
| `--website-camera-application-start`  | `4`    | Anwendung steht                                                    |
| `--website-camera-application-end`    | `4.35` | Ende der Anwendung, Kamera richtet sich zur Draufsicht auf         |
| `--website-camera-sheet-3-start`      | `5`    | Draufsicht, Blatt 3 steht, Schlussaussage folgt                    |
| `--website-camera-pinned-length`      | `6.3`  | so lange ist die Bühne angeheftet                                  |
| `--website-camera-stage-length`       | `7.3`  | Länge der ganzen Bühne                                             |

Bei „Bewegung reduzieren“ gibt es keine Kamerafahrt: jede Station steht als ruhende Zeichnung unter
ihrem Satz. Die Strecken werden dann nicht gelesen. Für kurze Zustandswechsel wie Hover und Fokus
gelten weiter `--motion-duration` und `--motion-ease` aus `tokens.css`.

### Kontraste

Berechnet nach derselben Formel wie oben (WCAG 2.1), nachgerechnet im Test auf zwei Stellen.
Schwellen: 4,5:1 für Text, 3:1 für Grafik, die Information trägt.

| Paar                                           | Hell      | Dunkel    | Stufe                           |
| ---------------------------------------------- | --------- | --------- | ------------------------------- |
| Tusche auf Raum                                | 15,20:1   | 16,37:1   | Text AA                         |
| Tusche auf Blatt                               | 17,49:1   | 14,73:1   | Text AA                         |
| Tusche auf Fläche                              | 15,73:1   | 13,51:1   | Text AA                         |
| Tusche 2 auf Raum                              | 6,05:1    | 7,31:1    | Text AA                         |
| Tusche 2 auf Blatt                             | 6,96:1    | 6,57:1    | Text AA                         |
| Tusche 2 auf Fläche                            | 6,26:1    | 6,03:1    | Text AA                         |
| Tusche auf Lichtband                           | 11,38:1   |           | Text AA                         |
| Lichtlinie auf Blatt                           | 17,49:1   | 11,48:1   | Grafik AA                       |
| Lichtlinie auf Raum                            |           | 12,76:1   | Grafik AA                       |
| Lichtlinie auf Lichtband                       |           | 8,30:1    | Grafik AA                       |
| Lichtpunkt auf Blatt                           | 17,49:1   |           | Grafik AA                       |
| Knopfschrift auf Knopf                         | 17,49:1   | 16,37:1   | Text AA                         |
| Haarlinie auf Blatt                            | 1,52:1    | 1,41:1    | trägt allein keine Information  |
| Lichtband auf Blatt                            | 1,54:1    | 1,38:1    | trägt allein keine Information  |

Im Dunklen ist der Lichtpunkt gleich der Lichtlinie (11,48:1 auf Blatt). Haarlinie und Lichtband
liegen bewusst niedrig: die Haarlinie trennt nur, das Band liegt unter einer Tuschelinie, die selbst
den Kontrast trägt.

Bewusste Ausnahme aus dem Mockup: Solange ein Blatt im Schatten liegt, werden Nebenwege und ihre
Beschriftungen zum Blatt hin gedämpft, im Hellen bis etwa 3,3:1. Das ist die Aussage des Bildes;
die Sätze daneben bleiben voll. Diese Mischwerte rechnet die Karte, sie sind keine Tokens.

### Auslieferung

Die Anwendung liefert `components/` unter `/components/` aus (docs/Betrieb.md, Abschnitt Ordner
components/). Relative Adressen in `website.css` lösen sich gegen die Adresse der Datei auf, die
Schriften liegen also unter `/components/tokens/fonts/`. Die allgemeine Content-Security-Policy
nennt kein `font-src`; es gilt `default-src 'self'`, Schriften vom eigenen Ursprung sind damit
erlaubt. Die Dateiendung `.woff2` gehört noch in die Liste der ausgelieferten Typen in `Program.cs`
(`font/woff2`), sonst antwortet die Anwendung mit 404.

## Test

```
node --test "components/tokens/*.test.mjs"
```

`tokens.test.mjs` liest `tokens.css`, prüft für jede Rolle der Designsprache die Existenz und den
Wert der Variable in hell und dunkel, prüft das 4-px-Raster, die Dauern unter 500ms und ihre Null
bei „Bewegung reduzieren“, und rechnet jeden Kontrast dieser README nach. Mit Node 22 unter Windows
wird ein Ordner als Argument nicht aufgelöst, deshalb das Muster. Gleichwertig sind `node --test`
im Projektwurzelverzeichnis oder im Ordner `components/tokens`.

`website.test.mjs` liest `website.css` und prüft: jeder Name beginnt mit `--website-` und keiner
kommt aus `tokens.css`; jede Farbe hat hell und dunkel den Wert des Mockups, beide dunklen Blöcke
sind gleich; der Schatten hat keine Unschärfe; die Skala stimmt breit und schmal; die Strecken der
Kamera laufen aufsteigend; die drei `@font-face` zeigen mit `font-display: swap` auf vorhandene,
vollständige woff2-Dateien in `fonts/`, ohne externe Quelle, ohne data-URI und ohne den reservierten
Namen; `fonts/OFL.txt` liegt bei; jeder Kontrast der Tabelle oben stimmt.

## Demo

`demo.html` im Browser öffnen. Sie bindet nur `tokens.css` ein, zeigt Farbfelder, Schriftskala,
Abstände, Radien, Schatten und Bewegung, listet jede Variable mit dem berechneten Wert und hat einen
Umschalter für System, Hell und Dunkel. Für `website.css` gibt es noch keine eigene Demo; die Werte
zeigt das Mockup, aus dem sie stammen.
