/**
 * Checks the pure helpers of the detail card with the plain Node test runner: the one history step a card takes, driven through a
 * fake history and fake timers, and the small resolvers for taps and for a card whose data did not arrive.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const source = readFileSync(new URL('../../Cabinet.Service/wwwroot/js/card-flow.js', import.meta.url), 'utf8');
const { createHistoryStep, sourceEntry, recordFromPlacement, indexPlacements, pullKind, choosePath, isMostlyOnScreen } = await import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));

/**
 * A stand-in for the browser history that records every call.
 * @returns {{ pushes: object[][], backs: number, replaces: object[][], pushState: Function, back: Function, replaceState: Function }}
 */
function createFakeHistory() {
  const history = {
    pushes: [],
    backs: 0,
    replaces: [],
    pushState: (...args) => history.pushes.push(args),
    back: () => {
      history.backs += 1;
    },
    replaceState: (...args) => history.replaces.push(args),
  };

  return history;
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
    setTimer: (callback, delay) => {
      delays.push(delay);
      timers.set(nextId, callback);

      return nextId++;
    },
    clearTimer: (id) => timers.delete(id),
    fire: () => {
      for (const [id, callback] of [...timers]) {
        timers.delete(id);
        callback();
      }
    },
    pending: () => timers.size,
  };
}

/**
 * Builds a history step over fakes.
 * @returns {object} The step, the fake history and the fake timers.
 */
function createStep() {
  const history = createFakeHistory();
  const timers = createFakeTimers();

  return { step: createHistoryStep({ history, setTimer: timers.setTimer, clearTimer: timers.clearTimer }), history, timers };
}

test('opening pushes one step with no address, and a second open while it stands pushes nothing', () => {
  const { step, history } = createStep();

  step.opened();
  step.opened();

  assert.deepEqual(history.pushes, [[{ card: true }, '']]);
});

test('closing by Escape, the close button or an outside tap removes the step with one Back and swallows the pop that follows', () => {
  const { step, history } = createStep();

  step.opened();
  step.closedHere();

  assert.equal(history.backs, 1);
  assert.equal(step.popped(), 'ignore');

  step.closedHere();

  assert.equal(history.backs, 1);
});

test('Back closes the card, and the close that follows leaves nothing behind', () => {
  const { step, history } = createStep();

  step.opened();

  assert.equal(step.popped(), 'close');

  step.closedHere();

  assert.equal(history.backs, 0);
});

test('Escape then Back, and Back then Back, each do one thing', () => {
  const { step, history } = createStep();

  step.opened();
  step.closedHere();
  step.popped();
  step.opened();
  assert.equal(history.pushes.length, 2);
  assert.equal(step.popped(), 'close');
  step.closedHere();

  assert.equal(history.backs, 1);
});

test('opening with no pending pop does not wait', async () => {
  const { step, timers } = createStep();
  let settled = false;

  step.beforeOpen().then(() => {
    settled = true;
  });
  await Promise.resolve();

  assert.equal(settled, true);
  assert.equal(timers.pending(), 0);
});

test('a re-open right after a close waits for the pending pop and then goes on', async () => {
  const { step, history, timers } = createStep();
  let settled = false;

  step.opened();
  step.closedHere();
  step.beforeOpen().then(() => {
    settled = true;
  });
  await Promise.resolve();

  assert.equal(settled, false);
  assert.deepEqual(timers.delays, [150]);

  step.popped();
  await Promise.resolve();

  assert.equal(settled, true);
  assert.equal(timers.pending(), 0);

  step.opened();

  assert.equal(history.pushes.length, 2);
});

test('a re-open waits at most 150 ms when the pop never comes', async () => {
  const { step, timers } = createStep();
  let settled = false;

  step.opened();
  step.closedHere();
  step.beforeOpen().then(() => {
    settled = true;
  });
  await Promise.resolve();

  assert.equal(settled, false);

  timers.fire();
  await Promise.resolve();

  assert.equal(settled, true);
});

test('a stale card step found on load is replaced by nothing, and any other state is left alone', () => {
  const { step, history } = createStep();

  step.clearStale({ other: 1 });
  step.clearStale(null);

  assert.equal(history.replaces.length, 0);

  step.clearStale({ card: true });

  assert.deepEqual(history.replaces, [[null, '']]);
});

test('a tap on the marker resolves to the same entry and says it came from the expansions', () => {
  assert.deepEqual(sourceEntry('moreMarker', '12'), { entryId: '12', atExpansions: true });
  assert.deepEqual(sourceEntry('cover', '12'), { entryId: '12', atExpansions: false });
  assert.deepEqual(sourceEntry('expansionLayer', 7), { entryId: 7, atExpansions: false });
});

test('a drawn box is enough to build an incomplete record with a link and a title', () => {
  const art = { url: '/art/0123456789abcdef-480.webp', width: 480, height: 600, fit: 'exact' };
  const record = recordFromPlacement({ entryId: 3, gameId: 30, title: 'Invented Harbour', kind: 'cover', toneIndex: 1, patternIndex: 2, art });

  assert.equal(record.incomplete, true);
  assert.equal(record.title, 'Invented Harbour');
  assert.equal(record.gameId, 30);
  assert.equal(record.isExpansion, false);
  assert.equal(record.ratio, 0.8);
  assert.equal(record.cover, art);
  assert.deepEqual(record.expansions, []);
  assert.equal(recordFromPlacement({ entryId: 4, gameId: 40, title: 'Invented Orchard', kind: 'expansionLayer', toneIndex: 0, patternIndex: 0 }).isExpansion, true);
});

test('the placement index skips the marker, keeps the first box of an entry and keys by text', () => {
  const layout = {
    sections: [{
      cubbies: [{
        placements: [
          { entryId: 1, kind: 'spine', title: 'First' },
          { entryId: 1, kind: 'moreMarker', title: 'Marker' },
          { entryId: 2, kind: 'cover', title: 'Second' },
          { entryId: 2, kind: 'spine', title: 'Again' },
        ],
      }],
    }],
  };
  const index = indexPlacements(layout);

  assert.deepEqual([...index.keys()], ['1', '2']);
  assert.equal(index.get('1').title, 'First');
  assert.equal(index.get('2').title, 'Second');
});

test('upright spines turn about the vertical axis, lying boxes tip about the horizontal axis and covers only lift', () => {
  assert.equal(pullKind('spine'), 'turn-y');
  assert.equal(pullKind('expansionSpine'), 'turn-y');
  assert.equal(pullKind('flatBox'), 'turn-x');
  assert.equal(pullKind('expansionLayer'), 'turn-x');
  assert.equal(pullKind('orphanExpansion'), 'turn-x');
  assert.equal(pullKind('cover'), 'lift');
});

test('reduced motion decides the path whatever else is true', () => {
  for (const hasViewTransition of [true, false]) {
    for (const boxOnScreen of [true, false]) {
      for (const swapped of [true, false]) {
        assert.equal(choosePath({ reducedMotion: true, hasViewTransition, boxOnScreen, swapped }), 'reduced');
      }
    }
  }
});

test('a browser without view transitions gets the plain fade', () => {
  assert.equal(choosePath({ reducedMotion: false, hasViewTransition: false, boxOnScreen: true, swapped: false }), 'fade');
});

test('a box that is less than half on screen gets the plain fade', () => {
  assert.equal(choosePath({ reducedMotion: false, hasViewTransition: true, boxOnScreen: false, swapped: false }), 'fade');
});

test('a swapped card gets the plain fade on close', () => {
  assert.equal(choosePath({ reducedMotion: false, hasViewTransition: true, boxOnScreen: true, swapped: true }), 'fade');
});

test('everything in order gets the view transition, and a missing swapped flag means not swapped', () => {
  assert.equal(choosePath({ reducedMotion: false, hasViewTransition: true, boxOnScreen: true, swapped: false }), 'view-transition');
  assert.equal(choosePath({ reducedMotion: false, hasViewTransition: true, boxOnScreen: true }), 'view-transition');
});

test('a box is mostly on screen at exactly half its area and not just below it', () => {
  const viewport = { width: 100, height: 100 };

  assert.equal(isMostlyOnScreen({ left: 10, top: 10, width: 20, height: 20 }, viewport), true);
  assert.equal(isMostlyOnScreen({ left: 90, top: 10, width: 20, height: 20 }, viewport), true);
  assert.equal(isMostlyOnScreen({ left: 91, top: 10, width: 20, height: 20 }, viewport), false);
  assert.equal(isMostlyOnScreen({ left: -10, top: 90, width: 20, height: 20 }, viewport), false);
});

test('a box fully outside the viewport or with no area is not on screen', () => {
  const viewport = { width: 100, height: 100 };

  assert.equal(isMostlyOnScreen({ left: 0, top: 150, width: 20, height: 20 }, viewport), false);
  assert.equal(isMostlyOnScreen({ left: 0, top: 0, width: 0, height: 20 }, viewport), false);
});
