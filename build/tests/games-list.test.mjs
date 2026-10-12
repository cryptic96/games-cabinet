/**
 * Checks the games list behind the cabinet with the plain Node test runner: the order of the entries, the nesting of expansions
 * under their base games, the name each entry announces in English and Dutch, the title-only list shown before the card data
 * arrives, and the list's keys on a small fake page. The scripts are loaded from a temporary copy of the script folder so the
 * module's relative imports resolve.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { copyFileSync, mkdtempSync, readdirSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

const FOLDER = mkdtempSync(join(tmpdir(), 'cabinet-games-list-'));

writeFileSync(join(FOLDER, 'package.json'), '{"type":"module"}');

for (const name of readdirSync(new URL('../../Cabinet.Service/wwwroot/js/', import.meta.url)).filter((file) => file.endsWith('.js'))) {
  copyFileSync(new URL('../../Cabinet.Service/wwwroot/js/' + name, import.meta.url), join(FOLDER, name));
}

const { orderEntries, entryName, titleOnlyEntries, initGamesList } = await import(pathToFileURL(join(FOLDER, 'games-list.js')).href);
const { copyFor } = await import(pathToFileURL(join(FOLDER, 'copy.js')).href);

const en = copyFor('en');
const nl = copyFor('nl');
const DASH = '–';

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

const titles = (entries) => entries.map((entry) => entry.record.title);

test('base games run A to Z and numbers count as numbers', () => {
  const entries = orderEntries([record(1, 'Game 10'), record(2, 'Zephyr'), record(3, 'Game 2'), record(4, 'Anchor')], 'en');

  assert.deepEqual(titles(entries), ['Anchor', 'Game 2', 'Game 10', 'Zephyr']);
});

test('titles that are equal under the collation stay separate, ordered by year and then entry id', () => {
  const entries = orderEntries([
    record(9, 'Éclair', { year: 2020 }),
    record(5, 'Eclair', { year: 2020 }),
    record(7, 'Eclair', { year: 2010 }),
    record(3, 'Eclair'),
  ], 'en');

  assert.deepEqual(entries.map((entry) => entry.entryId), [7, 5, 9, 3]);
});

test('two copies of one game are two entries', () => {
  const entries = orderEntries([record(12, 'Lantern & Harbour', { year: 2019 }), record(11, 'Lantern & Harbour', { year: 2019 })], 'en');

  assert.deepEqual(entries.map((entry) => entry.entryId), [11, 12]);
});

test('an expansion is nested under its base game in the order the base game lists them', () => {
  const second = record(21, 'Quiet Tides', { isExpansion: true });
  const first = record(22, 'Night Market', { isExpansion: true });
  const base = record(20, 'Lantern & Harbour', { expansions: [link(first), link(second)] });
  const entries = orderEntries([second, base, first], 'en');

  assert.equal(entries.length, 1);
  assert.deepEqual(entries[0].children.map((child) => child.entryId), [22, 21]);
  assert.equal(entries[0].children[0].baseTitle, 'Lantern & Harbour');
});

test('an expansion of two owned base games appears under both and is never merged', () => {
  const shared = record(30, 'Night Market', { isExpansion: true });
  const harbour = record(31, 'Harbour', { expansions: [link(shared)] });
  const lantern = record(32, 'Lantern', { expansions: [link(shared)] });
  const entries = orderEntries([shared, lantern, harbour], 'en');

  assert.deepEqual(titles(entries), ['Harbour', 'Lantern']);
  assert.deepEqual(entries.map((entry) => entry.children.map((child) => child.entryId)), [[30], [30]]);
  assert.deepEqual(entries.map((entry) => entry.children[0].baseTitle), ['Harbour', 'Lantern']);
});

test('an expansion with no owned base game sits at the top level under its own title and names its base game', () => {
  const orphan = record(40, 'Bay Lights', { isExpansion: true, bases: [{ entryId: null, gameId: 900, title: 'Distant Bay' }] });
  const entries = orderEntries([record(41, 'Zephyr'), orphan, record(42, 'Anchor')], 'en');

  assert.deepEqual(titles(entries), ['Anchor', 'Bay Lights', 'Zephyr']);
  assert.equal(entries[1].baseTitle, 'Distant Bay');
  assert.deepEqual(entries[1].children, []);
  assert.equal(entryName(entries[1].record, en, entries[1].baseTitle), 'Bay Lights, expansion for Distant Bay');
});

test('no records give no entries and one record gives one entry', () => {
  assert.deepEqual(orderEntries([], 'en'), []);
  assert.equal(orderEntries([record(1, 'Only Game')], 'en').length, 1);
});

test('the order of four hundred records is the same on every call and whatever order they arrive in', () => {
  const records = Array.from({ length: 400 }, (_, index) => record(index + 1, 'Game ' + ((index * 7) % 50), { year: 2000 + (index % 5) }));
  const first = orderEntries(records, 'en').map((entry) => entry.entryId);
  const second = orderEntries(records, 'en').map((entry) => entry.entryId);
  const reversed = orderEntries([...records].reverse(), 'en').map((entry) => entry.entryId);

  assert.equal(first.length, 400);
  assert.deepEqual(second, first);
  assert.deepEqual(reversed, first);
});

test('the input records are never reordered', () => {
  const records = [record(1, 'Zephyr'), record(2, 'Anchor')];

  orderEntries(records, 'en');

  assert.deepEqual(records.map((item) => item.entryId), [1, 2]);
});

const FULL = Object.freeze({ year: 2019, minPlayers: 2, maxPlayers: 4, playTime: 75, minPlayTime: 60, maxPlayTime: 90, weight: 2.4 });

test('a full entry announces title, year, players, minutes and the weight word', () => {
  assert.equal(entryName(record(1, 'Lantern & Harbour', FULL), en), `Lantern & Harbour (2019), 2${DASH}4 players, 60${DASH}90 minutes, medium-light`);
});

test('one player and one stated time read as singular players and plain minutes', () => {
  const quiet = record(2, 'Quiet Quarry', { year: 2021, minPlayers: 1, maxPlayers: 1, playTime: 45, weight: 3 });

  assert.equal(entryName(quiet, en), 'Quiet Quarry (2021), 1 player, 45 minutes, medium');
});

test('an expansion adds the game it is for after the year, or just says expansion', () => {
  const market = record(3, 'Night Market', { year: 2021, isExpansion: true, minPlayers: 2, maxPlayers: 4, playTime: 45 });

  assert.equal(entryName(market, en, 'Lantern & Harbour'), `Night Market (2021), expansion for Lantern & Harbour, 2${DASH}4 players, 45 minutes`);
  assert.equal(entryName({ ...market, maxPlayers: undefined, minPlayers: 2, playTime: undefined }, en), 'Night Market (2021), expansion, 2 players');
});

test('missing facts are left out with their comma, a missing year drops the brackets and a game with nothing known is its title', () => {
  assert.equal(entryName(record(4, 'Quiet Quarry', { weight: 3 }), en), 'Quiet Quarry, medium');
  assert.equal(entryName(record(4, 'Quiet Quarry', { year: 2021, playTime: 30 }), en), 'Quiet Quarry (2021), 30 minutes');
  assert.equal(entryName(record(4, 'Quiet Quarry'), en), 'Quiet Quarry');
  assert.equal(entryName(record(4, 'Quiet Quarry', { year: 0, minPlayers: 0, maxPlayers: null, weight: null }), en), 'Quiet Quarry');
});

test('a blank title reads as the untitled text in each language', () => {
  assert.equal(entryName(record(5, ''), en), 'Untitled game');
  assert.equal(entryName(record(5, '   '), nl), 'Spel zonder titel');
  assert.equal(entryName({ entryId: 5 }, nl), 'Spel zonder titel');
});

test('the Dutch entry writes spelers, minuten and its own weight and expansion words', () => {
  assert.equal(entryName(record(1, 'Lantern & Harbour', FULL), nl), `Lantern & Harbour (2019), 2${DASH}4 spelers, 60${DASH}90 minuten, vrij licht`);
  assert.equal(entryName(record(2, 'Quiet Quarry', { minPlayers: 1, maxPlayers: 1, playTime: 45, weight: 3 }), nl), 'Quiet Quarry, 1 speler, 45 minuten, gemiddeld');
  assert.equal(entryName(record(3, 'Night Market', { isExpansion: true, year: 2021 }), nl, 'Lantern & Harbour'), 'Night Market (2021), uitbreiding op Lantern & Harbour');
  assert.equal(entryName(record(3, 'Night Market', { isExpansion: true }), nl), 'Night Market, uitbreiding');
});

test('the title is never changed, only the words around it follow the language', () => {
  const odd = '  Ünïcode & <b>Tides</b>  ';

  assert.equal(entryName(record(1, odd, { minPlayers: 2 }), nl), odd + ', 2 spelers');
});

/**
 * Builds the placement of one drawn box.
 * @param {number} entryId The collection entry.
 * @param {string} title The title.
 * @param {object} [more] The other fields.
 * @returns {object}
 */
const placement = (entryId, title, more = {}) => ({ entryId, gameId: entryId * 10, title, kind: 'spine', ...more });

test('the title-only list has one entry per game, A to Z, with expansions marked where the box says so', () => {
  const entries = titleOnlyEntries([
    placement(1, 'Zephyr'),
    placement(2, 'Night Market', { kind: 'expansionLayer', isExpansion: true, baseTitle: 'Lantern & Harbour' }),
    placement(3, 'Bay Lights', { kind: 'orphanExpansion', isExpansion: true }),
    placement(1, 'Zephyr', { kind: 'moreMarker' }),
    placement(1, 'Zephyr'),
    placement(4, 'Anchor'),
    placement(5, ''),
  ], en);

  assert.deepEqual(entries.map((entry) => entry.name), [
    'Anchor',
    'Bay Lights, expansion',
    'Night Market, expansion for Lantern & Harbour',
    'Zephyr',
    'Untitled game',
  ]);
  assert.ok(entries.every((entry) => entry.children.length === 0));
});

test('the title-only list takes a map of placements and speaks Dutch', () => {
  const entries = titleOnlyEntries(new Map([['2', placement(2, 'Night Market', { isExpansion: true, kind: 'orphanExpansion' })]]), nl);

  assert.deepEqual(entries.map((entry) => entry.name), ['Night Market, uitbreiding']);
  assert.deepEqual(titleOnlyEntries([], nl), []);
});

/** A tiny element, enough for the list: children, attributes, text, listeners, focus and the two selectors the list uses. */
class FakeElement {
  constructor(page, tagName) {
    this.page = page;
    this.tagName = tagName.toUpperCase();
    this.children = [];
    this.parent = null;
    this.attributes = new Map();
    this.dataset = {};
    this.listeners = {};
    this.ownText = '';
    this.className = '';
  }

  get textContent() {
    return this.ownText + this.children.map((child) => child.textContent).join('');
  }

  set textContent(value) {
    this.children = [];
    this.ownText = String(value);
  }

  setAttribute(name, value) {
    this.attributes.set(name, String(value));
  }

  getAttribute(name) {
    return this.attributes.get(name) ?? null;
  }

  removeAttribute(name) {
    this.attributes.delete(name);
  }

  addEventListener(type, handler) {
    (this.listeners[type] ??= []).push(handler);
  }

  append(...nodes) {
    for (const node of nodes) {
      node.parent = this;
      this.children.push(node);
    }
  }

  replaceChildren(...nodes) {
    this.children = [];
    this.ownText = '';
    this.append(...nodes);
  }

  descendants() {
    return this.children.flatMap((child) => [child, ...child.descendants()]);
  }

  matches(selector) {
    return selector.startsWith('.') ? this.className.split(' ').includes(selector.slice(1)) : this.tagName === selector.toUpperCase();
  }

  querySelector(selector) {
    return this.descendants().find((node) => node.matches(selector)) ?? null;
  }

  querySelectorAll(selector) {
    return this.descendants().filter((node) => node.matches(selector));
  }

  closest(selector) {
    for (let node = this; node !== null; node = node.parent) {
      if (node.matches(selector)) {
        return node;
      }
    }

    return null;
  }

  focus() {
    this.page.activeElement = this;
  }

  press(key, modifiers = {}) {
    const event = { key, target: this, defaultPrevented: false, preventDefault() { this.defaultPrevented = true; }, ...modifiers };
    let node = this;

    while (node !== null) {
      (node.listeners.keydown ?? []).forEach((handler) => handler(event));
      node = node.parent;
    }

    return event;
  }

  click() {
    (this.listeners.click ?? []).forEach((handler) => handler({ target: this }));
  }
}

/**
 * Builds a fake list section and starts the list on it.
 * @param {object} [options] copy and language override the English defaults.
 * @returns {object} The section, the fake page, the controls and the log of opened cards.
 */
function startList({ copy = en, language = 'en' } = {}) {
  const page = { activeElement: null };
  const section = new FakeElement(page, 'section');
  const heading = new FakeElement(page, 'h2');
  const list = new FakeElement(page, 'ul');
  const opened = [];

  heading.className = 'games-list-heading';
  list.className = 'games-list-items';
  section.append(heading, list);
  page.createElement = (tag) => new FakeElement(page, tag);
  globalThis.document = page;

  const controls = initGamesList({ section, copy, language, openCard: (entryId, from) => opened.push({ entryId, opener: from.opener }) });

  return { page, section, heading, list, controls, opened, buttons: () => list.querySelectorAll('BUTTON') };
}

const SMALL = [
  record(1, 'Lantern & Harbour', { year: 2019, minPlayers: 2, maxPlayers: 4, weight: 2.4, expansions: [link(record(2, 'Night Market'))] }),
  record(2, 'Night Market', { isExpansion: true, bases: [link(record(1, 'Lantern & Harbour'))] }),
  record(3, 'Quiet Quarry'),
];

test('the list draws buttons that are not tab stops and each one opens a card with itself as the opener', () => {
  const { controls, buttons, opened } = startList();

  controls.showCards(SMALL);

  const all = buttons();

  assert.deepEqual(all.map((button) => button.textContent), [
    `Lantern & Harbour (2019), 2${DASH}4 players, medium-light`,
    'Night Market, expansion for Lantern & Harbour',
    'Quiet Quarry',
  ]);
  assert.ok(all.every((button) => button.getAttribute('tabindex') === '-1' && button.getAttribute('aria-haspopup') === 'dialog' && button.getAttribute('type') === 'button'));
  assert.equal(all[0].getAttribute('id'), 'games-list-start');
  assert.equal(all[1].getAttribute('id'), null);

  all[1].click();

  assert.deepEqual(opened, [{ entryId: '2', opener: all[1] }]);
});

test('arrow keys move through every entry in document order, nested ones included, and Home and End jump', () => {
  const { controls, buttons, page } = startList();

  controls.showCards(SMALL);

  const all = buttons();

  all[0].focus();

  const down = all[0].press('ArrowDown');

  assert.equal(page.activeElement, all[1]);
  assert.equal(down.defaultPrevented, true);

  all[1].press('ArrowDown');
  assert.equal(page.activeElement, all[2]);
  all[2].press('ArrowDown');
  assert.equal(page.activeElement, all[2]);
  all[2].press('ArrowUp');
  assert.equal(page.activeElement, all[1]);
  all[1].press('Home');
  assert.equal(page.activeElement, all[0]);
  all[0].press('ArrowUp');
  assert.equal(page.activeElement, all[0]);
  all[0].press('End');
  assert.equal(page.activeElement, all[2]);
});

test('Tab, Enter, Space and modified arrows are left alone', () => {
  const { controls, buttons, page } = startList();

  controls.showCards(SMALL);

  const all = buttons();

  all[0].focus();

  for (const key of ['Tab', 'Enter', ' ', 'ArrowRight']) {
    assert.equal(all[0].press(key).defaultPrevented, false, key);
  }

  assert.equal(all[0].press('ArrowDown', { altKey: true }).defaultPrevented, false);
  assert.equal(page.activeElement, all[0]);
});

test('title-only entries become full entries in place, so the entry that had focus keeps it', () => {
  const { controls, buttons, page } = startList();

  controls.showTitles([placement(1, 'Lantern & Harbour'), placement(3, 'Quiet Quarry')]);

  const before = buttons();

  assert.deepEqual(before.map((button) => button.textContent), ['Lantern & Harbour', 'Quiet Quarry']);
  before[1].focus();

  controls.showCards([SMALL[0], SMALL[2]].map((item) => ({ ...item, expansions: [] })));

  const after = buttons();

  assert.equal(after[1], before[1]);
  assert.equal(page.activeElement, before[1]);
  assert.equal(after[0].textContent, `Lantern & Harbour (2019), 2${DASH}4 players, medium-light`);
});

test('a list that gains nested entries keeps the buttons it already had and keeps focus on the same entry', () => {
  const { controls, buttons, page } = startList();

  controls.showTitles([placement(1, 'Lantern & Harbour'), placement(2, 'Night Market', { isExpansion: true }), placement(3, 'Quiet Quarry')]);

  const before = buttons();

  before[2].focus();
  controls.showCards(SMALL);

  const after = buttons();

  assert.equal(after.length, 3);
  assert.ok(before.every((button) => after.includes(button)));
  assert.equal(page.activeElement, before[2]);
  assert.equal(after[0].getAttribute('id'), 'games-list-start');
});

test('an unchanged shape only updates the text', () => {
  const { controls, list, buttons } = startList();

  controls.showCards(SMALL);

  const before = buttons();
  const replace = list.replaceChildren.bind(list);
  let replaced = 0;

  list.replaceChildren = (...nodes) => {
    replaced += 1;
    replace(...nodes);
  };
  controls.showCards(SMALL.map((item) => ({ ...item, title: item.title + '!' })));

  assert.equal(replaced, 0);
  assert.deepEqual(buttons(), before);
  assert.equal(buttons()[2].textContent, 'Quiet Quarry!');
});

test('the skip link target is the first entry, or the heading when the list is empty', () => {
  const { controls, heading, page, buttons } = startList();

  controls.focusStart();
  assert.equal(page.activeElement, heading);

  controls.showCards([]);
  controls.focusStart();
  assert.equal(page.activeElement, heading);
  assert.equal(buttons().length, 0);

  controls.showCards(SMALL);
  controls.focusStart();
  assert.equal(page.activeElement, buttons()[0]);
});

test('the Dutch list writes Dutch entry names around untouched titles', () => {
  const { controls, buttons } = startList({ copy: nl, language: 'nl' });

  controls.showCards(SMALL);

  assert.equal(buttons()[0].textContent, `Lantern & Harbour (2019), 2${DASH}4 spelers, vrij licht`);
  assert.equal(buttons()[1].textContent, 'Night Market, uitbreiding op Lantern & Harbour');
});
