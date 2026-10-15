/**
 * Checks, with the plain Node test runner, how the live look switch keeps the visitor's place when a new design changes the height of
 * the sections, and how it holds a look back while a box is being pulled out. The page is a fake whose sections move when the design
 * attribute changes, so the scroll position the switch ends with can be compared with where the section was before.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { copyFileSync, mkdtempSync, readdirSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

const PAGE_FOLDER = mkdtempSync(join(tmpdir(), 'cabinet-look-switch-'));

writeFileSync(join(PAGE_FOLDER, 'package.json'), '{"type":"module"}');

for (const name of readdirSync(new URL('../../Cabinet.Service/wwwroot/js/', import.meta.url)).filter((file) => file.endsWith('.js'))) {
  copyFileSync(new URL('../../Cabinet.Service/wwwroot/js/' + name, import.meta.url), join(PAGE_FOLDER, name));
}

const { applyLookStatus, sectionAtCentre, scrollCorrection } = await import(pathToFileURL(join(PAGE_FOLDER, 'look-switch.js')).href);

const BASE_MS = Date.parse('2030-01-15T12:00:00.000Z');
const BASE_LOOK = { design: 'classic', wood: 'brown', backs: 'wood', lights: 'off', depth: 'shaded', lightWall: false };
const LOOK_FIELDS = ['design', 'wood', 'backs', 'lights', 'depth'];
const VIEWPORT_HEIGHT = 900;
const FIRST_SECTION_TOP = 100;
const SECTION_COUNT = 4;
const SECTION_HEIGHTS = { classic: 500, library: 640, modern: 560 };
const PULL_MARKER = 'data-pull-kind';
const settle = () => new Promise((resolve) => setImmediate(resolve));

test('the section that holds the viewport centre is the one picked, and the nearest one when the centre falls in a gap', () => {
  const rects = [{ top: -400, bottom: 100 }, { top: 120, bottom: 700 }, { top: 760, bottom: 1300 }];

  assert.equal(sectionAtCentre(rects, 900), 1);
  assert.equal(sectionAtCentre(rects, 1700), 2);
  assert.equal(sectionAtCentre(rects, 1500), 2, 'the centre 750 lies in the gap, 50 below the second section and 10 above the third');
  assert.equal(sectionAtCentre([], 900), -1);
});

test('a centre in the gap goes to the section whose edge is nearer', () => {
  const rects = [{ top: 0, bottom: 400 }, { top: 500, bottom: 900 }];

  assert.equal(sectionAtCentre(rects, 880), 0, 'the centre 440 is 40 below the first section and 60 above the second');
  assert.equal(sectionAtCentre(rects, 960), 1, 'the centre 480 is 80 below the first section and 20 above the second');
});

test('the correction is how far the section moved', () => {
  assert.equal(scrollCorrection({ top: 200 }, { top: 340 }), 140);
  assert.equal(scrollCorrection({ top: 200 }, { top: 120 }), -80);
  assert.equal(scrollCorrection({ top: 200 }, { top: 200 }), 0);
});

/**
 * Builds a fake page whose four sections are laid out one under the other and take the height their design gives them, so a change
 * of the design attribute moves every section but the first. The log lists, in order, the style writes, the look writes and the
 * scrolls the switch made.
 * @param {object} [options] Whether the browser has view transitions, and the scroll position the page starts at.
 * @returns {object} The root, the log, the scroller and the transitions started.
 */
function scrollingPage({ transitions = false, scrollY = 900 } = {}) {
  const attributes = new Map(LOOK_FIELDS.map((field) => [`data-${field}`, BASE_LOOK[field]]));
  const log = [];
  const observers = [];
  const started = [];
  const scroller = { scrollY, clientHeight: VIEWPORT_HEIGHT, anchoring: null };
  const heightOf = () => SECTION_HEIGHTS[attributes.get('data-design')] ?? SECTION_HEIGHTS.classic;
  const sections = Array.from({ length: SECTION_COUNT }, (unused, index) => ({
    getBoundingClientRect: () => {
      const top = FIRST_SECTION_TOP + index * heightOf() - scroller.scrollY;

      return { top, bottom: top + heightOf() };
    },
  }));

  scroller.style = {
    setProperty: (name, value) => {
      scroller.anchoring = value;
      log.push(`style ${name}=${value}`);
    },
    removeProperty: (name) => {
      scroller.anchoring = null;
      log.push(`style ${name} removed`);
    },
  };
  scroller.scrollBy = (options) => {
    scroller.scrollY += options.top;
    log.push(`scroll ${options.top}`);
  };

  const logo = { getAttribute: () => null, setAttribute: () => {} };
  const doc = {
    scrollingElement: scroller,
    head: { append: () => {} },
    createElement: () => ({ addEventListener: () => {}, setAttribute: () => {} }),
    querySelector: (selector) => (selector.startsWith('meta') ? null : logo),
    querySelectorAll: (selector) => (selector === '#cabinet .section' ? sections : []),
    visibilityState: 'visible',
    addEventListener: () => {},
  };

  if (transitions) {
    doc.startViewTransition = (update) => {
      let finish;
      const finished = new Promise((resolve) => {
        finish = resolve;
      });

      started.push({ finish });
      setImmediate(update);

      return { ready: Promise.resolve(), updateCallbackDone: Promise.resolve(), finished };
    };
  }

  const root = {
    ownerDocument: doc,
    getAttribute: (name) => (attributes.has(name) ? attributes.get(name) : null),
    setAttribute: (name, value) => {
      const changed = attributes.get(name) !== String(value);

      attributes.set(name, String(value));

      if (changed && LOOK_FIELDS.some((field) => `data-${field}` === name)) {
        log.push(`write ${name}=${value}`);
      }
    },
    removeAttribute: (name) => {
      attributes.delete(name);
      observers.forEach((notify) => queueMicrotask(notify));
    },
    hasAttribute: (name) => attributes.has(name),
    get dataset() {
      return { lights: attributes.get('data-lights') };
    },
  };

  doc.documentElement = root;

  return { root, log, scroller, started, observers, sections };
}

/**
 * Runs a test body with the page globals the look switch reads, and puts the previous globals back afterwards.
 * @param {object} page The fake page.
 * @param {() => Promise<void>} body The test body.
 */
async function withPage(page, body) {
  const previous = { window: globalThis.window, observer: globalThis.MutationObserver };

  globalThis.window = {
    matchMedia: () => ({ matches: false }),
    cabinetEvening: { key: 'cabinet-lit' },
    setTimeout: () => 0,
    clearTimeout: () => {},
    setInterval: () => 0,
  };
  globalThis.MutationObserver = class {
    constructor(callback) {
      this.callback = callback;
    }

    observe() {
      page.observers.push(this.callback);
    }

    disconnect() {
      page.observers.splice(page.observers.indexOf(this.callback), 1);
    }
  };

  try {
    await body();
  } finally {
    globalThis.window = previous.window;
    globalThis.MutationObserver = previous.observer;
  }
}

let secondsLater = 0;

/**
 * Builds a status that carries a look and is newer than every status built before it.
 * @param {object} changes The look values that differ from the base look.
 * @returns {object} The status.
 */
function lookStatus(changes) {
  secondsLater += 10;

  return { serverTimeUtc: new Date(BASE_MS + secondsLater * 1000).toISOString(), look: { ...BASE_LOOK, ...changes } };
}

const sectionTop = (page, index) => page.sections[index].getBoundingClientRect().top;

test('a design change that makes the sections taller leaves the section in view where it was, with native anchoring off while it is applied', async () => {
  const page = scrollingPage();
  const before = sectionTop(page, 2);

  await withPage(page, async () => {
    await applyLookStatus(lookStatus({ design: 'library' }), page.root);
  });

  assert.equal(before, 200);
  assert.ok(Math.abs(sectionTop(page, 2) - before) <= 2);
  assert.deepEqual(page.log, ['style overflow-anchor=none', 'write data-design=library', 'scroll 280', 'style overflow-anchor removed']);
  assert.equal(page.scroller.anchoring, null);
});

test('a design change that makes the sections shorter scrolls back by the same amount', async () => {
  const page = scrollingPage({ scrollY: 1200 });

  await withPage(page, async () => {
    await applyLookStatus(lookStatus({ design: 'library' }), page.root);
    page.log.length = 0;
    await applyLookStatus(lookStatus({ design: 'classic' }), page.root);
  });

  assert.ok(page.log.includes('scroll -420'));
  assert.equal(page.scroller.scrollY, 1200);
});

test('inside a view transition the place is taken, the look written and the scroll made in the update, and anchoring is restored after it', async () => {
  const page = scrollingPage({ transitions: true });
  const before = sectionTop(page, 2);

  await withPage(page, async () => {
    await applyLookStatus(lookStatus({ design: 'library', wood: 'oak' }), page.root);

    assert.deepEqual(page.log, [], 'nothing happens before the transition runs the update');

    await settle();

    assert.equal(page.started.length, 1);
    assert.ok(Math.abs(sectionTop(page, 2) - before) <= 2);
    assert.equal(page.log[0], 'style overflow-anchor=none');
    assert.equal(page.log.at(-1), 'style overflow-anchor removed');
    assert.equal(page.log.some((entry) => entry.startsWith('scroll ')), true);
  });
});

test('a change of the wood, the backs, the depth or the lights neither scrolls nor touches the anchoring', async () => {
  const page = scrollingPage();

  await withPage(page, async () => {
    await applyLookStatus(lookStatus({ wood: 'oak' }), page.root);
    await applyLookStatus(lookStatus({ wood: 'oak', backs: 'painted' }), page.root);
    await applyLookStatus(lookStatus({ wood: 'oak', backs: 'painted', depth: 'deep' }), page.root);
    await applyLookStatus(lookStatus({ wood: 'oak', backs: 'painted', depth: 'deep', lights: 'on' }), page.root);
  });

  assert.equal(page.log.some((entry) => entry.startsWith('style ') || entry.startsWith('scroll ')), false);
  assert.equal(page.scroller.scrollY, 900);
});

test('a page whose sections have not been drawn yet takes the look without a scroll or a style write', async () => {
  const page = scrollingPage();

  page.root.ownerDocument.querySelectorAll = () => [];

  await withPage(page, async () => {
    await applyLookStatus(lookStatus({ design: 'modern' }), page.root);
  });

  assert.equal(page.root.getAttribute('data-design'), 'modern');
  assert.equal(page.log.some((entry) => entry.startsWith('style ') || entry.startsWith('scroll ')), false);
});

test('a look that arrives while a box is being pulled out is held and applied once the marker is gone', async () => {
  const page = scrollingPage();

  page.root.setAttribute(PULL_MARKER, 'box');

  await withPage(page, async () => {
    let applied = false;
    const applying = applyLookStatus(lookStatus({ design: 'library' }), page.root).then(() => {
      applied = true;
    });

    await settle();

    assert.equal(page.root.getAttribute('data-design'), 'classic');
    assert.equal(applied, false);
    assert.deepEqual(page.log.filter((entry) => entry.startsWith('write')), []);

    page.root.removeAttribute(PULL_MARKER);
    await applying;

    assert.equal(page.root.getAttribute('data-design'), 'library');
    assert.equal(page.observers.length, 0, 'the observer is dropped when the held look has been applied');
  });
});

test('only the latest look held back is applied, once', async () => {
  const page = scrollingPage();

  page.root.setAttribute(PULL_MARKER, 'spine');

  await withPage(page, async () => {
    const first = applyLookStatus(lookStatus({ design: 'library' }), page.root);
    const second = applyLookStatus(lookStatus({ design: 'modern' }), page.root);

    await settle();
    page.root.removeAttribute(PULL_MARKER);
    await Promise.all([first, second]);

    assert.equal(page.root.getAttribute('data-design'), 'modern');
    assert.deepEqual(page.log.filter((entry) => entry.startsWith('write')), ['write data-design=modern']);
  });
});

test('a status older than one already seen is still ignored while a pull-out runs', async () => {
  const page = scrollingPage();
  const older = lookStatus({ design: 'library' });
  const newer = lookStatus({ design: 'modern' });

  page.root.setAttribute(PULL_MARKER, 'box');

  await withPage(page, async () => {
    const held = applyLookStatus(newer, page.root);

    await applyLookStatus(older, page.root);
    await settle();
    page.root.removeAttribute(PULL_MARKER);
    await held;

    assert.equal(page.root.getAttribute('data-design'), 'modern');
  });
});

test('a held look that matches what the page shows by the time the pull-out ends changes nothing', async () => {
  const page = scrollingPage();

  page.root.setAttribute(PULL_MARKER, 'box');

  await withPage(page, async () => {
    const held = applyLookStatus(lookStatus({ design: 'classic' }), page.root);

    await settle();
    page.root.removeAttribute(PULL_MARKER);
    await held;

    assert.deepEqual(page.log, []);
  });
});

test('a look that waits for the evening script does not overwrite a newer look that arrived and was applied meanwhile', async () => {
  const page = scrollingPage();
  const listeners = {};
  const doc = page.root.ownerDocument;
  const logoQuery = doc.querySelector;

  doc.querySelector = (selector) => (selector.startsWith('meta') ? { getAttribute: () => '/js/evening.js' } : logoQuery(selector));
  doc.createElement = () => ({
    addEventListener: (type, listener) => {
      listeners[type] = listener;
    },
    setAttribute: () => {},
  });

  await withPage(page, async () => {
    globalThis.window.cabinetEvening = undefined;

    const older = applyLookStatus(lookStatus({ lights: 'on' }), page.root);

    await settle();
    await applyLookStatus(lookStatus({ wood: 'oak' }), page.root);

    assert.equal(page.root.getAttribute('data-wood'), 'oak');

    listeners.load();
    await older;

    assert.equal(page.root.getAttribute('data-wood'), 'oak');
    assert.equal(page.root.getAttribute('data-lights'), 'off', 'the older look that turned the lights on never wrote');
    assert.deepEqual(page.log.filter((entry) => entry.startsWith('write')), ['write data-wood=oak']);
  });
});

test('a newer look that reverts to what the page shows stops the older look waiting in a cross-fade from being written', async () => {
  const page = scrollingPage({ transitions: true });

  await withPage(page, async () => {
    await applyLookStatus(lookStatus({ design: 'library' }), page.root);
    await applyLookStatus(lookStatus({}), page.root);
    await settle();

    assert.equal(page.started.length, 1);
    assert.equal(page.root.getAttribute('data-design'), 'classic');
    assert.deepEqual(page.log.filter((entry) => entry.startsWith('write')), []);

    page.started[0].finish();
    await settle();

    assert.equal(page.root.getAttribute('data-design'), 'classic');
    assert.deepEqual(page.log.filter((entry) => entry.startsWith('write')), []);
  });
});

test('of two looks waiting in cross-fades only the newer one is written, whatever order the updates run in', async () => {
  const page = scrollingPage({ transitions: true });

  await withPage(page, async () => {
    await applyLookStatus(lookStatus({ design: 'library' }), page.root);
    await applyLookStatus(lookStatus({ design: 'modern' }), page.root);
    await settle();

    assert.equal(page.root.getAttribute('data-design'), 'modern');
    assert.deepEqual(page.log.filter((entry) => entry.startsWith('write')), ['write data-design=modern']);
  });
});

test('a look that arrives after a newer one was applied is still ignored as older', async () => {
  const page = scrollingPage();
  const older = lookStatus({ design: 'library' });
  const newer = lookStatus({ design: 'modern' });

  await withPage(page, async () => {
    await applyLookStatus(newer, page.root);
    await applyLookStatus(older, page.root);

    assert.equal(page.root.getAttribute('data-design'), 'modern');
  });
});
