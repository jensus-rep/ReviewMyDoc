Herkunft: ReviewMyDoc

# Theme

Version 1.0.0. Die Hausfarben von ReviewMyDoc: ein warmes, glänzendes Weiß als Grund, Graphit statt
Schwarz als Tinte, eine Aubergine als Akzent und vier flache Kunstfarben für Zustände. Das Dunkel
ist kein Schwarz, sondern ein warmes Graphit, damit die glänzenden Flächen darüber noch als Schicht
lesbar sind.

Der Baustein ist kein Element der Oberfläche, sondern eine Schicht über
[tokens](../tokens/README.md): Er schreibt die sieben Farbrollen aus `tokens.css` neu und lässt
Schrift, Größen, Abstände, Radien und Bewegung unangetastet. Jeder andere Baustein und jede Seite
liest weiterhin nur die Tokennamen; keiner von ihnen weiß, dass es diese Datei gibt.

## Einbinden

Unmittelbar nach den Tokens, vor allen anderen Bausteinen.

```html
<link rel="stylesheet" href="/components/tokens/tokens.css">
<link rel="stylesheet" href="/components/theme/theme.css">
```

Die Reihenfolge ist keine Geschmacksfrage: Beide Dateien schreiben auf `:root`, und bei gleicher
Spezifität gewinnt die spätere. `theme.css` setzt außerdem ausschließlich die Rollen
(`--color-bg`, `--color-ink`, …) und nie die kurzen Aliasnamen (`--bg`, `--ink`, …), weil
`tokens.css` die Aliasse mit `var()` an die Rollen bindet und diese Umleitung sonst zerrisse.

Hell und Dunkel folgen dem System, wie bei den Tokens. Eine Seite erzwingt eine Fassung mit
`data-theme="dark"` oder `data-theme="light"` auf `<html>`.

## Farben

| Rolle              | Bedeutung                     | Hell      | Dunkel    |
| ------------------ | ----------------------------- | --------- | --------- |
| `--color-bg`       | Grund, warmes Papierweiß      | `#f8f6f3` | `#191719` |
| `--color-bg-2`     | Sekundärgrund, vertieft       | `#edeae4` | `#221f23` |
| `--color-hairline` | Haarlinie                     | `#e2ded6` | `#332f34` |
| `--color-ink`      | Text                          | `#17161b` | `#f3f0ec` |
| `--color-ink-2`    | Sekundärtext                  | `#605d57` | `#a39e99` |
| `--color-blue`     | Akzent, interaktiv: Aubergine | `#6b2d5c` | `#d891c2` |
| `--color-red`      | Kritisch: Zinnober            | `#b4311c` | `#ff7d64` |

Zwei Namen lesen sich schief und bleiben trotzdem: `--color-blue` trägt eine Aubergine und
`--color-red` den Zinnober der Kunstpalette. Die Namen stammen aus Atelier und bezeichnen dort die
Rollen „Akzent, interaktiv" und „kritisch". Sie umzubenennen hieße, jeden kopierten Baustein
anzufassen, und an einer Kopie wird nichts geändert.

## Glänzende Fläche

Eine Fläche ist alles, was über dem Papier liegt: Karte, Dialog, Panel. Der Glanz ist ein Verlauf
und keine zweite Farbe, damit eine große Fläche ruhig bleibt und nur ihre Oberkante Licht fängt.

| Variable           | Wofür                                                        |
| ------------------ | ------------------------------------------------------------ |
| `--surface`        | die Flächenfarbe selbst, für Fälle ohne Verlauf              |
| `--surface-sheen`  | der Verlauf, als `background` einer Fläche                    |
| `--surface-edge`   | die Lichtkante, ein innerer Schatten an der Oberkante         |
| `--surface-shadow` | der Schatten unter der Fläche, zwei Lagen: Kontakt und Streu  |

Die vier Zeilen zusammen sind die Klasse `.surface`, das einzige Stück Aussehen, das dieser
Baustein selbst mitbringt:

```css
.surface {
  border: var(--hairline-width) solid var(--hairline);
  border-radius: var(--radius-m);
  background: var(--surface-sheen);
  box-shadow: var(--surface-edge), var(--surface-shadow);
}
```

Anders als `--shadow-float` aus den Tokens, der nur für schwebende Schichten wie Dialog und Menü
gedacht ist, darf `--surface-shadow` auch eine Karte im Fluss tragen. Das ist der bewusste
Unterschied zwischen dem matten Grundsystem und diesem Theme.

## Kunstpalette

Vier flache Farben für Zustände, nie für Fließtext. Jede kommt in zwei Formen vor:

- **kräftig** (`--art-<name>`) für eine Marke, einen Punkt, einen Balken oder eine kleine gefüllte
  Fläche. Text darauf ist `--art-on-solid`; die Ausnahme ist Chromgelb, das in beiden Fassungen
  hell ist und deshalb `--art-on-chrome` trägt.
- **ruhig** (`--art-<name>-quiet`) für eine breite Fläche, die Text trägt, etwa eine
  Zustandsmarkierung in einer Liste. Text darauf ist immer `--ink`.

| Kunstfarbe             | kräftig hell | ruhig hell | kräftig dunkel | ruhig dunkel |
| ---------------------- | ------------ | ---------- | -------------- | ------------ |
| `--art-vermilion`      | `#b4311c`    | `#f7e3dd`  | `#ff7d64`      | `#43221c`    |
| `--art-chrome`         | `#d99400`    | `#f7ecd2`  | `#e8b04a`      | `#3c2f16`    |
| `--art-malachite`      | `#0f6f4e`    | `#dceee6`  | `#4fbf92`      | `#16352b`    |
| `--art-plum`           | `#6b2d5c`    | `#efdfea`  | `#d891c2`      | `#37203a`    |

`--art-on-solid` wechselt mit der Fassung (`#fbf9f6` hell, `#17161b` dunkel), weil die kräftigen
Farben im Dunkeln die aufgehellte Form sind und dann dunklen Text tragen. `--art-on-chrome` bleibt
`#17161b`, weil Chromgelb in beiden Fassungen hell ist.

Welcher Zustand welche Farbe bekommt, entscheidet nicht dieser Baustein, sondern die Seite, die ihn
verwendet. Farbe ist dabei nie das einzige Merkmal: Jeder Zustand trägt zusätzlich sein Wort.

## Kontraste

Berechnet nach WCAG 2.1, geprüft in `theme.test.mjs` mit derselben Formel. Jedes Paar erreicht AA
für Fließtext (4,5:1); anders als bei den Basistokens gibt es hier kein Paar, das nur für große
Schrift reicht.

| Paar                        | Hell  | Dunkel |
| --------------------------- | ----- | ------ |
| Tinte auf Grund             | 16,68 | 15,69  |
| Tinte auf Sekundärgrund     | 14,99 | 14,35  |
| Tinte auf Fläche            | 17,99 | 14,17  |
| Sekundärtext auf Grund      | 6,08  | 6,71   |
| Sekundärtext auf Sekundär   | 5,46  | 6,14   |
| Akzent auf Grund            | 9,03  | 7,43   |
| Akzent auf Fläche           | 9,74  | 6,71   |
| Kritisch auf Grund          | 5,73  | 7,11   |
| Kritisch auf Fläche         | 6,18  | 6,41   |

Dazu prüft der Test zwei Dinge, die sich aus der Verwendung ergeben und nicht aus der Tabelle: die
gefüllte primäre Schaltfläche, die `--bg` auf `--blue` schreibt, und jede Kunstfarbe in beiden
Formen mit dem Text, den die Regeln oben ihr zuweisen.

## Prüfen

```
node --test components/theme/
```

Der Test liest `theme.css` als Text und prüft, dass jede Rolle in allen drei Blöcken steht (hell,
Systemdunkel, erzwungenes Dunkel), dass die beiden Dunkelblöcke nicht auseinanderlaufen, dass hier
keine Größe, kein Abstand und keine Bewegung steht und dass jeder Kontrast der Tabelle oben mit den
Werten in der Datei wirklich herauskommt.
