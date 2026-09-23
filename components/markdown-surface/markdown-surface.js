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
const BOLD_MARKER = "**";
const ITALIC_MARKER = "*";
const CODE_MARKER = "`";
const FENCE = "```";
const LINK_LABEL_PLACEHOLDER = "Text";
const LINK_TARGET_PLACEHOLDER = "https://";
const TABLE_SKELETON = "| Spalte | Spalte |\n| --- | --- |\n|  |  |";
const HEADING_RULE = {
  insert: "## ",
  detect: /^([ \t]*)#{1,6}[ \t]+/,
  exact: /^([ \t]*)##[ \t]/
};
const LIST_RULE = {
  insert: "- ",
  detect: /^([ \t]*)[-*+][ \t]+/,
  exact: /^([ \t]*)-[ \t]/
};
const QUOTE_RULE = {
  insert: "> ",
  detect: /^([ \t]*)>[ \t]?/,
  exact: /^([ \t]*)>/
};
const LIST_ITEM = /^([ \t]*)([-*+]|\d+\.|>)([ \t]+)(.*)$/;
const URL_LIKE = /^(?:https?:\/\/|mailto:|www\.)\S+$/;
function clamp(value, low, high) {
  return Math.min(Math.max(value, low), high);
}
function ordered(state) {
  const first = clamp(state.selectionStart, 0, state.text.length);
  const second = clamp(state.selectionEnd, 0, state.text.length);
  return { start: Math.min(first, second), end: Math.max(first, second) };
}
function lineStartAt(text, index) {
  return text.lastIndexOf("\n", clamp(index, 0, text.length) - 1) + 1;
}
function lineEndAt(text, index) {
  const found = text.indexOf("\n", clamp(index, 0, text.length));
  return found === -1 ? text.length : found;
}
function lineAt(text, index) {
  const start = lineStartAt(text, index);
  const end = lineEndAt(text, index);
  return { start, end, content: text.slice(start, end) };
}
function runAt(text, index, character) {
  let start = index;
  while (start > 0 && text[start - 1] === character) start -= 1;
  let end = index;
  while (end < text.length && text[end] === character) end += 1;
  return { start, length: end - start };
}
function isExactMarker(text, index, marker) {
  if (index < 0 || !text.startsWith(marker, index)) return false;
  const run = runAt(text, index, marker.charAt(0));
  return run.start === index && run.length === marker.length;
}
function findMarkerBefore(text, from, marker, limit) {
  for (let index = from - marker.length; index >= limit; index -= 1) {
    if (isExactMarker(text, index, marker)) return index;
  }
  return -1;
}
function findMarkerAfter(text, from, marker, limit) {
  for (let index = from; index + marker.length <= limit; index += 1) {
    if (isExactMarker(text, index, marker)) return index;
  }
  return -1;
}
function toggleInline(state, marker) {
  const { text } = state;
  const { start, end } = ordered(state);
  const width = marker.length;
  if (start === end) {
    if (start >= width && text.startsWith(marker, start - width) && text.startsWith(marker, start)) {
      return {
        text: text.slice(0, start - width) + text.slice(start + width),
        selectionStart: start - width,
        selectionEnd: start - width
      };
    }
    const before = findMarkerBefore(text, start, marker, lineStartAt(text, start));
    const after = findMarkerAfter(text, start, marker, lineEndAt(text, start));
    if (before >= 0 && after >= 0) {
      const stripped = text.slice(0, before) + text.slice(before + width, after) + text.slice(after + width);
      const caret = start - width;
      return { text: stripped, selectionStart: caret, selectionEnd: caret };
    }
    const opened = text.slice(0, start) + marker + marker + text.slice(start);
    return { text: opened, selectionStart: start + width, selectionEnd: start + width };
  }
  const selected = text.slice(start, end);
  if (selected.length >= 2 * width && isExactMarker(text, start, marker) && isExactMarker(text, end - width, marker)) {
    const inner = selected.slice(width, selected.length - width);
    return {
      text: text.slice(0, start) + inner + text.slice(end),
      selectionStart: start,
      selectionEnd: start + inner.length
    };
  }
  if (isExactMarker(text, start - width, marker) && isExactMarker(text, end, marker)) {
    return {
      text: text.slice(0, start - width) + selected + text.slice(end + width),
      selectionStart: start - width,
      selectionEnd: end - width
    };
  }
  const leading = selected.length - selected.trimStart().length;
  const trailing = selected.length - selected.trimEnd().length;
  const core = selected.slice(leading, selected.length - trailing);
  if (core.length === 0) {
    return toggleInline({ text, selectionStart: start, selectionEnd: start }, marker);
  }
  const coreStart = start + leading;
  const wrapped = text.slice(0, coreStart) + marker + core + marker + text.slice(coreStart + core.length);
  return {
    text: wrapped,
    selectionStart: coreStart + width,
    selectionEnd: coreStart + width + core.length
  };
}
function mapOffset(offset, edits, keepAtLineStart) {
  let delta = 0;
  for (const edit of edits) {
    if (offset < edit.lineStart) break;
    if (offset === edit.lineStart && keepAtLineStart) return offset + delta;
    if (offset <= edit.lineStart + edit.removed) return edit.lineStart + delta + edit.added;
    delta += edit.added - edit.removed;
  }
  return offset + delta;
}
function rewriteLines(state, rewrite) {
  const { text } = state;
  const { start, end } = ordered(state);
  const blockStart = lineStartAt(text, start);
  const blockEnd = lineEndAt(text, end);
  const lines = text.slice(blockStart, blockEnd).split("\n");
  const edits = [];
  const rebuilt = [];
  let lineStart = blockStart;
  for (const line of lines) {
    const change = rewrite(line);
    edits.push({ lineStart, removed: change.removed, added: change.insert.length });
    rebuilt.push(change.insert + line.slice(change.removed));
    lineStart += line.length + 1;
  }
  return {
    text: text.slice(0, blockStart) + rebuilt.join("\n") + text.slice(blockEnd),
    selectionStart: mapOffset(start, edits, start < end),
    selectionEnd: mapOffset(end, edits, false)
  };
}
function toggleLinePrefix(state, rule) {
  const { text } = state;
  const { start, end } = ordered(state);
  const lines = text.slice(lineStartAt(text, start), lineEndAt(text, end)).split("\n");
  const written = lines.filter((line) => line.trim().length > 0);
  const considered = written.length > 0 ? written : lines;
  const remove = considered.every((line) => rule.exact.test(line));
  return rewriteLines(state, (line) => {
    const indent = /^[ \t]*/.exec(line)?.[0] ?? "";
    if (written.length > 0 && line.trim().length === 0) return { removed: 0, insert: "" };
    const existing = rule.detect.exec(line);
    if (existing) {
      return { removed: existing[0].length, insert: remove ? indent : indent + rule.insert };
    }
    return remove ? { removed: 0, insert: "" } : { removed: indent.length, insert: indent + rule.insert };
  });
}
function nextMarker(marker) {
  const number = /^(\d+)\.$/.exec(marker);
  return number ? `${Number(number[1]) + 1}.` : marker;
}
function continueList(state) {
  const { text } = state;
  const { start, end } = ordered(state);
  const line = lineAt(text, start);
  const match = LIST_ITEM.exec(line.content);
  if (!match) return null;
  const indent = match[1] ?? "";
  const marker = match[2] ?? "";
  const spacing = match[3] ?? " ";
  const rest = match[4] ?? "";
  const markerEnd = line.start + indent.length + marker.length + spacing.length;
  if (start === end && start >= markerEnd && rest.trim().length === 0) {
    return {
      text: text.slice(0, line.start) + text.slice(line.end),
      selectionStart: line.start,
      selectionEnd: line.start
    };
  }
  const continued = `
${indent}${nextMarker(marker)}${spacing}`;
  const caret = start + continued.length;
  return {
    text: text.slice(0, start) + continued + text.slice(end),
    selectionStart: caret,
    selectionEnd: caret
  };
}
function insertLink(state) {
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
    selectionEnd: selectionStart + (open ? label.length : target.length)
  };
}
function insertTable(state) {
  const { text } = state;
  const { start, end } = ordered(state);
  const line = lineAt(text, end);
  const onBlankLine = start === end && line.content.trim().length === 0;
  const anchor = onBlankLine ? line.start : line.end;
  let before = "\n\n";
  if (onBlankLine) {
    const previousEnd = anchor - 1;
    before = anchor > 0 && previousEnd > lineStartAt(text, previousEnd) ? "\n" : "";
  }
  const rest = text.slice(line.end);
  let after = "\n\n";
  if (rest.length === 0 || rest.startsWith("\n\n")) after = "";
  else if (rest.startsWith("\n")) after = "\n";
  const inserted = before + TABLE_SKELETON + after;
  const headingStart = anchor + before.length + TABLE_SKELETON.indexOf("Spalte");
  return {
    text: text.slice(0, anchor) + inserted + rest,
    selectionStart: headingStart,
    selectionEnd: headingStart + "Spalte".length
  };
}
function toggleFence(state) {
  const { text } = state;
  const { start, end } = ordered(state);
  const blockStart = lineStartAt(text, start);
  const blockEnd = lineEndAt(text, end);
  const block = text.slice(blockStart, blockEnd);
  const lines = block.split("\n");
  const first = lines[0] ?? "";
  const last = lines[lines.length - 1] ?? "";
  if (lines.length >= 2 && first.trimStart().startsWith(FENCE) && last.trim() === FENCE) {
    const inner = lines.slice(1, -1).join("\n");
    return {
      text: text.slice(0, blockStart) + inner + text.slice(blockEnd),
      selectionStart: blockStart,
      selectionEnd: blockStart + inner.length
    };
  }
  const fenced = `${FENCE}
${block}
${FENCE}`;
  const innerStart = blockStart + FENCE.length + 1;
  return {
    text: text.slice(0, blockStart) + fenced + text.slice(blockEnd),
    selectionStart: innerStart,
    selectionEnd: innerStart + block.length
  };
}
function toggleCode(state) {
  const { start, end } = ordered(state);
  return state.text.slice(start, end).includes("\n") ? toggleFence(state) : toggleInline(state, CODE_MARKER);
}
function applyFormat(state, format) {
  switch (format) {
    case "heading":
      return toggleLinePrefix(state, HEADING_RULE);
    case "bold":
      return toggleInline(state, BOLD_MARKER);
    case "italic":
      return toggleInline(state, ITALIC_MARKER);
    case "list":
      return toggleLinePrefix(state, LIST_RULE);
    case "quote":
      return toggleLinePrefix(state, QUOTE_RULE);
    case "table":
      return insertTable(state);
    case "link":
      return insertLink(state);
    case "code":
      return toggleCode(state);
  }
}
function minimalEdit(before, after) {
  const shortest = Math.min(before.length, after.length);
  let prefix = 0;
  while (prefix < shortest && before[prefix] === after[prefix]) prefix += 1;
  let suffix = 0;
  while (suffix < shortest - prefix && before[before.length - 1 - suffix] === after[after.length - 1 - suffix]) {
    suffix += 1;
  }
  return { start: prefix, end: before.length - suffix, insert: after.slice(prefix, after.length - suffix) };
}
function collector() {
  const out = [];
  return {
    emit(start, end, marks) {
      if (end <= start) return;
      const last = out[out.length - 1];
      if (last && last.end === start && last.marks.join("|") === marks.join("|")) {
        out[out.length - 1] = { start: last.start, end, marks: last.marks };
        return;
      }
      out.push({ start, end, marks });
    },
    tokens: () => out
  };
}
const FENCE_LINE = /^[ \t]*(?:```|~~~)/;
const HEADING_LINE = /^([ \t]*#{1,6}[ \t]+)/;
const QUOTE_LINE = /^([ \t]*>[ \t]?)/;
const LIST_LINE = /^([ \t]*(?:[-*+]|\d+\.)[ \t]+)/;
const TABLE_LINE = /^[ \t]*\|/;
const TABLE_RULE_CELL = /^[ \t]*:?-+:?[ \t]*$/;
const WORD = /[\p{L}\p{N}]/u;
function atWordEdge(text, index, from, to) {
  const outside = index <= from ? "" : text.charAt(index - 1);
  const inside = index + 1 >= to ? "" : text.charAt(index + 1);
  return outside === "" || inside === "" || !WORD.test(outside) || !WORD.test(inside);
}
function closingMarker(text, from, to, marker, single) {
  for (let index = from; index + marker.length <= to; index += 1) {
    if (!text.startsWith(marker, index)) continue;
    if (single && (text.startsWith(marker + marker, index) || text.charAt(index - 1) === marker)) {
      continue;
    }
    return index;
  }
  return -1;
}
function inlineAt(text, index, to, base) {
  const character = text.charAt(index);
  if (character === "`") {
    const close = closingMarker(text, index + 1, to, "`", false);
    if (close < 0) return null;
    return {
      end: close + 1,
      write: (emit) => {
        emit(index, index + 1, [...base, "code", "marker"]);
        emit(index + 1, close, [...base, "code"]);
        emit(close, close + 1, [...base, "code", "marker"]);
      }
    };
  }
  if (text.startsWith("**", index)) {
    const close = closingMarker(text, index + 2, to, "**", false);
    if (close < 0 || close === index + 2) return null;
    return {
      end: close + 2,
      write: (emit) => {
        emit(index, index + 2, [...base, "strong", "marker"]);
        scanInline(text, index + 2, close, [...base, "strong"], emit);
        emit(close, close + 2, [...base, "strong", "marker"]);
      }
    };
  }
  if (character === "*" || character === "_") {
    if (character === "_" && !atWordEdge(text, index, 0, to)) return null;
    const close = closingMarker(text, index + 1, to, character, true);
    if (close < 0 || close === index + 1) return null;
    if (character === "_" && !atWordEdge(text, close, 0, to)) return null;
    return {
      end: close + 1,
      write: (emit) => {
        emit(index, index + 1, [...base, "emphasis", "marker"]);
        scanInline(text, index + 1, close, [...base, "emphasis"], emit);
        emit(close, close + 1, [...base, "emphasis", "marker"]);
      }
    };
  }
  if (character === "[") {
    const label = text.indexOf("]", index + 1);
    if (label < 0 || label >= to || text.charAt(label + 1) !== "(") return null;
    const target = text.indexOf(")", label + 2);
    if (target < 0 || target >= to) return null;
    return {
      end: target + 1,
      write: (emit) => {
        emit(index, index + 1, [...base, "link", "marker"]);
        scanInline(text, index + 1, label, [...base, "link"], emit);
        emit(label, label + 2, [...base, "link", "marker"]);
        emit(label + 2, target, [...base, "url"]);
        emit(target, target + 1, [...base, "link", "marker"]);
      }
    };
  }
  return null;
}
function scanInline(text, from, to, base, emit) {
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
function scanTableRow(text, from, to, emit) {
  const base = ["table"];
  const writeCell = (start, end) => {
    if (end <= start) return;
    if (TABLE_RULE_CELL.test(text.slice(start, end))) {
      emit(start, end, [...base, "marker"]);
      return;
    }
    scanInline(text, start, end, base, emit);
  };
  let cell = from;
  for (let index = from; index < to; index += 1) {
    if (text.charAt(index) !== "|") continue;
    writeCell(cell, index);
    emit(index, index + 1, [...base, "marker"]);
    cell = index + 1;
  }
  writeCell(cell, to);
}
function highlight(text) {
  const { emit, tokens } = collector();
  let fenced = false;
  let index = 0;
  const scanLine = (line, start, end) => {
    const heading = HEADING_LINE.exec(line);
    if (heading?.[1]) {
      emit(start, start + heading[1].length, ["heading", "marker"]);
      scanInline(text, start + heading[1].length, end, ["heading"], emit);
      return;
    }
    const quote = QUOTE_LINE.exec(line);
    if (quote?.[1]) {
      emit(start, start + quote[1].length, ["quote", "marker"]);
      scanInline(text, start + quote[1].length, end, ["quote"], emit);
      return;
    }
    const list = LIST_LINE.exec(line);
    if (list?.[1]) {
      emit(start, start + list[1].length, ["list", "marker"]);
      scanInline(text, start + list[1].length, end, ["list"], emit);
      return;
    }
    if (TABLE_LINE.test(line)) {
      scanTableRow(text, start, end, emit);
      return;
    }
    scanInline(text, start, end, [], emit);
  };
  for (; ; ) {
    const end = lineEndAt(text, index);
    const line = text.slice(index, end);
    if (FENCE_LINE.test(line)) {
      const open = index + line.search(/[`~]/);
      emit(index, open, ["code"]);
      emit(open, open + 3, ["code", "marker"]);
      emit(open + 3, end, ["code"]);
      fenced = !fenced;
    } else if (fenced) {
      emit(index, end, ["code"]);
    } else {
      scanLine(line, index, end);
    }
    if (end >= text.length) break;
    emit(end, end + 1, []);
    index = end + 1;
  }
  return tokens();
}
function withSelection(tokens, start, end) {
  const from = Math.min(start, end);
  const to = Math.max(start, end);
  if (to <= from) return tokens;
  const out = [];
  for (const token of tokens) {
    const cutStart = Math.max(token.start, from);
    const cutEnd = Math.min(token.end, to);
    if (cutEnd <= cutStart) {
      out.push(token);
      continue;
    }
    if (cutStart > token.start) out.push({ start: token.start, end: cutStart, marks: token.marks });
    out.push({ start: cutStart, end: cutEnd, marks: [...token.marks, "selection"] });
    if (cutEnd < token.end) out.push({ start: cutEnd, end: token.end, marks: token.marks });
  }
  return out;
}
function menuPosition(mark, menu, bounds, gap) {
  const centred = mark.left + mark.width / 2 - menu.width / 2;
  const rightmost = bounds.left + bounds.width - menu.width;
  const left = rightmost < bounds.left ? bounds.left : clamp(centred, bounds.left, rightmost);
  const over = mark.top - gap - menu.height;
  if (over >= bounds.top) return { left, top: over, above: true };
  const under = mark.top + mark.height + gap;
  const lowest = bounds.top + bounds.height - menu.height;
  return { left, top: lowest < bounds.top ? bounds.top : Math.min(under, lowest), above: false };
}
const CHANGE_EVENT = "markdown-surface:change";
const ACTION_EVENT = "markdown-surface:action";
const surfaces = /* @__PURE__ */ new WeakMap();
const SHORTCUTS = /* @__PURE__ */ new Map([
  ["b", "bold"],
  ["i", "italic"],
  ["k", "link"]
]);
const MENU_GAP = 8;
function isFormat(value) {
  return value === "heading" || value === "bold" || value === "italic" || value === "list" || value === "table" || value === "quote" || value === "link" || value === "code";
}
function initMarkdownSurface(root) {
  const known = surfaces.get(root);
  if (known) return known;
  const input = root.querySelector("[data-markdown-surface-input]");
  const layer = root.querySelector("[data-markdown-surface-layer]");
  if (!input || !layer) return null;
  const menu = root.querySelector("[data-markdown-surface-menu]");
  const entries = menu ? Array.from(
    menu.querySelectorAll("[data-markdown-format], [data-markdown-surface-action]")
  ) : [];
  const controller = new AbortController();
  const listen = { signal: controller.signal };
  const readState = () => ({
    text: input.value,
    selectionStart: input.selectionStart,
    selectionEnd: input.selectionEnd
  });
  const announce = () => {
    root.dispatchEvent(
      new CustomEvent(CHANGE_EVENT, { bubbles: true, detail: { text: input.value } })
    );
  };
  const paint = () => {
    const text = input.value;
    const tokens = withSelection(highlight(text), input.selectionStart, input.selectionEnd);
    const pieces = document.createDocumentFragment();
    for (const token of tokens) {
      const piece = text.slice(token.start, token.end);
      if (token.marks.length === 0) {
        pieces.append(piece);
        continue;
      }
      const span = document.createElement("span");
      span.className = token.marks.map((mark) => `markdown-surface__${mark}`).join(" ");
      if (token.marks[token.marks.length - 1] === "selection") {
        span.setAttribute("data-markdown-surface-mark", "");
      }
      span.textContent = piece;
      pieces.append(span);
    }
    pieces.append("\n");
    layer.replaceChildren(pieces);
    layer.scrollTop = input.scrollTop;
    layer.scrollLeft = input.scrollLeft;
  };
  const hideMenu = () => {
    if (menu) menu.hidden = true;
  };
  const placeMenu = () => {
    if (!menu) return;
    const mark = layer.querySelector("[data-markdown-surface-mark]");
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
        height: first.height
      },
      { width: size.width, height: size.height },
      { left: 0, top: 0, width: frame.width, height: frame.height },
      MENU_GAP
    );
    menu.style.setProperty("--markdown-surface-menu-x", `${Math.round(place.left)}px`);
    menu.style.setProperty("--markdown-surface-menu-y", `${Math.round(place.top)}px`);
    menu.setAttribute("data-markdown-surface-side", place.above ? "above" : "below");
  };
  const redraw = () => {
    paint();
    placeMenu();
  };
  const write = (next) => {
    const edit = minimalEdit(input.value, next.text);
    input.focus();
    input.setSelectionRange(edit.start, edit.end);
    let written = false;
    try {
      written = edit.insert.length === 0 && edit.end > edit.start ? document.execCommand("delete") : document.execCommand("insertText", false, edit.insert);
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
  const format = (name) => write(applyFormat(readState(), name));
  input.addEventListener(
    "input",
    () => {
      announce();
      redraw();
    },
    listen
  );
  input.addEventListener(
    "scroll",
    () => {
      layer.scrollTop = input.scrollTop;
      layer.scrollLeft = input.scrollLeft;
      placeMenu();
    },
    listen
  );
  input.addEventListener("blur", hideMenu, listen);
  input.addEventListener(
    "keydown",
    (event) => {
      const command = event.ctrlKey || event.metaKey;
      if (event.key === "Escape") {
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
      if (event.key === "Enter" && !event.shiftKey && !command && !event.altKey) {
        const next = continueList(readState());
        if (next) {
          event.preventDefault();
          write(next);
        }
      }
    },
    listen
  );
  document.addEventListener(
    "selectionchange",
    () => {
      if (document.activeElement === input) redraw();
    },
    listen
  );
  window.addEventListener("resize", placeMenu, listen);
  menu?.addEventListener("mousedown", (event) => event.preventDefault(), listen);
  entries.forEach((entry, index) => {
    entry.tabIndex = index === 0 ? 0 : -1;
    entry.addEventListener(
      "click",
      () => {
        const name = entry.dataset.markdownFormat;
        if (isFormat(name)) {
          format(name);
          return;
        }
        const action = entry.dataset.markdownSurfaceAction;
        if (!action) return;
        root.dispatchEvent(
          new CustomEvent(ACTION_EVENT, {
            bubbles: true,
            detail: {
              action,
              text: input.value,
              selectionStart: Math.min(input.selectionStart, input.selectionEnd),
              selectionEnd: Math.max(input.selectionStart, input.selectionEnd)
            }
          })
        );
      },
      listen
    );
  });
  menu?.addEventListener(
    "keydown",
    (event) => {
      if (event.key === "Escape") {
        hideMenu();
        input.focus();
        return;
      }
      const current = entries.indexOf(document.activeElement);
      if (current === -1) return;
      const step = event.key === "ArrowRight" ? 1 : event.key === "ArrowLeft" ? -1 : 0;
      let target = -1;
      if (step !== 0) target = (current + step + entries.length) % entries.length;
      else if (event.key === "Home") target = 0;
      else if (event.key === "End") target = entries.length - 1;
      const next = entries[target];
      if (!next) return;
      event.preventDefault();
      entries.forEach((item) => {
        item.tabIndex = item === next ? 0 : -1;
      });
      next.focus();
    },
    listen
  );
  root.classList.add("is-ready");
  paint();
  const handle = {
    element: root,
    input,
    getText: () => input.value,
    setText: (text) => {
      write({ text, selectionStart: text.length, selectionEnd: text.length });
    },
    getSelection: () => ({
      start: Math.min(input.selectionStart, input.selectionEnd),
      end: Math.max(input.selectionStart, input.selectionEnd)
    }),
    format,
    refresh: redraw,
    destroy: () => {
      controller.abort();
      hideMenu();
      root.classList.remove("is-ready");
      layer.replaceChildren();
      surfaces.delete(root);
    }
  };
  surfaces.set(root, handle);
  return handle;
}
if (typeof document !== "undefined") {
  document.querySelectorAll("[data-markdown-surface]").forEach((root) => {
    initMarkdownSurface(root);
  });
}
export {
  ACTION_EVENT,
  CHANGE_EVENT,
  applyFormat,
  continueList,
  highlight,
  initMarkdownSurface,
  insertLink,
  insertTable,
  lineAt,
  lineEndAt,
  lineStartAt,
  menuPosition,
  minimalEdit,
  toggleCode,
  toggleFence,
  toggleInline,
  toggleLinePrefix,
  withSelection
};
