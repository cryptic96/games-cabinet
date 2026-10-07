/**
 * The language and region the exact sync time is written in: English words with the Dutch date order and 24-hour clock.
 * Swapping this one constant is how a translated page changes the format.
 */
export const TIME_LOCALE = 'en-NL';

const SECONDS_PER_MINUTE = 60;
const SECONDS_PER_HOUR = 3600;
const SECONDS_PER_DAY = 86400;

const relativeFormat = new Intl.RelativeTimeFormat('en', { numeric: 'auto' });

const exactFormat = new Intl.DateTimeFormat(TIME_LOCALE, {
  day: 'numeric',
  month: 'long',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  timeZoneName: 'short',
});

const MILLISECONDS_PER_MINUTE = 60000;

/**
 * The length of a wait in words: less than a minute under 60 seconds, otherwise whole minutes rounded up.
 * @param {number} remainingMs Milliseconds left in the window.
 * @returns {string}
 */
function waitPhrase(remainingMs) {
  if (remainingMs < MILLISECONDS_PER_MINUTE) {
    return 'less than a minute';
  }

  const minutes = Math.ceil(remainingMs / MILLISECONDS_PER_MINUTE);

  return minutes === 1 ? '1 minute' : `${minutes} minutes`;
}

/**
 * Every visitor-facing string the cabinet scripts show lives here, so wording changes and translation touch one file.
 */
export const COPY = Object.freeze({
  loading: 'Loading the cabinet...',
  errorHeading: 'The cabinet could not be loaded.',
  errorBody: 'Check your connection and try again.',
  retry: 'Try again',
  untitled: 'Untitled game',

  /**
   * How long ago the collection was synced: just now under a minute or for a time in the future, then whole minutes, whole
   * hours up to a day, then whole days, where one day reads as yesterday.
   * @param {number} elapsedSeconds Seconds since the last good sync; negative when the time is in the future.
   * @returns {string}
   */
  syncedAgo(elapsedSeconds) {
    if (elapsedSeconds < SECONDS_PER_MINUTE) {
      return 'Synced just now';
    }

    if (elapsedSeconds < SECONDS_PER_HOUR) {
      return 'Synced ' + relativeFormat.format(-Math.floor(elapsedSeconds / SECONDS_PER_MINUTE), 'minute');
    }

    if (elapsedSeconds < SECONDS_PER_DAY) {
      return 'Synced ' + relativeFormat.format(-Math.floor(elapsedSeconds / SECONDS_PER_HOUR), 'hour');
    }

    return 'Synced ' + relativeFormat.format(-Math.floor(elapsedSeconds / SECONDS_PER_DAY), 'day');
  },

  /**
   * The exact moment in the visitor's own time zone, for example 6 October 2026 at 14:32 CEST.
   * @param {Date} date The moment to write.
   * @returns {string}
   */
  exactTime(date) {
    return exactFormat.format(date);
  },

  /**
   * The exact-time line under the status.
   * @param {string} exact The exact time from exactTime.
   * @returns {string}
   */
  lastSynced(exact) {
    return `Last synced ${exact}`;
  },

  /**
   * The note shown when recent syncs have not gone through.
   * @param {string} exact The exact time from exactTime.
   * @returns {string}
   */
  staleRecent(exact) {
    return `Showing the last sync from ${exact}. Recent syncs haven't gone through.`;
  },

  /**
   * The note shown while a suspicious result waits for the next sync to confirm.
   * @param {string} exact The exact time from exactTime.
   * @returns {string}
   */
  staleHeldBack(exact) {
    return `Showing the last sync from ${exact}. A much smaller collection from BGG is waiting for the next sync to confirm.`;
  },

  syncNow: 'Sync now',
  syncing: 'Syncing...',
  noteChanged: 'Collection updated. BGG can take a few minutes to show recent edits.',
  noteUnchanged: 'No changes found. BGG can take a few minutes to show recent edits.',
  noteFailed: "BGG didn't respond. The last collection is still showing.",
  noteHeldBack: 'BGG returned far fewer games than before, so the last collection is still showing.',
  noteRunning: 'A sync is already running.',
  noteOffline: "Couldn't start the sync. Check your connection and try again.",

  /**
   * The button text during the shared window.
   * @param {string} text The countdown from countdownText.
   * @returns {string}
   */
  syncAgainIn(text) {
    return `Sync again in ${text}`;
  },

  /**
   * The button's accessible name during the shared window: whole minutes rounded up, and less than a minute in the last one.
   * @param {number} remainingMs Milliseconds left in the window.
   * @returns {string}
   */
  syncAgainName(remainingMs) {
    return `Sync again in ${waitPhrase(remainingMs)}`;
  },

  /**
   * The sentence given when the button is pressed during the shared window.
   * @param {number} remainingMs Milliseconds left in the window.
   * @returns {string}
   */
  youCanSyncAgain(remainingMs) {
    return `You can sync again in ${waitPhrase(remainingMs)}.`;
  },

  /** Sub-label of an expansion whose base game is not known. */
  expansionLabel: 'Expansion',

  /**
   * Sub-label of an expansion whose base game is not owned.
   * @param {string} base The base game title.
   * @returns {string}
   */
  expansionFor(base) {
    return `Expansion for ${base}`;
  },

  /**
   * Accessible name of an expansion whose base game is not known.
   * @param {string} title The expansion title.
   * @returns {string}
   */
  expansionName(title) {
    return `${title}, expansion`;
  },

  /**
   * Accessible name of one expansion layer in a stack.
   * @param {string} title The expansion title.
   * @param {string} base The base game title.
   * @returns {string}
   */
  layerName(title, base) {
    return `${title}, expansion for ${base}`;
  },

  /**
   * Text on the marker that counts hidden expansions.
   * @param {number} n How many expansions are hidden.
   * @returns {string}
   */
  moreLabel(n) {
    return `+${n} more`;
  },

  /**
   * Accessible name of the marker that counts hidden expansions.
   * @param {number} n How many expansions are hidden.
   * @param {string} base The base game title.
   * @returns {string}
   */
  moreName(n, base) {
    return n === 1 ? `1 more expansion for ${base}` : `${n} more expansions for ${base}`;
  },
});
