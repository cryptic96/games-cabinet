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
const { serverOffsetMs, elapsedSeconds, isStale } = await loadPageScript('status.js');
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
