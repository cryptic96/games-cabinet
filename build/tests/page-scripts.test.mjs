/**
 * Checks the pure logic of the cabinet page scripts with the plain Node test runner: the relative-time sentences against the
 * case table the server's formatter is tested with, and the decisions about stale notes and clock skew. Each script is read
 * from disk and imported through a data URL, so no package, bundler or module configuration is needed.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

/**
 * Imports a script from the page's script folder through a data URL.
 * @param {string} name The file name inside the script folder.
 * @returns {Promise<object>} The module's exports.
 */
async function loadPageScript(name) {
  const source = readFileSync(new URL('../../Cabinet.Service/wwwroot/js/' + name, import.meta.url), 'utf8');

  return import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));
}

const { COPY } = await loadPageScript('copy.js');
const { serverOffsetMs, elapsedSeconds, isStale, buttonState, countdownText, wholeMinutesLeft, pressOutcome, shouldRedraw, reconnectDelayMs, isOutdatedStatus } = await loadPageScript('status.js');
const cases = JSON.parse(readFileSync(new URL('./fixtures/relative-time-cases.json', import.meta.url), 'utf8'));

const SYNCED = '2030-01-15T12:00:00.000Z';
const SYNCED_MS = Date.parse(SYNCED);
const THREE_HOURS = 3 * 60 * 60;

for (const { elapsedSeconds: seconds, text } of cases) {
  test(`${seconds} seconds reads as "${text}"`, () => {
    assert.equal(COPY.syncedAgo(seconds), text);
  });
}

test('the never-synced line is fixed copy', () => {
  assert.equal(COPY.notSynced, 'Not synced yet');
});

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
  assert.equal(wholeMinutesLeft(582000), 10);
  assert.equal(wholeMinutesLeft(0), 0);
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

test('the reconnect delays are at once, 2 s, 10 s, 30 s and then every minute', () => {
  assert.equal(reconnectDelayMs(0), 0);
  assert.equal(reconnectDelayMs(1), 2000);
  assert.equal(reconnectDelayMs(2), 10000);
  assert.equal(reconnectDelayMs(3), 30000);
  assert.equal(reconnectDelayMs(4), 60000);
  assert.equal(reconnectDelayMs(50), 60000);
});
