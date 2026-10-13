/**
 * Checks one promise of the owner style sheet with the plain Node test runner: a state mark the server writes on a tag is matched by
 * its value, never by its presence. The server renders a mark that does not apply as an empty attribute rather than leaving it
 * out, so a rule that only asks whether the attribute exists would style every row, placed or not.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const source = readFileSync(new URL('../../Cabinet.Service/wwwroot/css/owner.css', import.meta.url), 'utf8');

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

test('a state mark on a row is matched by its value so an empty attribute styles nothing', () => {
  for (const mark of ['data-unplaced', 'data-problem', 'data-automatic-pick', 'data-full']) {
    assert.doesNotMatch(stripped, new RegExp(`\\[${mark}\\]`), `${mark} must be matched as [${mark}="true"]`);
  }
});

test('the dashed place chip is for a game that is not placed only', () => {
  assert.match(stripped, /\.game-where\[data-unplaced="true"\]\s*\{[^}]*border-style:\s*dashed/);
});

/**
 * Lists the style rules of the owner style sheet as selector and body pairs, rules inside a media query included.
 * @returns {{selector: string, body: string}[]}
 */
function rules() {
  return [...stripped.matchAll(/([^{}]+)\{([^{}]*)\}/g)].map((match) => ({ selector: match[1].trim(), body: match[2] }));
}

/**
 * Finds the rule whose selector is exactly the one given.
 * @param {string} selector The selector.
 * @returns {{selector: string, body: string}}
 */
function ruleFor(selector) {
  const found = rules().find((rule) => rule.selector === selector);

  assert.ok(found, `the rule ${selector} must exist`);
  return found;
}

test('the danger colour is used only by the destructive buttons and the warning line above them', () => {
  const users = rules().filter((rule) => rule.body.includes('var(--danger)'));

  assert.ok(users.length > 0, 'the destructive buttons must use the danger colour');

  for (const rule of users) {
    assert.match(rule.selector, /owner-btn-danger|\.warning-line/, `${rule.selector} must not use the danger colour`);
  }
});

test('a link error is drawn with the ordinary treatment at body size', () => {
  const error = ruleFor('.pic-link-error');

  assert.match(error.body, /border-inline-start:\s*4px solid var\(--paper-label\)/);
  assert.match(error.body, /font-size:\s*16px/);
});

test('an invalid link field is marked by a border and an inset ring, not by colour alone', () => {
  const field = ruleFor('.pic-link-form input[type="url"][aria-invalid="true"]');

  assert.match(field.body, /border-color:\s*var\(--paper-label\)/);
  assert.match(field.body, /box-shadow:\s*inset 0 0 0 1px var\(--ink\)/);
});

test('a tile caption keeps its radio and its label on one line, the label wrapping beside the radio', () => {
  const caption = ruleFor('.pic-caption');

  assert.match(caption.body, /flex-wrap:\s*nowrap/);
  assert.match(caption.body, /align-items:\s*flex-start/);
});

test('a heading that only takes focus shows no ring while a real control keeps it', () => {
  const heading = rules().find((rule) => rule.selector.includes('h3[tabindex="-1"]:focus'));

  assert.ok(heading, 'the heading rule must exist');
  assert.match(heading.body, /outline:\s*none/);
  assert.match(stripped, /\.owner-panel button:focus-visible/);
});

test('the compact banner button is square and the narrowest phones read the shortest reminder', () => {
  const button = ruleFor('.owner-banner:not([data-full="true"]) .owner-btn-primary');
  const tiny = rules().filter((rule) => rule.selector === '.owner-banner:not([data-full="true"]) .banner-tiny');

  assert.match(button.body, /border-radius:\s*0/);
  assert.equal(tiny.length, 1);
  assert.match(tiny[0].body, /display:\s*inline/);
  assert.match(stripped, /@media \(max-width: 22\.5rem\)\s*\{\s*\.owner-banner:not/);
});
