/**
 * Checks the view switch with the plain Node test runner: where the page scrolls after a switch, what the device is told to remember and
 * when, that an open card is closed before anything is fetched, and that a switch is an instant swap under reduced motion and a view
 * transition otherwise. The page is a small stand-in: two radios, a live region, a document that records its events, and a fetch the
 * test answers.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { copyFileSync, mkdtempSync, readdirSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

const PAGE_FOLDER = mkdtempSync(join(tmpdir(), 'cabinet-locations-view-'));

writeFileSync(join(PAGE_FOLDER, 'package.json'), '{"type":"module"}');

for (const name of readdirSync(new URL('../../Cabinet.Service/wwwroot/js/', import.meta.url)).filter((file) => file.endsWith('.js'))) {
  copyFileSync(new URL('../../Cabinet.Service/wwwroot/js/' + name, import.meta.url), join(PAGE_FOLDER, name));
}

const { initLocationsView, scrollAfterSwitch, isLocationDocument, shelfScrollTarget, FOUND_HOLD_MS } = await import(pathToFileURL(join(PAGE_FOLDER, 'locations-view.js')).href);

const COPY = {
  viewAllAnnounce: 'all',
  viewLocationAnnounce: 'location',
  locationViewFailed: 'failed',
};
const ETAG = '"layout-1"';
const GOOD_DOCUMENT = { layout: ETAG, locations: [{ id: 'a1b2c3d4', name: 'Cabinet A', sections: [] }] };
const settle = () => new Promise((resolve) => setImmediate(resolve));

test('the page scrolls up to put the cabinet 16 px under the bar, and only when the cabinet slipped above it', () => {
  assert.equal(scrollAfterSwitch(-500, 112, 2000), 1372);
  assert.equal(scrollAfterSwitch(100, 112, 50), 22);
  assert.equal(scrollAfterSwitch(112, 112, 0), null);
  assert.equal(scrollAfterSwitch(300, 112, 0), null);
});

test('Show on shelf puts the heading 16 px under the bar when the heading and the box both fit below it', () => {
  const target = shelfScrollTarget({ headingTop: 600, boxTop: 700, boxBottom: 800, barBottom: 112, viewportHeight: 900, scrollY: 1000 });

  assert.equal(target, 1000 + 600 - 112 - 16);
});

test('Show on shelf centres the box below the bar when the heading and the box do not both fit', () => {
  const target = shelfScrollTarget({ headingTop: -900, boxTop: 1200, boxBottom: 1300, barBottom: 112, viewportHeight: 900, scrollY: 2000 });
  const room = 900 - 112;

  assert.equal(target, 2000 + 1250 - (112 + room / 2));
});

test('Show on shelf puts the top of a box taller than the space below the bar under the bar, and never scrolls above the page', () => {
  assert.equal(shelfScrollTarget({ headingTop: 0, boxTop: 500, boxBottom: 1500, barBottom: 0, viewportHeight: 600, scrollY: 100 }), 100 + 500 - 16);
  assert.equal(shelfScrollTarget({ headingTop: -50, boxTop: 40, boxBottom: 140, barBottom: 112, viewportHeight: 900, scrollY: 30 }), 0);
});

test('the box that Show on shelf found stays outlined for two seconds', () => {
  assert.equal(FOUND_HOLD_MS, 2000);
});

test('a scroll target is never below the current position', () => {
  for (const [top, bottom, scrollY] of [[-1, 112, 0], [0, 112, 900], [111, 112, 40], [-2000, 140, 5000]]) {
    const target = scrollAfterSwitch(top, bottom, scrollY);

    assert.ok(target !== null && target < scrollY, `${top}, ${bottom}, ${scrollY}`);
  }
});

test('a document is usable only with a layout tag and a list of locations that have sections', () => {
  assert.equal(isLocationDocument(GOOD_DOCUMENT), true);
  assert.equal(isLocationDocument({}), false);
  assert.equal(isLocationDocument({ layout: ETAG }), false);
  assert.equal(isLocationDocument({ layout: ETAG, locations: [{ name: 'x' }] }), false);
  assert.equal(isLocationDocument(null), false);
});

/**
 * Builds the stand-in page, starts the switch on it and returns what a test needs to drive and read it.
 * @param {object} [options] What differs from the plain case.
 * @param {'all' | 'location'} [options.initialView] The view remembered at the start.
 * @param {boolean} [options.reducedMotion] Whether the visitor asked for less motion.
 * @param {boolean} [options.transitions] Whether the browser has view transitions.
 * @param {(url: string) => { ok: boolean, body: unknown } | Promise<{ ok: boolean, body: unknown }>} [options.answer] Answers a request.
 * @returns {object} The page, the switch and the records.
 */
function startPage({ initialView = 'all', reducedMotion = false, transitions = false, answer = () => ({ ok: true, body: GOOD_DOCUMENT }) } = {}) {
  const log = [];
  const listeners = new Map();
  const attributes = new Set();
  const writes = [];
  const scrolls = [];
  const live = { textContent: '' };
  const radios = ['all', 'location'].map((value) => {
    const handlers = [];

    return { value, checked: value === initialView, addEventListener: (_name, handler) => handlers.push(handler), fire: () => handlers.forEach((handler) => handler()) };
  });
  const toggle = { hidden: false, querySelectorAll: () => radios };
  const storage = { setItem: (key, value) => writes.push([key, value]) };
  const state = { top: 0, bottom: 112, scrollY: 0 };

  globalThis.document = {
    getElementById: (id) => (id === 'filter-live' ? live : null),
    addEventListener: (name, handler) => listeners.set(name, handler),
    documentElement: {
      setAttribute: (name) => {
        attributes.add(name);
        log.push('mark');
      },
      removeAttribute: (name) => attributes.delete(name),
    },
  };

  if (transitions) {
    globalThis.document.startViewTransition = (update) => {
      log.push('transition');
      const updated = Promise.resolve().then(update);

      return { ready: updated, updateCallbackDone: updated, finished: updated.then(() => settle()) };
    };
  }

  globalThis.window = {
    matchMedia: () => ({ matches: reducedMotion }),
    get scrollY() {
      return state.scrollY;
    },
    scrollTo: (options) => scrolls.push(options),
  };
  globalThis.fetch = async (url) => {
    log.push('fetch');
    const reply = await answer(String(url));

    return { ok: reply.ok, json: async () => reply.body };
  };

  const view = initLocationsView({
    toggle,
    copy: COPY,
    getProfile: () => 'desktop',
    getSample: () => undefined,
    getLayoutEtag: () => ETAG,
    onSwitch: (next, doc) => log.push(`switch:${next}:${doc === null ? 'none' : 'doc'}`),
    initialView,
    storage,
    closeCard: async () => {
      log.push('close-card');
    },
    cabinet: { getBoundingClientRect: () => ({ top: state.top }) },
    bar: { getBoundingClientRect: () => ({ bottom: state.bottom }) },
  });

  return { view, radios, toggle, live, log, writes, scrolls, state, attributes, listeners };
}

/**
 * Clicks the radio of a view the way the visitor does and waits for the switch to settle.
 * @param {{ radios: { value: string, checked: boolean, fire: () => void }[] }} page The stand-in page.
 * @param {string} value The view.
 */
async function click(page, value) {
  for (const radio of page.radios) {
    radio.checked = radio.value === value;
  }

  page.radios.find((radio) => radio.value === value).fire();

  for (let turn = 0; turn < 8; turn += 1) {
    await settle();
  }
}

test('the remembered view is the one the switch starts in and its radio is checked', () => {
  const page = startPage({ initialView: 'location' });

  assert.equal(page.view.view, 'location');
  assert.equal(page.radios[1].checked, true);
  assert.deepEqual(page.writes, []);
});

test('the choice is written only after the chosen view is drawn', async () => {
  const page = startPage();

  await click(page, 'location');

  assert.deepEqual(page.log.filter((entry) => entry.startsWith('switch') || entry === 'fetch'), ['fetch', 'switch:location:doc']);
  assert.deepEqual(page.writes, [['cabinet-view', 'location']]);
  assert.equal(page.live.textContent, 'location');

  await click(page, 'all');

  assert.deepEqual(page.writes, [['cabinet-view', 'location'], ['cabinet-view', 'all']]);
});

test('a card is closed before the document is fetched and before anything is drawn', async () => {
  const page = startPage();

  await click(page, 'location');

  assert.deepEqual(page.log.filter((entry) => entry !== 'mark'), ['close-card', 'fetch', 'switch:location:doc']);
});

test('a failed request keeps the view, checks all together again, says so and writes nothing; the next switch asks again', async () => {
  let fail = true;
  const page = startPage({ answer: () => (fail ? { ok: false, body: {} } : { ok: true, body: GOOD_DOCUMENT }) });

  await click(page, 'location');

  assert.equal(page.log.some((entry) => entry.startsWith('switch')), false);
  assert.equal(page.radios[0].checked, true);
  assert.equal(page.live.textContent, 'failed');
  assert.deepEqual(page.writes, []);
  assert.equal(page.view.view, 'all');

  fail = false;
  await click(page, 'location');

  assert.equal(page.log.filter((entry) => entry === 'fetch').length, 2);
  assert.deepEqual(page.writes, [['cabinet-view', 'location']]);
});

test('an invalid body counts as a failed request', async () => {
  const page = startPage({ answer: () => ({ ok: true, body: {} }) });

  await click(page, 'location');

  assert.equal(page.live.textContent, 'failed');
  assert.deepEqual(page.writes, []);
});

test('a document that belongs to another layout is not drawn', async () => {
  const page = startPage({ answer: () => ({ ok: true, body: { ...GOOD_DOCUMENT, layout: '"other"' } }) });

  await click(page, 'location');

  assert.equal(page.log.some((entry) => entry.startsWith('switch')), false);
  assert.equal(page.live.textContent, 'failed');
  assert.deepEqual(page.writes, []);
});

test('giving up on the location view writes nothing', () => {
  const page = startPage({ initialView: 'location' });

  page.view.fallBack();

  assert.equal(page.view.view, 'all');
  assert.equal(page.radios[0].checked, true);
  assert.equal(page.live.textContent, 'failed');
  assert.deepEqual(page.writes, []);
});

test('showing the view quietly changes the switch and writes nothing', () => {
  const page = startPage({ initialView: 'location' });

  page.view.setView('all');

  assert.equal(page.radios[0].checked, true);
  assert.deepEqual(page.writes, []);
});

test('a filter pass that reports no locations returns to all together without remembering it', async () => {
  const page = startPage({ initialView: 'location' });

  page.listeners.get('cabinet:filtered')({ detail: { hasLocations: false } });
  await settle();
  await settle();

  assert.equal(page.toggle.hidden, true);
  assert.equal(page.view.view, 'all');
  assert.equal(page.radios[0].checked, true);
  assert.deepEqual(page.writes, []);
});

test('under reduced motion the swap is instant and no transition starts', async () => {
  const page = startPage({ reducedMotion: true, transitions: true });

  await click(page, 'location');

  assert.equal(page.log.includes('transition'), false);
  assert.equal(page.log.includes('mark'), false);
  assert.equal(page.log.includes('switch:location:doc'), true);
});

test('without view transitions the swap is instant', async () => {
  const page = startPage({ transitions: false });

  await click(page, 'location');

  assert.equal(page.log.includes('mark'), false);
  assert.equal(page.log.includes('switch:location:doc'), true);
});

test('a switch with view transitions marks the root, swaps once inside the transition and unmarks it afterwards', async () => {
  const page = startPage({ transitions: true });

  await click(page, 'location');

  assert.deepEqual(page.log.filter((entry) => entry !== 'close-card'), ['fetch', 'mark', 'transition', 'switch:location:doc']);
  assert.equal(page.attributes.has('data-switching-view'), false);
  assert.deepEqual(page.writes, [['cabinet-view', 'location']]);
});

test('the transition starts only after the document is in hand', async () => {
  const page = startPage({ transitions: true, answer: () => ({ ok: false, body: {} }) });

  await click(page, 'location');

  assert.equal(page.log.includes('transition'), false);
  assert.equal(page.attributes.has('data-switching-view'), false);
});

test('after a switch the page scrolls up when the cabinet slipped above the bar, and stays when it did not', async () => {
  const slipped = startPage();

  slipped.state.top = -500;
  slipped.state.scrollY = 2000;
  await click(slipped, 'location');

  assert.deepEqual(slipped.scrolls, [{ top: 1372, behavior: 'instant' }]);

  const resting = startPage();

  resting.state.top = 300;
  await click(resting, 'location');

  assert.deepEqual(resting.scrolls, []);
});
