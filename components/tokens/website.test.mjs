// Checks website.css against the approved hero "Richtung B" version 2: every colour
// role in light and dark mode with the value of the mockup, the three font files as
// real woff2 files next to the stylesheet under the OFL, the type scale for wide and
// narrow windows, the camera distances in order, and every contrast documented in
// README.md. It also guards the boundary to tokens.css: no name of design language
// 1.1 is declared here, so the admin and the customer applications cannot change.
// Run: node --test "components/tokens/*.test.mjs"
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));
const css = readFileSync(join(here, 'website.css'), 'utf8');

/**
 * Returns the body of the first rule whose text starts at `selector`.
 * Braces are counted, so nested rules inside a media query come back complete.
 * @param {string} source CSS text.
 * @param {string} selector literal selector or at-rule prelude.
 * @returns {string} the rule body without the outer braces.
 */
function block(source, selector) {
  const at = source.indexOf(selector);
  assert.ok(at >= 0, 'rule not found: ' + selector);
  const open = source.indexOf('{', at);
  let depth = 0;
  for (let i = open; i < source.length; i += 1) {
    if (source[i] === '{') depth += 1;
    else if (source[i] === '}') {
      depth -= 1;
      if (depth === 0) return source.slice(open + 1, i);
    }
  }
  throw new Error('unbalanced braces after ' + selector);
}

/**
 * Reads one declaration from a rule body.
 * @param {string} body rule body from block().
 * @param {string} name property name.
 * @returns {string|null} the trimmed value, or null if not declared.
 */
function decl(body, name) {
  const escaped = name.replace(/[-]/g, '\\-');
  const found = body.match(new RegExp('(?:^|[;{\\s])' + escaped + '\\s*:\\s*([^;]+);'));
  return found ? found[1].trim() : null;
}

/**
 * Names of all custom properties declared in a rule body.
 * @param {string} body rule body.
 * @returns {string[]} names in source order.
 */
function customProperties(body) {
  return [...body.matchAll(/(--[a-z0-9-]+)\s*:/g)].map((m) => m[1]);
}

// Comments are removed first, so a name mentioned in a comment is never taken for a
// declaration.
const code = css.replace(/\/\*[\s\S]*?\*\//g, '');
const base = block(code, ':root {');
const narrow = block(block(code, '@media (max-width: 759px)'), ':root {');
const darkMedia = block(block(code, '@media (prefers-color-scheme: dark)'), ':root:not([data-theme="light"])');
const darkForced = block(code, ':root[data-theme="dark"]');

// Values of docs/Design/mockups/hero/richtung-b/index.html, light and dark.
const COLOR_ROLES = {
  '--website-color-room': ['#eeebe4', '#0c0c0b'],
  '--website-color-sheet': ['#fcfbf8', '#191917'],
  '--website-color-surface': ['#f1efe8', '#21211e'],
  '--website-color-ink': ['#171613', '#ecebe4'],
  '--website-color-ink-2': ['#5a5750', '#a19e95'],
  '--website-color-line': ['#d2cec4', '#35342f'],
  '--website-color-light-line': ['#171613', '#f3cd5a'],
  '--website-color-light-band': ['#f1c94f', '#3a3219'],
  '--website-color-light-dot': ['#171613', '#f3cd5a'],
  '--website-color-button': ['#171613', '#ecebe4'],
  '--website-color-button-ink': ['#fcfbf8', '#0c0c0b'],
  '--website-shadow-color': ['rgba(70, 56, 30, 0.085)', 'rgba(0, 0, 0, 0.55)']
};

// Desktop value, and the narrow value where the mockup has one.
const TYPE_SCALE = {
  '--website-font-size-title': ['clamp(36px, 3.7vw, 56px)', 'clamp(32px, 9.4vw, 40px)'],
  '--website-line-height-title': ['1.02', null],
  '--website-letter-spacing-title': ['-0.018em', null],
  '--website-font-size-statement': ['clamp(27px, 2.6vw, 42px)', 'clamp(23px, 6.6vw, 28px)'],
  '--website-line-height-statement': ['1.08', '1.1'],
  '--website-letter-spacing-statement': ['-0.012em', null],
  '--website-font-size-lead': ['17.5px', '16px'],
  // Text stays 17px on the phone (design language 2.1, rule 5); the call under
  // the closing statement is the role that shrinks to 15.5px.
  '--website-font-size-text': ['17px', null],
  '--website-line-height-text': ['1.55', null],
  '--website-font-size-call': ['17px', '15.5px'],
  '--website-font-size-addition': ['16px', '15px'],
  '--website-font-size-item': ['17px', '15.5px'],
  '--website-font-size-button': ['17px', null],
  '--website-font-size-small': ['14.5px', '14px'],
  '--website-font-size-brand': ['21px', '19px'],
  '--website-font-size-label': ['11.5px', '10.5px'],
  '--website-letter-spacing-label': ['0.14em', null],
  '--website-font-size-title-block-value': ['14px', '12.5px'],
  '--website-font-size-title-block-field': ['9.5px', '8.5px'],
  '--website-font-size-map-step': ['16px', '12px'],
  '--website-font-size-map-tool': ['16px', '12px'],
  '--website-font-size-map-role': ['12.5px', '10px'],
  '--website-font-size-map-note': ['11.5px', '9px'],
  '--website-letter-spacing-map-caps': ['0.12em', null],
  '--website-font-weight-regular': ['400', null],
  '--website-font-weight-bold': ['700', null]
};

// The page frame of rule 6, wide and narrow.
const FRAME = {
  '--website-frame-edge': ['56px', '18px'],
  '--website-frame-header-height': ['64px', '56px'],
  '--website-frame-text-column': ['clamp(300px, 29vw, 430px)', 'calc(100vw - 36px)'],
  '--website-radius': ['3px', null]
};

// The camera schedule of the mockup, in viewport heights, in the order it runs.
const CAMERA = [
  ['--website-camera-sheet-1-end', 1.8],
  ['--website-camera-sheet-2-start', 2.5],
  ['--website-camera-sheet-2-end', 3.35],
  ['--website-camera-application-start', 4],
  ['--website-camera-application-end', 4.35],
  ['--website-camera-sheet-3-start', 5],
  ['--website-camera-pinned-length', 6.3],
  ['--website-camera-stage-length', 7.3]
];

const FONT_FACES = [
  ['"P51 Plan"', '400', 'fonts/p51-plan-400.woff2'],
  ['"P51 Plan"', '700', 'fonts/p51-plan-700.woff2'],
  ['"P51 Plan Condensed"', '400', 'fonts/p51-plan-condensed-400.woff2']
];

test('every name is a website token and none of design language 1.1 is touched', () => {
  const tokens = readFileSync(join(here, 'tokens.css'), 'utf8');
  const names11 = new Set(customProperties(tokens));
  for (const body of [base, narrow, darkMedia, darkForced]) {
    for (const name of customProperties(body)) {
      assert.ok(name.startsWith('--website-'), name + ' carries the prefix --website-');
      assert.ok(!names11.has(name), name + ' is not a name of tokens.css');
    }
  }
});

test('every colour role has the value of the mockup in light and dark mode', () => {
  for (const [name, [light, dark]] of Object.entries(COLOR_ROLES)) {
    assert.equal(decl(base, name), light, name + ' in light mode');
    assert.equal(decl(darkMedia, name), dark, name + ' in the prefers-color-scheme block');
    assert.equal(decl(darkForced, name), dark, name + ' in the [data-theme="dark"] block');
  }
});

test('the dark mode block and the forced dark mode never drift apart', () => {
  assert.deepEqual(customProperties(darkMedia), customProperties(darkForced));
  for (const name of customProperties(darkMedia)) {
    assert.equal(decl(darkMedia, name), decl(darkForced, name), name);
  }
});

test('the hard shadow has no blur and falls right and down', () => {
  assert.equal(decl(base, '--website-shadow-offset-x'), '0.16');
  assert.equal(decl(base, '--website-shadow-offset-y'), '0.52');
  assert.ok(!/blur|box-shadow|filter/.test(code), 'no blur and no ready made soft shadow');
});

test('the type scale matches the mockup for wide and narrow windows', () => {
  for (const [name, [wide, small]] of Object.entries(TYPE_SCALE)) {
    assert.equal(decl(base, name), wide, name + ' wide');
    assert.equal(decl(narrow, name), small, name + ' narrow');
  }
  assert.equal(decl(base, '--website-font-family'), '"P51 Plan", Bahnschrift, "DIN Alternate", "Arial Narrow", sans-serif');
  assert.equal(decl(base, '--website-font-family-map'), '"P51 Plan Condensed", "P51 Plan", Bahnschrift, "Arial Narrow", sans-serif');
});

test('the page frame matches rule 6 for wide and narrow windows', () => {
  for (const [name, [wide, small]] of Object.entries(FRAME)) {
    assert.equal(decl(base, name), wide, name + ' wide');
    assert.equal(decl(narrow, name), small, name + ' narrow');
  }
});

test('the camera distances run in order and stay inside the stage', () => {
  let previous = 0;
  for (const [name, expected] of CAMERA) {
    const value = Number(decl(base, name));
    assert.equal(value, expected, name);
    assert.ok(value > previous, name + ' comes after the step before');
    previous = value;
  }
});

test('three font faces load woff2 files from this folder with font-display swap', () => {
  const faces = [...code.matchAll(/@font-face\s*\{([^}]*)\}/g)].map((m) => m[1]);
  assert.equal(faces.length, FONT_FACES.length, 'exactly three faces');
  FONT_FACES.forEach(([family, weight, file], index) => {
    const face = faces[index];
    assert.equal(decl(face, 'font-family'), family, file + ' family');
    assert.equal(decl(face, 'font-weight'), weight, file + ' weight');
    assert.equal(decl(face, 'font-style'), 'normal', file + ' style');
    assert.equal(decl(face, 'font-display'), 'swap', file + ' display');
    assert.equal(decl(face, 'src'), 'url("' + file + '") format("woff2")', file + ' source');

    const bytes = readFileSync(join(here, file));
    assert.equal(bytes.subarray(0, 4).toString('latin1'), 'wOF2', file + ' is a woff2 file');
    assert.equal(bytes.readUInt32BE(8), bytes.length, file + ' is complete');
  });
  assert.ok(!/https?:|data:|@import/.test(code), 'no external source, no data URI, no import');
  assert.ok(!/font-family:\s*"D-DIN/.test(code), 'the reserved font name is not used');
});

test('the OFL travels with the font files', () => {
  const license = join(here, 'fonts', 'OFL.txt');
  assert.ok(existsSync(license), 'fonts/OFL.txt exists');
  const text = readFileSync(license, 'utf8');
  assert.match(text, /Copyright \(C\) 2017 Datto Inc\./);
  assert.match(text, /Reserved Font Names "D-DIN"/);
  assert.match(text, /SIL OPEN FONT LICENSE Version 1\.1/);
});

// WCAG 2.1 relative luminance and contrast ratio, same formula as in README.md.
const channel = (value) => {
  const c = value / 255;
  return c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
};

/**
 * Relative luminance of a six digit hex colour.
 * @param {string} hex e.g. '#171613'.
 * @returns {number} luminance between 0 and 1.
 */
function luminance(hex) {
  const n = parseInt(hex.slice(1), 16);
  return 0.2126 * channel((n >> 16) & 255) + 0.7152 * channel((n >> 8) & 255) + 0.0722 * channel(n & 255);
}

/**
 * Contrast ratio of two hex colours.
 * @param {string} a first colour.
 * @param {string} b second colour.
 * @returns {number} ratio between 1 and 21.
 */
function contrast(a, b) {
  const [x, y] = [luminance(a), luminance(b)];
  return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05);
}

// Every pair documented in README.md: mode, colour, ground, ratio and the level it
// is documented for. 'text' means AA for body text (4.5:1), 'graphic' means AA for
// a graphic object (3:1), 'none' is a pair that carries no information on its own.
const PAIRS = [
  ['hell', 'ink', 'room', 15.20, 'text'],
  ['hell', 'ink', 'sheet', 17.49, 'text'],
  ['hell', 'ink', 'surface', 15.73, 'text'],
  ['hell', 'ink-2', 'room', 6.05, 'text'],
  ['hell', 'ink-2', 'sheet', 6.96, 'text'],
  ['hell', 'ink-2', 'surface', 6.26, 'text'],
  ['hell', 'ink', 'light-band', 11.38, 'text'],
  ['hell', 'light-line', 'sheet', 17.49, 'graphic'],
  ['hell', 'light-dot', 'sheet', 17.49, 'graphic'],
  ['hell', 'button-ink', 'button', 17.49, 'text'],
  ['hell', 'line', 'sheet', 1.52, 'none'],
  ['hell', 'light-band', 'sheet', 1.54, 'none'],
  ['dunkel', 'ink', 'room', 16.37, 'text'],
  ['dunkel', 'ink', 'sheet', 14.73, 'text'],
  ['dunkel', 'ink', 'surface', 13.51, 'text'],
  ['dunkel', 'ink-2', 'room', 7.31, 'text'],
  ['dunkel', 'ink-2', 'sheet', 6.57, 'text'],
  ['dunkel', 'ink-2', 'surface', 6.03, 'text'],
  ['dunkel', 'light-line', 'sheet', 11.48, 'graphic'],
  ['dunkel', 'light-line', 'room', 12.76, 'graphic'],
  ['dunkel', 'light-line', 'light-band', 8.30, 'graphic'],
  ['dunkel', 'button-ink', 'button', 16.37, 'text'],
  ['dunkel', 'line', 'sheet', 1.41, 'none'],
  ['dunkel', 'light-band', 'sheet', 1.38, 'none']
];

test('every documented contrast holds for the values in website.css', () => {
  const minimum = { text: 4.5, graphic: 3, none: 1 };
  for (const [mode, fg, bg, expected, level] of PAIRS) {
    const source = mode === 'hell' ? base : darkForced;
    const a = decl(source, '--website-color-' + fg);
    const b = decl(source, '--website-color-' + bg);
    const ratio = contrast(a, b);
    const label = mode + ': ' + fg + ' auf ' + bg;
    assert.ok(Math.abs(ratio - expected) < 0.005, label + ' is ' + ratio.toFixed(3) + ', README says ' + expected);
    assert.ok(ratio >= minimum[level], label + ' reaches ' + level);
  }
});
