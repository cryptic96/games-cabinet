/**
 * Checks the pure logic of the cabinet page scripts with the plain Node test runner: the relative-time sentences against the
 * case table the server's formatter is tested with, and the decisions about stale notes and clock skew. Each script is read
 * from disk and imported through a data URL, so no package, bundler or module configuration is needed.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { copyFileSync, mkdtempSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

/**
 * Imports a script from the page's script folder through a data URL.
 * @param {string} name The file name inside the script folder.
 * @returns {Promise<object>} The module's exports.
 */
async function loadPageScript(name) {
  const source = readFileSync(new URL('../../Cabinet.Service/wwwroot/js/' + name, import.meta.url), 'utf8');

  return import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));
}

const { COPY, copyFor } = await loadPageScript('copy.js');
const { serverOffsetMs, elapsedSeconds, isStale, buttonState, countdownText, pressOutcome, shouldRedraw, reconnectDelayMs, isOutdatedStatus, ownSyncStillWaiting } = await loadPageScript('status.js');
const cases = JSON.parse(readFileSync(new URL('./fixtures/relative-time-cases.json', import.meta.url), 'utf8'));

const SYNCED = '2030-01-15T12:00:00.000Z';
const SYNCED_MS = Date.parse(SYNCED);
const THREE_HOURS = 3 * 60 * 60;

for (const { language = 'en', elapsedSeconds: seconds, text } of cases) {
  test(`${seconds} seconds reads as "${text}" in ${language}`, () => {
    assert.equal(copyFor(language).syncedAgo(seconds), text);
  });
}

test('the exact time is written in the visitor zone with English words and a 24-hour clock', () => {
  const text = COPY.exactTime(new Date(SYNCED));

  assert.match(text, /^\d{1,2} January 2030/);
  assert.match(text, /\d{2}:\d{2}/);
});

test('never synced is never stale', () => {
  assert.equal(isStale(null, SYNCED_MS, THREE_HOURS, false), false);
  assert.equal(isStale('', SYNCED_MS, THREE_HOURS, false), false);
});

test('2 hours 59 minutes is not stale and exactly 3 hours is', () => {
  assert.equal(isStale(SYNCED, SYNCED_MS + (THREE_HOURS - 60) * 1000, THREE_HOURS, false), false);
  assert.equal(isStale(SYNCED, SYNCED_MS + THREE_HOURS * 1000, THREE_HOURS, false), true);
});

test('a held-back result is stale at one minute', () => {
  assert.equal(isStale(SYNCED, SYNCED_MS + 60 * 1000, THREE_HOURS, true), true);
});

test('a last sync in the future is not stale', () => {
  assert.equal(isStale(SYNCED, SYNCED_MS - 60 * 1000, THREE_HOURS, false), false);
});

test('a server 5 minutes ahead of the visitor makes the elapsed time count on the server clock', () => {
  const clientNowMs = SYNCED_MS + 60 * 1000;
  const serverTime = new Date(clientNowMs + 5 * 60 * 1000).toISOString();
  const offset = serverOffsetMs(serverTime, clientNowMs);

  assert.equal(offset, 5 * 60 * 1000);
  assert.equal(elapsedSeconds(SYNCED, clientNowMs + offset), 6 * 60);
  assert.equal(elapsedSeconds(SYNCED, clientNowMs), 60);
  assert.equal(COPY.syncedAgo(elapsedSeconds(SYNCED, clientNowMs + offset)), 'Synced 6 minutes ago');
});

test('an unusable server time leaves the visitor clock as it is', () => {
  assert.equal(serverOffsetMs('', SYNCED_MS), 0);
  assert.equal(serverOffsetMs('not a time', SYNCED_MS), 0);
  assert.equal(elapsedSeconds('', SYNCED_MS), null);
});

test('held back without a last sync is not stale', () => {
  assert.equal(isStale(null, SYNCED_MS, THREE_HOURS, true), false);
  assert.equal(isStale('', SYNCED_MS, THREE_HOURS, true), false);
});

test('the stale notes carry no digit beyond the date they are given', () => {
  const exact = 'DATE';

  assert.doesNotMatch(COPY.staleRecent(exact), /\d/);
  assert.doesNotMatch(COPY.staleHeldBack(exact), /\d/);
});

const NOW_MS = Date.parse(SYNCED);
const inSeconds = (seconds) => new Date(NOW_MS + seconds * 1000).toISOString();

test('a running sync wins over a cooldown, a future end is a cooldown, and no or a past end is idle', () => {
  assert.deepEqual(buttonState({ running: true, cooldownEndsUtc: inSeconds(300) }, NOW_MS), { kind: 'running', remainingMs: 300000 });
  assert.deepEqual(buttonState({ running: false, cooldownEndsUtc: inSeconds(582) }, NOW_MS), { kind: 'cooldown', remainingMs: 582000 });
  assert.equal(buttonState({ running: false, cooldownEndsUtc: null }, NOW_MS).kind, 'idle');
  assert.equal(buttonState({ running: false, cooldownEndsUtc: inSeconds(-5) }, NOW_MS).kind, 'idle');
  assert.equal(buttonState({ running: false, cooldownEndsUtc: inSeconds(0) }, NOW_MS).kind, 'idle');
  assert.equal(buttonState({}, NOW_MS).kind, 'idle');
});

test('the countdown reads minutes and seconds with the seconds rounded up', () => {
  assert.equal(countdownText(582000), '9:42');
  assert.equal(countdownText(7000), '0:07');
  assert.equal(countdownText(200), '0:01');
  assert.equal(countdownText(0), '0:00');
  assert.equal(countdownText(-4000), '0:00');
  assert.equal(countdownText(600000), '10:00');
});

test('the button name and the press sentence round minutes up and have their own singular and sub-minute forms', () => {
  assert.equal(COPY.syncAgainName(59000), 'Sync again in less than a minute');
  assert.equal(COPY.syncAgainName(60000), 'Sync again in 1 minute');
  assert.equal(COPY.syncAgainName(61000), 'Sync again in 2 minutes');
  assert.equal(COPY.syncAgainName(120000), 'Sync again in 2 minutes');
  assert.equal(COPY.syncAgainName(582000), 'Sync again in 10 minutes');
  assert.equal(COPY.youCanSyncAgain(59000), 'You can sync again in less than a minute.');
  assert.equal(COPY.youCanSyncAgain(60000), 'You can sync again in 1 minute.');
  assert.equal(COPY.youCanSyncAgain(121000), 'You can sync again in 3 minutes.');
  assert.equal(COPY.youCanSyncAgain(582000), 'You can sync again in 10 minutes.');
  assert.equal(COPY.syncAgainIn('9:42'), 'Sync again in 9:42');
});

test('a finished press maps to its outcome and anything unknown counts as a failure', () => {
  assert.equal(pressOutcome('changed'), 'changed');
  assert.equal(pressOutcome('unchanged'), 'unchanged');
  assert.equal(pressOutcome('failed'), 'failed');
  assert.equal(pressOutcome('heldBack'), 'heldBack');
  assert.equal(pressOutcome(null), 'failed');
  assert.equal(pressOutcome(undefined), 'failed');
  assert.equal(pressOutcome('surprise'), 'failed');
});

test('every note and button string equals the copy contract and uses plain dots', () => {
  assert.equal(COPY.syncNow, 'Sync now');
  assert.equal(COPY.syncing, 'Syncing...');
  assert.equal(COPY.noteChanged, 'Collection updated. BGG can take a few minutes to show recent edits.');
  assert.equal(COPY.noteUnchanged, 'No changes found. BGG can take a few minutes to show recent edits.');
  assert.equal(COPY.noteFailed, "BGG didn't respond. The last collection is still showing.");
  assert.equal(COPY.noteHeldBack, 'BGG returned far fewer games than before, so the last collection is still showing.');
  assert.equal(COPY.noteRunning, 'A sync is already running.');
  assert.equal(COPY.noteOffline, "Couldn't start the sync. Check your connection and try again.");

  const strings = [COPY.syncNow, COPY.syncing, COPY.noteChanged, COPY.noteUnchanged, COPY.noteFailed, COPY.noteHeldBack, COPY.noteRunning, COPY.noteOffline];

  for (const text of strings) {
    assert.doesNotMatch(text, /\u2026/);
  }
});

test('a redraw is wanted only for a version other than the one on screen', () => {
  assert.equal(shouldRedraw('v1', { snapshotVersion: 'v1' }), false);
  assert.equal(shouldRedraw('v1', { snapshotVersion: 'v2' }), true);
  assert.equal(shouldRedraw('', { snapshotVersion: 'v2' }), true);
  assert.equal(shouldRedraw(null, { snapshotVersion: 'v2' }), true);
  assert.equal(shouldRedraw('v1', { snapshotVersion: null }), false);
  assert.equal(shouldRedraw('v1', { snapshotVersion: '' }), false);
  assert.equal(shouldRedraw('v1', {}), false);
  assert.equal(shouldRedraw('v1', null), false);
});

test('a status older than one already taken is outdated, an equal or newer or undated one is not', () => {
  const newest = Date.parse('2030-01-15T12:00:00.100Z');

  assert.equal(isOutdatedStatus(newest, { serverTimeUtc: '2030-01-15T12:00:00.000Z' }), true);
  assert.equal(isOutdatedStatus(newest, { serverTimeUtc: '2030-01-15T12:00:00.100Z' }), false);
  assert.equal(isOutdatedStatus(newest, { serverTimeUtc: '2030-01-15T12:00:00.200Z' }), false);
  assert.equal(isOutdatedStatus(newest, {}), false);
  assert.equal(isOutdatedStatus(newest, { serverTimeUtc: 'not a time' }), false);
  assert.equal(isOutdatedStatus(newest, null), false);
  assert.equal(isOutdatedStatus(Number.NEGATIVE_INFINITY, { serverTimeUtc: '2030-01-15T12:00:00.000Z' }), false);
});

test('the reconnect delays are a moment, 2 s, 10 s, 30 s and then every minute when the random source answers the middle', () => {
  const middle = () => 0.5;

  assert.equal(reconnectDelayMs(0, middle), 750);
  assert.equal(reconnectDelayMs(1, middle), 2000);
  assert.equal(reconnectDelayMs(2, middle), 10000);
  assert.equal(reconnectDelayMs(3, middle), 30000);
  assert.equal(reconnectDelayMs(4, middle), 60000);
  assert.equal(reconnectDelayMs(50, middle), 60000);
});

test('every reconnect delay is spread by at most 20 percent, the first is spread over a second and is never zero, and none is negative', () => {
  const lowest = () => 0;
  const highest = () => 0.9999999;

  assert.equal(reconnectDelayMs(0, lowest), 250);
  assert.equal(reconnectDelayMs(0, highest), 1249);
  assert.equal(reconnectDelayMs(1, lowest), 1600);
  assert.equal(reconnectDelayMs(1, highest), 2400);
  assert.equal(reconnectDelayMs(2, lowest), 8000);
  assert.equal(reconnectDelayMs(3, highest), 36000);
  assert.equal(reconnectDelayMs(4, lowest), 48000);
  assert.equal(reconnectDelayMs(9, highest), 72000);

  for (const unit of [-5, -0.1, Number.NaN, Number.POSITIVE_INFINITY, undefined, null, 0, 0.3, 1, 7]) {
    for (const retry of [0, 1, 2, 3, 4, 5, 100]) {
      const delay = reconnectDelayMs(retry, () => unit);

      assert.ok(Number.isInteger(delay) && delay >= 250, `retry ${retry} with ${unit} gave ${delay}`);
      assert.ok(delay <= 72000, `retry ${retry} with ${unit} gave ${delay}`);
    }
  }
});

test('the reconnect delay uses Math.random when no source is given and stays inside its bounds', () => {
  for (let attempt = 0; attempt < 200; attempt += 1) {
    const first = reconnectDelayMs(0);
    const steady = reconnectDelayMs(4);

    assert.ok(first >= 250 && first < 1250);
    assert.ok(steady >= 48000 && steady <= 72000);
  }
});

test('a press is followed while it is pending and before the deadline, and not otherwise', () => {
  assert.equal(ownSyncStillWaiting(true, 1000, 2000), true);
  assert.equal(ownSyncStillWaiting(true, 2000, 2000), false);
  assert.equal(ownSyncStillWaiting(true, 3000, 2000), false);
  assert.equal(ownSyncStillWaiting(false, 1000, 2000), false);
  assert.equal(ownSyncStillWaiting(undefined, 1000, 2000), false);
});

const PAGE_FOLDER = mkdtempSync(join(tmpdir(), 'cabinet-page-scripts-'));

writeFileSync(join(PAGE_FOLDER, 'package.json'), '{"type":"module"}');

for (const name of readdirSync(new URL('../../Cabinet.Service/wwwroot/js/', import.meta.url)).filter((file) => file.endsWith('.js'))) {
  copyFileSync(new URL('../../Cabinet.Service/wwwroot/js/' + name, import.meta.url), join(PAGE_FOLDER, name));
}

const { initSyncStatus } = await import(pathToFileURL(join(PAGE_FOLDER, 'sync.js')).href);
const { startLive } = await import(pathToFileURL(join(PAGE_FOLDER, 'live.js')).href);
const BASE_MS = Date.parse('2030-01-15T12:00:00.000Z');
const COOLDOWN_MS = 600000;
const flushMicrotasks = () => new Promise((resolve) => setImmediate(resolve));
const isoAt = (offsetMs) => new Date(BASE_MS + offsetMs).toISOString();

/**
 * A clock the page script's timers and Date.now run on, so a ten-minute follow-up takes no real time.
 * @param {boolean} [repeatingIntervals] Whether setInterval really repeats; off, it only hands out an id, which is all the sync block's tests need.
 * @returns {{ elapsed: () => number, now: () => number, advance: (ms: number) => Promise<void>, timers: object, repeatingDelays: () => number[] }} The clock.
 */
function createClock(repeatingIntervals = false) {
  let now = BASE_MS;
  let nextId = 1;
  const pending = new Map();

  return {
    elapsed: () => now - BASE_MS,
    timers: {
      setTimeout(callback, delay) {
        const id = nextId++;

        pending.set(id, { at: now + delay, callback });

        return id;
      },
      clearTimeout: (id) => pending.delete(id),
      setInterval(callback, delay) {
        const id = nextId++;

        if (repeatingIntervals) {
          pending.set(id, { at: now + delay, callback, every: delay });
        }

        return id;
      },
      clearInterval: (id) => pending.delete(id),
    },
    now: () => now,
    repeatingDelays: () => [...pending.values()].filter((timer) => timer.every !== undefined).map((timer) => timer.every),
    async advance(milliseconds) {
      const target = now + milliseconds;

      for (;;) {
        await flushMicrotasks();

        const due = [...pending.entries()].filter(([, timer]) => timer.at <= target).sort((a, b) => a[1].at - b[1].at)[0];

        if (due === undefined) {
          break;
        }

        now = Math.max(now, due[1].at);

        if (due[1].every === undefined) {
          pending.delete(due[0]);
        } else {
          due[1].at += due[1].every;
        }

        due[1].callback();
      }

      now = target;
      await flushMicrotasks();
    },
  };
}

/**
 * Starts the sync block against a hand-made page, a fake clock and the given fetch.
 * @param {object} setup fetchImpl builds the fetch from the clock; onCollectionChanged is passed through to the script; dataset
 *   adds to or replaces the first-paint attributes of the block; repeatingIntervals makes the clock's intervals really repeat.
 * @returns {{ clock: object, note: { textContent: string, dataset: object }, staleNote: { hidden: boolean, textContent: string }, button: object, api: object, press: () => Promise<void>, attributeLog: string[][], setVisibility: (state: string) => void }} The running page.
 */
function startPage({ fetchImpl, onCollectionChanged, dataset = {}, repeatingIntervals = false }) {
  const clock = createClock(repeatingIntervals);
  const note = { textContent: '', dataset: {} };
  const attributes = new Map([['aria-disabled', 'false']]);
  const attributeLog = [];
  const handlers = {};
  const documentHandlers = {};
  const button = {
    textContent: 'Sync now',
    hidden: false,
    getAttribute: (name) => (attributes.has(name) ? attributes.get(name) : null),
    setAttribute: (name, value) => {
      attributeLog.push([name, value]);
      attributes.set(name, value);
    },
    removeAttribute: (name) => attributes.delete(name),
    addEventListener: (type, handler) => {
      handlers[type] = handler;
    },
  };
  const staleNote = { hidden: true, textContent: '' };
  const elements = { '.sync-exact': { hidden: true, id: 'sync-exact' }, '.sync-stale': staleNote, '.sync-note': note };
  const root = {
    dataset: { serverTime: isoAt(0), running: 'false', staleAfter: '10800', snapshotVersion: 'v1', ...dataset },
    parentElement: { querySelector: (selector) => elements[selector] ?? null },
    querySelector: (selector) => (selector === '.sync-button' ? button : null),
    prepend: () => undefined,
  };
  const timeElement = {};
  const timeButton = { setAttribute: () => undefined, append: () => undefined, addEventListener: () => undefined, querySelector: () => timeElement };

  globalThis.window = clock.timers;
  globalThis.document = {
    visibilityState: 'visible',
    addEventListener: (type, handler) => (documentHandlers[type] ??= []).push(handler),
    createElement: (tagName) => (tagName === 'time' ? timeElement : timeButton),
  };
  globalThis.fetch = fetchImpl(clock);
  Date.now = clock.now;

  const api = initSyncStatus(root, { onCollectionChanged });
  const setVisibility = (state) => {
    globalThis.document.visibilityState = state;
    (documentHandlers.visibilitychange ?? []).forEach((handler) => handler());
  };

  return { clock, note, staleNote, button, api, press: () => handlers.click(), attributeLog, setVisibility };
}

const realNow = Date.now;
const answer = (status, body) => ({ status, ok: status < 300, json: async () => body, headers: { get: () => null } });
const running = (offsetMs) => ({ serverTimeUtc: isoAt(offsetMs), running: true, cooldownEndsUtc: isoAt(COOLDOWN_MS), lastResult: null, snapshotVersion: 'v1' });
const finished = (offsetMs, lastResult = 'changed') => ({ serverTimeUtc: isoAt(offsetMs), running: false, cooldownEndsUtc: isoAt(COOLDOWN_MS), lastResult, snapshotVersion: 'v1' });

test('a press whose sync ended without a push reaches its outcome sentence and frees the button, with no live connection involved', async () => {
  let server = running(0);
  const { clock, note, button, press } = startPage({
    fetchImpl: (fakeClock) => async (url, init) => {
      if (init?.method === 'POST') {
        return answer(202, { outcome: 'started', status: running(fakeClock.elapsed()) });
      }

      return answer(200, { ...server, serverTimeUtc: isoAt(fakeClock.elapsed()) });
    },
  });

  try {
    await press();
    assert.equal(button.textContent, COPY.syncing);
    assert.equal(note.textContent, '');

    server = finished(0);
    await clock.advance(0);

    assert.equal(note.textContent, COPY.noteChanged);
    assert.notEqual(button.textContent, COPY.syncing);
  } finally {
    Date.now = realNow;
  }
});

test('an answer that already shows the sync finished ends the press at once and starts no follow-up', async () => {
  let statusRequests = 0;
  const { clock, note, button, press } = startPage({
    fetchImpl: (fakeClock) => async (url, init) => {
      if (init?.method === 'POST') {
        return answer(202, { outcome: 'started', status: finished(fakeClock.elapsed(), 'unchanged') });
      }

      statusRequests += 1;

      return answer(200, finished(fakeClock.elapsed()));
    },
  });

  try {
    await press();
    await clock.advance(30000);

    assert.equal(note.textContent, COPY.noteUnchanged);
    assert.notEqual(button.textContent, COPY.syncing);
    assert.equal(statusRequests, 0);
  } finally {
    Date.now = realNow;
  }
});

test('a finished status pushed before the older answer is built still ends the press through the follow-up fetch', async () => {
  let releaseAnswer;
  const answerReady = new Promise((resolve) => {
    releaseAnswer = resolve;
  });
  const { clock, note, button, api, press } = startPage({
    fetchImpl: (fakeClock) => async (url, init) => {
      if (init?.method === 'POST') {
        await answerReady;

        return answer(202, { outcome: 'started', status: running(1000) });
      }

      return answer(200, finished(fakeClock.elapsed()));
    },
  });

  try {
    const pressed = press();

    await clock.advance(2000);
    api.applyStatus(finished(2000));
    assert.equal(note.textContent, '');
    releaseAnswer();
    await pressed;
    await clock.advance(0);

    assert.equal(note.textContent, COPY.noteChanged);
    assert.notEqual(button.textContent, COPY.syncing);
  } finally {
    Date.now = realNow;
  }
});

test('the status is fetched one last time at the deadline, and the follow-up stops after it', async () => {
  let statusRequests = 0;
  const { clock, note, button, press } = startPage({
    fetchImpl: (fakeClock) => async (url, init) => {
      if (init?.method === 'POST') {
        return answer(202, { outcome: 'started', status: running(fakeClock.elapsed()) });
      }

      statusRequests += 1;

      return answer(200, fakeClock.elapsed() >= 600000 ? finished(fakeClock.elapsed(), 'failed') : running(fakeClock.elapsed()));
    },
  });

  try {
    await press();
    await clock.advance(599000);
    assert.equal(button.textContent, COPY.syncing);
    assert.equal(note.textContent, '');

    await clock.advance(1000);
    assert.equal(note.textContent, COPY.noteFailed);

    const requestsAtEnd = statusRequests;

    await clock.advance(60000);
    assert.equal(statusRequests, requestsAtEnd);
  } finally {
    Date.now = realNow;
  }
});

test('a press request that hangs is given up on, says the offline sentence and lets the button be pressed again', async () => {
  let presses = 0;
  const { clock, note, api, press } = startPage({
    fetchImpl: () => (url, init) => {
      presses += 1;

      return new Promise((resolve, reject) => {
        init.signal.addEventListener('abort', () => reject(new Error('aborted')));
      });
    },
  });

  try {
    const hung = press();

    await clock.advance(14000);
    api.applyStatus({ ...finished(14000), cooldownEndsUtc: null });
    assert.equal(note.textContent, '');

    await clock.advance(1000);
    await hung;
    assert.equal(note.textContent, COPY.noteOffline);

    press();
    await flushMicrotasks();
    assert.equal(presses, 2);
  } finally {
    Date.now = realNow;
  }
});

test('a redraw that is superseded does not clear the guard of the redraw that replaced it', async () => {
  const redraws = [];
  const { api } = startPage({
    fetchImpl: () => async () => answer(200, {}),
    onCollectionChanged: () => new Promise((resolve) => redraws.push(resolve)),
  });
  const withVersion = (offsetMs, version) => ({ serverTimeUtc: isoAt(offsetMs), snapshotVersion: version });

  try {
    api.applyStatus(withVersion(1, 'v2'));
    api.applyStatus(withVersion(2, 'v3'));
    assert.equal(redraws.length, 2);

    redraws[0](false);
    await flushMicrotasks();
    api.applyStatus(withVersion(3, 'v3'));
    assert.equal(redraws.length, 2);

    redraws[1](true);
    await flushMicrotasks();
    api.applyStatus(withVersion(4, 'v3'));
    assert.equal(redraws.length, 2);
  } finally {
    Date.now = realNow;
  }
});

const FAILED_START = Symbol('failed start');

/**
 * A hand-made browser client: a builder that records the route and the reconnect schedule, and connections the test can start,
 * push to, drop and put through an automatic reconnect.
 * @param {{ elapsed: () => number }} clock The clock that start times are read from.
 * @param {(startNumber: number) => boolean} startSucceeds Whether the given start (counted from 1) succeeds.
 * @returns {{ client: object, connections: object[], startTimes: number[] }} The client to put on the page and what it recorded.
 */
function createFakeSignalR(clock, startSucceeds) {
  const connections = [];
  const startTimes = [];

  class FakeConnection {
    constructor(builder) {
      this.url = builder.url;
      this.reconnectDelay = builder.reconnectDelay;
      this.handlers = {};
      this.listeners = { reconnecting: [], reconnected: [], close: [] };
    }

    on(name, handler) {
      this.handlers[name] = handler;
    }

    onreconnecting(handler) {
      this.listeners.reconnecting.push(handler);
    }

    onreconnected(handler) {
      this.listeners.reconnected.push(handler);
    }

    onclose(handler) {
      this.listeners.close.push(handler);
    }

    start() {
      startTimes.push(clock.elapsed());

      return startSucceeds(startTimes.length) ? Promise.resolve() : Promise.reject(FAILED_START);
    }

    push(status) {
      this.handlers.statusChanged(status);
    }

    drop() {
      this.listeners.close.forEach((handler) => handler());
    }

    /**
     * The server turns the page away after the handshake: the connection ends with no permission to reconnect, so the page sees
     * the close and nothing else (no reconnecting, no reconnected).
     */
    refuse() {
      this.drop();
    }

    reconnecting() {
      this.listeners.reconnecting.forEach((handler) => handler());
    }

    reconnected() {
      this.listeners.reconnected.forEach((handler) => handler());
    }
  }

  class HubConnectionBuilder {
    withUrl(url) {
      this.url = url;

      return this;
    }

    withAutomaticReconnect(options) {
      this.reconnectDelay = options.nextRetryDelayInMilliseconds;

      return this;
    }

    configureLogging() {
      return this;
    }

    build() {
      const connection = new FakeConnection(this);

      connections.push(connection);

      return connection;
    }
  }

  return { client: { HubConnectionBuilder, LogLevel: { None: 0 } }, connections, startTimes };
}

/**
 * Starts the live behaviour against a hand-made page: a fake clock with repeating intervals, a fake document whose visibility
 * the test sets, a fake browser client, and a status fetch the test answers.
 * @param {{ withClient?: boolean, startSucceeds?: (startNumber: number) => boolean, fetchStatus?: () => Promise<object | null>, random?: () => number }} [setup]
 *   withClient puts the browser client on the page; startSucceeds decides each start; fetchStatus answers each status request;
 *   random is the source that spreads the reconnect waits, the middle of the range unless the test says otherwise.
 * @returns {object} The clock, what the script reported and asked for, and the levers for visibility and the network.
 */
function startLivePage({ withClient = true, startSucceeds = () => true, fetchStatus = async () => null, random = () => 0.5 } = {}) {
  const clock = createClock(true);
  const fake = createFakeSignalR(clock, startSucceeds);
  const windowHandlers = {};
  const documentHandlers = {};
  const applied = [];
  const page = { fetches: 0 };
  const fire = (handlers, type) => (handlers[type] ?? []).forEach((handler) => handler());

  globalThis.window = {
    ...clock.timers,
    addEventListener: (type, handler) => (windowHandlers[type] ??= []).push(handler),
  };
  globalThis.document = {
    visibilityState: 'visible',
    addEventListener: (type, handler) => (documentHandlers[type] ??= []).push(handler),
  };

  if (withClient) {
    globalThis.signalR = fake.client;
  }

  Date.now = clock.now;

  startLive({
    random,
    applyStatus: (status) => applied.push(status),
    fetchStatus: () => {
      page.fetches += 1;

      return fetchStatus();
    },
  });

  return {
    clock,
    applied,
    page,
    connections: fake.connections,
    startTimes: fake.startTimes,
    setVisible: (visible) => {
      globalThis.document.visibilityState = visible ? 'visible' : 'hidden';
    },
    changeVisibility: () => fire(documentHandlers, 'visibilitychange'),
    comeOnline: () => fire(windowHandlers, 'online'),
  };
}

/**
 * Takes the fake client off the page again and gives Date.now back.
 */
function leaveLivePage() {
  delete globalThis.signalR;
  Date.now = realNow;
}

test('without the browser client the status is checked once a minute on a visible tab, and when the tab returns or the browser is back online', async () => {
  const { clock, page, setVisible, changeVisibility, comeOnline } = startLivePage({ withClient: false });

  try {
    await clock.advance(59000);
    assert.equal(page.fetches, 0);

    await clock.advance(1000);
    assert.equal(page.fetches, 1);

    setVisible(false);
    await clock.advance(120000);
    changeVisibility();
    await clock.advance(0);
    assert.equal(page.fetches, 1);

    setVisible(true);
    changeVisibility();
    await clock.advance(0);
    assert.equal(page.fetches, 2);

    comeOnline();
    await clock.advance(0);
    assert.equal(page.fetches, 3);
  } finally {
    leaveLivePage();
  }
});

test('the live connection uses the live route and the reconnect schedule, reports pushed statuses and skips the minute check while it is up', async () => {
  const { clock, page, applied, connections } = startLivePage({ fetchStatus: async () => ({ marker: 'fetched' }) });

  try {
    await clock.advance(0);

    assert.equal(connections.length, 1);
    assert.equal(connections[0].url, '/cabinet/live');
    assert.deepEqual([0, 1, 2, 3, 4, 20].map((previousRetryCount) => connections[0].reconnectDelay({ previousRetryCount })), [750, 2000, 10000, 30000, 60000, 60000]);
    assert.equal(page.fetches, 1);
    assert.deepEqual(applied, [{ marker: 'fetched' }]);

    connections[0].push({ marker: 'pushed' });
    connections[0].push(null);
    assert.deepEqual(applied, [{ marker: 'fetched' }, { marker: 'pushed' }]);

    await clock.advance(180000);
    assert.equal(page.fetches, 1);
  } finally {
    leaveLivePage();
  }
});

test('a connection that has ended is retried while the status is checked once a minute in the meantime', async () => {
  const { clock, page, connections } = startLivePage({ startSucceeds: (startNumber) => startNumber === 1 });

  try {
    await clock.advance(1000);
    connections[0].drop();
    await clock.advance(59000);
    assert.equal(page.fetches, 2);

    await clock.advance(60000);
    assert.equal(page.fetches, 3);
  } finally {
    leaveLivePage();
  }
});

test('while the connection is reconnecting the status is checked once a minute, and once when it is back', async () => {
  const { clock, page, connections } = startLivePage();

  try {
    await clock.advance(1000);
    assert.equal(page.fetches, 1);

    connections[0].reconnecting();
    await clock.advance(59000);
    assert.equal(page.fetches, 2);

    connections[0].reconnected();
    await clock.advance(0);
    assert.equal(page.fetches, 3);

    await clock.advance(180000);
    assert.equal(page.fetches, 3);
  } finally {
    leaveLivePage();
  }
});

test('a connection that never starts is retried after a moment, then after about 2 s, 10 s, 30 s and every minute without giving up', async () => {
  const { clock, startTimes } = startLivePage({ startSucceeds: () => false });

  try {
    await clock.advance(170000);

    assert.deepEqual(startTimes, [0, 750, 2750, 12750, 42750, 102750, 162750]);
  } finally {
    leaveLivePage();
  }
});

test('the retry waits follow the random source the page was given, in the page loop and in the automatic reconnect', async () => {
  const { clock, startTimes, connections } = startLivePage({ startSucceeds: () => false, random: () => 0 });

  try {
    await clock.advance(10000);

    assert.deepEqual(startTimes, [0, 250, 1850, 9850]);
    assert.deepEqual([0, 1, 2].map((previousRetryCount) => connections[0].reconnectDelay({ previousRetryCount })), [250, 1600, 8000]);
  } finally {
    leaveLivePage();
  }
});

test('a page the server turns away after the handshake is retried more and more gently, never at once', async () => {
  const { clock, startTimes, connections } = startLivePage();

  try {
    await clock.advance(0);
    connections[0].refuse();
    await clock.advance(749);
    assert.deepEqual(startTimes, [0]);

    await clock.advance(1);
    connections[0].refuse();
    await clock.advance(1999);
    assert.deepEqual(startTimes, [0, 750]);

    await clock.advance(1);
    connections[0].refuse();
    await clock.advance(10000);

    assert.deepEqual(startTimes, [0, 750, 2750, 12750]);
  } finally {
    leaveLivePage();
  }
});

test('a connection that stayed up for a minute starts the retry schedule over', async () => {
  const { clock, startTimes, connections } = startLivePage();

  try {
    await clock.advance(0);
    connections[0].drop();
    await clock.advance(750);
    await clock.advance(60000);
    connections[0].drop();
    await clock.advance(750);
    connections[0].drop();
    await clock.advance(2000);

    assert.deepEqual(startTimes, [0, 750, 61500, 63500]);
  } finally {
    leaveLivePage();
  }
});

test('a status check that overlaps another is skipped, and a failed or empty one changes nothing and does not block the next', async () => {
  const answers = [];
  const { clock, page, applied, comeOnline } = startLivePage({
    withClient: false,
    fetchStatus: () => new Promise((resolve, reject) => answers.push({ resolve, reject })),
  });

  try {
    comeOnline();
    comeOnline();
    assert.equal(page.fetches, 1);

    answers[0].resolve({ marker: 'first' });
    await clock.advance(0);
    assert.deepEqual(applied, [{ marker: 'first' }]);

    comeOnline();
    answers[1].reject(new Error('offline'));
    await clock.advance(0);

    comeOnline();
    answers[2].resolve(null);
    await clock.advance(0);

    comeOnline();
    answers[3].resolve({ marker: 'last' });
    await clock.advance(0);

    assert.equal(page.fetches, 4);
    assert.deepEqual(applied, [{ marker: 'first' }, { marker: 'last' }]);
  } finally {
    leaveLivePage();
  }
});

const SECONDS = 1000;

/**
 * Starts the sync block, runs the check and gives Date.now back whatever happens.
 * @param {object} setup Passed to startPage.
 * @param {(page: object) => Promise<void>} check The assertions to run against the started page.
 * @returns {Promise<void>}
 */
async function withPage(setup, check) {
  try {
    await check(startPage(setup));
  } finally {
    Date.now = realNow;
  }
}

/**
 * Builds a fetch that counts the presses it receives and answers each with the given answer, and answers status checks with the
 * same finished status.
 * @param {(clock: object) => object} pressAnswer Builds the answer to a press from the clock.
 * @returns {{ presses: () => number, fetchImpl: (clock: object) => Function }} The press counter and the fetch builder.
 */
function countingFetch(pressAnswer) {
  let presses = 0;

  return {
    presses: () => presses,
    fetchImpl: (clock) => async (url, init) => {
      if (init?.method === 'POST') {
        presses += 1;

        return pressAnswer(clock);
      }

      return answer(200, finished(clock.elapsed()));
    },
  };
}

test('inside the shared window the button counts down without being natively disabled, and a press sends nothing and says when to come back', async () => {
  const server = countingFetch(() => answer(202, { outcome: 'started', status: running(0) }));

  await withPage({ fetchImpl: server.fetchImpl, dataset: { cooldownEnds: isoAt(150 * SECONDS) } }, async ({ button, note, press, attributeLog }) => {
    assert.equal(button.textContent, 'Sync again in 2:30');
    assert.equal(button.getAttribute('aria-disabled'), 'true');
    assert.equal(button.getAttribute('aria-label'), 'Sync again in 3 minutes');

    await press();

    assert.equal(server.presses(), 0);
    assert.equal(note.textContent, 'You can sync again in 3 minutes.');
    assert.equal(attributeLog.some(([name]) => name === 'disabled'), false);
  });
});

test('while any sync runs the button reads Syncing..., and a press sends nothing and says one is already running', async () => {
  const server = countingFetch(() => answer(202, { outcome: 'started', status: running(0) }));

  await withPage({ fetchImpl: server.fetchImpl, dataset: { running: 'true' } }, async ({ button, note, press, attributeLog }) => {
    assert.equal(button.textContent, COPY.syncing);
    assert.equal(button.getAttribute('aria-disabled'), 'true');

    await press();

    assert.equal(server.presses(), 0);
    assert.equal(note.textContent, COPY.noteRunning);
    assert.equal(attributeLog.some(([name]) => name === 'disabled'), false);
  });
});

test('a press the server answers as already running says so and shows the running button', async () => {
  const server = countingFetch((clock) => answer(409, { outcome: 'running', status: running(clock.elapsed()) }));

  await withPage({ fetchImpl: server.fetchImpl }, async ({ button, note, press }) => {
    await press();

    assert.equal(server.presses(), 1);
    assert.equal(note.textContent, COPY.noteRunning);
    assert.equal(button.textContent, COPY.syncing);
  });
});

test('a press the server refuses inside the window says when to come back, with the minutes rounded up', async () => {
  const cooling = (clock) => ({ serverTimeUtc: isoAt(clock.elapsed()), running: false, cooldownEndsUtc: isoAt(121 * SECONDS), lastResult: 'changed', snapshotVersion: 'v1' });
  const server = countingFetch((clock) => answer(429, { outcome: 'cooldown', status: cooling(clock) }));

  await withPage({ fetchImpl: server.fetchImpl }, async ({ button, note, press }) => {
    await press();

    assert.equal(note.textContent, 'You can sync again in 3 minutes.');
    assert.equal(button.textContent, 'Sync again in 2:01');
    assert.equal(button.getAttribute('aria-disabled'), 'true');
  });
});

test('a refusal without a readable body still counts down from the seconds the server asked to wait', async () => {
  const refusal = { status: 429, ok: false, json: async () => Promise.reject(new SyntaxError('no body')), headers: { get: (name) => (name === 'Retry-After' ? '120' : null) } };
  const server = countingFetch(() => refusal);

  await withPage({ fetchImpl: server.fetchImpl }, async ({ button, note, press }) => {
    await press();

    assert.equal(button.textContent, 'Sync again in 2:00');
    assert.equal(button.getAttribute('aria-disabled'), 'true');
    assert.equal(note.textContent, 'You can sync again in 2 minutes.');
  });
});

test('an own press whose result is held back ends with the held-back sentence and nothing else', async () => {
  let server = running(0);

  await withPage(
    {
      fetchImpl: (clock) => async (url, init) => {
        if (init?.method === 'POST') {
          return answer(202, { outcome: 'started', status: running(clock.elapsed()) });
        }

        return answer(200, { ...server, serverTimeUtc: isoAt(clock.elapsed()) });
      },
    },
    async ({ clock, button, note, press }) => {
      await press();
      assert.equal(note.textContent, '');

      server = finished(0, 'heldBack');
      await clock.advance(0);

      assert.equal(note.textContent, COPY.noteHeldBack);
      assert.notEqual(button.textContent, COPY.syncing);
    },
  );
});

test('the connection sentence goes away when the next status arrives, and only then', async () => {
  await withPage(
    {
      fetchImpl: (clock) => async (url, init) => {
        if (init?.method === 'POST') {
          throw new TypeError('offline');
        }

        return answer(200, finished(clock.elapsed()));
      },
    },
    async ({ clock, note, api, press }) => {
      await press();
      assert.equal(note.textContent, COPY.noteOffline);

      await clock.advance(30 * SECONDS);
      assert.equal(note.textContent, COPY.noteOffline);

      api.applyStatus(finished(31 * SECONDS));
      assert.equal(note.textContent, '');
    },
  );
});

test('a refused press that got a status back still ends with the connection sentence and a later status clears it', async () => {
  const server = countingFetch((clock) => answer(500, { status: finished(clock.elapsed()) }));

  await withPage({ fetchImpl: server.fetchImpl }, async ({ note, api, press }) => {
    await press();
    assert.equal(note.textContent, COPY.noteOffline);

    api.applyStatus(finished(5 * SECONDS));
    assert.equal(note.textContent, '');
  });
});

test('a status arriving inside the window leaves the sentence about when to come back alone', async () => {
  const server = countingFetch(() => answer(202, { outcome: 'started', status: running(0) }));

  await withPage({ fetchImpl: server.fetchImpl, dataset: { cooldownEnds: isoAt(150 * SECONDS) } }, async ({ note, api, press }) => {
    await press();
    assert.equal(note.textContent, 'You can sync again in 3 minutes.');

    api.applyStatus({ serverTimeUtc: isoAt(10 * SECONDS), running: false, cooldownEndsUtc: isoAt(150 * SECONDS), lastResult: 'changed' });
    assert.equal(note.textContent, 'You can sync again in 3 minutes.');
  });
});

test('the already-running sentence goes away when that sync ends, whether the window follows or not', async () => {
  for (const cooldownEndsUtc of [isoAt(COOLDOWN_MS), null]) {
    const server = countingFetch(() => answer(202, { outcome: 'started', status: running(0) }));

    await withPage({ fetchImpl: server.fetchImpl, dataset: { running: 'true' } }, async ({ note, button, api, press }) => {
      await press();
      assert.equal(note.textContent, COPY.noteRunning);

      api.applyStatus({ ...running(5 * SECONDS), cooldownEndsUtc });
      assert.equal(note.textContent, COPY.noteRunning);

      api.applyStatus({ ...finished(10 * SECONDS), cooldownEndsUtc });
      assert.equal(note.textContent, '');
      assert.notEqual(button.textContent, COPY.syncing);
    });
  }
});

test('a press that is waiting for its own outcome is not cleared by the sync ending, it gets its outcome sentence', async () => {
  let server = running(0);

  await withPage(
    {
      fetchImpl: (clock) => async (url, init) => {
        if (init?.method === 'POST') {
          return answer(202, { outcome: 'started', status: running(clock.elapsed()) });
        }

        return answer(200, { ...server, serverTimeUtc: isoAt(clock.elapsed()) });
      },
    },
    async ({ clock, note, press }) => {
      await press();
      server = finished(0, 'unchanged');
      await clock.advance(0);

      assert.equal(note.textContent, COPY.noteUnchanged);
    },
  );
});

test('the own held-back sentence stays out of sight while the older-sync note says it, and shows again when that note goes', async () => {
  let server = running(0);
  const dataset = { lastSynced: isoAt(-60 * SECONDS), heldBack: 'true' };

  await withPage(
    {
      dataset,
      fetchImpl: (clock) => async (url, init) => {
        if (init?.method === 'POST') {
          return answer(202, { outcome: 'started', status: running(clock.elapsed()) });
        }

        return answer(200, { ...server, serverTimeUtc: isoAt(clock.elapsed()) });
      },
    },
    async ({ clock, note, staleNote, api, press }) => {
      assert.equal(staleNote.hidden, false);

      await press();
      server = finished(0, 'heldBack');
      await clock.advance(0);

      assert.equal(note.textContent, COPY.noteHeldBack);
      assert.equal(note.dataset.covered, 'true');

      api.applyStatus({ serverTimeUtc: isoAt(5 * SECONDS), heldBack: false });

      assert.equal(staleNote.hidden, true);
      assert.equal(note.textContent, COPY.noteHeldBack);
      assert.equal(note.dataset.covered, undefined);
    },
  );
});

test('the own held-back sentence is shown as it is when there is no older-sync note to carry it', async () => {
  let server = running(0);

  await withPage(
    {
      fetchImpl: (clock) => async (url, init) => {
        if (init?.method === 'POST') {
          return answer(202, { outcome: 'started', status: running(clock.elapsed()) });
        }

        return answer(200, { ...server, serverTimeUtc: isoAt(clock.elapsed()) });
      },
    },
    async ({ clock, note, press }) => {
      await press();
      server = finished(0, 'heldBack');
      await clock.advance(0);

      assert.equal(note.textContent, COPY.noteHeldBack);
      assert.equal(note.dataset.covered, undefined);
    },
  );
});

test('the countdown name changes only when its whole-minute wording does, and at the end the button, its name and the note are reset and the tick stops', async () => {
  const server = countingFetch(() => answer(202, { outcome: 'started', status: running(0) }));
  const setup = { fetchImpl: server.fetchImpl, dataset: { cooldownEnds: isoAt(61 * SECONDS) }, repeatingIntervals: true };

  await withPage(setup, async ({ clock, button, note, press, attributeLog }) => {
    const labels = () => attributeLog.filter(([name]) => name === 'aria-label').map(([, value]) => value);

    assert.equal(button.textContent, 'Sync again in 1:01');
    assert.deepEqual(labels(), ['Sync again in 2 minutes']);
    assert.ok(clock.repeatingDelays().includes(SECONDS));

    await press();
    assert.equal(note.textContent, 'You can sync again in 2 minutes.');

    await clock.advance(SECONDS);
    assert.equal(button.textContent, 'Sync again in 1:00');
    await clock.advance(2 * SECONDS);
    assert.equal(button.textContent, 'Sync again in 0:58');
    await clock.advance(30 * SECONDS);
    assert.equal(button.textContent, 'Sync again in 0:28');
    assert.deepEqual(labels(), ['Sync again in 2 minutes', 'Sync again in 1 minute', 'Sync again in less than a minute']);

    await clock.advance(28 * SECONDS);

    assert.equal(button.textContent, COPY.syncNow);
    assert.equal(button.getAttribute('aria-disabled'), 'false');
    assert.equal(button.getAttribute('aria-label'), null);
    assert.equal(note.textContent, '');
    assert.equal(clock.repeatingDelays().includes(SECONDS), false);
    assert.equal(server.presses(), 0);
  });
});

test('the countdown stops ticking while the tab is hidden and picks up the right time when it is visible again', async () => {
  const server = countingFetch(() => answer(202, { outcome: 'started', status: running(0) }));
  const setup = { fetchImpl: server.fetchImpl, dataset: { cooldownEnds: isoAt(300 * SECONDS) }, repeatingIntervals: true };

  await withPage(setup, async ({ clock, button, setVisibility }) => {
    assert.ok(clock.repeatingDelays().includes(SECONDS));

    setVisibility('hidden');
    assert.equal(clock.repeatingDelays().includes(SECONDS), false);

    await clock.advance(10 * SECONDS);
    assert.equal(button.textContent, 'Sync again in 5:00');

    setVisibility('visible');
    assert.equal(button.textContent, 'Sync again in 4:50');
    assert.ok(clock.repeatingDelays().includes(SECONDS));
  });
});

/**
 * A small stand-in for a browser element: it keeps its class, data set, attributes, custom properties, children and text, which
 * is all the cabinet scripts touch, and answers the few selectors they use.
 */
class FakeElement {
  /**
   * @param {object} page The fake document the element belongs to, which tracks focus.
   * @param {string} tagName The element name.
   */
  constructor(page, tagName) {
    this.page = page;
    this.tagName = tagName.toUpperCase();
    this.className = '';
    this.dataset = {};
    this.attributes = new Map();
    this.properties = new Map();
    this.style = {
      setProperty: (name, value) => this.properties.set(name, value),
      removeProperty: (name) => this.properties.delete(name),
    };
    this.children = [];
    this.parent = null;
    this.ownText = '';
    this.focusCalls = [];
    this.blurred = false;
    this.removed = false;
    this.listeners = [];
  }

  get textContent() {
    return this.ownText + this.children.map((child) => child.textContent).join('');
  }

  set textContent(value) {
    this.children = [];
    this.ownText = String(value);
  }

  append(...nodes) {
    for (const node of nodes) {
      node.parent = this;
      this.children.push(node);
    }
  }

  prepend(...nodes) {
    for (const node of nodes) {
      node.parent = this;
    }

    this.children.unshift(...nodes);
  }

  replaceChildren(...nodes) {
    this.children = [];
    this.ownText = '';
    this.append(...nodes);
  }

  setAttribute(name, value) {
    this.attributes.set(name, String(value));
  }

  getAttribute(name) {
    return this.attributes.has(name) ? this.attributes.get(name) : null;
  }

  removeAttribute(name) {
    this.attributes.delete(name);
  }

  addEventListener(type, handler, options) {
    this.listeners.push({ type, handler, options });
  }

  descendants() {
    return this.children.flatMap((child) => [child, ...child.descendants()]);
  }

  matches(selector) {
    const entry = /^\[data-entry-id="(.*)"\]$/.exec(selector);

    if (entry !== null) {
      return this.dataset.entryId === entry[1];
    }

    return selector.startsWith('.') && this.className.split(' ').includes(selector.slice(1));
  }

  querySelector(selector) {
    return this.descendants().find((node) => node.matches(selector)) ?? null;
  }

  contains(node) {
    return node === this || this.descendants().includes(node);
  }

  remove() {
    if (this.parent !== null) {
      this.parent.children = this.parent.children.filter((child) => child !== this);
      this.parent = null;
    }

    this.removed = true;
  }

  focus(options) {
    this.focusCalls.push(options);
    this.page.activeElement = this;
  }

  blur() {
    this.blurred = true;

    if (this.page.activeElement === this) {
      this.page.activeElement = this.page.body;
    }
  }
}

/**
 * Creates a fake document whose elements are FakeElements.
 * @returns {object} The document, with a body and createElement.
 */
function createFakeDocument() {
  const page = { visibilityState: 'visible', hidden: false };

  page.createElement = (tagName) => new FakeElement(page, tagName);
  page.body = page.createElement('body');
  page.activeElement = page.body;

  return page;
}

const PLACEMENT_DEFAULTS = Object.freeze({ kind: 'spine', xMm: 0, yMm: 0, widthMm: 40, heightMm: 280, toneIndex: 0, patternIndex: 0 });

/**
 * Builds a layout in the shape the layout route answers with: one section holding one cubby with the given placements.
 * @param {object[]} placements The placements; missing geometry and colour fields take neutral defaults.
 * @returns {object} The layout.
 */
function layoutWith(placements) {
  return {
    palette: [{ background: '#6b4f3a', text: '#ffffff' }],
    sections: [
      {
        index: 0,
        widthMm: 600,
        heightMm: 320,
        frameMm: 20,
        cubbies: [{ xMm: 0, yMm: 0, widthMm: 600, heightMm: 320, placements: placements.map((placement) => ({ ...PLACEMENT_DEFAULTS, ...placement })) }],
      },
    ],
  };
}

/**
 * Draws the layout into a fresh mount on a fake document and returns the drawn boxes.
 * @param {object[]} placements The placements to draw.
 * @returns {FakeElement[]} The placement buttons in drawing order.
 */
function drawPlacements(placements) {
  const previous = globalThis.document;
  const page = createFakeDocument();
  const mount = page.createElement('div');

  globalThis.document = page;

  try {
    renderCabinet(mount, layoutWith(placements), COPY);
  } finally {
    globalThis.document = previous;
  }

  return mount.descendants().filter((node) => node.tagName === 'BUTTON');
}

const { renderCabinet } = await import(pathToFileURL(join(PAGE_FOLDER, 'render.js')).href);

test('every drawn box carries its collection entry next to its game, and two copies of one game are two boxes with one name', () => {
  const placements = [
    { gameId: 7, entryId: 70, title: 'Invented Lighthouse', label: 'Invented Lighthouse' },
    { gameId: 7, entryId: 71, title: 'Invented Lighthouse', label: 'Invented Lighthouse', xMm: 40 },
    { kind: 'cover', gameId: 8, entryId: 80, title: 'Invented Orchard', label: 'Invented Orchard', xMm: 80, widthMm: 220 },
    { kind: 'orphanExpansion', gameId: 9, entryId: 90, title: 'Invented Orchard Seasons', label: 'Invented Orchard Seasons', isExpansion: true, xMm: 300 },
  ];

  const buttons = drawPlacements(placements);

  assert.equal(buttons.length, placements.length);
  buttons.forEach((button, index) => {
    assert.equal(button.dataset.entryId, String(placements[index].entryId));
    assert.equal(button.dataset.gameId, String(placements[index].gameId));
  });
  assert.notEqual(buttons[0].dataset.entryId, buttons[1].dataset.entryId);
  assert.equal(buttons[0].getAttribute('aria-label'), buttons[1].getAttribute('aria-label'));
});

test('an expansion whose base game is not known says it is an expansion in its name and on a second line, and a blank title reads as an untitled game', () => {
  const [orphan, blank, cover] = drawPlacements([
    { kind: 'orphanExpansion', gameId: 9, entryId: 90, title: 'Invented Harbour Tides', label: 'Invented Harbour Tides', isExpansion: true, showBaseLine: true },
    { kind: 'orphanExpansion', gameId: 10, entryId: 100, title: '   ', label: '   ', isExpansion: true, showBaseLine: true, xMm: 40 },
    { kind: 'cover', gameId: 11, entryId: 110, title: 'Invented Harbour Winds', label: 'Invented Harbour Winds', isExpansion: true, xMm: 80, widthMm: 220 },
  ]);

  assert.equal(orphan.getAttribute('aria-label'), 'Invented Harbour Tides, expansion');
  assert.equal(orphan.title, 'Invented Harbour Tides, expansion');
  assert.equal(orphan.querySelector('.placement-label').textContent, 'Invented Harbour Tides');
  assert.equal(orphan.querySelector('.placement-sub').textContent, 'Expansion');
  assert.equal(orphan.querySelector('.placement-sub').getAttribute('dir'), 'auto');
  assert.equal(orphan.querySelector('.placement-label').getAttribute('dir'), 'auto');
  assert.equal(blank.getAttribute('aria-label'), 'Untitled game, expansion');
  assert.equal(blank.querySelector('.placement-label').textContent, COPY.untitled);
  assert.equal(cover.getAttribute('aria-label'), 'Invented Harbour Winds, expansion');
  assert.equal(cover.querySelector('.placement-sub').textContent, 'Expansion');
});

test('an expansion whose base game is not owned names that game on its second line and in its name', () => {
  const [orphan] = drawPlacements([
    { kind: 'orphanExpansion', gameId: 9, entryId: 90, title: 'Invented Harbour Tides', label: 'Invented Harbour Tides', isExpansion: true, baseTitle: 'Invented Harbour', showBaseLine: true },
  ]);

  assert.equal(orphan.querySelector('.placement-sub').textContent, 'Expansion for Invented Harbour');
  assert.equal(orphan.getAttribute('aria-label'), 'Invented Harbour Tides, expansion for Invented Harbour');
  assert.equal(orphan.title, 'Invented Harbour Tides, expansion for Invented Harbour');
});

test('an upright expansion draws its second line only when the layout says it has room, and keeps its full name either way', () => {
  const base = { gameId: 9, entryId: 90, title: 'Invented Harbour Tides', label: 'Invented Harbour Tides', isExpansion: true, baseTitle: 'Invented Harbour', familyId: 1 };
  const [thin, wide, undecided, loose] = drawPlacements([
    { ...base, kind: 'expansionSpine', showBaseLine: false },
    { ...base, kind: 'expansionSpine', showBaseLine: true, xMm: 40 },
    { ...base, kind: 'expansionSpine', xMm: 80 },
    { ...base, kind: 'expansionSpine', showBaseLine: 'true', xMm: 120 },
  ]);

  assert.equal(thin.querySelector('.placement-sub'), null);
  assert.equal(thin.dataset.lines, '1');
  assert.equal(thin.getAttribute('aria-label'), 'Invented Harbour Tides, expansion for Invented Harbour');
  assert.equal(thin.title, 'Invented Harbour Tides, expansion for Invented Harbour');
  assert.equal(thin.querySelector('.placement-label').textContent, 'Invented Harbour Tides');
  assert.equal(wide.querySelector('.placement-sub').textContent, 'Expansion for Invented Harbour');
  assert.equal(wide.dataset.lines, undefined);
  assert.equal(undecided.querySelector('.placement-sub'), null);
  assert.equal(undecided.dataset.lines, '1');
  assert.equal(loose.querySelector('.placement-sub'), null, 'only the boolean true counts');
});

test('an orphan box draws its second line only when the layout says it has room, and keeps its full name either way', () => {
  const orphan = { kind: 'orphanExpansion', gameId: 9, entryId: 90, title: 'Invented Harbour Tides', label: 'Invented Harbour Tides', isExpansion: true, baseTitle: 'Invented Harbour' };
  const [thin, tall] = drawPlacements([
    { ...orphan, showBaseLine: false },
    { ...orphan, showBaseLine: true, xMm: 40 },
  ]);

  assert.equal(thin.querySelector('.placement-sub'), null);
  assert.equal(thin.dataset.lines, '1');
  assert.equal(thin.getAttribute('aria-label'), 'Invented Harbour Tides, expansion for Invented Harbour');
  assert.equal(tall.querySelector('.placement-sub').textContent, 'Expansion for Invented Harbour');
  assert.equal(tall.dataset.lines, undefined);
});

test('a base game is named by its title alone and draws no second line', () => {
  const [spine] = drawPlacements([{ gameId: 7, entryId: 70, title: 'Invented Lighthouse', label: 'Invented Lighthouse' }]);

  assert.equal(spine.getAttribute('aria-label'), 'Invented Lighthouse');
  assert.equal(spine.querySelector('.placement-sub'), null);
  assert.equal(spine.dataset.isExpansion, undefined);
});

test('markup in a title is shown as text, never read as markup', () => {
  const title = '<img src=x onerror=alert(1)> & <b>Bold</b> Co';
  const [spine] = drawPlacements([{ gameId: 7, entryId: 70, title, label: title }]);
  const label = spine.querySelector('.placement-label');

  assert.equal(spine.getAttribute('aria-label'), title);
  assert.equal(label.textContent, title);
  assert.equal(label.children.length, 0);
});

const ART_URL = '/art/0123456789abcdef-240.webp';

/**
 * Builds the art of a cover placement the way the layout route states it.
 * @param {object} [overrides] Fields that replace the valid defaults.
 * @returns {object} The art.
 */
function artWith(overrides = {}) {
  return { url: ART_URL, width: 240, height: 320, fit: 'width', ...overrides };
}

test('a cover with valid art is one image and nothing else, with the title kept in its name', () => {
  const [cover] = drawPlacements([
    { kind: 'cover', gameId: 8, entryId: 80, title: 'Invented Orchard', label: 'Invented Orchard', widthMm: 220, heightMm: 300, art: artWith() },
  ]);
  const [image] = cover.children;

  assert.equal(cover.dataset.art, 'true');
  assert.equal(cover.dataset.fit, 'width');
  assert.equal(cover.dataset.pattern, undefined);
  assert.equal(cover.children.length, 1);
  assert.equal(cover.querySelector('.cover-plate'), null);
  assert.equal(cover.querySelector('.placement-label'), null);
  assert.equal(cover.textContent, '');
  assert.equal(image.tagName, 'IMG');
  assert.equal(image.className, 'cover-art');
  assert.equal(image.src, ART_URL);
  assert.equal(image.width, 240);
  assert.equal(image.height, 320);
  assert.equal(image.alt, '');
  assert.equal(image.loading, 'lazy');
  assert.equal(image.decoding, 'async');
  assert.equal(image.draggable, false);
  assert.equal(cover.getAttribute('aria-label'), 'Invented Orchard');
  assert.equal(cover.title, 'Invented Orchard');
});

test('edge colours are set only when all four are valid colours', () => {
  const edges = { top: '#112233', right: '#223344', bottom: '#334455', left: '#445566' };
  const [good, partial, bad] = drawPlacements([
    { kind: 'cover', gameId: 1, entryId: 1, title: 'A', label: 'A', art: artWith({ edges }) },
    { kind: 'cover', gameId: 2, entryId: 2, title: 'B', label: 'B', art: artWith({ edges: { top: '#112233' } }) },
    { kind: 'cover', gameId: 3, entryId: 3, title: 'C', label: 'C', art: artWith({ edges: { ...edges, left: 'red; background: url(x)' } }) },
  ]);

  assert.equal(good.properties.get('--edge-t'), '#112233');
  assert.equal(good.properties.get('--edge-r'), '#223344');
  assert.equal(good.properties.get('--edge-b'), '#334455');
  assert.equal(good.properties.get('--edge-l'), '#445566');
  assert.equal(partial.properties.has('--edge-t'), false);
  assert.equal(bad.properties.has('--edge-l'), false);
  assert.equal(bad.properties.has('--edge-t'), false);
});

test('a picture that fails to load is swapped for the generated cover, keeping the colours and saying nothing', () => {
  const [cover] = drawPlacements([
    { kind: 'cover', gameId: 8, entryId: 80, title: 'Invented Orchard', label: 'Invented Orchard', patternIndex: 2, art: artWith({ fit: 'height' }) },
  ]);
  const image = cover.children[0];
  const failure = image.listeners.find((listener) => listener.type === 'error');

  assert.ok(failure);
  assert.deepEqual(failure.options, { once: true });
  assert.equal(cover.properties.get('--bg'), '#6b4f3a');
  assert.equal(cover.properties.get('--fg'), '#ffffff');

  const previous = globalThis.document;
  globalThis.document = cover.page;

  try {
    failure.handler();
  } finally {
    globalThis.document = previous;
  }

  assert.equal(image.removed, true);
  assert.equal(cover.dataset.art, undefined);
  assert.equal(cover.dataset.fit, undefined);
  assert.equal(cover.dataset.pattern, 'dots');
  assert.equal(cover.querySelector('.cover-art'), null);
  assert.equal(cover.querySelector('.cover-plate').querySelector('.placement-label').textContent, 'Invented Orchard');
  assert.equal(cover.properties.get('--bg'), '#6b4f3a');
  assert.equal(cover.properties.get('--fg'), '#ffffff');
  assert.equal(cover.getAttribute('aria-label'), 'Invented Orchard');
});

test('art that is not on the site, not a stored file name or not well formed is ignored and the generated cover is drawn', () => {
  const invalid = [
    artWith({ url: 'https://cf.example.org/a.webp' }),
    artWith({ url: '/' + '/example.org/art/0123456789abcdef-240.webp' }),
    artWith({ url: '/art/../snapshot.json' }),
    artWith({ url: '/art/0123456789ABCDEF-240.webp' }),
    artWith({ url: '/art/0123456789abcdef-240.png' }),
    artWith({ url: 7 }),
    artWith({ width: 0 }),
    artWith({ width: 4097 }),
    artWith({ height: 12.5 }),
    artWith({ height: '320' }),
    artWith({ fit: 'cover' }),
    null,
    'text',
  ];
  const buttons = drawPlacements(invalid.map((art, index) => (
    { kind: 'cover', gameId: index + 1, entryId: index + 1, title: `Invented ${index}`, label: `Invented ${index}`, art }
  )));

  assert.equal(buttons.length, invalid.length);

  for (const button of buttons) {
    assert.equal(button.dataset.art, undefined);
    assert.equal(button.querySelector('.cover-art'), null);
    assert.notEqual(button.querySelector('.cover-plate'), null);
    assert.equal(button.dataset.pattern, 'stripes');
  }
});

test('only a cover ever draws a picture', () => {
  const [spine] = drawPlacements([
    { kind: 'spine', gameId: 1, entryId: 1, title: 'Invented Spine', label: 'Invented Spine', art: artWith() },
  ]);

  assert.equal(spine.dataset.art, undefined);
  assert.equal(spine.querySelector('.cover-art'), null);
});

const ART_COLOUR = Object.freeze({ background: '#bd1e28', text: '#ffffff' });
const PALETTE_BACKGROUND = '#6b4f3a';
const PALETTE_TEXT = '#ffffff';

test('every kind of box except the marker takes the colour taken from its art', () => {
  const kinds = ['spine', 'flatBox', 'expansionLayer', 'expansionSpine', 'orphanExpansion', 'cover'];
  const buttons = drawPlacements(kinds.map((kind, index) => (
    { kind, gameId: index + 1, entryId: index + 1, title: `Invented ${kind}`, label: `Invented ${kind}`, colour: ART_COLOUR }
  )));

  assert.equal(buttons.length, kinds.length);

  for (const button of buttons) {
    assert.equal(button.properties.get('--bg'), ART_COLOUR.background, button.dataset.kind);
    assert.equal(button.properties.get('--fg'), ART_COLOUR.text, button.dataset.kind);
  }
});

test('a black title is accepted next to an art colour', () => {
  const [spine] = drawPlacements([
    { kind: 'spine', gameId: 1, entryId: 1, title: 'Invented Lantern', label: 'Invented Lantern', colour: { background: '#f2c14e', text: '#000000' } },
  ]);

  assert.equal(spine.properties.get('--bg'), '#f2c14e');
  assert.equal(spine.properties.get('--fg'), '#000000');
});

test('a colour that is not a lowercase hex background with a white or black title is ignored and the palette tone is kept', () => {
  const invalid = [
    { background: '#bd1e28', text: '#2a1a10' },
    { background: '#bd1e28', text: '#FFFFFF' },
    { background: 'red', text: '#ffffff' },
    { background: 'url(x)', text: '#ffffff' },
    { background: '#BD1E28', text: '#ffffff' },
    { background: '#bd1', text: '#ffffff' },
    { background: '#bd1e28; background: url(x)', text: '#ffffff' },
    { background: '#bd1e28' },
    { text: '#ffffff' },
    null,
    'text',
  ];
  const buttons = drawPlacements(invalid.map((colour, index) => (
    { kind: 'spine', gameId: index + 1, entryId: index + 1, title: `Invented ${index}`, label: `Invented ${index}`, colour }
  )));

  assert.equal(buttons.length, invalid.length);

  for (const button of buttons) {
    assert.equal(button.properties.get('--bg'), PALETTE_BACKGROUND);
    assert.equal(button.properties.get('--fg'), PALETTE_TEXT);
  }
});

test('the marker ignores a colour and takes none of its own', () => {
  const [marker] = drawPlacements([
    { kind: 'moreMarker', gameId: 1, entryId: 1, title: 'Invented Lantern', label: '', moreCount: 3, colour: ART_COLOUR },
  ]);

  assert.equal(marker.properties.has('--bg'), false);
  assert.equal(marker.properties.has('--fg'), false);
});

test('a cover with art takes the art colour and a failed picture keeps it while its edge bars go', () => {
  const edges = { top: '#112233', right: '#223344', bottom: '#334455', left: '#445566' };
  const [cover] = drawPlacements([
    { kind: 'cover', gameId: 8, entryId: 80, title: 'Invented Orchard', label: 'Invented Orchard', colour: ART_COLOUR, art: artWith({ edges }) },
  ]);
  const image = cover.children[0];
  const failure = image.listeners.find((listener) => listener.type === 'error');

  assert.equal(cover.properties.get('--bg'), ART_COLOUR.background);
  assert.equal(cover.properties.get('--fg'), ART_COLOUR.text);
  assert.equal(cover.properties.get('--edge-t'), '#112233');

  const previous = globalThis.document;
  globalThis.document = cover.page;

  try {
    failure.handler();
  } finally {
    globalThis.document = previous;
  }

  assert.equal(cover.dataset.art, undefined);
  assert.equal(cover.properties.get('--bg'), ART_COLOUR.background);
  assert.equal(cover.properties.get('--fg'), ART_COLOUR.text);

  for (const property of ['--edge-t', '--edge-r', '--edge-b', '--edge-l']) {
    assert.equal(cover.properties.has(property), false, property);
  }

  assert.notEqual(cover.querySelector('.cover-plate'), null);
});

test('a generated cover of a game with a colour takes it, and one without keeps its palette tone', () => {
  const [coloured, plain] = drawPlacements([
    { kind: 'cover', gameId: 1, entryId: 1, title: 'Invented Lantern', label: 'Invented Lantern', colour: ART_COLOUR },
    { kind: 'cover', gameId: 2, entryId: 2, title: 'Invented Quarry', label: 'Invented Quarry' },
  ]);

  assert.equal(coloured.properties.get('--bg'), ART_COLOUR.background);
  assert.notEqual(coloured.querySelector('.cover-plate'), null);
  assert.equal(plain.properties.get('--bg'), PALETTE_BACKGROUND);
});

test('the page scripts never write a style attribute or an inline handler', () => {
  for (const name of ['render.js', 'cabinet.js', 'sync.js', 'status.js', 'live.js', 'copy.js']) {
    const source = readFileSync(new URL('../../Cabinet.Service/wwwroot/js/' + name, import.meta.url), 'utf8');

    assert.doesNotMatch(source, /setAttribute\(\s*['"]style['"]/);
    assert.doesNotMatch(source, /\.style\.cssText|\.style\s*=|innerHTML|insertAdjacentHTML|setAttribute\(\s*['"]on/);
  }

  const [cover] = drawPlacements([
    { kind: 'cover', gameId: 8, entryId: 80, title: 'Invented Orchard', label: 'Invented Orchard', colour: ART_COLOUR, art: artWith({ edges: { top: '#112233', right: '#223344', bottom: '#334455', left: '#445566' } }) },
  ]);

  for (const node of [cover, ...cover.descendants()]) {
    assert.equal(node.attributes.has('style'), false);
  }
});

const CABINET_LAYOUT_ROUTE = '/cabinet/layout?profile=desktop';
const INVENTED_TITLES = ['Invented Lighthouse', 'Invented Orchard', 'Invented Harbour', 'Invented Meadow'];

/**
 * Builds a layout whose boxes are the given collection entries, one spine each.
 * @param {number[]} entryIds The collection entries to draw.
 * @returns {object} The layout.
 */
function layoutOfEntries(entryIds) {
  return layoutWith(entryIds.map((entryId, index) => ({
    gameId: entryId * 10,
    entryId,
    xMm: index * 40,
    title: INVENTED_TITLES[index % INVENTED_TITLES.length],
    label: INVENTED_TITLES[index % INVENTED_TITLES.length],
  })));
}

let cabinetPageCount = 0;

/**
 * Loads a fresh copy of the cabinet page script against a fake page: a mount, a being-filled block, the sync block, a fake
 * clock, and a fetch whose layout answers the test hands out one at a time. No live client is on the page, so status checks
 * come from the fallback and are triggered by the browser coming back online.
 * @param {{ firstLayout: object, status: () => object }} setup The first layout drawn and the status the server reports.
 * @returns {Promise<object>} The page, its parts and the levers the tests pull.
 */
async function startCabinetPage({ firstLayout, status }) {
  const clock = createClock(true);
  const page = createFakeDocument();
  const mount = page.createElement('main');
  const filling = page.createElement('div');
  const note = page.createElement('p');
  const documentHandlers = {};
  const windowHandlers = {};
  const layoutRequests = [];
  const replaced = [];
  const fire = (handlers, type) => (handlers[type] ?? []).forEach((handler) => handler());
  const syncElements = { '.sync-exact': page.createElement('p'), '.sync-stale': page.createElement('p'), '.sync-note': note };
  const syncRoot = {
    dataset: { serverTime: isoAt(0), running: 'false', staleAfter: '10800', snapshotVersion: 'v1' },
    parentElement: { querySelector: (selector) => syncElements[selector] ?? null },
    querySelector: () => null,
  };

  filling.className = 'cabinet-message cabinet-filling';
  page.body.append(filling, mount);

  const replaceChildren = mount.replaceChildren.bind(mount);

  mount.replaceChildren = (...nodes) => {
    replaced.push(nodes);
    replaceChildren(...nodes);
  };

  page.getElementById = (id) => (id === 'cabinet' ? mount : null);
  page.querySelector = (selector) => (selector === '.sync' ? syncRoot : page.body.querySelector(selector));
  page.addEventListener = (type, handler) => (documentHandlers[type] ??= []).push(handler);

  globalThis.window = {
    ...clock.timers,
    matchMedia: () => ({ matches: false, addEventListener: () => undefined }),
    addEventListener: (type, handler) => (windowHandlers[type] ??= []).push(handler),
  };
  globalThis.document = page;
  globalThis.CSS = { escape: (value) => value };
  globalThis.fetch = (url) => {
    if (url === '/cabinet/status') {
      return Promise.resolve(answer(200, { ...status(), serverTimeUtc: isoAt(clock.elapsed()) }));
    }

    assert.equal(url, CABINET_LAYOUT_ROUTE);

    return new Promise((resolve, reject) => layoutRequests.push({ resolve, reject }));
  };
  delete globalThis.signalR;
  Date.now = clock.now;

  cabinetPageCount += 1;
  await import(pathToFileURL(join(PAGE_FOLDER, 'cabinet.js')).href + '?page=' + cabinetPageCount);

  const settle = async () => {
    for (let round = 0; round < 10; round += 1) {
      await flushMicrotasks();
    }
  };

  layoutRequests.shift().resolve(answer(200, firstLayout));
  await settle();

  return {
    page,
    mount,
    filling,
    note,
    replaced,
    layoutRequests,
    settle,
    boxes: () => mount.descendants().filter((node) => node.tagName === 'BUTTON'),
    box: (entryId) => mount.querySelector('[data-entry-id="' + entryId + '"]'),
    comeOnline: async () => {
      fire(windowHandlers, 'online');
      await settle();
    },
    setHidden: async (hidden) => {
      page.hidden = hidden;
      page.visibilityState = hidden ? 'hidden' : 'visible';
      fire(documentHandlers, 'visibilitychange');
      await settle();
    },
  };
}

/**
 * Puts back the globals the cabinet page replaced.
 */
function leaveCabinetPage() {
  delete globalThis.CSS;
  Date.now = realNow;
}

test('a new collection version swaps the cabinet in one step only when the new layout is ready, drops the being-filled block and keeps focus on the same entry', async () => {
  let version = 'v1';

  try {
    const cabinet = await startCabinetPage({ firstLayout: layoutOfEntries([1, 2, 3]), status: () => ({ running: false, snapshotVersion: version }) });
    const before = cabinet.replaced.length;

    cabinet.box('2').focus();
    version = 'v2';
    await cabinet.comeOnline();

    assert.equal(cabinet.layoutRequests.length, 1);
    assert.equal(cabinet.replaced.length, before);
    assert.deepEqual(cabinet.boxes().map((box) => box.dataset.entryId), ['1', '2', '3']);
    assert.equal(cabinet.filling.removed, false);

    cabinet.layoutRequests.shift().resolve(answer(200, layoutOfEntries([4, 2, 1])));
    await cabinet.settle();

    assert.equal(cabinet.replaced.length, before + 1);
    assert.ok(cabinet.replaced.at(-1).every((node) => node.className === 'section'));
    assert.equal(cabinet.mount.querySelector('.cabinet-loading'), null);
    assert.deepEqual(cabinet.boxes().map((box) => box.dataset.entryId), ['4', '2', '1']);
    assert.equal(cabinet.filling.removed, true);
    assert.equal(cabinet.page.activeElement, cabinet.box('2'));
    assert.deepEqual(cabinet.box('2').focusCalls, [{ preventScroll: true }]);
    assert.equal(cabinet.note.textContent, '');
  } finally {
    leaveCabinetPage();
  }
});

test('when the focused entry is gone after a redraw the focus leaves the cabinet', async () => {
  let version = 'v1';

  try {
    const cabinet = await startCabinetPage({ firstLayout: layoutOfEntries([1, 2]), status: () => ({ running: false, snapshotVersion: version }) });
    const focused = cabinet.box('2');

    focused.focus();
    version = 'v2';
    await cabinet.comeOnline();
    cabinet.layoutRequests.shift().resolve(answer(200, layoutOfEntries([1])));
    await cabinet.settle();

    assert.equal(focused.blurred, true);
    assert.equal(cabinet.page.activeElement, cabinet.page.body);
    assert.equal(cabinet.box('2'), null);
  } finally {
    leaveCabinetPage();
  }
});

test('a status with the version already on screen fetches no layout', async () => {
  try {
    const cabinet = await startCabinetPage({ firstLayout: layoutOfEntries([1, 2]), status: () => ({ running: false, snapshotVersion: 'v1' }) });
    const before = cabinet.replaced.length;

    await cabinet.comeOnline();

    assert.equal(cabinet.layoutRequests.length, 0);
    assert.equal(cabinet.replaced.length, before);
  } finally {
    leaveCabinetPage();
  }
});

test('a redraw whose layout cannot be had leaves the old cabinet without a word, and the next status tries again', async () => {
  let version = 'v1';

  try {
    const cabinet = await startCabinetPage({ firstLayout: layoutOfEntries([1, 2]), status: () => ({ running: false, snapshotVersion: version }) });
    const before = cabinet.replaced.length;

    version = 'v2';
    await cabinet.comeOnline();
    cabinet.layoutRequests.shift().resolve(answer(503, null));
    await cabinet.settle();

    await cabinet.comeOnline();
    cabinet.layoutRequests.shift().reject(new TypeError('offline'));
    await cabinet.settle();

    assert.equal(cabinet.replaced.length, before);
    assert.deepEqual(cabinet.boxes().map((box) => box.dataset.entryId), ['1', '2']);
    assert.equal(cabinet.filling.removed, false);
    assert.equal(cabinet.note.textContent, '');
    assert.equal(cabinet.mount.querySelector('.cabinet-message'), null);

    await cabinet.comeOnline();
    cabinet.layoutRequests.shift().resolve(answer(200, layoutOfEntries([3])));
    await cabinet.settle();

    assert.deepEqual(cabinet.boxes().map((box) => box.dataset.entryId), ['3']);
  } finally {
    leaveCabinetPage();
  }
});

test('a hidden tab waits with the redraw until it is visible and then fetches the layout once', async () => {
  let version = 'v1';

  try {
    const cabinet = await startCabinetPage({ firstLayout: layoutOfEntries([1, 2]), status: () => ({ running: false, snapshotVersion: version }) });

    await cabinet.setHidden(true);
    version = 'v2';
    await cabinet.comeOnline();

    assert.equal(cabinet.layoutRequests.length, 0);

    await cabinet.setHidden(false);

    assert.equal(cabinet.layoutRequests.length, 1);
    cabinet.layoutRequests.shift().resolve(answer(200, layoutOfEntries([5])));
    await cabinet.settle();

    assert.deepEqual(cabinet.boxes().map((box) => box.dataset.entryId), ['5']);
    assert.equal(cabinet.layoutRequests.length, 0);
  } finally {
    leaveCabinetPage();
  }
});

test('the marker names the hidden expansions with its visible text first, in the singular and the plural', () => {
  assert.equal(COPY.moreName(3, 'Invented Harbour'), '+3 more expansions for Invented Harbour');
  assert.equal(COPY.moreName(1, 'Invented Harbour'), '+1 more expansion for Invented Harbour');

  for (let count = 1; count <= 20; count += 1) {
    assert.ok(COPY.moreName(count, 'Invented Harbour').startsWith(COPY.moreLabel(count)), `the name for ${count} starts with the visible text`);
  }
});

test('a drawn marker shows only its count and carries the full name as its accessible name and tooltip', () => {
  const [marker] = drawPlacements([
    { kind: 'moreMarker', gameId: 1, entryId: 1, title: 'Invented Harbour', label: '', baseTitle: 'Invented Harbour', moreCount: 4, familyId: 1 },
  ]);

  assert.equal(marker.querySelector('.placement-label').textContent, '+4 more');
  assert.equal(marker.getAttribute('aria-label'), '+4 more expansions for Invented Harbour');
  assert.equal(marker.title, '+4 more expansions for Invented Harbour');
});

const STYLESHEET = readFileSync(new URL('../../Cabinet.Service/wwwroot/css/cabinet.css', import.meta.url), 'utf8');

test('the stylesheet carries the apron arch tokens, the length registration of the unit and the cover line steps', () => {
  assert.equal(STYLESHEET.includes('--arch-shade-alpha'), false);
  assert.match(STYLESHEET, /--arch-rise: 40;/);
  assert.match(STYLESHEET, /--arch-radius: 110;/);
  assert.match(STYLESHEET, /--arch-alpha-top: 0\.92;/);
  assert.match(STYLESHEET, /--arch-alpha-bottom: 0\.72;/);
  assert.match(STYLESHEET, /--arch-lit: 0\.14;/);
  assert.match(STYLESHEET, /\.section-base::before \{[^}]*border-radius: calc\(var\(--arch-radius\) \* var\(--u\)\)/);
  assert.match(STYLESHEET, /@property --u \{\s*syntax: '<length>';\s*inherits: true;\s*initial-value: 0px;\s*\}/);
  assert.match(STYLESHEET, /\.placement\[data-kind="cover"\] \{\s*container-type: size;/);

  const steps = [
    ['@container \\(min-height: 64px\\)', 4],
    ['@container \\(min-height: 50px\\) and \\(max-height: 63\\.99px\\)', 3],
    ['@container \\(min-height: 36px\\) and \\(max-height: 49\\.99px\\)', 2],
    ['@container \\(max-height: 35\\.99px\\)', 1],
  ];

  for (const [query, lines] of steps) {
    assert.match(
      STYLESHEET,
      new RegExp(`${query} \\{[^}]*-webkit-line-clamp: ${lines};\\s*line-clamp: ${lines};`),
      `${lines} lines`,
    );
  }
});

const { initLanguageToggle } = await import(pathToFileURL(join(PAGE_FOLDER, 'language.js')).href);

/**
 * A language toggle with a click handler the test can run without a browser, and a record of what the browser would have done.
 * @param {{ fetch: () => Promise<object> }} actions The answer the request gets.
 * @returns {{ click: (current: boolean, overrides?: object) => Promise<{ prevented?: boolean }>, calls: string[] }} The toggle.
 */
function createToggle(actions) {
  const calls = [];
  const handlers = [];
  const toggle = { addEventListener: (type, handler) => handlers.push(handler) };
  const target = (current) => ({
    closest: () => ({ href: 'http://127.0.0.1/language/nl', getAttribute: () => (current ? 'true' : null) }),
  });

  initLanguageToggle(toggle, {
    fetch: async (address, options) => {
      calls.push(`fetch ${address} ${options.redirect}`);

      return actions.fetch();
    },
    reload: () => calls.push('reload'),
    assign: (address) => calls.push(`assign ${address}`),
  });

  return {
    calls,
    async click(current, overrides = {}) {
      const event = { button: 0, target: target(current), defaultPrevented: false, preventDefault() { this.prevented = true; }, ...overrides };

      await handlers[0](event);

      return event;
    },
  };
}

test('a plain click on the other language asks the server and reloads the same address', async () => {
  const toggle = createToggle({ fetch: async () => ({ type: 'opaqueredirect', ok: false }) });
  const event = await toggle.click(false);

  assert.equal(event.prevented, true);
  assert.deepEqual(toggle.calls, ['fetch http://127.0.0.1/language/nl manual', 'reload']);
});

test('a failed request or a refused answer follows the link instead', async () => {
  const failing = createToggle({ fetch: async () => Promise.reject(new TypeError('offline')) });

  await failing.click(false);
  assert.deepEqual(failing.calls, ['fetch http://127.0.0.1/language/nl manual', 'assign http://127.0.0.1/language/nl']);

  const refused = createToggle({ fetch: async () => ({ type: 'basic', ok: false }) });

  await refused.click(false);
  assert.deepEqual(refused.calls, ['fetch http://127.0.0.1/language/nl manual', 'assign http://127.0.0.1/language/nl']);
});

test('a click on the current language, with a modifier key or a non-primary button is left to the browser', async () => {
  const toggle = createToggle({ fetch: async () => ({ type: 'opaqueredirect', ok: false }) });

  for (const event of [await toggle.click(true), await toggle.click(false, { ctrlKey: true }), await toggle.click(false, { metaKey: true }), await toggle.click(false, { button: 1 })]) {
    assert.equal(event.prevented, undefined);
  }

  assert.deepEqual(toggle.calls, []);
});

test('a page without a toggle gets no handler and no error', () => {
  assert.doesNotThrow(() => initLanguageToggle(null));
  assert.doesNotThrow(() => initLanguageToggle(undefined));
});
