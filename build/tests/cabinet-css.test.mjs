/**
 * Checks the promises of the cabinet's style sheet with the plain Node test runner: the style sheets are read from disk as text.
 * A cover whose picture failed looks like a game without one because every picture rule matches only a working picture; no
 * colour is hard-coded as a fallback inside a variable; the two fallback colours are declared once; and one-line box titles
 * step between exactly two sizes by the size the box is drawn at.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const cabinet = readFileSync(new URL('../../Cabinet.Service/wwwroot/css/cabinet.css', import.meta.url), 'utf8');
const site = readFileSync(new URL('../../Cabinet.Service/wwwroot/css/site.css', import.meta.url), 'utf8');

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

const stripped = withoutComments(cabinet);

/**
 * Finds the body of every container query whose condition contains the given text, with the rules inside it.
 * @param {string} condition Text the query condition must contain.
 * @returns {string[]}
 */
function containerQueries(condition) {
  return [...stripped.matchAll(/@container\s*([^{]*)\{((?:[^{}]*\{[^{}]*\})*)\s*\}/g)]
    .filter((match) => match[1].includes(condition))
    .map((match) => match[2]);
}

test('no variable carries a hard-coded colour as its fallback', () => {
  const fallbacks = [...stripped.matchAll(/var\(\s*--[\w-]+\s*,\s*#[0-9a-fA-F]{3,8}\s*\)/g)].map((match) => match[0]);

  assert.deepEqual(fallbacks, []);
});

test('every selector on the picture attribute matches only a working picture', () => {
  const selectors = [...stripped.matchAll(/\[data-art[^\]]*\]/g)].map((match) => match[0]);

  assert.ok(selectors.length > 0);
  assert.ok(selectors.every((selector) => selector === '[data-art="true"]'), selectors.join(' '));
});

test('the two fallback colours are declared once in the site style sheet', () => {
  const root = /:root\s*\{([^}]*)\}/.exec(withoutComments(site));

  assert.ok(root, 'the site style sheet has a root rule');
  assert.equal(root[1].match(/--box-fallback-bg\s*:\s*var\(--wood-edge\)\s*;/g)?.length, 1);
  assert.equal(root[1].match(/--box-fallback-fg\s*:\s*#ffffff\s*;/g)?.length, 1);
  assert.equal(site.match(/--box-fallback-bg\s*:/g)?.length, 1);
  assert.equal(site.match(/--box-fallback-fg\s*:/g)?.length, 1);
});

test('one-line titles step to their larger size inside size queries', () => {
  const widths = containerQueries('min-width: 26px').join('\n');
  const heights = containerQueries('min-height: 28px').join('\n');

  assert.match(widths, /font-size:\s*16px/);
  assert.match(heights, /font-size:\s*14px/);
});

test('the plinth lip is an outer edge at least one pixel thick, brighter in a narrow section', () => {
  assert.match(stripped, /calc\(-1 \* var\(--lw\)\) calc\(-1 \* var\(--lw\)\) 0 0 rgb\(255 235 210 \/ var\(--arch-lit\)\)/);
  assert.match(stripped, /--lw:\s*max\(1px, calc\(0\.8 \* var\(--u\)\)\)/);
  assert.match(containerQueries('max-width: 400px').join('\n'), /--arch-lit:\s*0\.22/);
});

test('the generated cover plate keeps a gap only when it holds more than one child', () => {
  assert.match(stripped, /\.cover-plate:has\(> \* \+ \*\)\s*\{\s*gap:\s*4px;\s*\}/);
  assert.doesNotMatch(stripped, /gap:\s*2px/);
});
