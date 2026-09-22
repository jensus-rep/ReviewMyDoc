// Checks theme.css against its README: every colour role and every art colour
// exists in light mode and in both dark blocks, the two dark blocks never drift
// apart, the file stays within its remit (colour and gloss, no size, spacing or
// motion), and every contrast the README names holds for the values in the file.
// Run: node --test components/theme/
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));
const css = readFileSync(join(here, 'theme.css'), 'utf8');

/**
 * Returns the body of the first rule whose selector text starts at `selector`.
 * Braces are counted, so nested rules inside a media query come back complete.
 * @param {string} source CSS text.
 * @param {string} selector literal selector or at-rule prelude, e.g. ':root {'.
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
 * Reads one custom property from a rule body.
 * @param {string} body rule body from block().
 * @param {string} name property name including the two dashes.
 * @returns {string|null} the trimmed value, or null if not declared.
 */
function decl(body, name) {
  const found = body.match(new RegExp('(?:^|[;{\\s])' + name + '\\s*:\\s*([^;]+);'));
  return found ? found[1].trim() : null;
}

const light = block(css, ':root {');
const darkMedia = block(block(css, '@media (prefers-color-scheme: dark)'), ':root:not([data-theme="light"])');
const darkForced = block(css, ':root[data-theme="dark"]');

// The seven roles of tokens.css that this theme replaces, with the values the
// README documents. Nothing else of tokens.css is touched.
const COLOR_ROLES = {
  '--color-bg': ['#f8f6f3', '#191719'],
  '--color-bg-2': ['#edeae4', '#221f23'],
  '--color-hairline': ['#e2ded6', '#332f34'],
  '--color-ink': ['#17161b', '#f3f0ec'],
  '--color-ink-2': ['#605d57', '#a39e99'],
  '--color-blue': ['#6b2d5c', '#d891c2'],
  '--color-red': ['#b4311c', '#ff7d64']
};

const ART = ['vermilion', 'chrome', 'malachite', 'plum'];
const GLOSS = ['--surface', '--surface-sheen', '--surface-edge', '--surface-shadow'];

test('every colour role is redefined in all three blocks', () => {
  for (const [name, [hell, dunkel]] of Object.entries(COLOR_ROLES)) {
    assert.equal(decl(light, name), hell, name + ' in light mode');
    assert.equal(decl(darkMedia, name), dunkel, name + ' in the prefers-color-scheme block');
    assert.equal(decl(darkForced, name), dunkel, name + ' in the [data-theme="dark"] block');
  }
});

test('the theme redefines the roles and never the aliases', () => {
  // tokens.css binds --bg and friends to the roles with var(). Redefining an
  // alias here would break that indirection for everything loaded afterwards.
  for (const alias of ['--bg', '--bg-2', '--hairline', '--ink', '--ink-2', '--blue', '--red']) {
    assert.equal(decl(light, alias), null, alias + ' stays the alias from tokens.css');
  }
});

test('every art colour exists solid and quiet in every mode', () => {
  for (const name of ART) {
    for (const suffix of ['', '-quiet']) {
      const variable = '--art-' + name + suffix;
      for (const [label, body] of [['light', light], ['media', darkMedia], ['forced', darkForced]]) {
        assert.match(decl(body, variable) ?? '', /^#[0-9a-f]{6}$/, variable + ' in ' + label);
      }
    }
  }
});

test('text on a solid art colour follows the mode, except on chrome yellow', () => {
  // The solid colours are deep in light mode and brightened in dark mode, so the
  // text on them turns over. Chrome yellow is light in both and keeps its ink.
  for (const body of [light, darkMedia, darkForced]) {
    assert.match(decl(body, '--art-on-solid') ?? '', /^#[0-9a-f]{6}$/, '--art-on-solid');
  }
  assert.notEqual(decl(light, '--art-on-solid'), decl(darkForced, '--art-on-solid'));
  assert.equal(decl(darkMedia, '--art-on-solid'), decl(darkForced, '--art-on-solid'));
  assert.match(decl(light, '--art-on-chrome') ?? '', /^#[0-9a-f]{6}$/, '--art-on-chrome');
  assert.equal(decl(darkMedia, '--art-on-chrome'), null, '--art-on-chrome is not redefined in dark mode');
  assert.equal(decl(darkForced, '--art-on-chrome'), null, '--art-on-chrome is not redefined in forced dark mode');
});

test('the glossy surface exists in every mode and as one class', () => {
  for (const name of GLOSS) {
    for (const [label, body] of [['light', light], ['media', darkMedia], ['forced', darkForced]]) {
      assert.ok(decl(body, name), name + ' in ' + label);
    }
  }
  const surface = block(css, '.surface {');
  assert.match(surface, /background:\s*var\(--surface-sheen\)/);
  assert.match(surface, /box-shadow:\s*var\(--surface-edge\),\s*var\(--surface-shadow\)/);
});

test('the theme owns colour and gloss and nothing else', () => {
  // Sizes, spacing, radii and motion stay in tokens.css. A value here would mean
  // two sources for the same thing.
  for (const name of ['--font-size-text-app', '--space-4', '--radius-m', '--motion-duration', '--font-sans']) {
    assert.equal(decl(light, name), null, name + ' belongs to tokens.css');
  }
  assert.ok(!/url\(|@import/.test(css), 'no webfont and no import');
});

test('the dark mode block and the forced dark mode never drift apart', () => {
  // The two blocks sit at different indentation, so a value written over several
  // lines differs in its whitespace and in nothing else. Only the value counts.
  const flat = (value) => value?.replace(/\s+/g, ' ') ?? null;
  const names = Object.keys(COLOR_ROLES)
    .concat(GLOSS, ART.flatMap((n) => ['--art-' + n, '--art-' + n + '-quiet']), '--art-on-solid');
  for (const name of names) {
    assert.equal(flat(decl(darkMedia, name)), flat(decl(darkForced, name)), name);
  }
});

// WCAG 2.1 relative luminance and contrast ratio, same formula as in tokens.
const channel = (value) => {
  const c = value / 255;
  return c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
};

/**
 * Relative luminance of a six digit hex colour.
 * @param {string} hex e.g. '#17161b'.
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

// Every pair the README names, with its ratio. All of them reach AA for body
// text (4.5:1); the theme has no pair that only reaches the large text level.
const PAIRS = [
  ['hell', '--color-ink', '--color-bg', 16.68],
  ['hell', '--color-ink', '--color-bg-2', 14.99],
  ['hell', '--color-ink', '--surface', 17.99],
  ['hell', '--color-ink-2', '--color-bg', 6.08],
  ['hell', '--color-ink-2', '--color-bg-2', 5.46],
  ['hell', '--color-blue', '--color-bg', 9.03],
  ['hell', '--color-blue', '--surface', 9.74],
  ['hell', '--color-red', '--color-bg', 5.73],
  ['hell', '--color-red', '--surface', 6.18],
  ['dunkel', '--color-ink', '--color-bg', 15.69],
  ['dunkel', '--color-ink', '--color-bg-2', 14.35],
  ['dunkel', '--color-ink', '--surface', 14.17],
  ['dunkel', '--color-ink-2', '--color-bg', 6.71],
  ['dunkel', '--color-ink-2', '--color-bg-2', 6.14],
  ['dunkel', '--color-blue', '--color-bg', 7.43],
  ['dunkel', '--color-blue', '--surface', 6.71],
  ['dunkel', '--color-red', '--color-bg', 7.11],
  ['dunkel', '--color-red', '--surface', 6.41]
];

test('every documented contrast holds for the values in theme.css', () => {
  for (const [mode, fg, bg, expected] of PAIRS) {
    const source = mode === 'hell' ? light : darkForced;
    const ratio = contrast(decl(source, fg), decl(source, bg));
    const label = mode + ': ' + fg + ' auf ' + bg;
    assert.ok(Math.abs(ratio - expected) < 0.005, label + ' is ' + ratio.toFixed(2) + ', README says ' + expected);
    assert.ok(ratio >= 4.5, label + ' reaches AA for body text');
  }
});

test('the filled primary button reaches AA in both modes', () => {
  // The building block button paints --color-blue and writes --color-bg on it.
  for (const [mode, body] of [['hell', light], ['dunkel', darkForced]]) {
    const ratio = contrast(decl(body, '--color-bg'), decl(body, '--color-blue'));
    assert.ok(ratio >= 4.5, mode + ': Grund auf Akzent is ' + ratio.toFixed(2));
  }
});

test('an art colour is readable the way the README says it is used', () => {
  const onChrome = decl(light, '--art-on-chrome');
  for (const [mode, body] of [['hell', light], ['dunkel', darkForced]]) {
    for (const name of ART) {
      // Solid: the text token of the mode on it, chrome yellow the one exception
      // that carries ink in both modes.
      const solid = decl(body, '--art-' + name);
      const text = name === 'chrome' ? onChrome : decl(body, '--art-on-solid');
      assert.ok(contrast(text, solid) >= 4.5, mode + ': Text auf --art-' + name + ' is ' + contrast(text, solid).toFixed(2));
      // Quiet: always the ink of the mode on it.
      const quiet = decl(body, '--art-' + name + '-quiet');
      const ink = decl(body, '--color-ink');
      assert.ok(contrast(ink, quiet) >= 4.5, mode + ': Tinte auf --art-' + name + '-quiet is ' + contrast(ink, quiet).toFixed(2));
    }
  }
});
