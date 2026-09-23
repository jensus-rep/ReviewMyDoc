/*!
 * Markdown surface 1.0.0 · Atelier (Process 51) · tests
 * Checks the computing part of the component with node --test and without a browser.
 *
 * Two halves. The first is the test of the formatting core, copied from markdown-editor 1.0.0
 * together with the core itself, so the copy stays proven here and does not quietly drift from its
 * origin: where the caret stands, what the eight formats do to a selection, what they do when the
 * format is already there and has to go away again, what happens when nothing is selected, how a
 * list continues at a line break, and that the field only ever replaces the piece that really
 * changed.
 *
 * The second half is what this component adds: the marking up of the text for the layer behind the
 * field, the selection laid over it, and where the menu belongs.
 */

import { test } from 'node:test';
import assert from 'node:assert/strict';

// The specifier names the built module, like the test of hero-sequence: TypeScript reads the
// source next to it, node --test the build, and npm run build keeps the two identical.
import {
  applyFormat,
  continueList,
  lineAt,
  lineEndAt,
  lineStartAt,
  minimalEdit,
  toggleInline,
  type EditorState,
  type MarkdownFormat,
} from './markdown-surface.js';

/**
 * Reads a state out of a marked text: ‸ is the caret, « and » enclose a selection.
 * @param marked the text with its markers.
 * @returns the state without them.
 */
function state(marked: string): EditorState {
  const caret = marked.indexOf('‸');
  if (caret !== -1) {
    return { text: marked.replace('‸', ''), selectionStart: caret, selectionEnd: caret };
  }
  const open = marked.indexOf('«');
  const close = marked.indexOf('»');
  assert.ok(open !== -1 && close > open, `no caret and no selection in "${marked}"`);
  return { text: marked.replace('«', '').replace('»', ''), selectionStart: open, selectionEnd: close - 1 };
}

/**
 * Writes a state back as a marked text, so an expectation reads like what the writer sees.
 * @param value the state.
 * @returns the text with ‸ or « and ».
 */
function show(value: EditorState): string {
  const { text, selectionStart, selectionEnd } = value;
  if (selectionStart === selectionEnd) {
    return `${text.slice(0, selectionStart)}‸${text.slice(selectionStart)}`;
  }
  return `${text.slice(0, selectionStart)}«${text.slice(selectionStart, selectionEnd)}»${text.slice(selectionEnd)}`;
}

/**
 * Applies a format to a marked text.
 * @param marked the state before.
 * @param format the format of the bar.
 * @returns the marked state afterwards.
 */
function press(marked: string, format: MarkdownFormat): string {
  return show(applyFormat(state(marked), format));
}

test('the caret knows its line, on an empty line and at the end of the text as well', () => {
  const text = 'eins\n\ndrei';
  assert.deepEqual(lineAt(text, 0), { start: 0, end: 4, content: 'eins' });
  assert.deepEqual(lineAt(text, 4), { start: 0, end: 4, content: 'eins' });
  assert.deepEqual(lineAt(text, 5), { start: 5, end: 5, content: '' });
  assert.deepEqual(lineAt(text, 10), { start: 6, end: 10, content: 'drei' });
  // Offsets outside the text are not an error: a field may report a stale selection.
  assert.equal(lineStartAt(text, 99), 6);
  assert.equal(lineEndAt(text, -5), 4);
});

test('bold puts its markers around the selection and a second press takes them away', () => {
  assert.equal(press('Ein «Wort» hier', 'bold'), 'Ein **«Wort»** hier');
  // The selection now stands inside the pair, which is where the first press left it.
  assert.equal(press('Ein **«Wort»** hier', 'bold'), 'Ein «Wort» hier');
  // And the same when the writer selected the markers along with the word.
  assert.equal(press('Ein «**Wort**» hier', 'bold'), 'Ein «Wort» hier');
});

test('a format the caret stands inside goes away without selecting anything first', () => {
  assert.equal(press('Ein **Wo‸rt** hier', 'bold'), 'Ein Wo‸rt hier');
  assert.equal(press('Ein *Wo‸rt* hier', 'italic'), 'Ein Wo‸rt hier');
  assert.equal(press('Ein `Wo‸rt` hier', 'code'), 'Ein Wo‸rt hier');
});

test('without a selection bold opens an empty pair and cancels it on a second press', () => {
  assert.equal(press('Satz ‸', 'bold'), 'Satz **‸**');
  assert.equal(press('Satz **‸**', 'bold'), 'Satz ‸');
  assert.equal(press('Satz ‸', 'italic'), 'Satz *‸*');
});

test('whitespace at the edge of a selection stays outside the markers', () => {
  // "**Wort **" is no emphasis in Markdown, so the space must not end up inside the pair.
  assert.equal(press('Ein «Wort » hier', 'bold'), 'Ein **«Wort»**  hier');
  // A selection of nothing but spaces behaves like no selection at all.
  assert.equal(press('Ein «  » hier', 'bold'), 'Ein **‸**   hier');
});

test('italic inside bold nests instead of breaking the pair of stars', () => {
  assert.equal(press('«**Wort**»', 'italic'), '*«**Wort**»*');
  assert.equal(press('**Wo‸rt**', 'italic'), '**Wo*‸*rt**');
});

test('a heading brings another level to this one and only goes away at level two', () => {
  assert.equal(press('‸Titel', 'heading'), '## ‸Titel');
  assert.equal(press('#### Ti‸tel', 'heading'), '## Ti‸tel');
  assert.equal(press('## Ti‸tel', 'heading'), 'Ti‸tel');
});

test('list and quote take every line the selection touches and switch off again', () => {
  assert.equal(press('«eins\nzwei»', 'list'), '«- eins\n- zwei»');
  assert.equal(press('«- eins\n- zwei»', 'list'), '«eins\nzwei»');
  assert.equal(press('«> eins\n> zwei»', 'quote'), '«eins\nzwei»');
  // A star as list marker becomes the marker of this component instead of a second one.
  assert.equal(press('«* eins\nzwei»', 'list'), '«- eins\n- zwei»');
});

test('a line prefix keeps the indentation and leaves empty lines empty', () => {
  assert.equal(press('«  eins\nzwei»', 'list'), '«  - eins\n- zwei»');
  assert.equal(press('«eins\n\nzwei»', 'list'), '«- eins\n\n- zwei»');
  // A single empty line is the normal start of a list, so there it does get a marker.
  assert.equal(press('‸', 'list'), '- ‸');
});

test('a list continues at a line break and a number counts up', () => {
  assert.equal(show(continueList(state('- eins‸')) as EditorState), '- eins\n- ‸');
  assert.equal(show(continueList(state('2. zwei‸')) as EditorState), '2. zwei\n3. ‸');
  assert.equal(show(continueList(state('9. neun‸')) as EditorState), '9. neun\n10. ‸');
  assert.equal(show(continueList(state('  - tief‸')) as EditorState), '  - tief\n  - ‸');
  assert.equal(show(continueList(state('> zitat‸')) as EditorState), '> zitat\n> ‸');
  // In the middle of an item the rest of the line moves to the new item, as everywhere else.
  assert.equal(show(continueList(state('- eins‸zwei')) as EditorState), '- eins\n- ‸zwei');
});

test('a line break on an item that stayed empty ends the list instead of growing it', () => {
  assert.equal(show(continueList(state('- eins\n- ‸')) as EditorState), '- eins\n‸');
  assert.equal(show(continueList(state('1. eins\n2. ‸')) as EditorState), '1. eins\n‸');
});

test('outside a list the line break stays the one of the browser', () => {
  assert.equal(continueList(state('Fließtext‸')), null);
  assert.equal(continueList(state('## Titel‸')), null);
  // A minus without a space is not a list yet and must not be continued as one.
  assert.equal(continueList(state('-‸')), null);
});

test('a link takes an address as its target and otherwise waits for one', () => {
  // The selection is the label, the target is selected and the next keystroke replaces it.
  assert.equal(press('«ReviewMyDoc»', 'link'), '[ReviewMyDoc](«https://»)');
  // A pasted address belongs into the brackets; then the label is what still has to be typed.
  assert.equal(press('«https://example.org/doc»', 'link'), '[«Text»](https://example.org/doc)');
  assert.equal(press('Siehe ‸', 'link'), 'Siehe [«Text»](https://)');
});

test('a table becomes a block of its own, with the empty lines a table needs', () => {
  assert.equal(
    press('Absatz‸', 'table'),
    'Absatz\n\n| «Spalte» | Spalte |\n| --- | --- |\n|  |  |',
  );
  // On the empty line below a paragraph the table still keeps one empty line above it.
  assert.equal(
    press('Absatz\n‸', 'table'),
    'Absatz\n\n| «Spalte» | Spalte |\n| --- | --- |\n|  |  |',
  );
  // And it keeps one below, so the following paragraph does not end up inside the table.
  assert.equal(
    press('Eins‸\nZwei', 'table'),
    'Eins\n\n| «Spalte» | Spalte |\n| --- | --- |\n|  |  |\n\nZwei',
  );
});

test('code is backticks inside a line and a fence across lines', () => {
  assert.equal(press('Der «Befehl» hier', 'code'), 'Der `«Befehl»` hier');
  assert.equal(press('«eins\nzwei»', 'code'), '```\n«eins\nzwei»\n```');
  assert.equal(press('«```\neins\nzwei\n```»', 'code'), '«eins\nzwei»');
});

test('every format of the bar changes something, none of the eight is a dead button', () => {
  const formats: readonly MarkdownFormat[] = [
    'heading',
    'bold',
    'italic',
    'list',
    'table',
    'quote',
    'link',
    'code',
  ];
  for (const format of formats) {
    const before = state('Ein «Wort» hier');
    const after = applyFormat(before, format);
    assert.notEqual(after.text, before.text, `${format} changed nothing`);
    assert.ok(after.selectionStart <= after.selectionEnd, `${format} returned a reversed selection`);
    assert.ok(after.selectionEnd <= after.text.length, `${format} left the selection outside the text`);
  }
});

test('a selection reported backwards is read in reading order', () => {
  const backwards: EditorState = { text: 'Ein Wort hier', selectionStart: 8, selectionEnd: 4 };
  assert.equal(show(toggleInline(backwards, '**')), 'Ein **«Wort»** hier');
});

test('the field replaces only the piece that really changed', () => {
  assert.deepEqual(minimalEdit('Ein Wort hier', 'Ein **Wort** hier'), {
    start: 4,
    end: 8,
    insert: '**Wort**',
  });
  assert.deepEqual(minimalEdit('Ein **Wort** hier', 'Ein Wort hier'), {
    start: 4,
    end: 12,
    insert: 'Wort',
  });
  assert.deepEqual(minimalEdit('Wort', 'Wort'), { start: 4, end: 4, insert: '' });
  assert.deepEqual(minimalEdit('Wor', 'Wort'), { start: 3, end: 3, insert: 't' });
});

// What the surface adds: the layer behind the field, the selection on it, and the menu.

import { highlight, menuPosition, withSelection } from './markdown-surface.js';
import type { SurfaceToken } from './markdown-surface.js';

/** The tokens as pairs of text and marks, which is how the expectations below read best. */
function pieces(text: string): [string, string][] {
  return highlight(text).map((token) => [text.slice(token.start, token.end), token.marks.join(' ')]);
}

/** The invariant the congruence of the layer rests on, checked on every text the tests use. */
function assertCovers(text: string, tokens: readonly SurfaceToken[]): void {
  let next = 0;
  for (const token of tokens) {
    assert.equal(token.start, next, `token starts at ${token.start}, expected ${next}`);
    assert.ok(token.end > token.start, 'a token is empty');
    next = token.end;
  }
  assert.equal(next, text.length, 'the tokens do not reach the end of the text');
  assert.equal(tokens.map((token) => text.slice(token.start, token.end)).join(''), text);
}

const SAMPLE = [
  '# Gutachten',
  '',
  'Ein Absatz mit **Fettem**, *Kursivem*, `Code` und einem [Link](https://example.org).',
  '',
  '## Ausgangslage',
  '',
  '- erster Punkt',
  '- zweiter Punkt mit **Fettem**',
  '',
  '> Ein Zitat.',
  '',
  '| Spalte | Wert |',
  '| --- | --- |',
  '| eins | zwei |',
  '',
  '```sql',
  'select 1 -- kein **Fettes** hier',
  '```',
  '',
].join('\n');

test('the layer writes the same characters as the field, in the same order', () => {
  assertCovers(SAMPLE, highlight(SAMPLE));
  for (const text of ['', 'a', '\n', '\n\n', 'ohne Auszeichnung', '**', '*', '`', '[]()']) {
    assertCovers(text, highlight(text));
  }
});

test('a heading is its hashes and its text', () => {
  assert.deepEqual(pieces('## Titel'), [
    ['## ', 'heading marker'],
    ['Titel', 'heading'],
  ]);
});

test('bold inside a heading keeps both marks, the stars keep all three', () => {
  assert.deepEqual(pieces('## Ein **fettes** Wort'), [
    ['## ', 'heading marker'],
    ['Ein ', 'heading'],
    ['**', 'heading strong marker'],
    ['fettes', 'heading strong'],
    ['**', 'heading strong marker'],
    [' Wort', 'heading'],
  ]);
});

test('an unclosed marker stays plain text, as Markdown reads it', () => {
  assert.deepEqual(pieces('**offen geblieben'), [['**offen geblieben', '']]);
  assert.deepEqual(pieces('ein `offenes Stueck'), [['ein `offenes Stueck', '']]);
  assert.deepEqual(pieces('[Text](ohne Klammer'), [['[Text](ohne Klammer', '']]);
});

test('a single star is italic and a double one is not mistaken for it', () => {
  assert.deepEqual(pieces('*schraeg*'), [
    ['*', 'emphasis marker'],
    ['schraeg', 'emphasis'],
    ['*', 'emphasis marker'],
  ]);
  assert.deepEqual(pieces('**fett**'), [
    ['**', 'strong marker'],
    ['fett', 'strong'],
    ['**', 'strong marker'],
  ]);
});

test('an underscore is only italic at a word edge', () => {
  assert.deepEqual(pieces('_schraeg_'), [
    ['_', 'emphasis marker'],
    ['schraeg', 'emphasis'],
    ['_', 'emphasis marker'],
  ]);
  assert.deepEqual(pieces('snake_case_name'), [['snake_case_name', '']]);
});

test('a link separates its label from its target', () => {
  assert.deepEqual(pieces('[Text](https://example.org)'), [
    ['[', 'link marker'],
    ['Text', 'link'],
    ['](', 'link marker'],
    ['https://example.org', 'url'],
    [')', 'link marker'],
  ]);
});

test('a list item and a quote are their marker and their line', () => {
  assert.deepEqual(pieces('- ein Punkt'), [
    ['- ', 'list marker'],
    ['ein Punkt', 'list'],
  ]);
  assert.deepEqual(pieces('1. ein Punkt'), [
    ['1. ', 'list marker'],
    ['ein Punkt', 'list'],
  ]);
  assert.deepEqual(pieces('> ein Zitat'), [
    ['> ', 'quote marker'],
    ['ein Zitat', 'quote'],
  ]);
});

test('a table row marks its bars, and the dashed row is marker throughout', () => {
  assert.deepEqual(pieces('| eins | zwei |'), [
    ['|', 'table marker'],
    [' eins ', 'table'],
    ['|', 'table marker'],
    [' zwei ', 'table'],
    ['|', 'table marker'],
  ]);
  // The whole dashed row is one piece: neighbours carrying the same marks are joined, so the
  // layer gets as few spans as possible.
  assert.deepEqual(pieces('| --- | :-: |'), [['| --- | :-: |', 'table marker']]);
});

test('inside a fence nothing is formatted, and the fence itself closes again', () => {
  const text = '```js\nlet a = **1**;\n```\n**danach**';
  assert.deepEqual(pieces(text), [
    ['```', 'code marker'],
    ['js', 'code'],
    ['\n', ''],
    ['let a = **1**;', 'code'],
    ['\n', ''],
    ['```', 'code marker'],
    ['\n', ''],
    ['**', 'strong marker'],
    ['danach', 'strong'],
    ['**', 'strong marker'],
  ]);
});

test('a line break belongs to no block, so the next line starts clean', () => {
  const tokens = highlight('## Titel\nAbsatz');
  assertCovers('## Titel\nAbsatz', tokens);
  assert.deepEqual(pieces('## Titel\nAbsatz'), [
    ['## ', 'heading marker'],
    ['Titel', 'heading'],
    // The line break carries no mark, and neither does the paragraph, so the two are one piece.
    ['\nAbsatz', ''],
  ]);
});

test('the selection splits the tokens it falls inside and marks only its own piece', () => {
  const text = '## Titel';
  const tokens = withSelection(highlight(text), 3, 6);
  assertCovers(text, tokens);
  assert.deepEqual(
    tokens.map((token) => [text.slice(token.start, token.end), token.marks.join(' ')]),
    [
      ['## ', 'heading marker'],
      ['Tit', 'heading selection'],
      ['el', 'heading'],
    ],
  );
});

test('an empty selection changes nothing, a reversed one is read in order', () => {
  const tokens = highlight('## Titel');
  assert.equal(withSelection(tokens, 4, 4), tokens);
  assert.deepEqual(withSelection(tokens, 6, 3), withSelection(tokens, 3, 6));
});

test('the menu stands centred over the selection', () => {
  const place = menuPosition(
    { left: 100, top: 100, width: 40, height: 20 },
    { width: 200, height: 30 },
    { left: 0, top: 0, width: 600, height: 400 },
    8,
  );
  assert.deepEqual(place, { left: 20, top: 62, above: true });
});

test('the menu stays inside its frame on both sides', () => {
  const menu = { width: 200, height: 30 };
  const bounds = { left: 0, top: 0, width: 600, height: 400 };
  const atLeftEdge = menuPosition({ left: 0, top: 200, width: 10, height: 20 }, menu, bounds, 8);
  assert.equal(atLeftEdge.left, 0);
  const atRightEdge = menuPosition({ left: 590, top: 200, width: 10, height: 20 }, menu, bounds, 8);
  assert.equal(atRightEdge.left, 400);
});

test('the menu moves underneath when there is no room over the selection', () => {
  const place = menuPosition(
    { left: 100, top: 4, width: 40, height: 20 },
    { width: 200, height: 30 },
    { left: 0, top: 0, width: 600, height: 400 },
    8,
  );
  assert.equal(place.above, false);
  assert.equal(place.top, 32);
});

test('a menu wider or higher than its frame still starts inside it', () => {
  const place = menuPosition(
    { left: 10, top: 10, width: 40, height: 20 },
    { width: 900, height: 500 },
    { left: 0, top: 0, width: 600, height: 400 },
    8,
  );
  assert.deepEqual(place, { left: 0, top: 0, above: false });
});
