/**
 * Pure decisions behind the sync status line: how far the visitor's clock is from the server's, how long ago the last good
 * sync was, and whether the page should say it is showing an older sync. No imports and no page access, so a plain Node
 * test can load this file.
 */

/**
 * Parses an ISO 8601 time into milliseconds since the epoch.
 * @param {string | null | undefined} iso The time text.
 * @returns {number | null} The milliseconds, or null when the text is missing or not a time.
 */
function parseMilliseconds(iso) {
  if (typeof iso !== 'string' || iso === '') {
    return null;
  }

  const milliseconds = Date.parse(iso);

  return Number.isNaN(milliseconds) ? null : milliseconds;
}

/**
 * How far ahead of the visitor's clock the server's clock is, so every later computation can be done on the server's time.
 * @param {string | null | undefined} serverTimeIso The server's time when the state was read.
 * @param {number} clientNowMs The visitor's clock when the state arrived.
 * @returns {number} Milliseconds to add to the visitor's clock to get the server's; zero when the server time is unusable.
 */
export function serverOffsetMs(serverTimeIso, clientNowMs) {
  const server = parseMilliseconds(serverTimeIso);

  return server === null ? 0 : server - clientNowMs;
}

/**
 * Whole seconds between the last good sync and now, counted on the server's clock; negative for a time in the future.
 * @param {string | null | undefined} lastSyncedIso When the collection was last confirmed.
 * @param {number} serverNowMs The current time on the server's clock.
 * @returns {number | null} The seconds rounded down, or null when there is no last sync.
 */
export function elapsedSeconds(lastSyncedIso, serverNowMs) {
  const last = parseMilliseconds(lastSyncedIso);

  return last === null ? null : Math.floor((serverNowMs - last) / 1000);
}

/**
 * Whether the page should say it is showing an older sync: only once something has been synced, and then while a result is
 * held back or when the last good sync is at least as old as the stale threshold.
 * @param {string | null | undefined} lastSyncedIso When the collection was last confirmed.
 * @param {number} serverNowMs The current time on the server's clock.
 * @param {number} staleAfterSeconds How old the collection may get before it counts as stale.
 * @param {boolean} heldBack Whether a suspicious result is waiting to be confirmed.
 * @returns {boolean}
 */
export function isStale(lastSyncedIso, serverNowMs, staleAfterSeconds, heldBack) {
  const last = parseMilliseconds(lastSyncedIso);

  if (last === null) {
    return false;
  }

  return heldBack || serverNowMs - last >= staleAfterSeconds * 1000;
}

/**
 * Where the sync button stands, from the status and the server's clock. A running sync wins over a cooldown, and a cooldown
 * wins over idle, so the button reads the same for everyone while a sync runs.
 * @param {{ running?: boolean, cooldownEndsUtc?: string | null }} status The running flag and the end of the shared window.
 * @param {number} serverNowMs The current time on the server's clock.
 * @returns {{ kind: 'running' | 'cooldown' | 'idle', remainingMs: number }} The state and the time left in the window.
 */
export function buttonState(status, serverNowMs) {
  const end = parseMilliseconds(status.cooldownEndsUtc);
  const remainingMs = end === null ? 0 : Math.max(0, end - serverNowMs);

  if (status.running === true) {
    return { kind: 'running', remainingMs };
  }

  return { kind: remainingMs > 0 ? 'cooldown' : 'idle', remainingMs };
}

/**
 * The countdown as minutes and seconds, with the seconds rounded up so the last fraction of a second still reads 0:01.
 * @param {number} remainingMs Milliseconds left in the window.
 * @returns {string} For example 9:42 or 0:07; 0:00 for zero or less.
 */
export function countdownText(remainingMs) {
  const totalSeconds = remainingMs > 0 ? Math.ceil(remainingMs / 1000) : 0;
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;

  return minutes + ':' + String(seconds).padStart(2, '0');
}

/**
 * Which of the four outcome sentences a finished press gets; anything unknown counts as a failure.
 * @param {string | null | undefined} lastResult The last result the status reports.
 * @returns {'changed' | 'unchanged' | 'failed' | 'heldBack'}
 */
export function pressOutcome(lastResult) {
  return lastResult === 'changed' || lastResult === 'unchanged' || lastResult === 'heldBack' ? lastResult : 'failed';
}

/**
 * Whether a status shows a collection version other than the one on screen. A status without a version never asks for a redraw.
 * @param {string | null | undefined} onScreenVersion The snapshot version the cabinet on screen was drawn from; empty before anything was synced.
 * @param {{ snapshotVersion?: string | null }} status The status that arrived.
 * @returns {boolean}
 */
export function shouldRedraw(onScreenVersion, status) {
  const version = status === null || typeof status !== 'object' ? null : status.snapshotVersion;

  return typeof version === 'string' && version !== '' && version !== (onScreenVersion ?? '');
}

/**
 * Whether a status was produced before one the page has already taken. Pushed and fetched statuses can arrive out of order (a
 * sync that ends within milliseconds is announced before the answer to the press reaches the page), and an older one must never
 * overwrite a newer one. A status without a readable server time is never treated as outdated.
 * @param {number} newestServerTimeMs The server time, in milliseconds, of the newest status taken so far.
 * @param {{ serverTimeUtc?: string }} status The status that arrived.
 * @returns {boolean}
 */
export function isOutdatedStatus(newestServerTimeMs, status) {
  const at = status === null || typeof status !== 'object' || typeof status.serverTimeUtc !== 'string'
    ? Number.NaN
    : Date.parse(status.serverTimeUtc);

  return Number.isFinite(at) && at < newestServerTimeMs;
}

/**
 * Whether the page should keep following the visitor's own press: while the press has not reached its outcome and the longest
 * plausible sync has not passed.
 * @param {boolean} pending Whether the press is still waiting for its outcome.
 * @param {number} nowMs The visitor's clock now.
 * @param {number} deadlineMs When to stop following the press, on the same clock.
 * @returns {boolean}
 */
export function ownSyncStillWaiting(pending, nowMs, deadlineMs) {
  return pending === true && nowMs < deadlineMs;
}

const RECONNECT_DELAYS_MS = Object.freeze([0, 2000, 10000, 30000]);
const STEADY_RECONNECT_DELAY_MS = 60000;
const RECONNECT_JITTER_RATIO = 0.2;
const FIRST_RETRY_FLOOR_MS = 250;
const FIRST_RETRY_SPREAD_MS = 1000;

/**
 * Turns a random source's answer into a number in [0, 1), so a faulty source can never push a delay out of its bounds.
 * @param {() => number} random The random source.
 * @returns {number} The answer clamped into [0, 1); the middle when it is not a number.
 */
function unitInterval(random) {
  const value = random();

  return Number.isFinite(value) ? Math.min(Math.max(value, 0), 1 - Number.EPSILON) : 0.5;
}

/**
 * How long to wait before the next try to reach the live connection: a moment (a quarter to one and a quarter seconds) for the
 * first try, then about 2 s, 10 s, 30 s, and about every 60 s after that without ever giving up. Every wait is spread so that
 * pages that lost the channel together do not all come back together: the later steps vary by up to 20 percent either way, and
 * the first one is spread over a second. A wait is never negative and the first is never zero.
 * @param {number} previousRetryCount How many tries have already failed since the connection was last up.
 * @param {() => number} [random] A source of numbers in [0, 1); Math.random unless a test hands in its own.
 * @returns {number} Whole milliseconds to wait.
 */
export function reconnectDelayMs(previousRetryCount, random = Math.random) {
  const unit = unitInterval(random);
  const base = RECONNECT_DELAYS_MS[previousRetryCount] ?? STEADY_RECONNECT_DELAY_MS;

  if (base === 0) {
    return FIRST_RETRY_FLOOR_MS + Math.floor(unit * FIRST_RETRY_SPREAD_MS);
  }

  return Math.round(base * (1 - RECONNECT_JITTER_RATIO + 2 * RECONNECT_JITTER_RATIO * unit));
}
