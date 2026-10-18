/**
 * Checks what the page remembers of the visitor's choices with the plain Node test runner: the stored filters read back field by
 * field, the one coalesced write, the remembered view, the remembered open or folded filter panel, and storage that throws. The module is read from disk and imported through a
 * data URL, and the storage and timers are stand-ins.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const source = readFileSync(new URL('../../Cabinet.Service/wwwroot/js/filter-state.js', import.meta.url), 'utf8');
const { FILTERS_KEY, VIEW_KEY, PANEL_KEY, readFilters, createFilterWriter, readView, writeView, readPanel, writePanel } = await import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));

const EMPTY = { q: '', players: null, time: null, location: null };

/**
 * A stand-in for a storage that records what is written.
 * @param {Record<string, string>} [initial] What it holds at the start.
 * @returns {{ items: Map<string, string>, sets: number, removes: number, getItem: Function, setItem: Function, removeItem: Function }}
 */
function createStorage(initial = {}) {
  const storage = {
    items: new Map(Object.entries(initial)),
    sets: 0,
    removes: 0,
    getItem: (key) => storage.items.get(key) ?? null,
    setItem: (key, value) => {
      storage.sets += 1;
      storage.items.set(key, value);
    },
    removeItem: (key) => {
      storage.removes += 1;
      storage.items.delete(key);
    },
  };

  return storage;
}

/**
 * A storage whose every call throws, as a blocked or full one does.
 * @returns {{ getItem: Function, setItem: Function, removeItem: Function, attempts: number }}
 */
function createThrowingStorage() {
  const storage = {
    attempts: 0,
    getItem: () => {
      storage.attempts += 1;
      throw new Error('blocked');
    },
    setItem: () => {
      storage.attempts += 1;
      throw new Error('blocked');
    },
    removeItem: () => {
      storage.attempts += 1;
      throw new Error('blocked');
    },
  };

  return storage;
}

/**
 * Timers that only run when the test says so.
 * @returns {{ setTimer: Function, clearTimer: Function, fire: () => void, delays: number[], pending: () => number }}
 */
function createFakeTimers() {
  const timers = new Map();
  const delays = [];
  let nextId = 1;

  return {
    delays,
    setTimer(callback, delay) {
      const id = nextId;

      nextId += 1;
      timers.set(id, callback);
      delays.push(delay);

      return id;
    },
    clearTimer(id) {
      timers.delete(id);
    },
    fire() {
      const due = [...timers.values()];

      timers.clear();

      for (const callback of due) {
        callback();
      }
    },
    pending: () => timers.size,
  };
}

/**
 * Builds a storage that holds the given filters.
 * @param {unknown} value What to store under the filters key.
 * @returns {ReturnType<typeof createStorage>}
 */
const holding = (value) => createStorage({ [FILTERS_KEY]: typeof value === 'string' ? value : JSON.stringify(value) });

test('the keys are fixed and different', () => {
  assert.equal(FILTERS_KEY, 'cabinet-filters');
  assert.equal(VIEW_KEY, 'cabinet-view');
});

test('a valid stored object is read back whole', () => {
  const stored = { q: 'catan', players: 4, time: 60, location: 'a1b2c3d4' };

  assert.deepEqual(readFilters(holding(stored)), stored);
});

test('the unknown location is kept', () => {
  assert.equal(readFilters(holding({ q: '', players: null, time: null, location: 'unknown' })).location, 'unknown');
});

test('nothing stored, or a storage with no such entry, gives the empty state', () => {
  assert.deepEqual(readFilters(createStorage()), EMPTY);
  assert.deepEqual(readFilters(null), EMPTY);
  assert.deepEqual(readFilters(undefined), EMPTY);
});

test('players outside 1 to 8 reset on their own', () => {
  for (const players of [0, 9, -1, 2.5, '4', null, 'many']) {
    assert.deepEqual(readFilters(holding({ q: 'x', players, time: 60, location: null })), { q: 'x', players: null, time: 60, location: null });
  }
});

test('a time that is not one of the offered times resets on its own', () => {
  for (const time of [50, 0, 150, '60', null]) {
    assert.deepEqual(readFilters(holding({ q: 'x', players: 4, time, location: null })), { q: 'x', players: 4, time: null, location: null });
  }
});

test('a location that is not eight hex digits or unknown resets on its own', () => {
  for (const location of ['ABC', 'a1b2c3d', 'a1b2c3d45', 'A1B2C3D4', 'zzzzzzzz', '', 7, {}]) {
    assert.deepEqual(readFilters(holding({ q: 'x', players: 4, time: 30, location })), { q: 'x', players: 4, time: 30, location: null });
  }
});

test('a query of 300 characters is cut to 100 and a query that is not text is emptied', () => {
  assert.equal(readFilters(holding({ q: 'a'.repeat(300) })).q, 'a'.repeat(100));
  assert.equal(readFilters(holding({ q: 12 })).q, '');
});

test('a query is cut by characters, not in the middle of one', () => {
  const cut = readFilters(holding({ q: '\u{1F3B2}'.repeat(150) })).q;

  assert.equal([...cut].length, 100);
  assert.equal(cut, '\u{1F3B2}'.repeat(100));
});

test('what is not an object gives the empty state', () => {
  for (const raw of ['not json', '[1,2]', '"text"', '42', 'null', 'true']) {
    assert.deepEqual(readFilters(holding(raw)), EMPTY, raw);
  }
});

test('a storage that throws on read gives the empty state', () => {
  assert.deepEqual(readFilters(createThrowingStorage()), EMPTY);
});

test('a burst of changes is written once, 250 ms after the first', () => {
  const storage = createStorage();
  const timers = createFakeTimers();
  const writer = createFilterWriter({ storage, setTimer: timers.setTimer, clearTimer: timers.clearTimer });

  writer.write({ q: 'c', players: null, time: null, location: null });
  writer.write({ q: 'ca', players: null, time: null, location: null });
  writer.write({ q: 'cat', players: 4, time: null, location: null });

  assert.equal(storage.sets, 0);
  assert.deepEqual(timers.delays, [250]);

  timers.fire();

  assert.equal(storage.sets, 1);
  assert.deepEqual(JSON.parse(storage.items.get(FILTERS_KEY)), { q: 'cat', players: 4, time: null, location: null });
  assert.equal(timers.pending(), 0);
});

test('a change after a write starts a new wait', () => {
  const storage = createStorage();
  const timers = createFakeTimers();
  const writer = createFilterWriter({ storage, setTimer: timers.setTimer, clearTimer: timers.clearTimer });

  writer.write({ q: 'a', players: null, time: null, location: null });
  timers.fire();
  writer.write({ q: 'ab', players: null, time: null, location: null });
  timers.fire();

  assert.equal(storage.sets, 2);
  assert.equal(JSON.parse(storage.items.get(FILTERS_KEY)).q, 'ab');
});

test('flush writes the latest state at once and stops the wait', () => {
  const storage = createStorage();
  const timers = createFakeTimers();
  const writer = createFilterWriter({ storage, setTimer: timers.setTimer, clearTimer: timers.clearTimer });

  writer.write({ q: 'cat', players: null, time: 45, location: null });
  writer.flush();

  assert.equal(storage.sets, 1);
  assert.equal(timers.pending(), 0);

  writer.flush();
  assert.equal(storage.sets, 1);
});

test('a state with nothing chosen removes the entry', () => {
  const storage = holding({ q: 'cat', players: 4, time: null, location: null });
  const timers = createFakeTimers();
  const writer = createFilterWriter({ storage, setTimer: timers.setTimer, clearTimer: timers.clearTimer });

  writer.write({ q: '', players: null, time: null, location: null });
  timers.fire();

  assert.equal(storage.removes, 1);
  assert.equal(storage.items.has(FILTERS_KEY), false);
});

test('the writer keeps working when the storage throws', () => {
  const storage = createThrowingStorage();
  const timers = createFakeTimers();
  const writer = createFilterWriter({ storage, setTimer: timers.setTimer, clearTimer: timers.clearTimer });

  writer.write({ q: 'cat', players: null, time: null, location: null });
  assert.doesNotThrow(() => timers.fire());
  writer.write({ q: 'cats', players: null, time: null, location: null });
  assert.doesNotThrow(() => writer.flush());
  assert.equal(storage.attempts, 2);
});

test('the writer keeps working with no storage at all', () => {
  const timers = createFakeTimers();
  const writer = createFilterWriter({ storage: null, setTimer: timers.setTimer, clearTimer: timers.clearTimer });

  writer.write({ q: 'cat', players: null, time: null, location: null });

  assert.doesNotThrow(() => timers.fire());
});

test('the view reads back only all and location', () => {
  assert.equal(readView(createStorage({ [VIEW_KEY]: 'location' })), 'location');
  assert.equal(readView(createStorage({ [VIEW_KEY]: 'all' })), 'all');
  assert.equal(readView(createStorage({ [VIEW_KEY]: 'grid' })), 'all');
  assert.equal(readView(createStorage()), 'all');
  assert.equal(readView(null), 'all');
  assert.equal(readView(createThrowingStorage()), 'all');
});

test('the view is written under its own key and an unknown view is not written', () => {
  const storage = createStorage();

  writeView(storage, 'location');
  assert.equal(storage.items.get(VIEW_KEY), 'location');

  writeView(storage, 'grid');
  assert.equal(storage.items.get(VIEW_KEY), 'location');
  assert.equal(storage.sets, 1);
});

test('writing the view swallows a throwing storage', () => {
  assert.doesNotThrow(() => writeView(createThrowingStorage(), 'location'));
  assert.doesNotThrow(() => writeView(null, 'location'));
});

test('the panel reads back open only for open, and folded for anything else', () => {
  assert.equal(readPanel(createStorage({ [PANEL_KEY]: 'open' })), 'open');
  assert.equal(readPanel(createStorage({ [PANEL_KEY]: 'folded' })), 'folded');
  assert.equal(readPanel(createStorage()), 'folded', 'a first visit starts folded');
  assert.equal(readPanel(createStorage({ [PANEL_KEY]: 'pushed' })), 'folded', 'an unknown value means folded');
  assert.equal(readPanel(createStorage({ [PANEL_KEY]: 'OPEN' })), 'folded');
  assert.equal(readPanel(createStorage({ [PANEL_KEY]: '' })), 'folded');
  assert.equal(readPanel(null), 'folded');
  assert.equal(readPanel(createThrowingStorage()), 'folded');
});

test('the panel is written under its own fixed key, and only open or folded', () => {
  const storage = createStorage();

  assert.equal(PANEL_KEY, 'cabinet-filter-panel');

  writePanel(storage, 'open');
  assert.equal(storage.items.get(PANEL_KEY), 'open');

  writePanel(storage, 'folded');
  assert.equal(storage.items.get(PANEL_KEY), 'folded');

  for (const value of ['push', 'rail', 'over', '', null, undefined, 1]) {
    writePanel(storage, value);
  }

  assert.equal(storage.items.get(PANEL_KEY), 'folded');
  assert.equal(storage.sets, 2);
  assert.deepEqual([...storage.items.keys()], [PANEL_KEY], 'nothing else is written');
});

test('the panel key is apart from the filters and the view', () => {
  assert.notEqual(PANEL_KEY, FILTERS_KEY);
  assert.notEqual(PANEL_KEY, VIEW_KEY);
});

test('writing the panel swallows a throwing storage', () => {
  assert.doesNotThrow(() => writePanel(createThrowingStorage(), 'open'));
  assert.doesNotThrow(() => writePanel(null, 'folded'));
});

test('the module never touches a cookie', () => {
  assert.equal(source.includes('document' + '.cookie'), false);
});
