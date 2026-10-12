/**
 * Checks the pure text logic of the detail card in English and Dutch: ranges, the weight bands and their words, one-decimal numbers
 * in the page language, the strip entries and the question of whether a record holds any detail at all. Each script is read from
 * disk and imported through a data URL, so no package, bundler or module configuration is needed.
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

const { copyFor } = await loadPageScript('copy.js');
const { oneDecimal, playersText, playTimeText, weightBand, ratingText, ageText, factsFor, hasAnyDetail } = await loadPageScript('format.js');

const en = copyFor('en');
const nl = copyFor('nl');
const DASH = '–';

const FULL = Object.freeze({
  minPlayers: 2,
  maxPlayers: 4,
  playTime: 60,
  minPlayTime: 60,
  maxPlayTime: 90,
  minAge: 10,
  weight: 2.4,
  rating: 7.8,
  designers: ['Invented Designer'],
  mechanics: ['Set collection'],
});

test('a range of players uses an en dash and equal ends read as one number', () => {
  assert.equal(playersText(2, 4), `2${DASH}4`);
  assert.equal(playersText(3, 3), '3');
});

test('one known end of the players is shown alone and nothing known gives null', () => {
  assert.equal(playersText(null, 5), '5');
  assert.equal(playersText(1, undefined), '1');
  assert.equal(playersText(0, 0), null);
  assert.equal(playersText(undefined, undefined), null);
  assert.equal(playersText(-1, 0), null);
});

test('a range of play time uses an en dash and a stated time stands alone', () => {
  assert.equal(playTimeText(60, 60, 90), `60${DASH}90`);
  assert.equal(playTimeText(45, null, null), '45');
  assert.equal(playTimeText(null, 30, null), '30');
  assert.equal(playTimeText(60, 60, 60), '60');
  assert.equal(playTimeText(null, null, 120), '120');
});

test('no play time known, or only zeros, gives null', () => {
  assert.equal(playTimeText(null, null, null), null);
  assert.equal(playTimeText(0, 0, 0), null);
});

test('weight bands are decided on the shown one-decimal value', () => {
  assert.equal(weightBand(1.4), 0);
  assert.equal(weightBand(1.5), 1);
  assert.equal(weightBand(2.4), 1);
  assert.equal(weightBand(2.5), 2);
  assert.equal(weightBand(3.4), 2);
  assert.equal(weightBand(3.5), 3);
  assert.equal(weightBand(4.4), 3);
  assert.equal(weightBand(4.5), 4);
  assert.equal(weightBand(5), 4);
  assert.equal(weightBand(0), null);
  assert.equal(weightBand(null), null);
});

test('a weight with more decimals than shown is banded by what is shown', () => {
  assert.equal(weightBand(1.4999), 1);
  assert.equal(weightBand(1.44), 0);
});

test('one decimal is written with the separator of the page language', () => {
  assert.equal(oneDecimal(7.8, 'en'), '7.8');
  assert.equal(oneDecimal(7.8, 'nl'), '7,8');
  assert.equal(oneDecimal(3, 'nl'), '3,0');
  assert.equal(oneDecimal(3, 'en'), '3.0');
});

test('the rating reads as a score out of ten in both languages', () => {
  assert.equal(ratingText(7.8, en), '7.8 / 10');
  assert.equal(ratingText(7.8, nl), '7,8 / 10');
  assert.equal(ratingText(0, en), null);
  assert.equal(ratingText(null, nl), null);
});

test('the minimum age has a plus sign and zero is missing', () => {
  assert.equal(ageText(10), '10+');
  assert.equal(ageText(0), null);
  assert.equal(ageText(undefined), null);
});

test('a full record gives the four strip entries in order in English', () => {
  assert.deepEqual(factsFor(FULL, en), [
    { icon: 'i-players', value: `2${DASH}4`, label: 'players' },
    { icon: 'i-time', value: `60${DASH}90 min`, label: 'play time' },
    { icon: 'i-weight', value: 'Medium-light', label: 'weight 2.4 / 5' },
    { icon: 'i-age', value: '10+', label: 'min. age' },
  ]);
});

test('a full record gives the four strip entries in order in Dutch', () => {
  assert.deepEqual(factsFor(FULL, nl), [
    { icon: 'i-players', value: `2${DASH}4`, label: 'spelers' },
    { icon: 'i-time', value: `60${DASH}90 min`, label: 'speelduur' },
    { icon: 'i-weight', value: 'Vrij licht', label: 'zwaarte 2,4 / 5' },
    { icon: 'i-age', value: '10+', label: 'min. leeftijd' },
  ]);
});

test('one player uses the singular label in both languages', () => {
  const record = { minPlayers: 1, maxPlayers: 1 };

  assert.deepEqual(factsFor(record, en), [{ icon: 'i-players', value: '1', label: 'player' }]);
  assert.deepEqual(factsFor(record, nl), [{ icon: 'i-players', value: '1', label: 'speler' }]);
});

test('the weight word and the shown number agree at a band edge', () => {
  const [fact] = factsFor({ weight: 1.5 }, en);

  assert.equal(fact.value, 'Medium-light');
  assert.equal(fact.label, 'weight 1.5 / 5');
  assert.equal(factsFor({ weight: 1.4 }, nl)[0].value, 'Licht');
  assert.equal(factsFor({ weight: 4.5 }, nl)[0].value, 'Zwaar');
  assert.equal(factsFor({ weight: 4.4 }, nl)[0].value, 'Vrij zwaar');
});

test('entries whose value is missing are left out and the rest close up', () => {
  assert.deepEqual(factsFor({ minPlayers: 2, maxPlayers: 2, minAge: 8 }, en), [
    { icon: 'i-players', value: '2', label: 'players' },
    { icon: 'i-age', value: '8+', label: 'min. age' },
  ]);
  assert.deepEqual(factsFor({ playTime: 45 }, nl), [{ icon: 'i-time', value: '45 min', label: 'speelduur' }]);
});

test('zero counts, times and ages never reach the strip', () => {
  const zeros = { minPlayers: 0, maxPlayers: 0, playTime: 0, minPlayTime: 0, maxPlayTime: 0, minAge: 0, weight: 0 };

  assert.deepEqual(factsFor(zeros, en), []);
  assert.deepEqual(factsFor(zeros, nl), []);
});

test('a record with nothing known has no detail', () => {
  assert.equal(hasAnyDetail({ designers: [], mechanics: [] }), false);
  assert.equal(hasAnyDetail({ minPlayers: 0, playTime: 0, weight: 0, rating: 0, designers: [], mechanics: [] }), false);
  assert.equal(hasAnyDetail({}), false);
});

test('any one of the seven details makes a record have detail', () => {
  const only = [
    { minPlayers: 2 },
    { maxPlayers: 2 },
    { playTime: 30 },
    { weight: 2.1 },
    { minAge: 8 },
    { rating: 6.5 },
    { designers: ['Invented Designer'] },
    { mechanics: ['Drafting'] },
  ];

  for (const record of only) {
    assert.equal(hasAnyDetail(record), true, JSON.stringify(record));
  }
});

test('the singular and plural row headings follow the count in both languages', () => {
  assert.equal(en.designersLabel(1), 'Designer');
  assert.equal(en.designersLabel(2), 'Designers');
  assert.equal(en.mechanicsLabel(1), 'Mechanic');
  assert.equal(en.mechanicsLabel(3), 'Mechanics');
  assert.equal(nl.designersLabel(1), 'Ontwerper');
  assert.equal(nl.mechanicsLabel(1), 'Mechanisme');
  assert.equal(nl.mechanicsLabel(3), 'Mechanismen');
});
