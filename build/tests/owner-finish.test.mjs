/**
 * Checks the finish panel script with no browser: a tile chosen while a save is under way is saved after it, and a failed save puts
 * the tile of the stored finish back. The script is read from disk next to its shared helpers and run against a stand-in for the
 * form, its tiles and the status line, with the browser's fetch replaced by a controllable one.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

/**
 * Imports a script from the page's script folder through a data URL, with the imports of its siblings pointed at their own data URLs.
 * @param {string} name The file name inside the script folder.
 * @returns {Promise<object>} The module's exports.
 */
async function loadPageScript(name) {
  const read = (file) => readFileSync(new URL('../../Cabinet.Service/wwwroot/js/' + file, import.meta.url), 'utf8');
  const shared = 'data:text/javascript;base64,' + Buffer.from(read('owner-shared.js')).toString('base64');
  const format = 'data:text/javascript;base64,' + Buffer.from(read('owner-format.js')).toString('base64');
  const source = read(name).replace("'./owner-shared.js'", `'${shared}'`).replace("'./owner-format.js'", `'${format}'`);

  return import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));
}

const { initFinish } = await loadPageScript('owner-finish.js');

/**
 * Makes a stand-in for the document that can make elements and holds nothing else.
 * @returns {object}
 */
function documentStub() {
  const doc = {
    createElement: () => {
      const node = {
        children: [],
        dataset: {},
        textContent: '',
        append: (...nodes) => node.children.push(...nodes),
        replaceChildren: () => {
          node.children = [];
        },
        removeAttribute() {},
        addEventListener() {},
        querySelector: () => null,
      };

      return node;
    },
    querySelector: () => null,
    querySelectorAll: () => [],
    getElementById: () => null,
  };

  return doc;
}

/**
 * Builds the panel: a form with a group of tiles, one of them checked, and a status line.
 * @param {string[]} values The finish of each tile.
 * @param {string} checkedValue The finish that is checked at the start.
 * @returns {{ doc: object, radios: object[], region: object, click: (value: string) => void }}
 */
function panel(values, checkedValue) {
  const doc = documentStub();
  const radios = values.map((value) => ({ value, checked: value === checkedValue, matches: () => true }));
  const listeners = {};
  const fieldset = {
    addEventListener: (type, handler) => {
      listeners[type] = handler;
    },
    querySelector: () => radios.find((radio) => radio.checked) ?? null,
    querySelectorAll: () => radios,
  };
  const saveButton = { hidden: false };
  const form = {
    getAttribute: () => '?handler=Finish',
    addEventListener() {},
    querySelector: (selector) => (selector === 'fieldset' ? fieldset : saveButton),
  };
  const region = doc.createElement('div');

  region.ownerDocument = doc;
  doc.querySelector = (selector) => (selector === 'form[data-finish-form]' ? form : null);
  doc.getElementById = (id) => (id === 'finish-status' ? region : null);

  return {
    doc,
    radios,
    region,
    click(value) {
      for (const radio of radios) {
        radio.checked = radio.value === value;
      }

      listeners.click({ target: radios.find((radio) => radio.value === value) });
    },
  };
}

/**
 * Replaces the browser's fetch with one that holds each answer until the test releases it.
 * @returns {{ sent: string[], release: (status: number, body: string) => void }}
 */
function heldFetch() {
  const sent = [];
  const waiting = [];

  globalThis.fetch = (address, init) => {
    sent.push(init.body.get('finish'));

    return new Promise((resolve) => {
      waiting.push((status, body) => resolve({ status, text: async () => body }));
    });
  };

  return { sent, release: (status, body) => waiting.shift()(status, body) };
}

/**
 * Lets the pending promise callbacks run.
 * @returns {Promise<void>}
 */
const settle = () => new Promise((resolve) => setImmediate(resolve));

test('a tile chosen while a save is under way is saved after it', async () => {
  const fetchStub = heldFetch();
  const view = panel(['classic', 'oak', 'walnut'], 'classic');

  initFinish(view.doc);
  view.click('oak');
  view.click('walnut');
  await settle();
  assert.deepEqual(fetchStub.sent, ['oak']);

  fetchStub.release(200, '{"outcome":"saved","revision":2}');
  await settle();
  assert.deepEqual(fetchStub.sent, ['oak', 'walnut']);

  fetchStub.release(200, '{"outcome":"saved","revision":3}');
  await settle();
  assert.equal(view.radios.find((radio) => radio.checked).value, 'walnut');
  assert.equal(fetchStub.sent.length, 2);
});

test('a failed save puts the tile of the stored finish back', async () => {
  const fetchStub = heldFetch();
  const view = panel(['classic', 'oak'], 'classic');

  initFinish(view.doc);
  view.click('oak');
  await settle();
  fetchStub.release(500, '');
  await settle();

  assert.equal(view.radios.find((radio) => radio.checked).value, 'classic');
  assert.equal(view.region.dataset.problem, 'true');
});

test('after one finish saved, a later failure goes back to that finish and not the first one', async () => {
  const fetchStub = heldFetch();
  const view = panel(['classic', 'oak', 'walnut'], 'classic');

  initFinish(view.doc);
  view.click('oak');
  await settle();
  fetchStub.release(200, '{"outcome":"saved","revision":2}');
  await settle();
  view.click('walnut');
  await settle();
  fetchStub.release(500, '');
  await settle();

  assert.equal(view.radios.find((radio) => radio.checked).value, 'oak');
});

test('choosing the checked tile again still saves once', async () => {
  const fetchStub = heldFetch();
  const view = panel(['classic'], 'classic');

  initFinish(view.doc);
  view.click('classic');
  await settle();
  fetchStub.release(200, '{"outcome":"noChange","revision":1}');
  await settle();

  assert.deepEqual(fetchStub.sent, ['classic']);
  assert.equal(view.region.dataset.problem, undefined);
});
