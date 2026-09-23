/*!
 * Markdown surface 1.0.0 · Atelier (Process 51)
 * A writing surface for Markdown: one text field, behind it a second layer that shows the same
 * text marked up, congruent to the character. Tools appear only at the selection; without a
 * selection there is no bar to see. The component knows neither an application nor a server. It
 * takes text, reports text back, and reports when somebody chose the extra entry of the menu,
 * together with the beginning and the end of the selection.
 *
 * Everything that only computes text, marks and offsets stands in pure functions below, without
 * any access to the DOM, so node --test checks them without a browser.
 *
 * The formatting core - the eight formats, the line rules, the smallest replacement - is a copy of
 * markdown-editor 1.0.0. A component in this folder does not depend on another one, so it is
 * copied rather than imported. It is unchanged apart from this note.
 */
/** The content of a text field together with its selection, as the pure functions see it. */
export interface EditorState {
  /** The complete text of the field. */
  readonly text: string;
  /** Offset where the selection starts; equal to selectionEnd when only a caret stands there. */
  readonly selectionStart: number;
  /** Offset where the selection ends. */
  readonly selectionEnd: number;
}

/** The formats the bar offers. A document under review carries no other formatting. */
export type MarkdownFormat =
  | 'heading'
  | 'bold'
  | 'italic'
  | 'list'
  | 'table'
  | 'quote'
  | 'link'
  | 'code';

/** One line of the text with its boundaries, the answer to "where does the caret stand". */
export interface LinePosition {
  /** Offset of the first character of the line. */
  readonly start: number;
  /** Offset behind the last character of the line, the line break itself excluded. */
  readonly end: number;
  /** The line without its line break. */
  readonly content: string;
}

/** The smallest replacement that turns one text into another. */
export interface TextEdit {
  /** Offset where the replaced piece starts. */
  readonly start: number;
  /** Offset where the replaced piece ends. */
  readonly end: number;
  /** What takes its place. */
  readonly insert: string;
}

const BOLD_MARKER = '**';
const ITALIC_MARKER = '*';
const CODE_MARKER = '`';
const FENCE = '```';
const LINK_LABEL_PLACEHOLDER = 'Text';
const LINK_TARGET_PLACEHOLDER = 'https://';
const TABLE_SKELETON = '| Spalte | Spalte |\n| --- | --- |\n|  |  |';

/** A prefix that belongs to whole lines: heading, list item, quote. */
export interface LinePrefixRule {
  /** What a line gets. */
  readonly insert: string;
  /** Matches the prefix a line already has, group 1 the indentation that survives a removal. */
  readonly detect: RegExp;
  /** Matches the prefix this rule itself writes; only such a line is switched off again. */
  readonly exact: RegExp;
}

const HEADING_RULE: LinePrefixRule = {
  insert: '## ',
  detect: /^([ \t]*)#{1,6}[ \t]+/,
  exact: /^([ \t]*)##[ \t]/,
};

const LIST_RULE: LinePrefixRule = {
  insert: '- ',
  detect: /^([ \t]*)[-*+][ \t]+/,
  exact: /^([ \t]*)-[ \t]/,
};

const QUOTE_RULE: LinePrefixRule = {
  insert: '> ',
  detect: /^([ \t]*)>[ \t]?/,
  exact: /^([ \t]*)>/,
};

/** A list item or a quote line: indentation, marker, spacing, rest. */
const LIST_ITEM = /^([ \t]*)([-*+]|\d+\.|>)([ \t]+)(.*)$/;

/** A selection that is already a link target and therefore belongs into the brackets, not before. */
const URL_LIKE = /^(?:https?:\/\/|mailto:|www\.)\S+$/;

function clamp(value: number, low: number, high: number): number {
  return Math.min(Math.max(value, low), high);
}

/** Selection boundaries in reading order, clamped to the text; a field may report them reversed. */
function ordered(state: EditorState): { readonly start: number; readonly end: number } {
  const first = clamp(state.selectionStart, 0, state.text.length);
  const second = clamp(state.selectionEnd, 0, state.text.length);
  return { start: Math.min(first, second), end: Math.max(first, second) };
}

/**
 * Offset of the beginning of the line the given offset stands in.
 * @param text the complete text.
 * @param index an offset inside it.
 * @returns the offset of the first character of that line.
 */
export function lineStartAt(text: string, index: number): number {
  return text.lastIndexOf('\n', clamp(index, 0, text.length) - 1) + 1;
}

/**
 * Offset of the end of the line the given offset stands in, the line break excluded.
 * @param text the complete text.
 * @param index an offset inside it.
 * @returns the offset behind the last character of that line.
 */
export function lineEndAt(text: string, index: number): number {
  const found = text.indexOf('\n', clamp(index, 0, text.length));
  return found === -1 ? text.length : found;
}

/**
 * The line an offset stands in.
 * @param text the complete text.
 * @param index an offset inside it.
 * @returns boundaries and content of the line.
 */
export function lineAt(text: string, index: number): LinePosition {
  const start = lineStartAt(text, index);
  const end = lineEndAt(text, index);
  return { start, end, content: text.slice(start, end) };
}

/** The run of one repeated character an index stands in, so ** is never mistaken for a single *. */
function runAt(text: string, index: number, character: string): { start: number; length: number } {
  let start = index;
  while (start > 0 && text[start - 1] === character) start -= 1;
  let end = index;
  while (end < text.length && text[end] === character) end += 1;
  return { start, length: end - start };
}

/** True when the marker stands at the index and is neither shorter nor part of a longer run. */
function isExactMarker(text: string, index: number, marker: string): boolean {
  if (index < 0 || !text.startsWith(marker, index)) return false;
  const run = runAt(text, index, marker.charAt(0));
  return run.start === index && run.length === marker.length;
}

/** The nearest marker before an offset, not further back than the limit; -1 when there is none. */
function findMarkerBefore(text: string, from: number, marker: string, limit: number): number {
  for (let index = from - marker.length; index >= limit; index -= 1) {
    if (isExactMarker(text, index, marker)) return index;
  }
  return -1;
}

/** The nearest marker at or behind an offset, not beyond the limit; -1 when there is none. */
function findMarkerAfter(text: string, from: number, marker: string, limit: number): number {
  for (let index = from; index + marker.length <= limit; index += 1) {
    if (isExactMarker(text, index, marker)) return index;
  }
  return -1;
}

/**
 * Puts an inline format around the selection or takes it away again. Already formatted text loses
 * its markers instead of collecting a second pair, whitespace at the edges of the selection stays
 * outside the markers, and a bare caret opens an empty pair and stands between its halves.
 * @param state text and selection before the change.
 * @param marker the markers, for instance ** for bold.
 * @returns text and selection afterwards.
 */
export function toggleInline(state: EditorState, marker: string): EditorState {
  const { text } = state;
  const { start, end } = ordered(state);
  const width = marker.length;

  if (start === end) {
    // An empty pair the caret stands in is the press before this one, taken back.
    if (start >= width && text.startsWith(marker, start - width) && text.startsWith(marker, start)) {
      return {
        text: text.slice(0, start - width) + text.slice(start + width),
        selectionStart: start - width,
        selectionEnd: start - width,
      };
    }
    const before = findMarkerBefore(text, start, marker, lineStartAt(text, start));
    const after = findMarkerAfter(text, start, marker, lineEndAt(text, start));
    if (before >= 0 && after >= 0) {
      const stripped =
        text.slice(0, before) + text.slice(before + width, after) + text.slice(after + width);
      const caret = start - width;
      return { text: stripped, selectionStart: caret, selectionEnd: caret };
    }
    const opened = text.slice(0, start) + marker + marker + text.slice(start);
    return { text: opened, selectionStart: start + width, selectionEnd: start + width };
  }

  const selected = text.slice(start, end);
  if (
    selected.length >= 2 * width &&
    isExactMarker(text, start, marker) &&
    isExactMarker(text, end - width, marker)
  ) {
    const inner = selected.slice(width, selected.length - width);
    return {
      text: text.slice(0, start) + inner + text.slice(end),
      selectionStart: start,
      selectionEnd: start + inner.length,
    };
  }
  if (isExactMarker(text, start - width, marker) && isExactMarker(text, end, marker)) {
    return {
      text: text.slice(0, start - width) + selected + text.slice(end + width),
      selectionStart: start - width,
      selectionEnd: end - width,
    };
  }

  const leading = selected.length - selected.trimStart().length;
  const trailing = selected.length - selected.trimEnd().length;
  const core = selected.slice(leading, selected.length - trailing);
  if (core.length === 0) {
    return toggleInline({ text, selectionStart: start, selectionEnd: start }, marker);
  }
  const coreStart = start + leading;
  const wrapped =
    text.slice(0, coreStart) + marker + core + marker + text.slice(coreStart + core.length);
  return {
    text: wrapped,
    selectionStart: coreStart + width,
    selectionEnd: coreStart + width + core.length,
  };
}

/** One line whose first characters are replaced, as the offset mapping needs it. */
interface LineEdit {
  readonly lineStart: number;
  readonly removed: number;
  readonly added: number;
}

/**
 * Where an offset of the old text stands in the new one, after prefixes changed before it.
 * An offset that sat exactly on the beginning of a line has two right answers: a caret belongs
 * behind the new prefix, so that typing continues the line, while the beginning of a selection
 * belongs in front of it, so that the selected block stays selected.
 */
function mapOffset(offset: number, edits: readonly LineEdit[], keepAtLineStart: boolean): number {
  let delta = 0;
  for (const edit of edits) {
    if (offset < edit.lineStart) break;
    if (offset === edit.lineStart && keepAtLineStart) return offset + delta;
    if (offset <= edit.lineStart + edit.removed) return edit.lineStart + delta + edit.added;
    delta += edit.added - edit.removed;
  }
  return offset + delta;
}

/** Rewrites the first characters of every line the selection touches and carries the offsets along. */
function rewriteLines(
  state: EditorState,
  rewrite: (line: string) => { readonly removed: number; readonly insert: string },
): EditorState {
  const { text } = state;
  const { start, end } = ordered(state);
  const blockStart = lineStartAt(text, start);
  const blockEnd = lineEndAt(text, end);
  const lines = text.slice(blockStart, blockEnd).split('\n');

  const edits: LineEdit[] = [];
  const rebuilt: string[] = [];
  let lineStart = blockStart;
  for (const line of lines) {
    const change = rewrite(line);
    edits.push({ lineStart, removed: change.removed, added: change.insert.length });
    rebuilt.push(change.insert + line.slice(change.removed));
    lineStart += line.length + 1;
  }

  return {
    text: text.slice(0, blockStart) + rebuilt.join('\n') + text.slice(blockEnd),
    selectionStart: mapOffset(start, edits, start < end),
    selectionEnd: mapOffset(end, edits, false),
  };
}

/**
 * Puts a line prefix in front of every line the selection touches or takes it away again. Lines
 * that already carry another level or another marker are brought to this one, empty lines in a
 * block stay empty, and indentation survives.
 * @param state text and selection before the change.
 * @param rule the prefix and how an existing one is recognised.
 * @returns text and selection afterwards.
 */
export function toggleLinePrefix(state: EditorState, rule: LinePrefixRule): EditorState {
  const { text } = state;
  const { start, end } = ordered(state);
  const lines = text.slice(lineStartAt(text, start), lineEndAt(text, end)).split('\n');
  const written = lines.filter((line) => line.trim().length > 0);
  const considered = written.length > 0 ? written : lines;
  const remove = considered.every((line) => rule.exact.test(line));

  return rewriteLines(state, (line) => {
    const indent = /^[ \t]*/.exec(line)?.[0] ?? '';
    if (written.length > 0 && line.trim().length === 0) return { removed: 0, insert: '' };
    const existing = rule.detect.exec(line);
    if (existing) {
      return { removed: existing[0].length, insert: remove ? indent : indent + rule.insert };
    }
    return remove ? { removed: 0, insert: '' } : { removed: indent.length, insert: indent + rule.insert };
  });
}

/** The marker the next item of a list carries; a number counts up, everything else repeats. */
function nextMarker(marker: string): string {
  const number = /^(\d+)\.$/.exec(marker);
  return number ? `${Number(number[1]) + 1}.` : marker;
}

/**
 * Continues a list, a numbered list or a quote at a line break, the way a writer expects it: the
 * new line carries the same marker, a number counts up, and a line break on an item that stayed
 * empty ends the list instead of adding another empty item.
 * @param state text and selection at the moment of the line break.
 * @returns text and selection afterwards, or null when the plain line break of the browser is right.
 */
export function continueList(state: EditorState): EditorState | null {
  const { text } = state;
  const { start, end } = ordered(state);
  const line = lineAt(text, start);
  const match = LIST_ITEM.exec(line.content);
  if (!match) return null;

  const indent = match[1] ?? '';
  const marker = match[2] ?? '';
  const spacing = match[3] ?? ' ';
  const rest = match[4] ?? '';
  const markerEnd = line.start + indent.length + marker.length + spacing.length;

  if (start === end && start >= markerEnd && rest.trim().length === 0) {
    return {
      text: text.slice(0, line.start) + text.slice(line.end),
      selectionStart: line.start,
      selectionEnd: line.start,
    };
  }

  const continued = `\n${indent}${nextMarker(marker)}${spacing}`;
  const caret = start + continued.length;
  return {
    text: text.slice(0, start) + continued + text.slice(end),
    selectionStart: caret,
    selectionEnd: caret,
  };
}

/**
 * Writes a link. A selection that is itself an address becomes the target and the label waits to be
 * typed; any other selection becomes the label and the target waits. Whatever still has to be
 * replaced is selected, so the next keystroke or paste lands in the right place.
 * @param state text and selection before the change.
 * @returns text and selection afterwards.
 */
export function insertLink(state: EditorState): EditorState {
  const { text } = state;
  const { start, end } = ordered(state);
  const selected = text.slice(start, end);
  const leading = selected.length - selected.trimStart().length;
  const trailing = selected.length - selected.trimEnd().length;
  const core = selected.slice(leading, selected.length - trailing);
  const coreStart = start + leading;
  const coreEnd = end - trailing;

  const isTarget = URL_LIKE.test(core);
  const label = core.length === 0 || isTarget ? LINK_LABEL_PLACEHOLDER : core;
  const target = isTarget ? core : LINK_TARGET_PLACEHOLDER;
  const snippet = `[${label}](${target})`;
  const open = core.length === 0 || isTarget;
  const selectionStart = open ? coreStart + 1 : coreStart + label.length + 3;

  return {
    text: text.slice(0, coreStart) + snippet + text.slice(coreEnd),
    selectionStart,
    selectionEnd: selectionStart + (open ? label.length : target.length),
  };
}

/**
 * Writes an empty table as a block of its own, with the blank lines a table needs in order to be
 * read as one, and selects the first column heading.
 * @param state text and selection before the change.
 * @returns text and selection afterwards.
 */
export function insertTable(state: EditorState): EditorState {
  const { text } = state;
  const { start, end } = ordered(state);
  const line = lineAt(text, end);
  const onBlankLine = start === end && line.content.trim().length === 0;
  const anchor = onBlankLine ? line.start : line.end;

  let before = '\n\n';
  if (onBlankLine) {
    const previousEnd = anchor - 1;
    before = anchor > 0 && previousEnd > lineStartAt(text, previousEnd) ? '\n' : '';
  }

  const rest = text.slice(line.end);
  let after = '\n\n';
  if (rest.length === 0 || rest.startsWith('\n\n')) after = '';
  else if (rest.startsWith('\n')) after = '\n';

  const inserted = before + TABLE_SKELETON + after;
  const headingStart = anchor + before.length + TABLE_SKELETON.indexOf('Spalte');
  return {
    text: text.slice(0, anchor) + inserted + rest,
    selectionStart: headingStart,
    selectionEnd: headingStart + 'Spalte'.length,
  };
}

/**
 * Puts a fenced block around the lines the selection touches or takes the fence away again.
 * @param state text and selection before the change.
 * @returns text and selection afterwards.
 */
export function toggleFence(state: EditorState): EditorState {
  const { text } = state;
  const { start, end } = ordered(state);
  const blockStart = lineStartAt(text, start);
  const blockEnd = lineEndAt(text, end);
  const block = text.slice(blockStart, blockEnd);
  const lines = block.split('\n');
  const first = lines[0] ?? '';
  const last = lines[lines.length - 1] ?? '';

  if (lines.length >= 2 && first.trimStart().startsWith(FENCE) && last.trim() === FENCE) {
    const inner = lines.slice(1, -1).join('\n');
    return {
      text: text.slice(0, blockStart) + inner + text.slice(blockEnd),
      selectionStart: blockStart,
      selectionEnd: blockStart + inner.length,
    };
  }

  const fenced = `${FENCE}\n${block}\n${FENCE}`;
  const innerStart = blockStart + FENCE.length + 1;
  return {
    text: text.slice(0, blockStart) + fenced + text.slice(blockEnd),
    selectionStart: innerStart,
    selectionEnd: innerStart + block.length,
  };
}

/**
 * Code as the writer means it: inside a line the backticks, across lines the fenced block.
 * @param state text and selection before the change.
 * @returns text and selection afterwards.
 */
export function toggleCode(state: EditorState): EditorState {
  const { start, end } = ordered(state);
  return state.text.slice(start, end).includes('\n')
    ? toggleFence(state)
    : toggleInline(state, CODE_MARKER);
}

/**
 * Applies one of the eight formats to the state of a field.
 * @param state text and selection before the change.
 * @param format the format of the bar that was pressed.
 * @returns text and selection afterwards.
 */
export function applyFormat(state: EditorState, format: MarkdownFormat): EditorState {
  switch (format) {
    case 'heading':
      return toggleLinePrefix(state, HEADING_RULE);
    case 'bold':
      return toggleInline(state, BOLD_MARKER);
    case 'italic':
      return toggleInline(state, ITALIC_MARKER);
    case 'list':
      return toggleLinePrefix(state, LIST_RULE);
    case 'quote':
      return toggleLinePrefix(state, QUOTE_RULE);
    case 'table':
      return insertTable(state);
    case 'link':
      return insertLink(state);
    case 'code':
      return toggleCode(state);
  }
}

/**
 * The smallest replacement that turns one text into the other. The field writes only this piece,
 * so the undo history of the browser keeps working after a press on the bar.
 * @param before the text as it stands in the field.
 * @param after the text the format produced.
 * @returns the piece to replace and what takes its place.
 */
export function minimalEdit(before: string, after: string): TextEdit {
  const shortest = Math.min(before.length, after.length);
  let prefix = 0;
  while (prefix < shortest && before[prefix] === after[prefix]) prefix += 1;
  let suffix = 0;
  while (
    suffix < shortest - prefix &&
    before[before.length - 1 - suffix] === after[after.length - 1 - suffix]
  ) {
    suffix += 1;
  }
  return { start: prefix, end: before.length - suffix, insert: after.slice(prefix, after.length - suffix) };
}

/**
 * What a piece of the text is. A token carries the marks from the outside in, so a bold word in a
 * heading is ['heading', 'strong'] and the stars around it are ['heading', 'strong', 'marker'].
 * 'marker' is always the innermost: it is the Markdown punctuation itself, which stays visible and
 * is only taken back.
 */
export type SurfaceMark =
  | 'marker'
  | 'heading'
  | 'strong'
  | 'emphasis'
  | 'code'
  | 'quote'
  | 'list'
  | 'table'
  | 'link'
  | 'url'
  | 'selection';

/** A stretch of the text with what it is. Tokens are contiguous and cover the whole text. */
export interface SurfaceToken {
  /** Offset of the first character. */
  readonly start: number;
  /** Offset behind the last character. */
  readonly end: number;
  /** Marks from the outside in; empty for plain text. */
  readonly marks: readonly SurfaceMark[];
}

/** Takes the pieces a scan produces. */
type Emit = (start: number, end: number, marks: readonly SurfaceMark[]) => void;

/** Collects tokens, drops empty ones and merges neighbours that carry the same marks. */
function collector(): { emit: Emit; tokens: () => SurfaceToken[] } {
  const out: SurfaceToken[] = [];
  return {
    emit(start, end, marks) {
      if (end <= start) return;
      const last = out[out.length - 1];
      if (last && last.end === start && last.marks.join('|') === marks.join('|')) {
        out[out.length - 1] = { start: last.start, end, marks: last.marks };
        return;
      }
      out.push({ start, end, marks });
    },
    tokens: () => out,
  };
}

const FENCE_LINE = /^[ \t]*(?:```|~~~)/;
const HEADING_LINE = /^([ \t]*#{1,6}[ \t]+)/;
const QUOTE_LINE = /^([ \t]*>[ \t]?)/;
const LIST_LINE = /^([ \t]*(?:[-*+]|\d+\.)[ \t]+)/;
const TABLE_LINE = /^[ \t]*\|/;
const TABLE_RULE_CELL = /^[ \t]*:?-+:?[ \t]*$/;
const WORD = /[\p{L}\p{N}]/u;

/** True when an underscore at this offset stands at a word edge and may open or close emphasis. */
function atWordEdge(text: string, index: number, from: number, to: number): boolean {
  const outside = index <= from ? '' : text.charAt(index - 1);
  const inside = index + 1 >= to ? '' : text.charAt(index + 1);
  return outside === '' || inside === '' || !WORD.test(outside) || !WORD.test(inside);
}

/**
 * Finds the closing marker of an inline pair.
 * @param text the complete text.
 * @param from offset behind the opening marker.
 * @param to end of the stretch being scanned.
 * @param marker the marker that has to close the pair.
 * @param single true for a lone star or underscore, which a doubled one must not satisfy.
 * @returns offset of the closing marker, or -1 when the pair stays open.
 */
function closingMarker(
  text: string,
  from: number,
  to: number,
  marker: string,
  single: boolean,
): number {
  for (let index = from; index + marker.length <= to; index += 1) {
    if (!text.startsWith(marker, index)) continue;
    if (single && (text.startsWith(marker + marker, index) || text.charAt(index - 1) === marker)) {
      continue;
    }
    return index;
  }
  return -1;
}

/** An inline construct recognised at one offset. */
interface InlineMatch {
  /** Offset behind the whole construct, where the scan carries on. */
  readonly end: number;
  /** Writes the pieces of the construct in order. */
  readonly write: (emit: Emit) => void;
}

/**
 * Recognises the inline construct standing at one offset: code span, bold, italic or link.
 * An unclosed marker is no construct; it stays plain text, exactly as Markdown reads it.
 * @param text the complete text.
 * @param index offset to look at.
 * @param to end of the stretch being scanned.
 * @param base the marks of the surroundings.
 * @returns the construct, or null when none starts here.
 */
function inlineAt(
  text: string,
  index: number,
  to: number,
  base: readonly SurfaceMark[],
): InlineMatch | null {
  const character = text.charAt(index);

  if (character === '`') {
    const close = closingMarker(text, index + 1, to, '`', false);
    if (close < 0) return null;
    return {
      end: close + 1,
      write: (emit) => {
        emit(index, index + 1, [...base, 'code', 'marker']);
        emit(index + 1, close, [...base, 'code']);
        emit(close, close + 1, [...base, 'code', 'marker']);
      },
    };
  }

  if (text.startsWith('**', index)) {
    const close = closingMarker(text, index + 2, to, '**', false);
    if (close < 0 || close === index + 2) return null;
    return {
      end: close + 2,
      write: (emit) => {
        emit(index, index + 2, [...base, 'strong', 'marker']);
        scanInline(text, index + 2, close, [...base, 'strong'], emit);
        emit(close, close + 2, [...base, 'strong', 'marker']);
      },
    };
  }

  if (character === '*' || character === '_') {
    if (character === '_' && !atWordEdge(text, index, 0, to)) return null;
    const close = closingMarker(text, index + 1, to, character, true);
    if (close < 0 || close === index + 1) return null;
    if (character === '_' && !atWordEdge(text, close, 0, to)) return null;
    return {
      end: close + 1,
      write: (emit) => {
        emit(index, index + 1, [...base, 'emphasis', 'marker']);
        scanInline(text, index + 1, close, [...base, 'emphasis'], emit);
        emit(close, close + 1, [...base, 'emphasis', 'marker']);
      },
    };
  }

  if (character === '[') {
    const label = text.indexOf(']', index + 1);
    if (label < 0 || label >= to || text.charAt(label + 1) !== '(') return null;
    const target = text.indexOf(')', label + 2);
    if (target < 0 || target >= to) return null;
    return {
      end: target + 1,
      write: (emit) => {
        emit(index, index + 1, [...base, 'link', 'marker']);
        scanInline(text, index + 1, label, [...base, 'link'], emit);
        emit(label, label + 2, [...base, 'link', 'marker']);
        emit(label + 2, target, [...base, 'url']);
        emit(target, target + 1, [...base, 'link', 'marker']);
      },
    };
  }

  return null;
}

/**
 * Walks one stretch of a line and writes its inline constructs and the plain text between them.
 * @param text the complete text.
 * @param from offset where the stretch starts.
 * @param to offset where it ends.
 * @param base the marks the stretch already carries from its line.
 * @param emit takes the pieces.
 */
function scanInline(
  text: string,
  from: number,
  to: number,
  base: readonly SurfaceMark[],
  emit: Emit,
): void {
  let index = from;
  let plain = from;
  while (index < to) {
    const match = inlineAt(text, index, to, base);
    if (!match) {
      index += 1;
      continue;
    }
    emit(plain, index, base);
    match.write(emit);
    index = match.end;
    plain = index;
  }
  emit(plain, to, base);
}

/** Writes a table row: every bar is a marker, a dashed cell is one too, the rest is content. */
function scanTableRow(text: string, from: number, to: number, emit: Emit): void {
  const base: readonly SurfaceMark[] = ['table'];

  const writeCell = (start: number, end: number): void => {
    if (end <= start) return;
    if (TABLE_RULE_CELL.test(text.slice(start, end))) {
      emit(start, end, [...base, 'marker']);
      return;
    }
    scanInline(text, start, end, base, emit);
  };

  let cell = from;
  for (let index = from; index < to; index += 1) {
    if (text.charAt(index) !== '|') continue;
    writeCell(cell, index);
    emit(index, index + 1, [...base, 'marker']);
    cell = index + 1;
  }
  writeCell(cell, to);
}

/**
 * Marks up Markdown for the layer behind the field. Pure computation: text in, marked pieces out,
 * no DOM anywhere near it.
 *
 * The tokens are contiguous and cover the whole text, so joining their stretches gives the text
 * back unchanged. That is what keeps the layer congruent with the field: the layer writes the same
 * characters in the same order, and only their colour differs.
 * @param text the Markdown.
 * @returns the pieces in reading order.
 */
export function highlight(text: string): readonly SurfaceToken[] {
  const { emit, tokens } = collector();
  let fenced = false;
  let index = 0;

  /** One ordinary line: its block prefix as a marker, the rest as inline text under that block. */
  const scanLine = (line: string, start: number, end: number): void => {
    const heading = HEADING_LINE.exec(line);
    if (heading?.[1]) {
      emit(start, start + heading[1].length, ['heading', 'marker']);
      scanInline(text, start + heading[1].length, end, ['heading'], emit);
      return;
    }

    const quote = QUOTE_LINE.exec(line);
    if (quote?.[1]) {
      emit(start, start + quote[1].length, ['quote', 'marker']);
      scanInline(text, start + quote[1].length, end, ['quote'], emit);
      return;
    }

    const list = LIST_LINE.exec(line);
    if (list?.[1]) {
      emit(start, start + list[1].length, ['list', 'marker']);
      scanInline(text, start + list[1].length, end, ['list'], emit);
      return;
    }

    if (TABLE_LINE.test(line)) {
      scanTableRow(text, start, end, emit);
      return;
    }

    scanInline(text, start, end, [], emit);
  };

  for (;;) {
    const end = lineEndAt(text, index);
    const line = text.slice(index, end);

    if (FENCE_LINE.test(line)) {
      const open = index + line.search(/[`~]/);
      emit(index, open, ['code']);
      emit(open, open + 3, ['code', 'marker']);
      emit(open + 3, end, ['code']);
      fenced = !fenced;
    } else if (fenced) {
      emit(index, end, ['code']);
    } else {
      scanLine(line, index, end);
    }

    if (end >= text.length) break;
    emit(end, end + 1, []);
    index = end + 1;
  }

  return tokens();
}

/**
 * Lays the selection over already marked up tokens, splitting the two it falls inside. The layer
 * draws that piece, and the component measures it to find out where the menu belongs - the same
 * layer that is congruent with the field anyway, so no second mirror of the text is needed.
 * @param tokens what highlight produced.
 * @param start offset where the selection starts.
 * @param end offset where it ends.
 * @returns the tokens with 'selection' added on the covered stretch; the input when it is empty.
 */
export function withSelection(
  tokens: readonly SurfaceToken[],
  start: number,
  end: number,
): readonly SurfaceToken[] {
  const from = Math.min(start, end);
  const to = Math.max(start, end);
  if (to <= from) return tokens;

  const out: SurfaceToken[] = [];
  for (const token of tokens) {
    const cutStart = Math.max(token.start, from);
    const cutEnd = Math.min(token.end, to);
    if (cutEnd <= cutStart) {
      out.push(token);
      continue;
    }
    if (cutStart > token.start) out.push({ start: token.start, end: cutStart, marks: token.marks });
    out.push({ start: cutStart, end: cutEnd, marks: [...token.marks, 'selection'] });
    if (cutEnd < token.end) out.push({ start: cutEnd, end: token.end, marks: token.marks });
  }
  return out;
}

/** A box on the screen, as the component reads it from the layer. */
export interface SurfaceRect {
  readonly left: number;
  readonly top: number;
  readonly width: number;
  readonly height: number;
}

/** Where the menu goes and whether it ended up over or under the selection. */
export interface MenuPlacement {
  readonly left: number;
  readonly top: number;
  /** True when the menu stands over the selection, false when it had to move underneath. */
  readonly above: boolean;
}

/**
 * Where the menu belongs: centred over the selection, a gap away from it, and inside the field.
 * It moves underneath as soon as there is no room above, which is the case for a selection in the
 * first line. Pure computation, so node --test checks it without a browser.
 * @param mark the box of the selection.
 * @param menu how wide and how high the menu is.
 * @param bounds the box the menu has to stay inside.
 * @param gap the distance between menu and selection.
 * @returns the position of the top left corner and the side it ended up on.
 */
export function menuPosition(
  mark: SurfaceRect,
  menu: { readonly width: number; readonly height: number },
  bounds: SurfaceRect,
  gap: number,
): MenuPlacement {
  const centred = mark.left + mark.width / 2 - menu.width / 2;
  const rightmost = bounds.left + bounds.width - menu.width;
  const left = rightmost < bounds.left ? bounds.left : clamp(centred, bounds.left, rightmost);

  const over = mark.top - gap - menu.height;
  if (over >= bounds.top) return { left, top: over, above: true };

  const under = mark.top + mark.height + gap;
  const lowest = bounds.top + bounds.height - menu.height;
  return { left, top: lowest < bounds.top ? bounds.top : Math.min(under, lowest), above: false };
}

/** The event the component sends after every change of the text; its detail carries the text. */
export const CHANGE_EVENT = 'markdown-surface:change';

/**
 * The event the component sends when somebody chose an entry of the menu that is not one of the
 * eight formats. Its detail carries the name of the entry and the selection it was chosen for.
 */
export const ACTION_EVENT = 'markdown-surface:action';

/** What the host learns when an entry of the menu was chosen. */
export interface SurfaceAction {
  /** The value of data-markdown-surface-action on the entry. */
  readonly action: string;
  /** The complete text at that moment. */
  readonly text: string;
  /** Offset where the selection starts. */
  readonly selectionStart: number;
  /** Offset where the selection ends. */
  readonly selectionEnd: number;
}

/** What the host can do with an initialised surface. */
export interface MarkdownSurfaceHandle {
  /** The element the surface was initialised on. */
  readonly element: HTMLElement;
  /** The text field itself, for a host that needs to focus or measure it. */
  readonly input: HTMLTextAreaElement;
  /** The current Markdown text. */
  getText(): string;
  /** Replaces the text; sends a change like a keystroke does. */
  setText(text: string): void;
  /** Where the selection stands right now. */
  getSelection(): { readonly start: number; readonly end: number };
  /** Applies a format as a press in the menu would. */
  format(format: MarkdownFormat): void;
  /** Draws the layer again, after the host changed the value of the field behind its back. */
  refresh(): void;
  /** Removes every listener, hides the menu and gives the field its own colour back. */
  destroy(): void;
}

const surfaces = new WeakMap<HTMLElement, MarkdownSurfaceHandle>();

/** The formats a keyboard shortcut reaches; the rest stands in the menu. */
const SHORTCUTS: ReadonlyMap<string, MarkdownFormat> = new Map([
  ['b', 'bold'],
  ['i', 'italic'],
  ['k', 'link'],
]);

/** How far the menu stands from the selection, in pixels. */
const MENU_GAP = 8;

function isFormat(value: string | undefined): value is MarkdownFormat {
  return (
    value === 'heading' ||
    value === 'bold' ||
    value === 'italic' ||
    value === 'list' ||
    value === 'table' ||
    value === 'quote' ||
    value === 'link' ||
    value === 'code'
  );
}

/**
 * Initialises a surface on an element that carries the markup of the component.
 * Calling it twice on the same element returns the handle of the first call.
 * @param root the element with data-markdown-surface.
 * @returns the handle, or null when the element carries no text field or no layer.
 */
export function initMarkdownSurface(root: HTMLElement): MarkdownSurfaceHandle | null {
  const known = surfaces.get(root);
  if (known) return known;

  const input = root.querySelector<HTMLTextAreaElement>('[data-markdown-surface-input]');
  const layer = root.querySelector<HTMLElement>('[data-markdown-surface-layer]');
  if (!input || !layer) return null;
  const menu = root.querySelector<HTMLElement>('[data-markdown-surface-menu]');
  const entries = menu
    ? Array.from(
        menu.querySelectorAll<HTMLButtonElement>('[data-markdown-format], [data-markdown-surface-action]'),
      )
    : [];
  const controller = new AbortController();
  const listen = { signal: controller.signal };

  const readState = (): EditorState => ({
    text: input.value,
    selectionStart: input.selectionStart,
    selectionEnd: input.selectionEnd,
  });

  const announce = (): void => {
    root.dispatchEvent(
      new CustomEvent(CHANGE_EVENT, { bubbles: true, detail: { text: input.value } }),
    );
  };

  /**
   * Draws the layer: the same characters in the same order as the field, each piece in a span that
   * says what it is. The trailing line break is the one a field shows as an empty last line; the
   * layer needs it too, or the two come apart at the bottom.
   */
  const paint = (): void => {
    const text = input.value;
    const tokens = withSelection(highlight(text), input.selectionStart, input.selectionEnd);
    const pieces = document.createDocumentFragment();
    for (const token of tokens) {
      const piece = text.slice(token.start, token.end);
      if (token.marks.length === 0) {
        pieces.append(piece);
        continue;
      }
      const span = document.createElement('span');
      span.className = token.marks.map((mark) => `markdown-surface__${mark}`).join(' ');
      if (token.marks[token.marks.length - 1] === 'selection') {
        span.setAttribute('data-markdown-surface-mark', '');
      }
      span.textContent = piece;
      pieces.append(span);
    }
    pieces.append('\n');
    layer.replaceChildren(pieces);
    layer.scrollTop = input.scrollTop;
    layer.scrollLeft = input.scrollLeft;
  };

  const hideMenu = (): void => {
    if (menu) menu.hidden = true;
  };

  /**
   * Puts the menu over the selection. The box of the selection comes from the layer, which draws
   * it anyway and stands congruent over the field; a selection running over several lines is
   * measured by its first line, so the menu stays where the selection began.
   */
  const placeMenu = (): void => {
    if (!menu) return;
    const mark = layer.querySelector('[data-markdown-surface-mark]');
    if (!mark || document.activeElement !== input) {
      hideMenu();
      return;
    }
    const first = mark.getClientRects()[0] ?? mark.getBoundingClientRect();
    const frame = root.getBoundingClientRect();
    menu.hidden = false;
    const size = menu.getBoundingClientRect();
    const place = menuPosition(
      {
        left: first.left - frame.left,
        top: first.top - frame.top,
        width: first.width,
        height: first.height,
      },
      { width: size.width, height: size.height },
      { left: 0, top: 0, width: frame.width, height: frame.height },
      MENU_GAP,
    );
    menu.style.setProperty('--markdown-surface-menu-x', `${Math.round(place.left)}px`);
    menu.style.setProperty('--markdown-surface-menu-y', `${Math.round(place.top)}px`);
    menu.setAttribute('data-markdown-surface-side', place.above ? 'above' : 'below');
  };

  const redraw = (): void => {
    paint();
    placeMenu();
  };

  /**
   * Writes a computed state back into the field. Only the changed piece is replaced, and through
   * the editing command of the browser where it exists, so undo still reaches the step before.
   */
  const write = (next: EditorState): void => {
    const edit = minimalEdit(input.value, next.text);
    input.focus();
    input.setSelectionRange(edit.start, edit.end);
    let written = false;
    try {
      written =
        edit.insert.length === 0 && edit.end > edit.start
          ? document.execCommand('delete')
          : document.execCommand('insertText', false, edit.insert);
    } catch {
      written = false;
    }
    if (!written || input.value !== next.text) {
      input.value = next.text;
      input.setSelectionRange(next.selectionStart, next.selectionEnd);
      announce();
      redraw();
      return;
    }
    input.setSelectionRange(next.selectionStart, next.selectionEnd);
    redraw();
  };

  const format = (name: MarkdownFormat): void => write(applyFormat(readState(), name));

  input.addEventListener(
    'input',
    () => {
      announce();
      redraw();
    },
    listen,
  );
  input.addEventListener(
    'scroll',
    () => {
      layer.scrollTop = input.scrollTop;
      layer.scrollLeft = input.scrollLeft;
      placeMenu();
    },
    listen,
  );
  input.addEventListener('blur', hideMenu, listen);
  input.addEventListener(
    'keydown',
    (event: KeyboardEvent) => {
      const command = event.ctrlKey || event.metaKey;
      if (event.key === 'Escape') {
        hideMenu();
        return;
      }
      if (command && !event.altKey) {
        const shortcut = SHORTCUTS.get(event.key.toLowerCase());
        if (shortcut) {
          event.preventDefault();
          format(shortcut);
          return;
        }
      }
      if (event.key === 'Enter' && !event.shiftKey && !command && !event.altKey) {
        const next = continueList(readState());
        if (next) {
          event.preventDefault();
          write(next);
        }
      }
    },
    listen,
  );

  // The selection of a text field changes on far more than a keystroke: dragging, double clicking,
  // Select all, the caret moving with an arrow key. One document-wide listener catches all of it.
  document.addEventListener(
    'selectionchange',
    () => {
      if (document.activeElement === input) redraw();
    },
    listen,
  );
  window.addEventListener('resize', placeMenu, listen);

  // Pressing in the menu must not take the focus out of the field: a text field that loses focus
  // loses its selection with it, and the format would have nothing left to work on.
  menu?.addEventListener('mousedown', (event: MouseEvent) => event.preventDefault(), listen);

  entries.forEach((entry, index) => {
    entry.tabIndex = index === 0 ? 0 : -1;
    entry.addEventListener(
      'click',
      () => {
        const name = entry.dataset.markdownFormat;
        if (isFormat(name)) {
          format(name);
          return;
        }
        const action = entry.dataset.markdownSurfaceAction;
        if (!action) return;
        root.dispatchEvent(
          new CustomEvent<SurfaceAction>(ACTION_EVENT, {
            bubbles: true,
            detail: {
              action,
              text: input.value,
              selectionStart: Math.min(input.selectionStart, input.selectionEnd),
              selectionEnd: Math.max(input.selectionStart, input.selectionEnd),
            },
          }),
        );
      },
      listen,
    );
  });

  // The menu is one stop in the tab order; inside it the arrow keys move, as a toolbar is expected
  // to. Escape gives the field the focus back without changing the selection.
  menu?.addEventListener(
    'keydown',
    (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        hideMenu();
        input.focus();
        return;
      }
      const current = entries.indexOf(document.activeElement as HTMLButtonElement);
      if (current === -1) return;
      const step = event.key === 'ArrowRight' ? 1 : event.key === 'ArrowLeft' ? -1 : 0;
      let target = -1;
      if (step !== 0) target = (current + step + entries.length) % entries.length;
      else if (event.key === 'Home') target = 0;
      else if (event.key === 'End') target = entries.length - 1;
      const next = entries[target];
      if (!next) return;
      event.preventDefault();
      entries.forEach((item) => {
        item.tabIndex = item === next ? 0 : -1;
      });
      next.focus();
    },
    listen,
  );

  root.classList.add('is-ready');
  paint();

  const handle: MarkdownSurfaceHandle = {
    element: root,
    input,
    getText: () => input.value,
    setText: (text: string) => {
      write({ text, selectionStart: text.length, selectionEnd: text.length });
    },
    getSelection: () => ({
      start: Math.min(input.selectionStart, input.selectionEnd),
      end: Math.max(input.selectionStart, input.selectionEnd),
    }),
    format,
    refresh: redraw,
    destroy: () => {
      controller.abort();
      hideMenu();
      root.classList.remove('is-ready');
      layer.replaceChildren();
      surfaces.delete(root);
    },
  };

  surfaces.set(root, handle);
  return handle;
}

// Surfaces already in the page start by themselves. The check for a document keeps the module
// loadable outside a browser, which is what lets node --test import this very file.
if (typeof document !== 'undefined') {
  document.querySelectorAll<HTMLElement>('[data-markdown-surface]').forEach((root) => {
    initMarkdownSurface(root);
  });
}
