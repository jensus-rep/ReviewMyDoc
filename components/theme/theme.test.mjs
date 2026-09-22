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
  '--color-bg': ['#f7f8fa', '#24282d'],
  '--color-bg-2': ['#eef1f4', '#2d333a'],
  '--color-hairline': ['#d7dde3', '#414a54'],
  '--color-ink': ['#24282d', '#ffffff'],
  '--color-ink-2': ['#59636e', '#bcc5ce'],
  '--color-blue': ['#123a5f', '#9cc4e4'],
  '--color-red': ['#214d73', '#b4d2e9']
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

test('text on a solid art colour follows the mode', () => {
  // The solid blues are deep in light mode and brightened in dark mode, so the
  // text on them turns over.
  for (const body of [light, darkMedia, darkForced]) {
    assert.match(decl(body, '--art-on-solid') ?? '', /^#[0-9a-f]{6}$/, '--art-on-solid');
    assert.equal(decl(body, '--art-on-chrome'), decl(body, '--art-on-solid'));
  }
  assert.notEqual(decl(light, '--art-on-solid'), decl(darkForced, '--art-on-solid'));
  assert.equal(decl(darkMedia, '--art-on-solid'), decl(darkForced, '--art-on-solid'));
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
    .concat(GLOSS, ART.flatMap((n) => ['--art-' + n, '--art-' + n + '-quiet']), '--art-on-solid', '--art-on-chrome');
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
 * @param {string} hex e.g. '#24282d'.
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
  ['hell', '--color-ink', '--color-bg', 13.95],
  ['hell', '--color-ink', '--color-bg-2', 13.08],
  ['hell', '--color-ink', '--surface', 14.83],
  ['hell', '--color-ink-2', '--color-bg', 5.75],
  ['hell', '--color-ink-2', '--color-bg-2', 5.39],
  ['hell', '--color-blue', '--color-bg', 11.00],
  ['hell', '--color-blue', '--surface', 11.69],
  ['hell', '--color-red', '--color-bg', 8.33],
  ['hell', '--color-red', '--surface', 8.85],
  ['dunkel', '--color-ink', '--color-bg', 14.83],
  ['dunkel', '--color-ink', '--color-bg-2', 12.76],
  ['dunkel', '--color-ink', '--surface', 11.86],
  ['dunkel', '--color-ink-2', '--color-bg', 8.48],
  ['dunkel', '--color-ink-2', '--color-bg-2', 7.30],
  ['dunkel', '--color-blue', '--color-bg', 8.07],
  ['dunkel', '--color-blue', '--surface', 6.46],
  ['dunkel', '--color-red', '--color-bg', 9.42],
  ['dunkel', '--color-red', '--surface', 7.53]
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
  for (const [mode, body] of [['hell', light], ['dunkel', darkForced]]) {
    for (const name of ART) {
      // Solid: the text token of the mode on it.
      const solid = decl(body, '--art-' + name);
      const text = decl(body, '--art-on-solid');
      assert.ok(contrast(text, solid) >= 4.5, mode + ': Text auf --art-' + name + ' is ' + contrast(text, solid).toFixed(2));
      // Quiet: always the ink of the mode on it.
      const quiet = decl(body, '--art-' + name + '-quiet');
      const ink = decl(body, '--color-ink');
      assert.ok(contrast(ink, quiet) >= 4.5, mode + ': Tinte auf --art-' + name + '-quiet is ' + contrast(ink, quiet).toFixed(2));
    }
  }
});
