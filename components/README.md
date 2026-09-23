# Bausteine

Wiederverwendbare Bausteine für die Oberfläche von ReviewMyDoc. Jeder Baustein steht für sich:
eigener Ordner, eigene CSS-Datei, TypeScript nur wenn nötig, keine Abhängigkeit zu anderen
Bausteinen, kein Framework. Jeder hat eine README und eine `demo.html`, die sich direkt im Browser
öffnen lässt. `ReviewMyDoc.Web` liefert diesen Ordner unter `/components/` aus, siehe
[docs/Konventionen.md](../docs/Konventionen.md), Abschnitt Struktur.

Vorhanden sind `tokens`, `button`, `field`, `rows`, `busy-button`, `busy-region`, `progress-line`,
`skeleton`, `loading-dots`, `dashboard` und `markdown-surface`, alle aus Atelier übernommen, und
`theme`, hier entstanden. `markdown-surface` ist die Schreibfläche für Dokumente: ein
Textfeld mit einer deckungsgleichen Schicht dahinter, die den Text beim Tippen auszeichnet, und
einem Menü, das nur an der Markierung erscheint. Es ist der erste übernommene Baustein mit
TypeScript, also der erste, für den `npm test` und `npm run build` hier wirklich etwas zu tun
haben. `theme` ist kein Element der Oberfläche, sondern die Schicht mit den Hausfarben von
ReviewMyDoc: Es schreibt die Farbrollen aus `tokens` neu und lässt alles andere daran unberührt.
Deshalb wird es unmittelbar nach `tokens` geladen und vor allen übrigen Bausteinen, siehe
[theme/README.md](theme/README.md).

Die Dokumentenansicht verwendet inzwischen den hier entstandenen
[document-editor](document-editor/README.md): direkt formatierte Textbearbeitung, Speichern und
Reviewaktionen auf Markierungen. `markdown-surface` bleibt als unveränderte Atelier-Komponente
vorhanden, wird aber nicht für den neuen dokumentorientierten Ablauf verwendet.

Zwei Dinge fallen beim Lesen der übernommenen Bausteine auf, beide sind bekannt und bewusst nicht
geändert, weil an einer Kopie nichts geändert wird außer der Herkunftszeile:

- Die READMEs einiger Bausteine verweisen auf `docs/Design/…` und auf Mockups. Diese Pfade gibt es
  nur in Atelier. Wohin sie zeigen, sagt die Herkunftszeile in Zeile eins derselben Datei. Aus
  demselben Grund verweist `markdown-surface/README.md` auf `markdown-editor`, seinen Vorgänger, den es
  hier nie gab: Er ist in Atelier geblieben, weil diese Anwendung Feld und Vorschau nicht
  nebeneinander zeigt.
- `button/demo.html` und `busy-button/demo.html` binden jeweils das CSS des anderen ein, um die
  Kombination zu zeigen. Die Bausteine selbst hängen nicht voneinander ab, nur diese beiden
  Vorführseiten.

## Regeln

Es gelten dieselben Regeln wie in Atelier `components/README.md`, von dort übernommen:

- Klassen nach BEM mit dem Bausteinnamen als Präfix, zum Beispiel `busy-button__spinner`,
  `busy-button--disabled`. Zustände als `is-…` oder über ARIA-Attribute (`aria-busy`,
  `aria-selected`).
- Einstellbares als CSS-Variablen mit Bausteinpräfix. Standardwerte stehen in `:where(…)`, damit
  jede Seite sie ohne Spezifitätskampf überschreiben kann.
- Ruhig: keine automatischen Abläufe, kein Nachfedern, kurze Übergänge. Jeder Baustein
  berücksichtigt „Bewegung reduzieren“ (`prefers-reduced-motion`).
- Versionsnummer im Kopf der Datei und in der README des Bausteins.
- Design-Tokens kommen aus Atelier und sind die einzige Quelle für Schrift, Abstände, Radien und
  Bewegung; die Farbwerte darin gelten in der Fassung, die `theme` daraus macht. Kein Baustein und
  keine Seite verwendet Rohwerte.
- Ein Baustein wird erst in eine Seite eingebaut, wenn seine `demo.html` steht.

## Herkunft

Die README jedes Bausteins nennt in ihrer ersten Zeile, auf welchem der beiden Wege er entstanden
ist, siehe [docs/Konventionen.md](../docs/Konventionen.md), Abschnitt „Bausteine und ihre
Herkunft“:

- **Aus Atelier kopiert.** Erste Zeile
  `Herkunft: Atelier components/<name>, Stand <Datum>, Commit <sha>`. Geändert wird dann in
  Atelier und neu kopiert, nicht hier gepflegt.
- **Hier entstanden.** Erste Zeile `Herkunft: ReviewMyDoc`. Ist der Baustein auch außerhalb dieses
  Projekts sinnvoll, wird er nach Fertigstellung nach Atelier zurückgegeben und die Zeile
  umgeschrieben.

## Werkzeugkette

TypeScript in Bausteinen ist `strict`, ohne Abhängigkeiten außer der DOM-Typisierung.
Rechenlogik steht in eigenen Funktionen ohne DOM-Zugriff, damit sie mit `node --test` ohne Browser
prüfbar ist. Aus dieser Vorgabe übernommen aus Atelier: `package.json`, `tsconfig.json` und
`scripts/build-components.mjs` im Repositorywurzelverzeichnis.

| Befehl               | Prüft                                                                                |
| -------------------- | ------------------------------------------------------------------------------------- |
| `npm install`        | Werkzeuge installieren: `esbuild`, `typescript`, `@types/node`                        |
| `npm run typecheck`  | `tsc --noEmit` über `components/**/*.ts` und das Build-Skript unter `scripts/`        |
| `npm test`           | `node --test` über die Bausteintests: `components/**/*.test.mjs` und `components/**/*.test.ts` |
| `npm run build`      | esbuild erzeugt aus jeder `components/<name>/<name>.ts` das ES-Modul `<name>.js` daneben, committet, damit `demo.html` und `/components/` ohne Werkzeug funktionieren |

Nach `npm run build` prüft `git diff --exit-code components/`, dass die eingecheckten `.js`-Dateien
zum TypeScript-Quelltext passen. Dieser Befehl läuft im Build-Job der Pipeline, nicht lokal von
Hand.

Zwei Eigenheiten der Umgebung, aus Atelier übernommen, damit die Befehle überall gleich laufen:

- Die Muster stehen in Anführungszeichen (`node --test "components/**/*.test.mjs"`). Unter Windows
  löst die Shell sie sonst selbst auf, und Node bekommt kein Muster zu sehen.
- `npm test` läuft mit `--experimental-strip-types`, weil Node TypeScript-Testdateien sonst nicht
  ausführt. Eine Testdatei importiert ihren Baustein mit Endung (`./name.ts`), weil Node den Pfad
  nicht umschreibt.
