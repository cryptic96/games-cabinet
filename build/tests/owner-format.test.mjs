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

const {
  OWNER_TEXT,
  fill,
  gameCount,
  movedText,
  deleteDialogParts,
  deletedText,
  refusalText,
  headingText,
  selectedText,
  moveTitleText,
  movedGamesText,
  clearedGamesText,
  effectiveLocationIds,
  showingText,
  noMatchText,
  resultText,
  unplacedLabel,
  counterText,
  worthALookLinkText,
  allGamesLinkText,
  shortAddress,
  leadAndRest,
  neverDownloaded,
  bannerTexts,
  whenText,
} =
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

test('the dock counts the ticked games in the singular and the plural', () => {
  assert.equal(selectedText(1), '1 selected');
  assert.equal(selectedText(3), '3 selected');
  assert.equal(moveTitleText(1), 'Move 1 game to');
  assert.equal(moveTitleText(4), 'Move 4 games to');
});

test('the status after a move into a location names the count and the place', () => {
  assert.equal(movedGamesText(1, 'Shelf B'), 'Moved 1 game to Shelf B');
  assert.equal(movedGamesText(2, 'Shelf B'), 'Moved 2 games to Shelf B');
});

test('the status after games were taken out of their location says which are not placed and which follow a base game', () => {
  assert.equal(clearedGamesText(1, 0), '1 game is now not placed');
  assert.equal(clearedGamesText(3, 0), '3 games are now not placed');
  assert.equal(clearedGamesText(0, 1), '1 expansion now follows its base game');
  assert.equal(clearedGamesText(0, 2), '2 expansions now follow their base game');
  assert.equal(clearedGamesText(1, 2), '1 game is now not placed and 2 expansions now follow their base game');
});

test('a game without a location of its own is shown where its base game is shown', () => {
  const rows = [
    { id: '1', own: 'a', baseId: null },
    { id: '2', own: '', baseId: '1' },
    { id: '3', own: 'b', baseId: '1' },
    { id: '4', own: '', baseId: null },
  ];

  const shown = effectiveLocationIds(rows);

  assert.deepEqual([...shown], [['1', 'a'], ['2', 'a'], ['3', 'b'], ['4', '']]);
});

test('leaving a location out shows its games, and the expansions that followed them, nowhere', () => {
  const rows = [
    { id: '1', own: 'a', baseId: null },
    { id: '2', own: '', baseId: '1' },
    { id: '3', own: 'a', baseId: '1' },
  ];

  const shown = effectiveLocationIds(rows, 'a');

  assert.deepEqual([...shown], [['1', ''], ['2', ''], ['3', '']]);
});

test('the count line says how many games are shown', () => {
  assert.equal(showingText(2, 65), 'Showing 2 of 65 games');
  assert.equal(showingText(1, 1), 'Showing 1 of 1 games');
});

test('the no-match line names the query and keeps it as plain text', () => {
  assert.equal(noMatchText('zzz'), 'No games match "zzz".');
  assert.equal(noMatchText('<b>"x"</b>'), 'No games match "<b>"x"</b>".');
});

test('the result words follow the query and the not-placed switch', () => {
  assert.equal(resultText({ shown: 2, total: 65, query: 'cafe', unplacedOnly: false }), 'Showing 2 of 65 games');
  assert.equal(resultText({ shown: 0, total: 65, query: 'zzz', unplacedOnly: false }), 'No games match "zzz".');
  assert.equal(resultText({ shown: 0, total: 65, query: 'zzz', unplacedOnly: true }), 'No games match "zzz".');
  assert.equal(resultText({ shown: 0, total: 65, query: '', unplacedOnly: true }), 'Every game is placed.');
  assert.equal(resultText({ shown: 3, total: 65, query: '', unplacedOnly: true }), 'Showing 3 of 65 games');
});

test('the not-placed switch carries its count', () => {
  assert.equal(unplacedLabel(0), 'Not placed yet (0)');
  assert.equal(unplacedLabel(12), 'Not placed yet (12)');
});

test('the words of the picker and the empty states are in the table', () => {
  assert.equal(OWNER_TEXT.saved, 'Saved');
  assert.equal(OWNER_TEXT.noMatch, 'No games match.');
  assert.equal(OWNER_TEXT.everyPlaced, 'Every game is placed.');
});

test('the worklist counter counts down, says when everything is checked and when nothing needs a look', () => {
  assert.equal(counterText(1, 2), '1 of 2 to check');
  assert.equal(counterText(23, 23), '23 of 23 to check');
  assert.equal(counterText(0, 3), "You've checked all 3 games.");
  assert.equal(counterText(0, 1), "You've checked 1 game.");
  assert.equal(counterText(0, 0), 'Nothing needs a look right now.');
});

test('the two view links carry their counts', () => {
  assert.equal(worthALookLinkText(0), 'Worth a look (0)');
  assert.equal(worthALookLinkText(14), 'Worth a look (14)');
  assert.equal(allGamesLinkText(65), 'All games (65)');
});

test('the words of the looks-right button and its status are in the table', () => {
  assert.equal(OWNER_TEXT.looksRight, 'Looks right');
  assert.equal(OWNER_TEXT.checked, 'Checked');
  assert.equal(OWNER_TEXT.markedChecked, 'Marked as checked');
  assert.equal(OWNER_TEXT.wontLoad, "Picture won't load");
});

test('a stored picture address is shortened to its host and its file name', () => {
  assert.equal(shortAddress('https://cf.geekdo-images.com/abc__original/img/x/0x0/pic1.png'), 'cf.geekdo-images.com/.../pic1.png');
  assert.equal(shortAddress('https://example.org/a%20b.png'), 'example.org/.../a b.png');
  assert.equal(shortAddress('https://example.org/'), 'example.org');
  assert.equal(shortAddress('https://example.org/a%E0%A4%A.png'), 'example.org/.../a%E0%A4%A.png');
  assert.equal(shortAddress('not an address'), '');
  assert.equal(shortAddress(''), '');
});

test('a message is split into its lead sentence and the next step', () => {
  assert.deepEqual(leadAndRest('Paste a picture link first.'), { lead: 'Paste a picture link first.', rest: '' });
  assert.deepEqual(leadAndRest("That picture isn't there. Check that you copied the whole link."), {
    lead: "That picture isn't there.",
    rest: 'Check that you copied the whole link.',
  });
  assert.deepEqual(leadAndRest('That picture is too large to use (5.4 MB; the limit is 12.6 MB). Pick a smaller version from the gallery.'), {
    lead: 'That picture is too large to use (5.4 MB; the limit is 12.6 MB).',
    rest: 'Pick a smaller version from the gallery.',
  });
});

test('the words of the stored link and its status are in the table', () => {
  assert.equal(OWNER_TEXT.linkRemoved, 'Link removed');
  assert.equal(OWNER_TEXT.tileFetching, 'Fetching...');
  assert.equal(OWNER_TEXT.markFetching, 'Fetching your link...');
  assert.equal(OWNER_TEXT.preparing, 'Preparing...');
});

test('the banners count changes in the singular and the plural at every width', () => {
  assert.deepEqual(bannerTexts(1, false), {
    full: '1 change since your last download',
    compact: '1 change not downloaded',
    tiny: '1 not downloaded',
  });
  assert.deepEqual(bannerTexts(23, false), {
    full: '23 changes since your last download',
    compact: '23 changes not downloaded',
    tiny: '23 not downloaded',
  });
});

test('the banner of data that was never downloaded says the changes are only on the server', () => {
  assert.equal(neverDownloaded(1), "You haven't downloaded your owner data yet. 1 change is only on this server.");
  assert.equal(neverDownloaded(3), "You haven't downloaded your owner data yet. 3 changes are only on this server.");
});

test('the banner texts follow the state of the backup and are empty at zero changes', () => {
  assert.deepEqual(bannerTexts(0, false), { full: '', compact: '', tiny: '' });
  assert.deepEqual(bannerTexts(0, true), { full: '', compact: '', tiny: '' });
  assert.deepEqual(bannerTexts(2, false), {
    full: '2 changes since your last download',
    compact: '2 changes not downloaded',
    tiny: '2 not downloaded',
  });
  assert.deepEqual(bannerTexts(1, true), {
    full: "You haven't downloaded your owner data yet. 1 change is only on this server.",
    compact: '1 change not downloaded',
    tiny: '1 not downloaded',
  });
});

const AMSTERDAM = 'Europe/Amsterdam';
const NOW = new Date('2030-10-12T10:30:00Z');

test('a moment on the same local day reads today at the time on the viewer clock', () => {
  assert.equal(whenText('2030-10-12T07:14:00Z', NOW, { timeZone: AMSTERDAM }), 'today at 09:14');
  assert.equal(whenText('2030-10-12T19:14:00Z', NOW, { timeZone: AMSTERDAM }), 'today at 21:14');
});

test('the day is read in the viewer zone, so the same moment can be yesterday in one zone and today in another', () => {
  const moment = '2030-10-11T22:30:00Z';

  assert.equal(whenText(moment, NOW, { timeZone: AMSTERDAM }), 'today at 00:30');
  assert.equal(whenText(moment, NOW, { timeZone: 'America/New_York' }), 'yesterday at 18:30');
});

test('the day before reads yesterday and an older day reads the day and the month', () => {
  assert.equal(whenText('2030-10-11T19:14:00Z', NOW, { timeZone: AMSTERDAM }), 'yesterday at 21:14');
  assert.equal(whenText('2030-10-09T19:14:00Z', NOW, { timeZone: AMSTERDAM }), '9 October at 21:14');
  assert.equal(whenText('2030-01-02T08:05:00Z', NOW, { timeZone: AMSTERDAM }), '2 January at 09:05');
});

test('the clock is a 24-hour clock with midnight as 00', () => {
  assert.equal(whenText('2030-10-11T22:05:00Z', NOW, { timeZone: AMSTERDAM }), 'today at 00:05');
});

test('a moment that starts a line gets a capital letter and a date keeps its own', () => {
  assert.equal(whenText('2030-10-12T19:14:00Z', NOW, { timeZone: AMSTERDAM, capitalise: true }), 'Today at 21:14');
  assert.equal(whenText('2030-10-11T19:14:00Z', NOW, { timeZone: AMSTERDAM, capitalise: true }), 'Yesterday at 21:14');
  assert.equal(whenText('2030-10-09T19:14:00Z', NOW, { timeZone: AMSTERDAM, capitalise: true }), '9 October at 21:14');
});

test('text that is not a moment gives an empty text and a Date works as well', () => {
  assert.equal(whenText('not a time', NOW, { timeZone: AMSTERDAM }), '');
  assert.equal(whenText(new Date('2030-10-12T19:14:00Z'), NOW, { timeZone: AMSTERDAM }), 'today at 21:14');
});
