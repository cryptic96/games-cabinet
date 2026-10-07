/**
 * Wires the sync block: rewrites the server's UTC first paint into the visitor's own time, shows the exact time when the
 * relative time is pressed, keeps the relative time and the older-sync note current from local timers, runs the "Sync now"
 * button (its states, its countdown and the one sentence that follows the visitor's own press) and takes a newer status from
 * whatever fetches one. Only the visitor's own press ever writes to the note; the page may only clear it, when what it says is no
 * longer true. A failed status fetch simply leaves the last values.
 */
import { COPY } from './copy.js';
import { serverOffsetMs, elapsedSeconds, isStale, buttonState, countdownText, pressOutcome, shouldRedraw, isOutdatedStatus, ownSyncStillWaiting } from './status.js';

const REFRESH_INTERVAL_MS = 30000;
const POLL_INTERVAL_MS = 5000;
const POLL_LIMIT_MS = 600000;
const PRESS_TIMEOUT_MS = 15000;
const TICK_INTERVAL_MS = 1000;
const OUTCOME_NOTES = Object.freeze({
  changed: COPY.noteChanged,
  unchanged: COPY.noteUnchanged,
  failed: COPY.noteFailed,
  heldBack: COPY.noteHeldBack,
});

/**
 * Asks the server for the current sync status.
 * @returns {Promise<object | null>} The status, or null when it could not be had; a failure is never shown.
 */
export async function fetchStatus() {
  try {
    const response = await fetch('/cabinet/status');

    return response.ok ? await response.json() : null;
  } catch {
    return null;
  }
}

/**
 * Starts the sync block behaviour.
 * @param {HTMLElement} root The element carrying the first-paint state as data attributes.
 * @param {{ onCollectionChanged?: () => Promise<boolean | void> | void }} [options] onCollectionChanged is called when the status
 *   shows a collection version that is not the one on screen; a result of false means the redraw did not happen and the next
 *   status tries again.
 * @returns {{ applyStatus: (status: object, options?: { ownPress?: boolean }) => void, refresh: () => void }} The status takers other scripts call.
 */
export function initSyncStatus(root, options = {}) {
  const scope = root.parentElement;
  const exactLine = scope.querySelector('.sync-exact');
  const staleNote = scope.querySelector('.sync-stale');
  const note = scope.querySelector('.sync-note');
  const button = root.querySelector('.sync-button');

  let timeButton = root.querySelector('.sync-time');
  let lastSyncedIso = root.dataset.lastSynced ?? '';
  let heldBack = root.dataset.heldBack === 'true';
  let staleAfterSeconds = Number(root.dataset.staleAfter);
  let offsetMs = serverOffsetMs(root.dataset.serverTime, Date.now());
  let running = root.dataset.running === 'true';
  let cooldownEndsIso = root.dataset.cooldownEnds ?? '';
  let shownVersion = root.dataset.snapshotVersion ?? '';
  let redrawingVersion = null;
  let previousKind = null;
  let ticker = null;
  let pressing = false;
  let ownSyncPending = false;
  let pollTimer = null;
  let pollGeneration = 0;
  let newestServerTimeMs = Date.parse(root.dataset.serverTime ?? '');

  if (!Number.isFinite(newestServerTimeMs)) {
    newestServerTimeMs = Number.NEGATIVE_INFINITY;
  }

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
      updateNoteCover();
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
    updateNoteCover();
  }

  /**
   * Where the button stands on the server's clock right now.
   * @returns {{ kind: 'running' | 'cooldown' | 'idle', remainingMs: number }}
   */
  function currentState() {
    return buttonState({ running, cooldownEndsUtc: cooldownEndsIso }, Date.now() + offsetMs);
  }

  /**
   * Sets an attribute only when its value differs, so a tick that changes nothing touches nothing.
   * @param {string} name The attribute name.
   * @param {string | null} value The wanted value; null removes the attribute.
   */
  function setButtonAttribute(name, value) {
    if (value === null) {
      button.removeAttribute(name);
    } else if (button.getAttribute(name) !== value) {
      button.setAttribute(name, value);
    }
  }

  /**
   * Draws the button from the current state: its words, its aria-disabled and, during the window, an accessible name that only
   * changes when the whole-minute wording does. Starts or stops the one-second tick as the state needs.
   */
  function renderButton() {
    if (button === null) {
      return;
    }

    const state = currentState();
    let text = COPY.syncNow;
    let disabled = 'false';
    let label = null;

    if (state.kind === 'running') {
      text = COPY.syncing;
      disabled = 'true';
    } else if (state.kind === 'cooldown') {
      text = COPY.syncAgainIn(countdownText(state.remainingMs));
      disabled = 'true';
      label = COPY.syncAgainName(state.remainingMs);
    }

    if (button.textContent !== text) {
      button.textContent = text;
    }

    setButtonAttribute('aria-disabled', disabled);
    setButtonAttribute('aria-label', label);
    button.hidden = false;

    if (previousKind === 'cooldown' && state.kind === 'idle') {
      say('');
    }

    if (previousKind === 'running' && state.kind !== 'running' && !ownSyncPending && note.textContent === COPY.noteRunning) {
      say('');
    }

    previousKind = state.kind;
    updateTicker(state.kind === 'cooldown');
  }

  /**
   * Runs the one-second tick only while the window is active and the tab is visible; stops it otherwise.
   * @param {boolean} wanted Whether the window is active.
   */
  function updateTicker(wanted) {
    const shouldTick = wanted && document.visibilityState === 'visible';

    if (shouldTick && ticker === null) {
      ticker = window.setInterval(renderButton, TICK_INTERVAL_MS);
    } else if (!shouldTick && ticker !== null) {
      window.clearInterval(ticker);
      ticker = null;
    }
  }

  /**
   * Writes the one sentence of the visitor's own press cycle, or clears it when given an empty text.
   * @param {string} text The sentence.
   */
  function say(text) {
    note.textContent = text;
    updateNoteCover();
  }

  /**
   * Keeps the visitor's own held-back sentence out of sight while the older-sync note already says the same thing. The sentence
   * stays in the live region, so it is still announced, and shows again if the older-sync note goes away.
   */
  function updateNoteCover() {
    if (note.textContent === COPY.noteHeldBack && !staleNote.hidden && heldBack) {
      note.dataset.covered = 'true';
    } else {
      delete note.dataset.covered;
    }
  }

  /**
   * Clears the connection sentence once any status has arrived, because the site has just been reached.
   */
  function clearOfflineNote() {
    if (note.textContent === COPY.noteOffline) {
      say('');
    }
  }

  /**
   * Asks the page to redraw when the status names a collection version that is not on screen, and remembers the version once the
   * redraw reports success. A redraw already running for that version is not started twice.
   * @param {string | null | undefined} version The version the status reports.
   */
  async function redrawIfChanged(version) {
    if (typeof options.onCollectionChanged !== 'function' || !shouldRedraw(shownVersion, { snapshotVersion: version }) || version === redrawingVersion) {
      return;
    }

    redrawingVersion = version;

    try {
      const drawn = await options.onCollectionChanged();

      if (drawn !== false) {
        shownVersion = version;
      }
    } catch {
      return;
    } finally {
      if (redrawingVersion === version) {
        redrawingVersion = null;
      }
    }
  }

  /**
   * Takes a status in the shape the status route answers with: corrects the clock offset, redraws every field and the button, and
   * asks for a redraw of the cabinet when the collection version changed. For the visitor's own finished press it also writes
   * the one outcome sentence.
   * @param {object} status The status; missing fields leave the last values in place.
   * @param {{ ownPress?: boolean }} [context] ownPress is true only for the status that ends this visitor's own sync. A page that
   *   started a sync also treats the first status that finds nothing running as the end of it, wherever that status came from.
   */
  function applyStatus(status, context = {}) {
    clearOfflineNote();

    if (isOutdatedStatus(newestServerTimeMs, status)) {
      return;
    }

    if (typeof status.serverTimeUtc === 'string') {
      newestServerTimeMs = Math.max(newestServerTimeMs, Date.parse(status.serverTimeUtc) || newestServerTimeMs);
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
      running = status.running === true;
      root.dataset.running = String(running);
    }

    if ('cooldownEndsUtc' in status) {
      cooldownEndsIso = status.cooldownEndsUtc ?? '';
      root.dataset.cooldownEnds = cooldownEndsIso;
    }

    if ('snapshotVersion' in status) {
      root.dataset.snapshotVersion = status.snapshotVersion ?? '';
    }

    refresh();
    renderButton();

    if ((context.ownPress === true || ownSyncPending) && !running) {
      ownSyncPending = false;
      window.clearTimeout(pollTimer);
      pollTimer = null;
      say(OUTCOME_NOTES[pressOutcome(status.lastResult)]);
    }

    redrawIfChanged(status.snapshotVersion);
  }

  /**
   * Fetches the status once and applies it.
   * @param {boolean} ownPress Whether this status may end the visitor's own press cycle.
   * @returns {Promise<boolean>} True when the status arrived and no sync is running any more.
   */
  async function fetchAndApplyStatus(ownPress) {
    const status = await fetchStatus();

    if (status === null || typeof status !== 'object') {
      return false;
    }

    applyStatus(status, { ownPress });

    return status.running !== true;
  }

  /**
   * Follows this page's accepted press until the sync ends or the longest plausible sync has passed, so the button can never stay
   * on "Syncing..." for good. The status is fetched at once and then every few seconds whether or not pushed statuses arrive, and
   * once more when the time runs out, because a push can be lost and an answer can be older than the push that followed it.
   */
  function pollOwnSync() {
    const deadline = Date.now() + POLL_LIMIT_MS;

    pollGeneration += 1;

    const generation = pollGeneration;

    ownSyncPending = true;
    window.clearTimeout(pollTimer);

    const step = async () => {
      pollTimer = null;
      await fetchAndApplyStatus(ownSyncPending);

      if (generation !== pollGeneration) {
        return;
      }

      if (!ownSyncStillWaiting(ownSyncPending, Date.now(), deadline)) {
        ownSyncPending = false;
        return;
      }

      pollTimer = window.setTimeout(step, POLL_INTERVAL_MS);
    };

    pollTimer = window.setTimeout(step, 0);
  }

  /**
   * Reads the answer body of the sync route, which carries the status under every outcome.
   * @param {Response} response The answer.
   * @returns {Promise<{ status?: object } | null>} The body, or null when it is not the expected JSON.
   */
  async function readAnswer(response) {
    try {
      return await response.json();
    } catch {
      return null;
    }
  }

  /**
   * Adopts the window from a refusal that carried no status, using the whole seconds the server asked the visitor to wait.
   * @param {Response} response The refusal.
   */
  function adoptRetryAfter(response) {
    const seconds = Number(response.headers.get('Retry-After'));

    if (Number.isFinite(seconds) && seconds > 0) {
      applyStatus({ cooldownEndsUtc: new Date(Date.now() + offsetMs + seconds * 1000).toISOString() });
    }
  }

  /**
   * Handles a press: a button that is not pressable explains why and sends nothing; an idle one posts to the sync route and
   * follows the answer.
   */
  async function onPress() {
    if (pressing) {
      return;
    }

    const state = currentState();

    if (state.kind === 'running') {
      say(COPY.noteRunning);
      return;
    }

    if (state.kind === 'cooldown') {
      say(COPY.youCanSyncAgain(state.remainingMs));
      return;
    }

    pressing = true;

    const controller = new AbortController();
    const timeout = window.setTimeout(() => controller.abort(), PRESS_TIMEOUT_MS);

    try {
      const response = await fetch('/cabinet/sync', { method: 'POST', signal: controller.signal });
      const answer = await readAnswer(response);
      const accepted = response.status === 202;

      if (accepted) {
        ownSyncPending = true;
      }

      if (answer !== null && typeof answer.status === 'object' && answer.status !== null) {
        applyStatus(answer.status);
      }

      if (accepted) {
        if (ownSyncPending) {
          say('');
          pollOwnSync();
        }
      } else if (response.status === 409) {
        say(COPY.noteRunning);
      } else if (response.status === 429) {
        if (answer === null || answer.status === undefined) {
          adoptRetryAfter(response);
        }

        say(COPY.youCanSyncAgain(currentState().remainingMs));
      } else {
        say(COPY.noteOffline);
      }
    } catch {
      ownSyncPending = false;
      say(COPY.noteOffline);
    } finally {
      window.clearTimeout(timeout);
      pressing = false;
    }
  }

  if (timeButton !== null) {
    timeButton.addEventListener('click', toggleExact);
  }

  if (button !== null) {
    button.addEventListener('click', onPress);
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

    renderButton();
  });

  refresh();
  renderButton();

  return { applyStatus, refresh };
}
