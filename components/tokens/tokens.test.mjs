// Checks tokens.css against the design language: every role named in
// docs/Design/designsprache.md exists as a variable in light and dark mode, the
// spacing scale stays on the 4 px grid, motion stays below half a second and goes
// to zero on "reduce motion", and every text colour on every ground reaches the
// contrast documented in README.md.
// Run: node --test components/tokens/
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));
const css = readFileSync(join(here, 'tokens.css'), 'utf8');

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

const base = block(css, ':root {');
const darkMedia = block(block(css, '@media (prefers-color-scheme: dark)'), ':root:not([data-theme="light"])');
const darkForced = block(css, ':root[data-theme="dark"]');
const reduced = block(block(css, '@media (prefers-reduced-motion: reduce)'), ':root {');

const COLOR_ROLES = {
  '--color-bg': ['#ffffff', '#000000'],
  '--color-bg-2': ['#f5f5f7', '#1c1c1e'],
  '--color-hairline': ['#e5e5ea', '#2c2c2e'],
  '--color-ink': ['#1d1d1f', '#f5f5f7'],
  '--color-ink-2': ['#6e6e73', '#98989d'],
  '--color-blue': ['#0a7aff', '#0a84ff'],
  '--color-red': ['#ff3b30', '#ff453a']
};

const ALIASES = {
  '--bg': '--color-bg',
  '--bg-2': '--color-bg-2',
  '--hairline': '--color-hairline',
  '--ink': '--color-ink',
  '--ink-2': '--color-ink-2',
  '--blue': '--color-blue',
  '--red': '--color-red'
};

const TYPE_ROLES = [
  '--font-size-page-title-app', '--font-size-page-title-web',
  '--font-size-section-title-app', '--font-size-section-title-web',
  '--font-size-text-app', '--font-size-text-web',
  '--font-size-label-app', '--font-size-label-web'
];

const SPACE_STEPS = 12;

test('every colour role exists with the value from the design language', () => {
  for (const [name, [light, dark]] of Object.entries(COLOR_ROLES)) {
    assert.equal(decl(base, name), light, name + ' in light mode');
    assert.equal(decl(darkMedia, name), dark, name + ' in the prefers-color-scheme block');
    assert.equal(decl(darkForced, name), dark, name + ' in the [data-theme="dark"] block');
  }
});

test('short aliases point at the colour roles', () => {
  for (const [alias, role] of Object.entries(ALIASES)) {
    assert.equal(decl(base, alias), 'var(' + role + ')', alias);
  }
});

test('the system font stack and every type role exist', () => {
  const font = decl(base, '--font-sans');
  assert.ok(font && font.includes('-apple-system') && font.endsWith('sans-serif'), '--font-sans');
  assert.ok(!/url\(|@import/.test(css), 'no webfont for our own brand');
  for (const name of TYPE_ROLES) {
    assert.match(decl(base, name) ?? '', /^\d+px$/, name);
  }
  assert.equal(decl(base, '--line-height-text'), '1.55');
  for (const name of ['--font-weight-text', '--font-weight-title', '--measure-text', '--letter-spacing-page-title-web']) {
    assert.ok(decl(base, name), name);
  }
});

test('the spacing scale has twelve steps on the 4 px grid', () => {
  for (let step = 1; step <= SPACE_STEPS; step += 1) {
    const value = decl(base, '--space-' + step);
    assert.match(value ?? '', /^\d+px$/, '--space-' + step);
    assert.equal(parseInt(value, 10) % 4, 0, '--space-' + step + ' is on the 4 px grid');
  }
  assert.equal(decl(base, '--space-' + (SPACE_STEPS + 1)), null, 'no thirteenth step');
});

test('geometry and elevation exist as named in the design language', () => {
  assert.equal(decl(base, '--radius-s'), '8px');
  assert.equal(decl(base, '--radius-m'), '12px');
  assert.equal(decl(base, '--radius-button'), '7px');
  assert.equal(decl(base, '--hairline-width'), '1px');
  assert.ok(decl(base, '--shadow-float'), '--shadow-float');
  assert.ok(decl(darkForced, '--shadow-float'), '--shadow-float in dark mode');
});

test('motion stays below half a second and goes to zero on reduce', () => {
  for (const name of ['--motion-duration', '--motion-duration-story']) {
    const value = decl(base, name);
    assert.match(value ?? '', /^\d+ms$/, name);
    assert.ok(parseInt(value, 10) < 500, name + ' below 500 ms');
    assert.equal(decl(reduced, name), '0ms', name + ' on reduce');
  }
  assert.ok(decl(base, '--motion-ease'), '--motion-ease');
});

test('the tabular-nums helper class exists', () => {
  assert.match(css, /\.tabular-nums\s*\{[^}]*font-variant-numeric:\s*tabular-nums/);
});

// WCAG 2.1 relative luminance and contrast ratio, same formula as in README.md.
const channel = (value) => {
  const c = value / 255;
  return c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
};

/**
 * Relative luminance of a six digit hex colour.
 * @param {string} hex e.g. '#1d1d1f'.
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

// Every pair documented in README.md: colour, ground, expected ratio and the level
// it is documented for. 'text' means AA for body text (4.5:1), 'large' means AA for
// large text only (3:1); the two 'large' pairs in light mode are an open point.
const PAIRS = [
  ['hell', '--color-ink', '--color-bg', 16.83, 'text'],
  ['hell', '--color-ink', '--color-bg-2', 15.46, 'text'],
  ['hell', '--color-ink-2', '--color-bg', 5.07, 'text'],
  ['hell', '--color-ink-2', '--color-bg-2', 4.66, 'text'],
  ['hell', '--color-blue', '--color-bg', 4.01, 'large'],
  ['hell', '--color-blue', '--color-bg-2', 3.68, 'large'],
  ['hell', '--color-red', '--color-bg', 3.55, 'large'],
  ['hell', '--color-red', '--color-bg-2', 3.26, 'large'],
  ['dunkel', '--color-ink', '--color-bg', 19.29, 'text'],
  ['dunkel', '--color-ink', '--color-bg-2', 15.63, 'text'],
  ['dunkel', '--color-ink-2', '--color-bg', 7.31, 'text'],
  ['dunkel', '--color-ink-2', '--color-bg-2', 5.93, 'text'],
  ['dunkel', '--color-blue', '--color-bg', 5.76, 'text'],
  ['dunkel', '--color-blue', '--color-bg-2', 4.66, 'text'],
  ['dunkel', '--color-red', '--color-bg', 6.16, 'text'],
  ['dunkel', '--color-red', '--color-bg-2', 4.99, 'text']
];

test('every documented contrast holds for the values in tokens.css', () => {
  for (const [mode, fg, bg, expected, level] of PAIRS) {
    const source = mode === 'hell' ? base : darkForced;
    const ratio = contrast(decl(source, fg), decl(source, bg));
    const label = mode + ': ' + fg + ' auf ' + bg;
    assert.ok(Math.abs(ratio - expected) < 0.005, label + ' is ' + ratio.toFixed(2) + ', README says ' + expected);
    assert.ok(ratio >= (level === 'text' ? 4.5 : 3), label + ' reaches AA for ' + level);
  }
});

test('the dark mode block and the forced dark mode never drift apart', () => {
  for (const name of Object.keys(COLOR_ROLES).concat('--shadow-float')) {
    assert.equal(decl(darkMedia, name), decl(darkForced, name), name);
  }
});
