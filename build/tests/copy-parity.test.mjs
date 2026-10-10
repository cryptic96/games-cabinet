/**
 * Keeps the English and Dutch string tables of the page scripts in step: the same keys, the same kinds of value, plain dots only,
 * informal address, and the Dutch wording of the copy contract. Loaded from disk through a data URL, as the page script tests do.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const source = readFileSync(new URL('../../Cabinet.Service/wwwroot/js/copy.js', import.meta.url), 'utf8');
const { COPY_BY_LANGUAGE, copyFor, pageLanguage, COPY } = await import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));
const en = COPY_BY_LANGUAGE.en;
const nl = COPY_BY_LANGUAGE.nl;

const SAMPLE_ARGUMENTS = [['2:05'], [30000], [180000], ['Base'], ['Title', 'Base'], [3, 'Base'], [new Date('2030-01-15T12:00:00Z')]];

/**
 * Every text a table can produce: its plain strings and what each function gives for a few sample arguments.
 * @param {object} table A string table.
 * @returns {string[]}
 */
function allTexts(table) {
  const texts = [];

  for (const value of Object.values(table)) {
    if (typeof value === 'string') {
      texts.push(value);
      continue;
    }

    for (const args of SAMPLE_ARGUMENTS.filter((candidate) => candidate.length === value.length)) {
      try {
        texts.push(String(value(...args)));
      } catch {
        continue;
      }
    }
  }

  return texts;
}

test('both languages have the same keys', () => {
  assert.deepEqual(Object.keys(nl).sort(), Object.keys(en).sort());
});

test('each key holds the same kind of value in both languages, and functions take the same number of arguments', () => {
  for (const key of Object.keys(en)) {
    assert.equal(typeof nl[key], typeof en[key], `kind of ${key}`);

    if (typeof en[key] === 'function') {
      assert.equal(nl[key].length, en[key].length, `argument count of ${key}`);
    }
  }
});

test('no string uses the ellipsis character and the Dutch has no formal address', () => {
  for (const text of allTexts(en)) {
    assert.doesNotMatch(text, /…/, text);
  }

  for (const text of allTexts(nl)) {
    assert.doesNotMatch(text, /…/, text);
    assert.doesNotMatch(text, /(^|[^\p{L}])[Uu]([^\p{L}]|$)/u, text);
  }
});

test('the plain strings of the Dutch table equal the copy contract', () => {
  assert.equal(nl.loading, 'De kast wordt geladen...');
  assert.equal(nl.errorHeading, 'De kast kon niet worden geladen.');
  assert.equal(nl.errorBody, 'Controleer je verbinding en probeer het opnieuw.');
  assert.equal(nl.retry, 'Opnieuw proberen');
  assert.equal(nl.untitled, 'Spel zonder titel');
  assert.equal(nl.syncNow, 'Nu synchroniseren');
  assert.equal(nl.syncing, 'Bezig met synchroniseren...');
  assert.equal(nl.noteChanged, 'Collectie bijgewerkt. Het kan een paar minuten duren voordat BGG recente wijzigingen laat zien.');
  assert.equal(nl.noteUnchanged, 'Geen wijzigingen gevonden. Het kan een paar minuten duren voordat BGG recente wijzigingen laat zien.');
  assert.equal(nl.noteFailed, 'BGG reageerde niet. De laatste collectie blijft zichtbaar.');
  assert.equal(nl.noteHeldBack, 'BGG gaf veel minder spellen terug dan eerder, dus de laatste collectie blijft zichtbaar.');
  assert.equal(nl.noteRunning, 'Er loopt al een synchronisatie.');
  assert.equal(nl.noteOffline, 'De synchronisatie kon niet starten. Controleer je verbinding en probeer het opnieuw.');
  assert.equal(nl.expansionLabel, 'Uitbreiding');
});

test('the Dutch sentences with arguments equal the copy contract', () => {
  assert.equal(nl.syncAgainIn('2:05'), 'Opnieuw synchroniseren over 2:05');
  assert.equal(nl.syncAgainName(30000), 'Opnieuw synchroniseren over minder dan een minuut');
  assert.equal(nl.syncAgainName(60000), 'Opnieuw synchroniseren over 1 minuut');
  assert.equal(nl.syncAgainName(61000), 'Opnieuw synchroniseren over 2 minuten');
  assert.equal(nl.youCanSyncAgain(180000), 'Je kunt over 3 minuten opnieuw synchroniseren.');
  assert.equal(nl.youCanSyncAgain(60000), 'Je kunt over 1 minuut opnieuw synchroniseren.');
  assert.equal(nl.youCanSyncAgain(30000), 'Je kunt over minder dan een minuut opnieuw synchroniseren.');
  assert.equal(nl.expansionFor('B'), 'Uitbreiding op B');
  assert.equal(nl.expansionName('T'), 'T, uitbreiding');
  assert.equal(nl.layerName('T', 'B'), 'T, uitbreiding op B');
  assert.equal(nl.moreLabel(3), '+3 meer');
  assert.equal(nl.moreName(1, 'B'), '+1 meer uitbreiding op B');
  assert.equal(nl.moreName(3, 'B'), '+3 meer uitbreidingen op B');
  assert.equal(nl.lastSynced('x'), 'Laatst gesynchroniseerd op x');
  assert.equal(nl.staleRecent('x'), 'Je ziet de laatste synchronisatie van x. Recente synchronisaties zijn niet gelukt.');
  assert.equal(nl.staleHeldBack('x'), 'Je ziet de laatste synchronisatie van x. Een veel kleinere collectie van BGG wacht op bevestiging bij de volgende synchronisatie.');
});

test('the card strings equal the copy contract in both languages', () => {
  assert.deepEqual([en.close, en.bggLink, en.newTabHint, en.noDetails], ['Close', 'View on BoardGameGeek', '(opens in a new tab)', 'More details arrive after the next sync.']);
  assert.deepEqual([nl.close, nl.bggLink, nl.newTabHint, nl.noDetails], ['Sluiten', 'Bekijk op BoardGameGeek', '(opent in een nieuw tabblad)', 'Meer details volgen na de volgende synchronisatie.']);
  assert.deepEqual([...en.weightWords], ['Light', 'Medium-light', 'Medium', 'Medium-heavy', 'Heavy']);
  assert.deepEqual([...nl.weightWords], ['Licht', 'Vrij licht', 'Gemiddeld', 'Vrij zwaar', 'Zwaar']);
  assert.deepEqual([...nl.weightWordsLower], ['licht', 'vrij licht', 'gemiddeld', 'vrij zwaar', 'zwaar']);
  assert.deepEqual([nl.playTimeLabel, nl.ageLabel, nl.storedIn, nl.ownedExpansions, nl.ratingLabel, nl.expansionForHeading], ['speelduur', 'min. leeftijd', 'Staat in', 'Uitbreidingen in de kast', 'BGG-score', 'Uitbreiding op']);
  assert.equal(nl.weightLabel('2,4'), 'zwaarte 2,4 / 5');
  assert.equal(en.weightLabel('2.4'), 'weight 2.4 / 5');
  assert.deepEqual([nl.playersLabel(1), nl.playersLabel(2), en.playersLabel(1), en.playersLabel(4)], ['speler', 'spelers', 'player', 'players']);
  assert.deepEqual([nl.designersLabel(1), nl.designersLabel(2), nl.mechanicsLabel(1), nl.mechanicsLabel(9)], ['Ontwerper', 'Ontwerpers', 'Mechanisme', 'Mechanismen']);
  assert.equal(nl.listPlayers('2–4', 4), '2–4 spelers');
  assert.equal(en.listPlayers('1', 1), '1 player');
  assert.equal(nl.listMinutes('60–90'), '60–90 minuten');
  assert.equal(en.listMinutes('45'), '45 minutes');
  assert.equal(nl.listExpansionFor('B'), 'uitbreiding op B');
  assert.deepEqual([en.numberLocale, nl.numberLocale], ['en', 'nl']);
});

test('the exact time follows the language: English words on the English page, Dutch month and om on the Dutch page', () => {
  const moment = new Date('2030-01-15T12:00:00Z');

  assert.match(en.exactTime(moment), /^\d{1,2} January 2030 at \d{2}:\d{2}/);
  assert.match(nl.exactTime(moment), /^\d{1,2} januari 2030 om \d{2}:\d{2}/);
});

test('the "+N more" names start with the text the marker shows in both languages', () => {
  for (const table of [en, nl]) {
    assert.ok(table.moreName(3, 'B').startsWith(table.moreLabel(3)));
    assert.ok(table.moreName(1, 'B').startsWith(table.moreLabel(1)));
  }
});

test('the BGG title passed into a sentence comes back unchanged', () => {
  const title = 'Lantern & Harbour: De Uitbreiding';

  for (const table of [en, nl]) {
    assert.ok(table.expansionFor(title).endsWith(title));
    assert.ok(table.layerName('Night Market', title).endsWith(title));
    assert.ok(table.expansionName(title).startsWith(title));
  }
});

test('an unknown language gets English, and without a document the page language is English', () => {
  assert.equal(copyFor('fr'), en);
  assert.equal(copyFor(undefined), en);
  assert.equal(copyFor('nl'), nl);
  assert.equal(pageLanguage(), 'en');
  assert.equal(COPY, en);
});

test('the page language is read from the document language', () => {
  const previous = globalThis.document;

  try {
    globalThis.document = { documentElement: { lang: 'nl' } };
    assert.equal(pageLanguage(), 'nl');
    globalThis.document = { documentElement: { lang: 'NL-nl' } };
    assert.equal(pageLanguage(), 'nl');
    globalThis.document = { documentElement: { lang: 'en' } };
    assert.equal(pageLanguage(), 'en');
    globalThis.document = { documentElement: { lang: '' } };
    assert.equal(pageLanguage(), 'en');
  } finally {
    globalThis.document = previous;
  }
});
