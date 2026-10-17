/**
 * What the page remembers of the visitor's choices. The filters are kept for the tab only, in its session storage, so a reload, the
 * language switch and a live redraw keep the question while a new tab or a later visit starts clean; the chosen view is kept for the
 * device in its local storage. Nothing goes in a cookie or in the page address and nothing is sent to the server. The module takes
 * the storage and the timers as parameters and imports nothing, so it runs the same in the browser and under the plain Node test
 * runner. Every storage call is guarded, so storage that is missing, full or blocked leaves the state in memory and breaks nothing.
 */

/** The key under which the tab's filters are kept in its session storage. */
export const FILTERS_KEY = 'cabinet-filters';

/** The key under which the chosen view is kept in the device's local storage. */
export const VIEW_KEY = 'cabinet-view';

/** The longest search text that is kept, in characters. */
const MAX_QUERY_LENGTH = 100;

/** The player counts the chips offer. */
const PLAYER_VALUES = Object.freeze([1, 2, 3, 4, 5, 6, 7, 8]);

/** The play times the chips offer, in minutes. */
const TIME_VALUES = Object.freeze([30, 45, 60, 90, 120]);

/** The text of the location choice for games with no place. */
const UNKNOWN_PLACE = 'unknown';

/** What an owner's location id looks like: eight lowercase hex digits. */
const PLACE_ID = /^[0-9a-f]{8}$/;

/** The views the page can show. */
const VIEWS = Object.freeze(['all', 'location']);

/** How long a burst of changes waits before the one write, in milliseconds. */
const WRITE_DELAY_MS = 250;

/**
 * Gives the state with nothing chosen.
 * @returns {{ q: string, players: null, time: null, location: null }}
 */
function emptyState() {
  return { q: '', players: null, time: null, location: null };
}

/**
 * Reads the stored filters, field by field. A field that is missing or not one of the allowed values is reset on its own; the
 * others are kept. Storage that throws, holds nothing or holds something that is not an object gives the empty state.
 * @param {{ getItem: (key: string) => string | null } | null | undefined} storage The tab's session storage.
 * @returns {{ q: string, players: number | null, time: number | null, location: string | null }}
 */
export function readFilters(storage) {
  let parsed;

  try {
    const raw = storage?.getItem(FILTERS_KEY);

    parsed = typeof raw === 'string' ? JSON.parse(raw) : null;
  } catch {
    return emptyState();
  }

  if (parsed === null || typeof parsed !== 'object' || Array.isArray(parsed)) {
    return emptyState();
  }

  return {
    q: typeof parsed.q === 'string' ? [...parsed.q].slice(0, MAX_QUERY_LENGTH).join('') : '',
    players: PLAYER_VALUES.includes(parsed.players) ? parsed.players : null,
    time: TIME_VALUES.includes(parsed.time) ? parsed.time : null,
    location: typeof parsed.location === 'string' && (parsed.location === UNKNOWN_PLACE || PLACE_ID.test(parsed.location)) ? parsed.location : null,
  };
}

/**
 * Tells whether a state has nothing chosen.
 * @param {{ q: string, players: number | null, time: number | null, location: string | null }} state The state.
 * @returns {boolean}
 */
function isEmpty(state) {
  return state.q === '' && state.players === null && state.time === null && state.location === null;
}

/**
 * Makes the writer of the tab's filters. A burst of changes is written once, a moment after the first of them, so typing never
 * writes per keystroke; a state with nothing chosen removes the entry instead of keeping an empty one.
 * @param {object} parts What the writer needs.
 * @param {{ setItem: Function, removeItem: Function } | null | undefined} parts.storage The tab's session storage.
 * @param {(callback: Function, delay: number) => unknown} parts.setTimer Starts a timer.
 * @param {(timer: unknown) => void} parts.clearTimer Stops a timer.
 * @param {number} [parts.delayMs] How long a burst waits before it is written.
 * @returns {{ write: (state: object) => void, flush: () => void }} The controls: take the latest state to write, and write it now.
 */
export function createFilterWriter({ storage, setTimer, clearTimer, delayMs = WRITE_DELAY_MS }) {
  let pending = null;
  let timer = null;

  /**
   * Writes the latest state held, if any, and forgets it.
   */
  function flush() {
    if (timer !== null) {
      clearTimer(timer);
      timer = null;
    }

    if (pending === null) {
      return;
    }

    const state = pending;

    pending = null;

    try {
      if (isEmpty(state)) {
        storage.removeItem(FILTERS_KEY);
      } else {
        storage.setItem(FILTERS_KEY, JSON.stringify({ q: state.q, players: state.players, time: state.time, location: state.location }));
      }
    } catch {
      pending = null;
    }
  }

  return {
    write(state) {
      pending = state;

      if (timer === null) {
        timer = setTimer(flush, delayMs);
      }
    },

    flush,
  };
}

/**
 * Reads the view the visitor chose last on this device.
 * @param {{ getItem: (key: string) => string | null } | null | undefined} storage The device's local storage.
 * @returns {'all' | 'location'} The stored view, or all when there is none or it is not a known view.
 */
export function readView(storage) {
  try {
    const raw = storage?.getItem(VIEW_KEY);

    return VIEWS.includes(raw) ? raw : 'all';
  } catch {
    return 'all';
  }
}

/**
 * Remembers the view the visitor chose. A view that is not a known one is not written, and storage that throws is ignored.
 * @param {{ setItem: (key: string, value: string) => void } | null | undefined} storage The device's local storage.
 * @param {string} view The view to remember.
 */
export function writeView(storage, view) {
  if (!VIEWS.includes(view)) {
    return;
  }

  try {
    storage.setItem(VIEW_KEY, view);
  } catch {
    return;
  }
}
