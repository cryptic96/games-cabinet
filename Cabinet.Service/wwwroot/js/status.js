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
 * Whole minutes left in the window, rounded up.
 * @param {number} remainingMs Milliseconds left in the window.
 * @returns {number} Zero for zero or less.
 */
export function wholeMinutesLeft(remainingMs) {
  return remainingMs > 0 ? Math.ceil(remainingMs / 60000) : 0;
}

/**
 * Which of the four outcome sentences a finished press gets; anything unknown counts as a failure.
 * @param {string | null | undefined} lastResult The last result the status reports.
 * @returns {'changed' | 'unchanged' | 'failed' | 'heldBack'}
 */
export function pressOutcome(lastResult) {
  return lastResult === 'changed' || lastResult === 'unchanged' || lastResult === 'heldBack' ? lastResult : 'failed';
}
