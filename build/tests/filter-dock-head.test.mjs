/**
 * Checks the head script that reserves the filter dock's column before the first paint, with the plain Node test runner: it sets the
 * panel open beside the cabinet only when the device remembers it open and the window is wide enough for that, and the folded rail in
 * every other case, including storage that throws. Its key is the one the page's state module writes, its width query is the one the
 * dock module uses, it writes nothing and it adds no global name. The script runs in an isolated context against a fake root, a fake
 * storage and a fake media query, so what it may touch is exactly what the test hands it.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { copyFileSync, mkdtempSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';
import vm from 'node:vm';
import { fakeRoot, fakeStorage } from './fake-page.mjs';

const SCRIPTS = new URL('../../Cabinet.Service/wwwroot/js/', import.meta.url);
const HEAD_SOURCE = readFileSync(new URL('filter-dock-head.js', SCRIPTS), 'utf8');
const COMMENT_SLASHES = '/'.repeat(2);
const FOLDER = mkdtempSync(join(tmpdir(), 'cabinet-filter-dock-head-'));

writeFileSync(join(FOLDER, 'package.json'), '{"type":"module"}');

for (const name of readdirSync(SCRIPTS).filter((file) => file.endsWith('.js'))) {
  copyFileSync(new URL(name, SCRIPTS), join(FOLDER, name));
}

const { PANEL_KEY } = await import(pathToFileURL(join(FOLDER, 'filter-state.js')).href);
const { PUSH_QUERY } = await import(pathToFileURL(join(FOLDER, 'filter-dock.js')).href);

/**
 * Runs the head script against a fake page.
 * @param {object} setup What the page holds before the script runs.
 * @param {Record<string, string>} [setup.stored] What the device's local storage holds.
 * @param {boolean} [setup.throws] Whether every use of the storage throws.
 * @param {boolean} [setup.blocked] Whether reaching the storage at all throws, as a browser that blocks it does.
 * @param {boolean} [setup.wide] Whether the window is wide enough for the panel to stand beside the cabinet.
 * @returns {{ root: object, storage: object, context: object, window: object, keys: string[], queries: string[], writes: number }}
 */
function runScript({ stored = {}, throws = false, blocked = false, wide = true } = {}) {
  const root = fakeRoot({});
  const base = fakeStorage({ throws, initial: stored });
  const keys = [];
  const queries = [];
  let writes = 0;
  const storage = {
    getItem: (key) => {
      keys.push(key);

      return base.getItem(key);
    },
    setItem: (key, value) => {
      writes += 1;
      base.setItem(key, value);
    },
  };
  const window = {
    matchMedia: (query) => {
      queries.push(query);

      return { matches: wide && query === PUSH_QUERY, media: query };
    },
  };

  Object.defineProperty(window, 'localStorage', {
    get: () => {
      if (blocked) {
        throw new Error('storage blocked');
      }

      return storage;
    },
    enumerable: true,
  });

  const context = vm.createContext({ window, document: { documentElement: root } });

  vm.runInContext(HEAD_SOURCE, context);

  return { root, storage, context, window, keys, queries, writes };
}

test('the head script is a classic script: no comment slashes, no import, no export, and it is small', () => {
  assert.equal(HEAD_SOURCE.includes(COMMENT_SLASHES), false);
  assert.doesNotMatch(HEAD_SOURCE, /^\s*(import|export)\b/m);
  assert.ok(Buffer.byteLength(HEAD_SOURCE) < 2048, 'one read and one attribute');
});

test('a panel left open at a push width is open beside the cabinet before the first paint', () => {
  const { root } = runScript({ stored: { [PANEL_KEY]: 'open' }, wide: true });

  assert.equal(root.dataset.filterDock, 'push');
});

test('every other case gives the folded rail', () => {
  const cases = [
    { name: 'folded', stored: { [PANEL_KEY]: 'folded' } },
    { name: 'a first visit', stored: {} },
    { name: 'an unknown value', stored: { [PANEL_KEY]: 'pushed' } },
    { name: 'an empty value', stored: { [PANEL_KEY]: '' } },
    { name: 'storage that throws', stored: { [PANEL_KEY]: 'open' }, throws: true },
    { name: 'storage that cannot be reached', stored: { [PANEL_KEY]: 'open' }, blocked: true },
    { name: 'open below the push width, never a fly-over at load', stored: { [PANEL_KEY]: 'open' }, wide: false },
  ];

  for (const { name, ...setup } of cases) {
    const { root } = runScript(setup);

    assert.equal(root.dataset.filterDock, 'rail', name);
  }
});

test('the key it reads is the key the state module writes, and it reads nothing else', () => {
  const { keys } = runScript({ stored: { [PANEL_KEY]: 'open' } });

  assert.deepEqual(keys, [PANEL_KEY]);
  assert.equal(PANEL_KEY, 'cabinet-filter-panel');
});

test('the width it asks about is the push query of the dock module', () => {
  const { queries } = runScript({ stored: { [PANEL_KEY]: 'open' } });

  assert.deepEqual(queries, [PUSH_QUERY]);
});

test('it writes nothing to the storage', () => {
  for (const value of ['open', 'folded', 'pushed']) {
    const { writes } = runScript({ stored: { [PANEL_KEY]: value } });

    assert.equal(writes, 0, value);
  }

  assert.equal(runScript({ stored: {} }).writes, 0, 'a first visit');
});

test('it sets one attribute on the root and adds no global name', () => {
  const { root, context, window } = runScript({ stored: { [PANEL_KEY]: 'open' } });

  assert.deepEqual(Object.keys(root.dataset), ['filterDock']);
  assert.deepEqual(Object.keys(context).sort(), ['document', 'window']);
  assert.deepEqual(Object.keys(window).sort(), ['localStorage', 'matchMedia']);
});
