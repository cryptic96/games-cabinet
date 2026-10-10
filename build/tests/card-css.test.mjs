/**
 * Checks the contract of the detail card's style sheet with the plain Node test runner: the style sheet is read from disk as text and
 * its rules are looked at one by one. These are the promises a later edit must not break: the ruling is one background, the red
 * rule is a double line, pressable rows are whole rulings tall, the focus ring on paper is ink, and the card uses no blur and no
 * accent colour.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const source = readFileSync(new URL('../../Cabinet.Service/wwwroot/css/card.css', import.meta.url), 'utf8');

/**
 * Removes the comments of a style sheet, so a word that only a comment mentions is not mistaken for a rule.
 * @param {string} text The style sheet.
 * @returns {string}
 */
function withoutComments(text) {
  const open = '/' + '*';
  const close = '*' + '/';
  let result = text;

  for (let start = result.indexOf(open); start !== -1; start = result.indexOf(open)) {
    const end = result.indexOf(close, start + open.length);

    result = result.slice(0, start) + (end === -1 ? '' : result.slice(end + close.length));
  }

  return result;
}

const stripped = withoutComments(source);

/**
 * Splits the style sheet into its rules, nested ones included: each entry is a selector list and the text of its declarations.
 * @returns {{ selectors: string[], body: string }[]}
 */
function rules() {
  return [...stripped.matchAll(/([^{}]+)\{([^{}]*)\}/g)].map((match) => ({
    selectors: match[1].split(',').map((selector) => selector.trim()),
    body: match[2],
  }));
}

/**
 * Finds the rules that have the given selector in their selector list.
 * @param {string} selector The exact selector.
 * @returns {{ selectors: string[], body: string }[]}
 */
function rulesFor(selector) {
  return rules().filter((rule) => rule.selectors.includes(selector));
}

/**
 * Tells whether any rule for the selector has the declaration.
 * @param {string} selector The exact selector.
 * @param {string} declaration The declaration text, without the semicolon.
 * @returns {boolean}
 */
function declares(selector, declaration) {
  return rulesFor(selector).some((rule) => rule.body.replace(/\s+/g, ' ').includes(declaration));
}

test('the card never blurs anything and has no backdrop filter', () => {
  assert.doesNotMatch(stripped, /backdrop-filter/);
  assert.doesNotMatch(stripped, /blur\(/);
});

test('the accent colour of the wood is not used on the card', () => {
  assert.doesNotMatch(stripped, /--wood-light/);
});

test('the ruled area draws its ruling as one repeating gradient', () => {
  assert.ok(declares('.card-ruled', 'repeating-linear-gradient('));
  assert.ok(declares('.card-ruled', 'line-height: var(--ruling)'));
});

test('the header is closed by a double red rule', () => {
  assert.ok(declares('.card-head', 'border-bottom: 3px double var(--paper-red)'));
});

test('every pressable row is as many rulings tall as the hit rows say', () => {
  const minimum = 'min-height: calc(var(--hit-rows) * var(--ruling))';

  assert.ok(declares('.exp-row', minimum));
  assert.ok(declares('.card-link', minimum));
});

test('the focus ring inside the card is ink, never the accent colour', () => {
  assert.ok(declares('.card :focus-visible', 'outline: 2px solid var(--ink)'));
  assert.ok(declares('.card :focus-visible', 'outline-offset: 2px'));
});

test('the groups of the ruled area are one blank ruled line apart and nothing else separates them', () => {
  assert.ok(declares('.card-ruled > * + *', 'margin-block-start: var(--ruling)'));
  assert.doesNotMatch(stripped, /\.card-ruled[^{}]*\{[^{}]*border/);
});

test('the swap fades the header and the ruled area over the swap token', () => {
  assert.match(stripped, /\.card\[data-swap\] :is\(\.card-head, \.card-ruled\)\s*\{\s*opacity: 0;/);
  assert.ok(declares('.card-head', 'transition: opacity var(--swap-fade)'));
});

test('the punched hole takes no press and is two rulings tall', () => {
  assert.ok(declares('.card-ruled > .card-hole', 'pointer-events: none'));
  assert.ok(declares('.card-ruled > .card-hole', 'height: calc(2 * var(--ruling))'));
});

test('the chip takes its colour from the row, and the text and paper never take an art colour', () => {
  assert.ok(declares('.exp-chip', 'background: var(--chip)'));
  assert.doesNotMatch(stripped, /\.card(-ruled|-head|-titles|-title|-year)?\s*\{[^{}]*var\(--(bg|fg|chip)\)/);
});
