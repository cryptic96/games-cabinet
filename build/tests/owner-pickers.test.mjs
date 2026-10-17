/**
 * Checks the owner list pickers with no browser: a choice a page that goes to the background still holds is sent at once with the
 * keepalive flag, not behind a request that is still under way, while an ordinary choice waits its turn in the queue. The scripts
 * are copied to a folder of their own with a module package file, so they import each other as they do in the page, and run
 * against a tiny stand-in for the elements they touch.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { copyFileSync, mkdtempSync, readdirSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

const SCRIPT_FOLDER = new URL('../../Cabinet.Service/wwwroot/js/', import.meta.url);
const COPY_FOLDER = mkdtempSync(join(tmpdir(), 'cabinet-owner-pickers-'));

writeFileSync(join(COPY_FOLDER, 'package.json'), '{"type":"module"}');

for (const name of readdirSync(SCRIPT_FOLDER).filter((file) => file.endsWith('.js'))) {
  copyFileSync(new URL(name, SCRIPT_FOLDER), join(COPY_FOLDER, name));
}

const { initPickers } = await import(pathToFileURL(join(COPY_FOLDER, 'owner-games.js')).href);

/**
 * Makes a stand-in for an element that can hold children and listeners.
 * @param {object} doc The document that made it.
 * @returns {object}
 */
function element(doc) {
  const node = {
    ownerDocument: doc,
    children: [],
    dataset: {},
    listeners: {},
    textContent: '',
    append: (...nodes) => node.children.push(...nodes),
    replaceChildren: () => {
      node.children = [];
    },
    removeAttribute() {},
    querySelector: () => null,
    addEventListener: (type, handler) => {
      node.listeners[type] = handler;
    },
  };

  return node;
}

/**
 * Builds a page with one picker that holds the value `a`, and starts the pickers on it.
 * @returns {{ doc: object, select: object, queued: Function[], fetched: object[] }}
 */
function pickerPage() {
  const doc = element(null);
  const region = element(doc);
  const select = element(doc);
  const queued = [];
  const fetched = [];
  const form = {
    parentElement: { querySelector: () => region },
    getAttribute: () => '?handler=Assign',
    elements: { gameId: { value: '7' }, collectionId: { value: '70' } },
    querySelector: () => ({ hidden: false }),
    addEventListener() {},
  };

  doc.ownerDocument = doc;
  doc.createElement = () => element(doc);
  doc.querySelectorAll = (selector) => (selector === 'select[data-picker]' ? [select] : []);
  doc.visibilityState = 'visible';
  select.options = [{ defaultSelected: true, value: 'a' }];
  select.value = 'a';
  select.closest = () => form;
  globalThis.fetch = async (address, init) => {
    fetched.push({ address, keepalive: init.keepalive === true, body: init.body });

    return { status: 200, text: async () => '{"outcome":"saved","revision":2}' };
  };

  initPickers(doc, {
    enqueue: (task) => queued.push(task),
    showProblem() {},
    undoer: () => () => {},
    gamesChanged() {},
  });

  return { doc, select, queued, fetched };
}

/**
 * Lets the pending promise callbacks run.
 * @returns {Promise<void>}
 */
const settle = () => new Promise((resolve) => setImmediate(resolve));

test('a choice waiting when the page goes to the background is sent at once, behind no earlier request', async () => {
  const page = pickerPage();

  page.select.listeners.keydown({ key: 'ArrowDown', timeStamp: 1000 });
  page.select.value = 'b';
  page.select.listeners.change({ timeStamp: 1010 });
  page.doc.visibilityState = 'hidden';
  page.doc.listeners.visibilitychange();
  await settle();

  assert.equal(page.queued.length, 0, 'nothing waited in the queue');
  assert.equal(page.fetched.length, 1);
  assert.equal(page.fetched[0].keepalive, true);
  assert.equal(page.fetched[0].body.get('locationId'), 'b');
  assert.equal(page.fetched[0].body.get('gameId'), '7');
  assert.equal(page.fetched[0].body.get('collectionId'), '70');
});

test('a choice made with a pointer waits its turn in the queue', async () => {
  const page = pickerPage();

  page.select.value = 'b';
  page.select.listeners.change({ timeStamp: 50000 });
  await settle();

  assert.equal(page.fetched.length, 0, 'nothing was sent before the queue ran');
  assert.equal(page.queued.length, 1);

  await page.queued[0]();

  assert.equal(page.fetched.length, 1);
  assert.equal(page.fetched[0].keepalive, false);
});
