/**
 * The language and region the exact sync time is written in on the English page: English words with the Dutch date order and
 * 24-hour clock. The Dutch page uses nl-NL.
 */
const TIME_LOCALE = 'en-NL';

const SECONDS_PER_MINUTE = 60;
const SECONDS_PER_HOUR = 3600;
const SECONDS_PER_DAY = 86400;
const MILLISECONDS_PER_MINUTE = 60000;

const englishRelative = new Intl.RelativeTimeFormat('en', { numeric: 'auto' });
const dutchRelative = new Intl.RelativeTimeFormat('nl', { numeric: 'always' });

/**
 * The length of a time in whole units rounded down, as the number and the unit name Intl.RelativeTimeFormat takes.
 * @param {number} elapsedSeconds Seconds since the last good sync, at least one minute.
 * @returns {{ count: number, unit: string }}
 */
function wholeUnits(elapsedSeconds) {
  if (elapsedSeconds < SECONDS_PER_HOUR) {
    return { count: Math.floor(elapsedSeconds / SECONDS_PER_MINUTE), unit: 'minute' };
  }

  if (elapsedSeconds < SECONDS_PER_DAY) {
    return { count: Math.floor(elapsedSeconds / SECONDS_PER_HOUR), unit: 'hour' };
  }

  return { count: Math.floor(elapsedSeconds / SECONDS_PER_DAY), unit: 'day' };
}

/**
 * Upper-cases the first letter of a sentence.
 * @param {string} text The sentence.
 * @returns {string}
 */
function capitalise(text) {
  return text.charAt(0).toUpperCase() + text.slice(1);
}

/**
 * The length of a wait in English: less than a minute under 60 seconds, otherwise whole minutes rounded up.
 * @param {number} remainingMs Milliseconds left in the window.
 * @returns {string}
 */
function englishWaitPhrase(remainingMs) {
  if (remainingMs < MILLISECONDS_PER_MINUTE) {
    return 'less than a minute';
  }

  const minutes = Math.ceil(remainingMs / MILLISECONDS_PER_MINUTE);

  return minutes === 1 ? '1 minute' : `${minutes} minutes`;
}

/**
 * The length of a wait in Dutch: less than a minute under 60 seconds, otherwise whole minutes rounded up.
 * @param {number} remainingMs Milliseconds left in the window.
 * @returns {string}
 */
function dutchWaitPhrase(remainingMs) {
  if (remainingMs < MILLISECONDS_PER_MINUTE) {
    return 'minder dan een minuut';
  }

  const minutes = Math.ceil(remainingMs / MILLISECONDS_PER_MINUTE);

  return minutes === 1 ? '1 minuut' : `${minutes} minuten`;
}

const EXACT_OPTIONS = {
  day: 'numeric',
  month: 'long',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  timeZoneName: 'short',
};

const ENGLISH_EXACT_FORMAT = new Intl.DateTimeFormat(TIME_LOCALE, EXACT_OPTIONS);
const DUTCH_EXACT_FORMAT = new Intl.DateTimeFormat('nl-NL', EXACT_OPTIONS);

const ENGLISH = Object.freeze({
  loading: 'Loading the cabinet...',
  errorHeading: 'The cabinet could not be loaded.',
  errorBody: 'Check your connection and try again.',
  retry: 'Try again',
  untitled: 'Untitled game',
  close: 'Close',
  bggLink: 'View on BoardGameGeek',
  newTabHint: '(opens in a new tab)',
  noDetails: 'More details arrive after the next sync.',

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

    const { count, unit } = wholeUnits(elapsedSeconds);

    return 'Synced ' + englishRelative.format(-count, unit);
  },

  /**
   * The exact moment in the visitor's own time zone, for example 6 October 2026 at 14:32 CEST.
   * @param {Date} date The moment to write.
   * @returns {string}
   */
  exactTime(date) {
    return ENGLISH_EXACT_FORMAT.format(date);
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
    return `Sync again in ${englishWaitPhrase(remainingMs)}`;
  },

  /**
   * The sentence given when the button is pressed during the shared window.
   * @param {number} remainingMs Milliseconds left in the window.
   * @returns {string}
   */
  youCanSyncAgain(remainingMs) {
    return `You can sync again in ${englishWaitPhrase(remainingMs)}.`;
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
   * Accessible name of the marker that counts hidden expansions. It starts with the text the marker shows, so a
   * visitor who speaks what they see finds it.
   * @param {number} n How many expansions are hidden.
   * @param {string} base The base game title.
   * @returns {string}
   */
  moreName(n, base) {
    return n === 1 ? `+${n} more expansion for ${base}` : `+${n} more expansions for ${base}`;
  },
});

const DUTCH = Object.freeze({
  loading: 'De kast wordt geladen...',
  errorHeading: 'De kast kon niet worden geladen.',
  errorBody: 'Controleer je verbinding en probeer het opnieuw.',
  retry: 'Opnieuw proberen',
  untitled: 'Spel zonder titel',
  close: 'Sluiten',
  bggLink: 'Bekijk op BoardGameGeek',
  newTabHint: '(opent in een nieuw tabblad)',
  noDetails: 'Meer details volgen na de volgende synchronisatie.',

  /**
   * How long ago the collection was synced, with the time first and the verb last: just now under a minute or for a time in the
   * future, then whole minutes, whole hours up to a day, then whole days, where one day reads as yesterday.
   * @param {number} elapsedSeconds Seconds since the last good sync; negative when the time is in the future.
   * @returns {string}
   */
  syncedAgo(elapsedSeconds) {
    if (elapsedSeconds < SECONDS_PER_MINUTE) {
      return 'Zojuist gesynchroniseerd';
    }

    const { count, unit } = wholeUnits(elapsedSeconds);

    if (unit === 'day' && count === 1) {
      return 'Gisteren gesynchroniseerd';
    }

    return capitalise(dutchRelative.format(-count, unit)) + ' gesynchroniseerd';
  },

  /**
   * The exact moment in the visitor's own time zone, for example 6 oktober 2026 om 14:32 CEST.
   * @param {Date} date The moment to write.
   * @returns {string}
   */
  exactTime(date) {
    return DUTCH_EXACT_FORMAT.format(date);
  },

  /**
   * The exact-time line under the status.
   * @param {string} exact The exact time from exactTime.
   * @returns {string}
   */
  lastSynced(exact) {
    return `Laatst gesynchroniseerd op ${exact}`;
  },

  /**
   * The note shown when recent syncs have not gone through.
   * @param {string} exact The exact time from exactTime.
   * @returns {string}
   */
  staleRecent(exact) {
    return `Je ziet de laatste synchronisatie van ${exact}. Recente synchronisaties zijn niet gelukt.`;
  },

  /**
   * The note shown while a suspicious result waits for the next sync to confirm.
   * @param {string} exact The exact time from exactTime.
   * @returns {string}
   */
  staleHeldBack(exact) {
    return `Je ziet de laatste synchronisatie van ${exact}. Een veel kleinere collectie van BGG wacht op bevestiging bij de volgende synchronisatie.`;
  },

  syncNow: 'Nu synchroniseren',
  syncing: 'Bezig met synchroniseren...',
  noteChanged: 'Collectie bijgewerkt. Het kan een paar minuten duren voordat BGG recente wijzigingen laat zien.',
  noteUnchanged: 'Geen wijzigingen gevonden. Het kan een paar minuten duren voordat BGG recente wijzigingen laat zien.',
  noteFailed: 'BGG reageerde niet. De laatste collectie blijft zichtbaar.',
  noteHeldBack: 'BGG gaf veel minder spellen terug dan eerder, dus de laatste collectie blijft zichtbaar.',
  noteRunning: 'Er loopt al een synchronisatie.',
  noteOffline: 'De synchronisatie kon niet starten. Controleer je verbinding en probeer het opnieuw.',

  /**
   * The button text during the shared window.
   * @param {string} text The countdown from countdownText.
   * @returns {string}
   */
  syncAgainIn(text) {
    return `Opnieuw synchroniseren over ${text}`;
  },

  /**
   * The button's accessible name during the shared window: whole minutes rounded up, and less than a minute in the last one.
   * @param {number} remainingMs Milliseconds left in the window.
   * @returns {string}
   */
  syncAgainName(remainingMs) {
    return `Opnieuw synchroniseren over ${dutchWaitPhrase(remainingMs)}`;
  },

  /**
   * The sentence given when the button is pressed during the shared window.
   * @param {number} remainingMs Milliseconds left in the window.
   * @returns {string}
   */
  youCanSyncAgain(remainingMs) {
    return `Je kunt over ${dutchWaitPhrase(remainingMs)} opnieuw synchroniseren.`;
  },

  /** Sub-label of an expansion whose base game is not known. */
  expansionLabel: 'Uitbreiding',

  /**
   * Sub-label of an expansion whose base game is not owned.
   * @param {string} base The base game title.
   * @returns {string}
   */
  expansionFor(base) {
    return `Uitbreiding op ${base}`;
  },

  /**
   * Accessible name of an expansion whose base game is not known.
   * @param {string} title The expansion title.
   * @returns {string}
   */
  expansionName(title) {
    return `${title}, uitbreiding`;
  },

  /**
   * Accessible name of one expansion layer in a stack.
   * @param {string} title The expansion title.
   * @param {string} base The base game title.
   * @returns {string}
   */
  layerName(title, base) {
    return `${title}, uitbreiding op ${base}`;
  },

  /**
   * Text on the marker that counts hidden expansions.
   * @param {number} n How many expansions are hidden.
   * @returns {string}
   */
  moreLabel(n) {
    return `+${n} meer`;
  },

  /**
   * Accessible name of the marker that counts hidden expansions. It starts with the text the marker shows, so a
   * visitor who speaks what they see finds it.
   * @param {number} n How many expansions are hidden.
   * @param {string} base The base game title.
   * @returns {string}
   */
  moreName(n, base) {
    return n === 1 ? `+${n} meer uitbreiding op ${base}` : `+${n} meer uitbreidingen op ${base}`;
  },
});

/**
 * Every visitor-facing string the cabinet scripts show, once per page language. Both tables have the same keys, which a test checks.
 */
export const COPY_BY_LANGUAGE = Object.freeze({ en: ENGLISH, nl: DUTCH });

/**
 * The string table for a language code; any code that is not Dutch or English gets English.
 * @param {string} language The language code.
 * @returns {object}
 */
export function copyFor(language) {
  return Object.hasOwn(COPY_BY_LANGUAGE, language) ? COPY_BY_LANGUAGE[language] : COPY_BY_LANGUAGE.en;
}

/**
 * The language the server wrote the page in, read from the document's language. Dutch when it starts with nl, otherwise English,
 * and English when there is no document.
 * @returns {string}
 */
export function pageLanguage() {
  const language = globalThis.document?.documentElement?.lang ?? '';

  return language.toLowerCase().startsWith('nl') ? 'nl' : 'en';
}

/**
 * The strings in the language of this page.
 */
export const COPY = copyFor(pageLanguage());
