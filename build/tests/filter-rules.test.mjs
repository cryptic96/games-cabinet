/**
 * Checks the rules behind the filter bar with the plain Node test runner: how a typed text is folded, which games stand in the light
 * for it, how a family of a base game and its expansions is lit together, and how the lit entries are counted against the entries of
 * the screen-reader list. The scripts are loaded from a temporary copy of the script folder so the module's relative imports resolve.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { copyFileSync, mkdtempSync, readdirSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

const FOLDER = mkdtempSync(join(tmpdir(), 'cabinet-filter-rules-'));

writeFileSync(join(FOLDER, 'package.json'), '{"type":"module"}');

for (const name of readdirSync(new URL('../../Cabinet.Service/wwwroot/js/', import.meta.url)).filter((file) => file.endsWith('.js'))) {
  copyFileSync(new URL('../../Cabinet.Service/wwwroot/js/' + name, import.meta.url), join(FOLDER, name));
}

const { EMPTY_FILTERS, fold, buildModel, isActive, evaluate, listEntries, countLit } = await import(pathToFileURL(join(FOLDER, 'filter-rules.js')).href);

/**
 * Builds an invented card record; fields not given are unknown.
 * @param {number} entryId The collection entry.
 * @param {string} title The title.
 * @param {object} [more] The other fields.
 * @returns {object}
 */
const record = (entryId, title, more = {}) => ({ entryId, gameId: entryId * 10, title, isExpansion: false, expansions: [], bases: [], ...more });

/**
 * Builds the link a base game keeps to an owned expansion, or an expansion keeps to a base game.
 * @param {object} target The record linked to.
 * @returns {{ entryId: number, gameId: number, title: string }}
 */
const link = (target) => ({ entryId: target.entryId, gameId: target.gameId, title: target.title });

const harbourLights = record(2, 'Harbour Lights', { isExpansion: true });
const lantern = record(1, 'Lantern & Harbour', { expansions: [link(harbourLights)] });
harbourLights.bases = [link(lantern)];
const quarry = record(3, 'Quiet Quarry');
const saltRoad = record(4, 'Salt Road', { isExpansion: true, bases: [{ entryId: null, gameId: 999, title: 'Not Owned' }] });
const COLLECTION = [lantern, harbourLights, quarry, saltRoad];

const search = (q) => ({ ...EMPTY_FILTERS, q });
const litIds = (records, q) => [...evaluate(search(q), buildModel(records)).lit].sort();

test('folding drops capitals, accents and extra spacing', () => {
  assert.equal(fold('  Café   DU  Monde '), 'cafe du monde');
  assert.equal(fold('CAFÉ'), fold('Cafe'));
  assert.equal(fold('   \t '), '');
  assert.equal(fold(undefined), '');
  assert.equal(fold(null), '');
});

test('folding compares code points after decomposition, so a precomposed and a decomposed accent are the same', () => {
  assert.equal(fold('Café'), fold('Café'));
  assert.equal(fold('Ångström'), 'angstrom');
});

test('an empty or blank search is no filter and lights everything', () => {
  const model = buildModel(COLLECTION);

  for (const q of ['', '   ', '\t']) {
    const result = evaluate(search(q), model);

    assert.equal(isActive(search(q)), false);
    assert.equal(result.active, false);
    assert.deepEqual([...result.lit].sort(), ['1', '2', '3', '4']);
  }
});

test('a search lights the whole family of a matching base game or owned expansion and nothing else', () => {
  assert.deepEqual(litIds(COLLECTION, 'lights'), ['1', '2']);
  assert.deepEqual(litIds(COLLECTION, 'lantern'), ['1', '2']);
  assert.deepEqual(litIds(COLLECTION, 'quarry'), ['3']);
  assert.deepEqual(litIds(COLLECTION, 'salt'), ['4']);
  assert.deepEqual(litIds(COLLECTION, 'nothing like it'), []);
});

test('a search finds accented titles from plain letters and the reverse, ignoring capitals and spacing', () => {
  const records = [record(1, 'Café Müller'), record(2, 'Plain Game')];

  assert.deepEqual(litIds(records, 'CAFE'), ['1']);
  assert.deepEqual(litIds(records, 'muller'), ['1']);
  assert.deepEqual(litIds([record(1, 'Cafe'), record(2, 'CAFÉ')], 'café'), ['1', '2']);
  assert.deepEqual(litIds(records, '  café   mül'), ['1']);
});

test('the search text is one substring, not a set of words', () => {
  assert.deepEqual(litIds(COLLECTION, 'quiet quarry'), ['3']);
  assert.deepEqual(litIds(COLLECTION, 'quarry quiet'), []);
  assert.deepEqual(litIds(COLLECTION, 'lantern salt'), []);
});

test('a single character lights every family with a title containing it', () => {
  assert.deepEqual(litIds(COLLECTION, 'q'), ['3']);
  assert.deepEqual(litIds(COLLECTION, 'a'), ['1', '2', '3', '4']);
});

test('an expansion owned for two base games is lit when either family matches, and the other base game stays dim', () => {
  const season = record(12, 'Shared Season', { isExpansion: true });
  const first = record(10, 'First Orchard', { expansions: [link(season)] });
  const second = record(11, 'Second Orchard', { expansions: [link(season)] });
  season.bases = [link(first), link(second)];
  const records = [first, second, season];

  assert.deepEqual(litIds(records, 'first'), ['10', '12']);
  assert.deepEqual(litIds(records, 'second'), ['11', '12']);
  assert.deepEqual(litIds(records, 'shared'), ['10', '11', '12']);
  assert.deepEqual(litIds(records, 'zzz'), []);
});

test('the count is a whole number of list entries: an expansion beside its base is not counted, an orphan is, copies are separate', () => {
  const model = buildModel(COLLECTION);
  const entries = listEntries(model, { language: 'en' });
  const result = evaluate(search('lights'), model);

  assert.deepEqual(countLit(entries, result.lit), { lit: 1, total: 3 });
  assert.deepEqual(countLit(entries, evaluate(EMPTY_FILTERS, model).lit), { lit: 3, total: 3 });
  assert.deepEqual(countLit(entries, evaluate(search('zzz'), model).lit), { lit: 0, total: 3 });

  const copies = [record(1, 'Twin Game'), record(2, 'Twin Game'), record(3, 'Other')];
  const copyModel = buildModel(copies);

  assert.deepEqual(countLit(listEntries(copyModel, { language: 'en' }), evaluate(search('twin'), copyModel).lit), { lit: 2, total: 3 });
});

test('an entry counts as lit when any of its boxes is lit', () => {
  const entries = [{ entryId: '1', boxes: ['1', '2'] }, { entryId: '3', boxes: ['3'] }];

  assert.deepEqual(countLit(entries, new Set(['2'])), { lit: 1, total: 2 });
  assert.deepEqual(countLit(entries, new Set()), { lit: 0, total: 2 });
});

test('records made of drawn boxes alone group by the family id, and a record with neither is a family of one', () => {
  const base = { entryId: 1, gameId: 10, title: 'Lantern & Harbour', isExpansion: false, familyId: 10, expansions: [], bases: [] };
  const layer = { entryId: 2, gameId: 20, title: 'Harbour Lights', isExpansion: true, familyId: 10, expansions: [], bases: [] };
  const loose = { entryId: 3, gameId: 30, title: 'Salt Road', isExpansion: true, expansions: [], bases: [] };
  const records = [base, layer, loose];

  assert.deepEqual(litIds(records, 'lights'), ['1', '2']);
  assert.deepEqual(litIds(records, 'lantern'), ['1', '2']);
  assert.deepEqual(litIds(records, 'salt'), ['3']);
  assert.deepEqual(countLit(listEntries(buildModel(records), { language: 'en' }), evaluate(search('lights'), buildModel(records)).lit), { lit: 2, total: 3 });
});

test('the state of no filter holds nothing, and a chosen group value alone makes a filter active', () => {
  assert.equal(isActive(EMPTY_FILTERS), false);
  assert.equal(isActive({ ...EMPTY_FILTERS, players: 4 }), true);
  assert.equal(isActive({ ...EMPTY_FILTERS, time: 60 }), true);
  assert.equal(isActive({ ...EMPTY_FILTERS, location: 'x' }), true);
  assert.equal(Object.isFrozen(EMPTY_FILTERS), true);
});

test('matching never changes what the records say', () => {
  const before = JSON.stringify(COLLECTION);

  evaluate(search('Café'), buildModel(COLLECTION));

  assert.equal(JSON.stringify(COLLECTION), before);
});
