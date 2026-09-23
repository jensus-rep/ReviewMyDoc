Herkunft: ReviewMyDoc

# Theme

Version 1.1.0. Die Hausfarben von ReviewMyDoc bilden ein reduziertes System aus Weiß, Anthrazit und
Dunkelblau. Im hellen Modus tragen weiße Flächen anthrazitfarbenen Text und dunkelblaue Akzente;
im dunklen Modus werden die Rollen mit zugänglichen Abstufungen derselben Farbfamilien umgekehrt.

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

| Rolle              | Bedeutung                           | Hell      | Dunkel    |
| ------------------ | ----------------------------------- | --------- | --------- |
| `--color-bg`       | Grund, kühles Weiß                  | `#f7f8fa` | `#24282d` |
| `--color-bg-2`     | Sekundärgrund                       | `#eef1f4` | `#2d333a` |
| `--color-hairline` | Haarlinie                           | `#d7dde3` | `#414a54` |
| `--color-ink`      | Text, Anthrazit bzw. Weiß           | `#24282d` | `#ffffff` |
| `--color-ink-2`    | Sekundärtext                        | `#59636e` | `#bcc5ce` |
| `--color-blue`     | Akzent, interaktiv: Dunkelblau      | `#123a5f` | `#9cc4e4` |
| `--color-red`      | Kritisch, innerhalb der Blaupalette | `#214d73` | `#b4d2e9` |

`--color-red` bleibt als technischer Rollenname aus Atelier bestehen, trägt in diesem Theme jedoch
bewusst eine blaue Abstufung. So bleibt die öffentliche Schnittstelle der kopierten Bausteine
stabil, während die sichtbare Oberfläche konsequent in der neuen Palette bleibt.

## Glänzende Fläche

Eine Fläche ist alles, was über dem Papier liegt: Karte, Dialog, Panel. Der Glanz ist ein Verlauf
und keine zweite Farbe, damit eine große Fläche ruhig bleibt und nur ihre Oberkante Licht fängt.

| Variable           | Wofür                                                        |
| ------------------ | ------------------------------------------------------------ |
| `--surface`        | die Flächenfarbe selbst, für Fälle ohne Verlauf              |
| `--surface-sheen`  | der Verlauf, als `background` einer Fläche                    |
| `--surface-edge`   | die Lichtkante, ein innerer Schatten an der Oberkante         |
| `--surface-shadow` | der Schatten unter der Fläche, zwei Lagen: Kontakt und Streu  |

Zusätzlich hebt `--paper-shadow` das Dokumentblatt stärker vom Hintergrund ab;
`--well-shadow` vertieft die Pipeline-Spalten dezent. Beide folgen dem hellen und dunklen Modus.

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

## Tonale Zustandspalette

Vier Abstufungen des Dunkelblaus für Zustände, nie für Fließtext. Die historischen Variablennamen
bleiben aus Kompatibilitätsgründen erhalten. Jede Abstufung kommt in zwei Formen vor:

- **kräftig** (`--art-<name>`) für eine Marke, einen Punkt, einen Balken oder eine kleine gefüllte
  Fläche. Text darauf ist `--art-on-solid`; der ältere Alias `--art-on-chrome` hat denselben Wert.
- **ruhig** (`--art-<name>-quiet`) für eine breite Fläche, die Text trägt, etwa eine
  Zustandsmarkierung in einer Liste. Text darauf ist immer `--ink`.

| Farbrolle              | kräftig hell | ruhig hell | kräftig dunkel | ruhig dunkel |
| ---------------------- | ------------ | ---------- | -------------- | ------------ |
| `--art-vermilion`      | `#0b2d4d`    | `#e7eef4`  | `#8fb9da`      | `#20384d`    |
| `--art-chrome`         | `#123a5f`    | `#e2ebf2`  | `#9bc3e1`      | `#233f58`    |
| `--art-malachite`      | `#1a466f`    | `#dce7f0`  | `#a9cce6`      | `#274661`    |
| `--art-plum`           | `#23537f`    | `#d6e3ed`  | `#b7d5eb`      | `#2b4d6a`    |

`--art-on-solid` und `--art-on-chrome` wechseln mit der Fassung (`#ffffff` hell, `#24282d`
dunkel), weil die kräftigen Farben im Dunkeln aufgehellt werden und dann anthrazitfarbenen Text
tragen.

Welcher Zustand welche Farbe bekommt, entscheidet nicht dieser Baustein, sondern die Seite, die ihn
verwendet. Farbe ist dabei nie das einzige Merkmal: Jeder Zustand trägt zusätzlich sein Wort.

## Kontraste

Berechnet nach WCAG 2.1, geprüft in `theme.test.mjs` mit derselben Formel. Jedes Paar erreicht AA
für Fließtext (4,5:1); anders als bei den Basistokens gibt es hier kein Paar, das nur für große
Schrift reicht.

| Paar                        | Hell  | Dunkel |
| --------------------------- | ----- | ------ |
| Tinte auf Grund             | 13,95 | 14,83  |
| Tinte auf Sekundärgrund     | 13,08 | 12,76  |
| Tinte auf Fläche            | 14,83 | 11,86  |
| Sekundärtext auf Grund      | 5,75  | 8,48   |
| Sekundärtext auf Sekundär   | 5,39  | 7,30   |
| Akzent auf Grund            | 11,00 | 8,07   |
| Akzent auf Fläche           | 11,69 | 6,46   |
| Kritisch auf Grund          | 8,33  | 9,42   |
| Kritisch auf Fläche         | 8,85  | 7,53   |

Dazu prüft der Test zwei Dinge, die sich aus der Verwendung ergeben und nicht aus der Tabelle: die
gefüllte primäre Schaltfläche, die `--bg` auf `--blue` schreibt, und jede Tonstufe in beiden
Formen mit dem Text, den die Regeln oben ihr zuweisen.

## Prüfen

```
node --test components/theme/
```

Der Test liest `theme.css` als Text und prüft, dass jede Rolle in allen drei Blöcken steht (hell,
Systemdunkel, erzwungenes Dunkel), dass die beiden Dunkelblöcke nicht auseinanderlaufen, dass hier
keine Größe, kein Abstand und keine Bewegung steht und dass jeder Kontrast der Tabelle oben mit den
Werten in der Datei wirklich herauskommt.
