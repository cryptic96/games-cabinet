/**
 * Checks the pure wording of the owner pages' scripts: the counts, the status after a move or a delete, the two sentences of the
 * delete question, the words for a refused name, and the rules every string of the frozen table keeps. The script is read from disk
 * and imported through a data URL, so no package, bundler or module configuration is needed.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

/**
 * Imports a script from the page's script folder through a data URL.
 * @param {string} name The file name inside the script folder.
 * @returns {Promise<object>} The module's exports.
 */
async function loadPageScript(name) {
  const source = readFileSync(new URL('../../Cabinet.Service/wwwroot/js/' + name, import.meta.url), 'utf8');

  return import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));
}

const { OWNER_TEXT, fill, gameCount, movedText, deleteDialogParts, deleteDialogBody, deletedText, refusalText, headingText } =
  await loadPageScript('owner-format.js');

test('the table of strings is frozen', () => {
  assert.equal(Object.isFrozen(OWNER_TEXT), true);
  assert.throws(() => {
    'use strict';
    OWNER_TEXT.undo = 'Changed';
  });
});

test('every string of the table uses plain dots, no ellipsis character and no exclamation mark', () => {
  for (const [key, value] of Object.entries(OWNER_TEXT)) {
    assert.equal(typeof value, 'string', key);
    assert.equal(value.includes('…'), false, key + ' holds an ellipsis character');
    assert.equal(value.includes('!'), false, key + ' holds an exclamation mark');
    assert.equal(value.trim(), value, key + ' has edge spaces');
  }
});

test('no string of the table uses a typographic quote or dash', () => {
  for (const [key, value] of Object.entries(OWNER_TEXT)) {
    assert.doesNotMatch(value, /[‘’“”–—]/, key);
  }
});

test('a count of games reads No games, 1 game or the number with games', () => {
  assert.equal(gameCount(0), 'No games');
  assert.equal(gameCount(1), '1 game');
  assert.equal(gameCount(4), '4 games');
  assert.equal(gameCount(50), '50 games');
});

test('a move is announced with the name, the direction and the new place', () => {
  assert.equal(movedText('Cabinet A', 'down', 2, 3), 'Moved "Cabinet A" down. Now 2 of 3.');
  assert.equal(movedText('Shelf B', 'up', 1, 3), 'Moved "Shelf B" up. Now 1 of 3.');
});

test('the delete question for one game is singular and for several games is plural', () => {
  assert.equal(deleteDialogBody(1), "1 game will become not placed. It isn't moved anywhere else.");
  assert.equal(deleteDialogBody(4), "4 games will become not placed. They aren't moved anywhere else.");
});

test('the delete question is split into the lead and the rest', () => {
  assert.deepEqual(deleteDialogParts(2), { lead: '2 games will become not placed.', rest: "They aren't moved anywhere else." });
  assert.deepEqual(deleteDialogParts(1), { lead: '1 game will become not placed.', rest: "It isn't moved anywhere else." });
});

test('the status after a delete names how many games became not placed', () => {
  assert.equal(deletedText('Crate C', 0), 'Deleted "Crate C"');
  assert.equal(deletedText('Shelf B', 1), 'Deleted "Shelf B". 1 game is now not placed.');
  assert.equal(deletedText('Shelf B', 2), 'Deleted "Shelf B". 2 games are now not placed.');
});

test('a refused name has its own words and the duplicate message names what was entered', () => {
  assert.equal(refusalText('name-empty', ''), 'Give the location a name.');
  assert.equal(refusalText('name-too-long', 'x'), 'Use 60 characters or fewer.');
  assert.equal(refusalText('name-taken', 'shelf b'), 'You already have a location called "shelf b".');
  assert.equal(refusalText('too-many-locations', 'x'), 'You can have up to 50 locations.');
  assert.equal(refusalText('name-characters', 'x'), 'Use letters, numbers and ordinary punctuation only.');
  assert.equal(refusalText('unknown-location', 'x'), null);
  assert.equal(refusalText(undefined, 'x'), null);
});

test('the panel heading carries the count, and none at zero', () => {
  assert.equal(headingText(0), 'Your locations');
  assert.equal(headingText(1), 'Your locations (1)');
  assert.equal(headingText(12), 'Your locations (12)');
});

test('filling a template keeps a marker whose value is missing', () => {
  assert.equal(fill('Moved {name} to {place}', { name: 'A' }), 'Moved A to {place}');
  assert.equal(fill('{n} of {n}', { n: 3 }), '3 of 3');
});

test('a name that looks like a marker is written as it is and not filled again', () => {
  assert.equal(fill('Added "{name}"', { name: '{n}' }), 'Added "{n}"');
});
