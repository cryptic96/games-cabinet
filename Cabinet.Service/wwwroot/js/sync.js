/**
 * Wires the sync status line: rewrites the server's UTC first paint into the visitor's own time, shows the exact time when the
 * relative time is pressed, keeps the relative time and the older-sync note current from local timers, and takes a newer
 * status from whatever fetches one. It never writes error text; a failed fetch elsewhere simply leaves the last values.
 */
import { COPY } from './copy.js';
import { serverOffsetMs, elapsedSeconds, isStale } from './status.js';

const REFRESH_INTERVAL_MS = 30000;

/**
 * Starts the status line behaviour for the sync block.
 * @param {HTMLElement} root The element carrying the first-paint state as data attributes.
 * @returns {{ applyStatus: (status: object) => void, refresh: () => void }} The status takers other scripts call.
 */
export function initSyncStatus(root) {
  const scope = root.parentElement;
  const exactLine = scope.querySelector('.sync-exact');
  const staleNote = scope.querySelector('.sync-stale');

  let timeButton = root.querySelector('.sync-time');
  let lastSyncedIso = root.dataset.lastSynced ?? '';
  let heldBack = root.dataset.heldBack === 'true';
  let staleAfterSeconds = Number(root.dataset.staleAfter);
  let offsetMs = serverOffsetMs(root.dataset.serverTime, Date.now());

  /**
   * Builds the relative-time button when the first sync lands on a page that loaded before any had.
   */
  function ensureTimeButton() {
    if (timeButton !== null) {
      return;
    }

    const time = document.createElement('time');
    timeButton = document.createElement('button');
    timeButton.type = 'button';
    timeButton.className = 'sync-time';
    timeButton.setAttribute('aria-expanded', exactLine.hidden ? 'false' : 'true');
    timeButton.setAttribute('aria-controls', exactLine.id);
    timeButton.append(time);
    timeButton.addEventListener('click', toggleExact);

    const placeholder = root.querySelector('.sync-time-text');

    if (placeholder !== null) {
      placeholder.replaceWith(timeButton);
    } else {
      root.prepend(timeButton);
    }
  }

  /**
   * Shows or hides the exact-time line and keeps the button's expanded state in step.
   */
  function toggleExact() {
    exactLine.hidden = !exactLine.hidden;
    timeButton.setAttribute('aria-expanded', exactLine.hidden ? 'false' : 'true');
  }

  /**
   * Rewrites the relative time, the exact time and the older-sync note from the current state and the visitor's clock.
   */
  function refresh() {
    if (lastSyncedIso === '') {
      staleNote.hidden = true;
      staleNote.textContent = '';
      return;
    }

    ensureTimeButton();

    const serverNow = Date.now() + offsetMs;
    const exact = COPY.exactTime(new Date(lastSyncedIso));
    const time = timeButton.querySelector('time');

    time.dateTime = lastSyncedIso;
    time.textContent = COPY.syncedAgo(elapsedSeconds(lastSyncedIso, serverNow));
    timeButton.title = exact;
    exactLine.textContent = COPY.lastSynced(exact);

    const stale = isStale(lastSyncedIso, serverNow, staleAfterSeconds, heldBack);

    staleNote.hidden = !stale;
    staleNote.textContent = stale ? (heldBack ? COPY.staleHeldBack(exact) : COPY.staleRecent(exact)) : '';
  }

  /**
   * Takes a status in the shape the status route answers with: corrects the clock offset and redraws every field.
   * @param {object} status The status; missing fields leave the last values in place.
   */
  function applyStatus(status) {
    if (typeof status.serverTimeUtc === 'string') {
      offsetMs = serverOffsetMs(status.serverTimeUtc, Date.now());
      root.dataset.serverTime = status.serverTimeUtc;
    }

    if ('lastSyncedUtc' in status) {
      lastSyncedIso = status.lastSyncedUtc ?? '';
      root.dataset.lastSynced = lastSyncedIso;
    }

    if ('heldBack' in status) {
      heldBack = status.heldBack === true;
      root.dataset.heldBack = String(heldBack);
    }

    if (typeof status.staleAfterSeconds === 'number') {
      staleAfterSeconds = status.staleAfterSeconds;
      root.dataset.staleAfter = String(staleAfterSeconds);
    }

    if ('running' in status) {
      root.dataset.running = String(status.running === true);
    }

    if ('cooldownEndsUtc' in status) {
      root.dataset.cooldownEnds = status.cooldownEndsUtc ?? '';
    }

    if ('snapshotVersion' in status) {
      root.dataset.snapshotVersion = status.snapshotVersion ?? '';
    }

    refresh();
  }

  if (timeButton !== null) {
    timeButton.addEventListener('click', toggleExact);
  }

  window.setInterval(() => {
    if (document.visibilityState === 'visible') {
      refresh();
    }
  }, REFRESH_INTERVAL_MS);

  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible') {
      refresh();
    }
  });

  refresh();

  return { applyStatus, refresh };
}
