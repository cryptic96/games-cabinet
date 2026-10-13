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
