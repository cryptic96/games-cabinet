/**
 * Checks the scripts that turn the cabinet's lights on with the plain Node test runner: the head script that lights the page
 * before its first paint when this tab has already faded the lights on, and the module that fades them on after the first paint.
 * The head script runs in an isolated context against a fake page, so what it may touch is exactly what the test hands it.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';
import { fakeDate, fakeRoot, fakeStorage } from './fake-page.mjs';

const COMMENT_SLASHES = '/'.repeat(2);
const EVENING_SOURCE = readFileSync(new URL('../../Cabinet.Service/wwwroot/js/evening.js', import.meta.url), 'utf8');
const LOOK_SOURCE = readFileSync(new URL('../../Cabinet.Service/wwwroot/js/look.js', import.meta.url), 'utf8');
const { followClock, initLook, isLit, refreshLamp } = await import('data:text/javascript;base64,' + Buffer.from(LOOK_SOURCE).toString('base64'));
const WINTER_MIDDAY = [2027, 0, 15, 12, 0];

/**
 * Runs the head script in an isolated context against a fake page.
 * @param {object} root The fake root element.
 * @param {object} storage The fake session storage.
 * @param {number[]} [local] The local clock reading the script sees; a winter midday, when it is not evening, by default.
 * @returns {{ context: object, window: object }} The context and the window it saw.
 */
function runHeadScript(root, storage, local = WINTER_MIDDAY) {
  const window = {};

  Object.defineProperty(window, 'sessionStorage', { get: () => storage, enumerable: true });

  const context = vm.createContext({ window, document: { documentElement: root }, Date: fakeDate({ local }) });

  vm.runInContext(EVENING_SOURCE, context);

  return { context, window };
}

test('the head script is a classic script: no comment slashes, no import, no export, and it is small', () => {
  assert.equal(EVENING_SOURCE.includes(COMMENT_SLASHES), false);
  assert.doesNotMatch(EVENING_SOURCE, /^\s*(import|export)\b/m);
  assert.ok(Buffer.byteLength(EVENING_SOURCE) < 8192, 'a small table and a few rules');
});

test('the head script assigns only the cabinetEvening bridge and leaks no other global', () => {
  const { context, window } = runHeadScript(fakeRoot({ lights: 'on' }), fakeStorage());

  assert.deepEqual(Object.keys(context).sort(), ['Date', 'document', 'window']);
  assert.deepEqual(Object.keys(window).sort(), ['cabinetEvening', 'sessionStorage']);
  assert.equal(typeof window.cabinetEvening.key, 'string');
  assert.equal(typeof window.cabinetEvening.markLit, 'function');
  assert.equal(typeof window.cabinetEvening.isEvening, 'function');
  assert.equal(typeof window.cabinetEvening.msToNextChange, 'function');
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
  const cleared = [];
  const intervals = [];
  const events = [];
  const doc = {
    documentElement: root,
    visibilityState: 'visible',
    querySelector: () => ({}),
    addEventListener: (type, handler) => events.push({ type, handler }),
  };

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
    clearTimeout: (id) => cleared.push(id),
    setInterval: (callback, milliseconds) => {
      intervals.push({ callback, milliseconds });

      return intervals.length;
    },
  };

  await initLook(doc);

  return { root, storage, timers, cleared, intervals, events, doc };
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
    setInterval: () => 1,
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

/**
 * Builds a stand-in for the clock the head script provides, whose answers the test changes by hand.
 * @param {boolean} evening Whether it is evening at the start.
 * @returns {{ evening: boolean, isEvening: Function, msToNextChange: Function }} The stand-in.
 */
function fakeClock(evening) {
  const clock = {
    evening,
    isEvening: () => clock.evening,
    msToNextChange: () => 90000,
  };

  return clock;
}

test('an open page lights when the clock says evening: the minute check sets the evening marker, fades and remembers the tab', async () => {
  const clock = fakeClock(false);
  const { root, storage, intervals } = await startLook({ dataset: { lights: 'evenings' }, bridge: clock });

  assert.equal(root.hasAttribute('data-lit'), false);
  assert.equal(intervals.length, 1);
  assert.equal(intervals[0].milliseconds, 60000);

  clock.evening = true;
  intervals[0].callback();

  assert.equal(root.hasAttribute('data-evening'), true);
  assert.equal(root.hasAttribute('data-fading'), true, 'the lamp fades on');
  assert.equal(root.hasAttribute('data-lit'), true);
  assert.deepEqual([...storage.values.entries()], [['cabinet-lit', '1']]);
});

test('at sunrise the open page loses the evening marker and the lamp fades out', async () => {
  const clock = fakeClock(true);
  const { root, intervals } = await startLook({ dataset: { lights: 'evenings' }, bridge: clock });

  assert.equal(root.hasAttribute('data-lit'), true, 'the load itself lit the evening page');

  for (const listener of [...root.listeners]) {
    listener({ propertyName: 'opacity' });
  }

  assert.equal(root.hasAttribute('data-fading'), false, 'the first fade is over');

  clock.evening = false;
  intervals[0].callback();

  assert.equal(root.hasAttribute('data-evening'), false);
  assert.equal(root.hasAttribute('data-lit'), false);
  assert.equal(root.hasAttribute('data-fading'), true, 'the lamp fades out');
});

test('the minute check does nothing in a hidden tab, and the tab catches up the moment it is shown', async () => {
  const clock = fakeClock(false);
  const { root, doc, intervals, events } = await startLook({ dataset: { lights: 'evenings' }, bridge: clock });
  const change = events.find(({ type }) => type === 'visibilitychange');

  clock.evening = true;
  doc.visibilityState = 'hidden';
  intervals[0].callback();
  change.handler();

  assert.equal(root.hasAttribute('data-lit'), false, 'nothing runs while the tab is hidden');

  doc.visibilityState = 'visible';
  change.handler();

  assert.equal(root.hasAttribute('data-evening'), true);
  assert.equal(root.hasAttribute('data-lit'), true);
});

test('one timer waits for the next boundary and is set again each time it fires', async () => {
  const clock = fakeClock(false);
  const { root, timers, cleared } = await startLook({ dataset: { lights: 'evenings' }, bridge: clock, fade: '0ms' });

  assert.equal(timers.length, 1);
  assert.equal(timers[0].milliseconds, 90250, 'the next change plus a small margin');

  clock.evening = true;
  timers[0].callback();

  assert.equal(root.hasAttribute('data-lit'), true);
  assert.equal(timers.length, 2, 'set again');
  assert.equal(cleared.includes(1), true, 'the old timer was cleared before the new one was set');
  assert.equal(timers[1].milliseconds, 90250);
});

test('always on and off never arm the boundary timer, and off never carries the evening marker', async () => {
  const on = await startLook({ dataset: { lights: 'on' }, bridge: fakeClock(true), fade: '0ms' });
  const off = await startLook({ dataset: { lights: 'off' }, bridge: fakeClock(true), fade: '0ms' });

  assert.equal(on.timers.length, 0);
  assert.equal(off.timers.length, 0);
  assert.equal(off.root.hasAttribute('data-evening'), false);
  assert.equal(off.root.hasAttribute('data-lit'), false);
  assert.equal(on.root.hasAttribute('data-lit'), true);
});

test('under reduced motion the evening comes and goes with no fading hint', async () => {
  const clock = fakeClock(false);
  const { root, intervals } = await startLook({ dataset: { lights: 'evenings' }, bridge: clock, fade: '0ms' });

  clock.evening = true;
  intervals[0].callback();

  assert.equal(root.hasAttribute('data-lit'), true);
  assert.equal(root.hasAttribute('data-fading'), false);
});

test('without the head script there is no clock to follow and nothing changes', async () => {
  const { root, intervals, timers } = await startLook({ dataset: { lights: 'evenings' } });

  intervals[0].callback();

  assert.equal(root.hasAttribute('data-evening'), false);
  assert.equal(root.hasAttribute('data-lit'), false);
  assert.equal(timers.length, 0);
});

test('following the clock of a page twice sets up one interval and one listener', async () => {
  const { doc, intervals, events } = await startLook({ dataset: { lights: 'evenings' }, bridge: fakeClock(false) });

  followClock(doc);

  assert.equal(intervals.length, 1);
  assert.equal(events.filter(({ type }) => type === 'visibilitychange').length, 1);
});
