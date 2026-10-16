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

const { EMPTY_FILTERS, fold, buildModel, isActive, evaluate, listEntries, countLit, familyRange, PLAYER_CHOICES, TIME_CHOICES, locationChoices, effectiveLocation, UNKNOWN_LOCATION } = await import(pathToFileURL(join(FOLDER, 'filter-rules.js')).href);

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

/**
 * Builds a collection of one base game with the given player span and playing time, plus optional owned expansions.
 * @param {object} base The fields of the base game.
 * @param {object[]} [extras] The fields of owned expansions, each becoming a linked expansion record.
 * @returns {object[]} The records, base first.
 */
function family(base, extras = []) {
  const baseRecord = record(100, 'Family Base', base);
  const expansions = extras.map((fields, index) => record(101 + index, 'Family Layer ' + index, { isExpansion: true, ...fields }));

  baseRecord.expansions = expansions.map(link);
  expansions.forEach((expansion) => {
    expansion.bases = [link(baseRecord)];
  });

  return [baseRecord, ...expansions];
}

const withPlayers = (n) => ({ ...EMPTY_FILTERS, players: n });
const withTime = (t) => ({ ...EMPTY_FILTERS, time: t });
const isLit = (records, state, entryId = 100) => evaluate(state, buildModel(records)).lit.has(String(entryId));

test('the family range runs from the smallest known minimum to the largest known maximum', () => {
  assert.deepEqual(familyRange([{ minPlayers: 3, maxPlayers: 4 }, { minPlayers: 5, maxPlayers: 6 }]), { min: 3, max: 6 });
  assert.deepEqual(familyRange([{ minPlayers: 1, maxPlayers: 2 }, { minPlayers: 5, maxPlayers: 6 }]), { min: 1, max: 6 });
});

test('a missing or zero value on one member is ignored, and a family with no usable end has no range', () => {
  assert.deepEqual(familyRange([{ minPlayers: 2, maxPlayers: 4 }, { minPlayers: 0, maxPlayers: null }]), { min: 2, max: 4 });
  assert.equal(familyRange([{ minPlayers: 2 }, { minPlayers: 3, maxPlayers: 0 }]), null);
  assert.equal(familyRange([{ maxPlayers: 4 }]), null);
  assert.equal(familyRange([{ minPlayers: -1, maxPlayers: 4 }]), null);
  assert.equal(familyRange([{}]), null);
  assert.equal(familyRange([]), null);
});

test('five lights both boxes of a family that an owned expansion makes playable for five', () => {
  const records = family({ minPlayers: 3, maxPlayers: 4, playTime: 60 }, [{ minPlayers: 5, maxPlayers: 6, playTime: 15 }]);

  assert.deepEqual([...evaluate(withPlayers(5), buildModel(records)).lit].sort(), ['100', '101']);
  assert.equal(isLit(records, withPlayers(2)), false);
});

test('the players test is inclusive at both ends', () => {
  const records = family({ minPlayers: 2, maxPlayers: 4 });

  assert.deepEqual([1, 2, 3, 4, 5].map((n) => isLit(records, withPlayers(n))), [false, true, true, true, false]);
});

test('eight or more lights when the family maximum is 8 or more', () => {
  assert.equal(isLit(family({ minPlayers: 2, maxPlayers: 7 }), withPlayers(8)), false);
  assert.equal(isLit(family({ minPlayers: 2, maxPlayers: 8 }), withPlayers(8)), true);
  assert.equal(isLit(family({ minPlayers: 2, maxPlayers: 12 }), withPlayers(8)), true);
});

test('ranges that leave a gap are joined into one span', () => {
  const records = family({ minPlayers: 1, maxPlayers: 2 }, [{ minPlayers: 5, maxPlayers: 6 }]);

  assert.deepEqual([1, 2, 3, 4, 5, 6, 7].map((n) => isLit(records, withPlayers(n))), [true, true, true, true, true, true, false]);
});

test('a family with an unknown player range stays in shadow while a count is chosen, and lights for any', () => {
  const unknown = family({ minPlayers: 2, maxPlayers: 0 });

  assert.equal(isLit(unknown, withPlayers(2)), false);
  assert.equal(isLit(family({ minPlayers: null, maxPlayers: 4 }), withPlayers(2)), false);
  assert.equal(isLit(unknown, EMPTY_FILTERS), true);
});

test('choosing a count only changes the lit set, not the records or their order', () => {
  const records = family({ minPlayers: 2, maxPlayers: 4 });
  const before = JSON.stringify(records);

  evaluate(withPlayers(3), buildModel(records));

  assert.equal(JSON.stringify(records), before);
});

test('the chip sets are the fixed ones', () => {
  assert.deepEqual([...PLAYER_CHOICES], [1, 2, 3, 4, 5, 6, 7, 8]);
  assert.deepEqual([...TIME_CHOICES], [30, 45, 60, 90, 120]);
});

test('a time lights a game done within it, judged at the stated playing time', () => {
  assert.equal(isLit(family({ playTime: 60 }), withTime(60)), true);
  assert.equal(isLit(family({ playTime: 61 }), withTime(60)), false);
  assert.equal(isLit(family({ playTime: 90, minPlayTime: 45, maxPlayTime: 90 }), withTime(60)), false);
  assert.equal(isLit(family({ playTime: 90, minPlayTime: 45, maxPlayTime: 90 }), withTime(90)), true);
  assert.equal(isLit(family({ playTime: 90, minPlayTime: 45, maxPlayTime: 90 }), withTime(120)), true);
});

test('an expansion with a shorter time does not change the family result', () => {
  const records = family({ playTime: 90 }, [{ playTime: 20 }]);

  assert.equal(isLit(records, withTime(60)), false);
  assert.equal(isLit(records, withTime(60), 101), false);
  assert.equal(isLit(records, withTime(90), 101), true);
});

test('an orphan expansion is judged by its own time', () => {
  const orphan = record(5, 'Loose Layer', { isExpansion: true, bases: [{ entryId: null, gameId: 1, title: 'Not Owned' }], playTime: 30 });

  assert.equal(isLit([orphan], withTime(30), 5), true);
  assert.equal(isLit([{ ...orphan, playTime: 31 }], withTime(30), 5), false);
});

test('a playing time of 0 or missing stays in shadow while a time is chosen', () => {
  assert.equal(isLit(family({ playTime: 0 }), withTime(120)), false);
  assert.equal(isLit(family({ playTime: null }), withTime(120)), false);
  assert.equal(isLit(family({}), withTime(120)), false);
  assert.equal(isLit(family({}), EMPTY_FILTERS), true);
});

test('search, players and time combine with AND, and an entry is lit when one of its families is', () => {
  const records = family({ minPlayers: 2, maxPlayers: 4, playTime: 60 });
  const both = { ...EMPTY_FILTERS, q: 'family base', players: 4, time: 60 };

  assert.equal(isLit(records, both), true);
  assert.equal(isLit(records, { ...both, q: 'nothing like it' }), false);
  assert.equal(isLit(records, { ...both, players: 5 }), false);
  assert.equal(isLit(records, { ...both, time: 45 }), false);
  assert.equal(isActive(withTime(45)), true);
  assert.equal(isActive(withPlayers(2)), true);
});

const PLACES = [
  { id: 'aaaa0001', name: 'Cabinet A', games: 2 },
  { id: 'bbbb0002', name: 'Shelf B', games: 1 },
  { id: 'cccc0003', name: 'Lent out', games: 0 },
];

/**
 * Builds a collection with a base on one place, its expansion on another, and games with no place.
 * @returns {object[]}
 */
function placedCollection() {
  const expansion = record(12, 'Harbour Lights', { isExpansion: true, locationId: 'bbbb0002' });
  const base = record(11, 'Lantern & Harbour', { expansions: [link(expansion)], locationId: 'aaaa0001' });
  expansion.bases = [link(base)];

  return [base, expansion, record(13, 'Quiet Quarry', { locationId: 'aaaa0001' }), record(14, 'Salt Road')];
}

const placed = (location, more = {}) => ({ ...EMPTY_FILTERS, location, ...more });
const litWhere = (state, records = placedCollection(), places = PLACES) => [...evaluate(state, buildModel(records, places)).lit].sort();

test('a chosen location lights each box by its own place, so an expansion kept apart lights while its base dims', () => {
  assert.deepEqual(litWhere(placed('bbbb0002')), ['12']);
  assert.deepEqual(litWhere(placed('aaaa0001')), ['11', '13']);
});

test('unknown lights only the boxes with no location', () => {
  assert.deepEqual(litWhere(placed(UNKNOWN_LOCATION)), ['14']);
});

test('a location combines with search by AND', () => {
  assert.deepEqual(litWhere(placed('aaaa0001', { q: 'quarry' })), ['13']);
  assert.deepEqual(litWhere(placed('bbbb0002', { q: 'quarry' })), []);
});

test('a location combines with players and time by AND', () => {
  const records = placedCollection();

  records[2] = { ...records[2], minPlayers: 2, maxPlayers: 4, playTime: 45 };
  assert.deepEqual(litWhere(placed('aaaa0001', { players: 3 }), records), ['13']);
  assert.deepEqual(litWhere(placed('aaaa0001', { players: 3, time: 30 }), records), []);
});

test('an id that is not offered falls back to Any and is not a filter', () => {
  const model = buildModel(placedCollection(), PLACES);

  assert.equal(effectiveLocation('cccc0003', model), null);
  assert.equal(effectiveLocation('zzzz9999', model), null);
  assert.equal(effectiveLocation(7, model), null);
  assert.equal(evaluate(placed('zzzz9999'), model).active, false);
  assert.deepEqual(litWhere(placed('zzzz9999')), ['11', '12', '13', '14']);
});

test('the choices are the owner places that hold a game in the owner order, and unknown only while some game has none', () => {
  const choices = locationChoices(buildModel(placedCollection(), PLACES));

  assert.deepEqual(choices.places.map((place) => place.name), ['Cabinet A', 'Shelf B']);
  assert.equal(choices.unknown, true);
  assert.equal(locationChoices(buildModel(placedCollection().slice(0, 3), PLACES)).unknown, false);
});

test('with no owner locations nothing is offered and unknown is not a choice', () => {
  const model = buildModel(placedCollection());
  const choices = locationChoices(model);

  assert.deepEqual(choices.places, []);
  assert.equal(choices.unknown, false);
  assert.equal(effectiveLocation(UNKNOWN_LOCATION, model), null);
});
