/**
 * Checks the scripts that turn the cabinet's lights on with the plain Node test runner: the head script that lights the page
 * before its first paint when this tab has already faded the lights on, and the module that fades them on after the first paint.
 * The head script runs in an isolated context against a fake page, so what it may touch is exactly what the test hands it.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const COMMENT_SLASHES = '/'.repeat(2);
const EVENING_SOURCE = readFileSync(new URL('../../Cabinet.Service/wwwroot/js/evening.js', import.meta.url), 'utf8');
const LOOK_SOURCE = readFileSync(new URL('../../Cabinet.Service/wwwroot/js/look.js', import.meta.url), 'utf8');
const { initLook, isLit, refreshLamp } = await import('data:text/javascript;base64,' + Buffer.from(LOOK_SOURCE).toString('base64'));

/**
 * Builds a fake root element that keeps its attributes and its listeners.
 * @param {object} dataset The data attributes the server wrote.
 * @param {string[]} [attributes] Attributes that are present before any script runs.
 * @returns {object} The root.
 */
function fakeRoot(dataset, attributes = []) {
  const present = new Set(attributes);
  const listeners = new Set();

  return {
    dataset,
    present,
    listeners,
    hasAttribute: (name) => present.has(name),
    setAttribute: (name) => present.add(name),
    removeAttribute: (name) => present.delete(name),
    getAttribute: (name) => (present.has(name) ? '' : (dataset[name.replace(/^data-/, '')] ?? null)),
    addEventListener: (type, handler) => listeners.add(handler),
    removeEventListener: (type, handler) => listeners.delete(handler),
  };
}

/**
 * Builds a fake storage that holds what is written to it, or throws on every use.
 * @param {object} [options] Whether the storage refuses every use, and what it holds before the script runs.
 * @returns {object} The storage.
 */
function fakeStorage({ throws = false, initial = {} } = {}) {
  const values = new Map(Object.entries(initial));

  return {
    values,
    getItem: (key) => {
      if (throws) {
        throw new Error('storage refused');
      }

      return values.has(key) ? values.get(key) : null;
    },
    setItem: (key, value) => {
      if (throws) {
        throw new Error('storage refused');
      }

      values.set(key, value);
    },
  };
}

/**
 * Runs the head script in an isolated context against a fake page.
 * @param {object} root The fake root element.
 * @param {object} storage The fake session storage.
 * @returns {{ context: object, window: object }} The context and the window it saw.
 */
function runHeadScript(root, storage) {
  const window = {};

  Object.defineProperty(window, 'sessionStorage', { get: () => storage, enumerable: true });

  const context = vm.createContext({ window, document: { documentElement: root } });

  vm.runInContext(EVENING_SOURCE, context);

  return { context, window };
}

test('the head script is a classic script: no comment slashes, no import, no export, and it is small', () => {
  assert.equal(EVENING_SOURCE.includes(COMMENT_SLASHES), false);
  assert.doesNotMatch(EVENING_SOURCE, /^\s*(import|export)\b/m);
  assert.ok(Buffer.byteLength(EVENING_SOURCE) < 2048, 'about a kilobyte');
});

test('the head script assigns only the cabinetEvening bridge and leaks no other global', () => {
  const { context, window } = runHeadScript(fakeRoot({ lights: 'on' }), fakeStorage());

  assert.deepEqual(Object.keys(context).sort(), ['document', 'window']);
  assert.deepEqual(Object.keys(window).sort(), ['cabinetEvening', 'sessionStorage']);
  assert.equal(typeof window.cabinetEvening.key, 'string');
  assert.equal(typeof window.cabinetEvening.markLit, 'function');
});

test('with the lights on and this tab already faded in, the head script lights the page before the first paint', () => {
  const root = fakeRoot({ lights: 'on' });
  const { window } = runHeadScript(root, fakeStorage());

  assert.equal(root.hasAttribute('data-lit'), false);

  const marked = fakeRoot({ lights: 'on' });
  runHeadScript(marked, fakeStorage({ initial: { [window.cabinetEvening.key]: '1' } }));

  assert.equal(marked.hasAttribute('data-lit'), true);
  assert.equal(marked.hasAttribute('data-settled'), false, 'only the module settles the page');
});

test('a tab flag that is not the fixed value does not light the page', () => {
  const root = fakeRoot({ lights: 'on' });
  const key = runHeadScript(fakeRoot({ lights: 'on' }), fakeStorage()).window.cabinetEvening.key;

  runHeadScript(root, fakeStorage({ initial: { [key]: 'yes' } }));

  assert.equal(root.hasAttribute('data-lit'), false);
});

test('storage that throws leaves the page unlit so it fades on every load', () => {
  const root = fakeRoot({ lights: 'on' });

  assert.doesNotThrow(() => runHeadScript(root, fakeStorage({ throws: true })));
  assert.equal(root.hasAttribute('data-lit'), false);
});

test('lights off and evenings by day are not lit by the head script, flag or no flag', () => {
  const key = runHeadScript(fakeRoot({ lights: 'on' }), fakeStorage()).window.cabinetEvening.key;

  for (const lights of ['off', 'evenings', 'unknown', undefined]) {
    const root = fakeRoot({ lights });

    runHeadScript(root, fakeStorage({ initial: { [key]: '1' } }));

    assert.equal(root.hasAttribute('data-lit'), false, String(lights));
  }
});

test('the bridge marks a root lit only when its attributes say the lights are lit', () => {
  const { window } = runHeadScript(fakeRoot({ lights: 'on' }), fakeStorage());
  const cases = [
    [{ lights: 'on' }, [], true],
    [{ lights: 'off' }, [], false],
    [{ lights: 'evenings' }, [], false],
    [{ lights: 'evenings' }, ['data-evening'], true],
  ];

  for (const [dataset, attributes, expected] of cases) {
    const root = fakeRoot(dataset, attributes);

    assert.equal(window.cabinetEvening.markLit(root), expected, JSON.stringify([dataset, attributes]));
    assert.equal(root.hasAttribute('data-lit'), expected);
  }
});

test('the lights are lit for always on, and for evenings only while it is evening', () => {
  assert.equal(isLit('on', false), true);
  assert.equal(isLit('off', true), false);
  assert.equal(isLit('evenings', true), true);
  assert.equal(isLit('evenings', false), false);
  assert.equal(isLit(undefined, true), false);
  assert.equal(isLit('on', undefined), true);
});

test('refreshing a root sets or removes the lit marker from the attributes it carries', () => {
  const root = fakeRoot({ lights: 'evenings' });

  assert.equal(refreshLamp(root), false);
  root.setAttribute('data-evening');
  assert.equal(refreshLamp(root), true);
  assert.equal(root.hasAttribute('data-lit'), true);
  root.removeAttribute('data-evening');
  assert.equal(refreshLamp(root), false);
  assert.equal(root.hasAttribute('data-lit'), false);
});

/**
 * Sets up the globals the module reads and runs its start-up against a fake page whose cabinet is drawn at once. The fake window
 * stays installed afterwards, because the timers and listeners the start-up left behind still use it.
 * @param {object} setup The look on the root, the markers already there, the storage and the fade length.
 * @returns {Promise<object>} The root, the storage and the timers the module asked for.
 */
async function startLook({ dataset, attributes = [], storage = fakeStorage(), fade = '2000ms', bridge = undefined }) {
  const root = fakeRoot(dataset, attributes);
  const timers = [];
  const drawn = [];

  globalThis.window = {
    sessionStorage: storage,
    cabinetEvening: bridge,
    getComputedStyle: () => ({ getPropertyValue: () => fade }),
    requestAnimationFrame: (callback) => {
      callback(0);

      return 0;
    },
    setTimeout: (callback, milliseconds) => {
      timers.push({ callback, milliseconds });

      return timers.length;
    },
    clearTimeout: () => undefined,
  };

  const doc = {
    documentElement: root,
    querySelector: () => ({}),
    addEventListener: (type, handler) => drawn.push({ type, handler }),
  };

  await initLook(doc);

  return { root, storage, timers };
}

test('on the first load in a tab the lights settle, fade and light in one task and the tab is remembered', async () => {
  const { root, storage, timers } = await startLook({ dataset: { lights: 'on' } });

  assert.equal(root.hasAttribute('data-settled'), true);
  assert.equal(root.hasAttribute('data-fading'), true);
  assert.equal(root.hasAttribute('data-lit'), true);
  assert.deepEqual([...storage.values.entries()], [['cabinet-lit', '1']]);
  assert.equal(timers.length, 1);
  assert.equal(timers[0].milliseconds > 2000, true, 'the fading hint ends shortly after the fade should have');
});

test('the fading hint ends with the opacity transition', async () => {
  const { root } = await startLook({ dataset: { lights: 'on' } });

  assert.equal(root.hasAttribute('data-fading'), true);

  for (const listener of [...root.listeners]) {
    listener({ propertyName: 'color' });
  }

  assert.equal(root.hasAttribute('data-fading'), true, 'another property ending does not count');

  for (const listener of [...root.listeners]) {
    listener({ propertyName: 'opacity' });
  }

  assert.equal(root.hasAttribute('data-fading'), false);
  assert.equal(root.listeners.size, 0);
});

test('the key of the tab flag comes from the head script when it is there', async () => {
  const { storage } = await startLook({ dataset: { lights: 'on' }, bridge: { key: 'invented-key' } });

  assert.deepEqual([...storage.values.keys()], ['invented-key']);
});

test('a page the head script already lit only settles, and the flag is left alone', async () => {
  const { root, storage, timers } = await startLook({ dataset: { lights: 'on' }, attributes: ['data-lit'] });

  assert.equal(root.hasAttribute('data-settled'), true);
  assert.equal(root.hasAttribute('data-fading'), false);
  assert.equal(root.hasAttribute('data-lit'), true);
  assert.equal(storage.values.size, 0);
  assert.equal(timers.length, 0);
});

test('with the lights off, or evenings by day, the page only settles and nothing is remembered', async () => {
  for (const lights of ['off', 'evenings']) {
    const { root, storage } = await startLook({ dataset: { lights } });

    assert.equal(root.hasAttribute('data-settled'), true, lights);
    assert.equal(root.hasAttribute('data-lit'), false, lights);
    assert.equal(root.hasAttribute('data-fading'), false, lights);
    assert.equal(storage.values.size, 0, lights);
  }
});

test('storage that throws still lights the page, so it fades on every load', async () => {
  const { root } = await startLook({ dataset: { lights: 'on' }, storage: fakeStorage({ throws: true }) });

  assert.equal(root.hasAttribute('data-lit'), true);
  assert.equal(root.hasAttribute('data-fading'), true);
});

test('with a fade of nothing the lights are lit at once and no fading hint is set', async () => {
  const { root, timers } = await startLook({ dataset: { lights: 'on' }, fade: '0ms' });

  assert.equal(root.hasAttribute('data-lit'), true);
  assert.equal(root.hasAttribute('data-settled'), true);
  assert.equal(root.hasAttribute('data-fading'), false);
  assert.equal(timers.length, 0);
});

test('a fade given in seconds is read as seconds', async () => {
  const { timers } = await startLook({ dataset: { lights: 'on' }, fade: '1.5s' });

  assert.equal(timers[0].milliseconds >= 1500 && timers[0].milliseconds < 2000, true);
});

test('start-up waits for the cabinet to be drawn when no light layer exists yet', async () => {
  const root = fakeRoot({ lights: 'on' });
  const handlers = [];
  const previous = globalThis.window;

  globalThis.window = {
    sessionStorage: fakeStorage(),
    getComputedStyle: () => ({ getPropertyValue: () => '2000ms' }),
    requestAnimationFrame: (callback) => {
      callback(0);

      return 0;
    },
    setTimeout: () => 1,
    clearTimeout: () => undefined,
  };

  try {
    const doc = { documentElement: root, querySelector: () => null, addEventListener: (type, handler) => handlers.push({ type, handler }) };
    const started = initLook(doc);

    await Promise.resolve();
    assert.equal(root.hasAttribute('data-settled'), false, 'nothing is marked before the cabinet is drawn');
    assert.deepEqual(handlers.map(({ type }) => type), ['cabinet:drawn']);

    handlers[0].handler();
    await started;

    assert.equal(root.hasAttribute('data-lit'), true);
  } finally {
    globalThis.window = previous;
  }
});

test('starting up twice on one page changes nothing the second time', async () => {
  const first = await startLook({ dataset: { lights: 'on' } });
  const marks = [...first.root.present];
  const previous = globalThis.window;

  globalThis.window = { sessionStorage: fakeStorage(), setTimeout: () => 1 };

  try {
    await initLook({ documentElement: first.root, querySelector: () => ({}), addEventListener: () => undefined });
  } finally {
    globalThis.window = previous;
  }

  assert.deepEqual([...first.root.present], marks);
});
