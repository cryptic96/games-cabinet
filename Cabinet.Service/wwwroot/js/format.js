/**
 * The text logic of the detail card, per page language: players, play time, weight, rating and minimum age. Every function is pure
 * and takes the numbers as the card data gives them, where zero, a negative number or anything that is not a number counts as
 * missing. The module has no imports and touches no page, so it loads the same way in the page and in a plain Node test.
 */

/** The en dash that joins the two ends of a range. */
const RANGE_DASH = '–';

/** The lowest weight, in the one-decimal value the page shows, of each band after the first. */
const WEIGHT_BAND_STARTS = Object.freeze([1.5, 2.5, 3.5, 4.5]);

/**
 * Tells whether a value is a usable amount: a finite number above zero.
 * @param {unknown} value The value from the card data.
 * @returns {boolean}
 */
function isKnown(value) {
  return typeof value === 'number' && Number.isFinite(value) && value > 0;
}

/**
 * Joins the two ends of a range, or names the one number when both ends are the same.
 * @param {number} low The lower end.
 * @param {number} high The upper end.
 * @returns {string}
 */
function rangeText(low, high) {
  return low === high ? String(low) : String(low) + RANGE_DASH + String(high);
}

/**
 * Writes a number with exactly one fraction digit in the separator style of a language, without ever rounding a value that
 * already has one decimal.
 * @param {number} value The number, already rounded to one decimal by the server.
 * @param {string} locale The locale of the page language.
 * @returns {string}
 */
export function oneDecimal(value, locale) {
  return new Intl.NumberFormat(locale, { minimumFractionDigits: 1, maximumFractionDigits: 1 }).format(value);
}

/**
 * Writes the number of players: a range when both ends are known and differ, otherwise the one number known.
 * @param {number | null | undefined} minPlayers The fewest players.
 * @param {number | null | undefined} maxPlayers The most players.
 * @returns {string | null} The text, or null when neither end is known.
 */
export function playersText(minPlayers, maxPlayers) {
  if (isKnown(minPlayers) && isKnown(maxPlayers)) {
    return rangeText(Math.min(minPlayers, maxPlayers), Math.max(minPlayers, maxPlayers));
  }

  if (isKnown(maxPlayers)) {
    return String(maxPlayers);
  }

  return isKnown(minPlayers) ? String(minPlayers) : null;
}

/**
 * Writes the playing time in minutes without the unit: a range when both ends are known and differ, otherwise the stated time,
 * otherwise whichever end is known.
 * @param {number | null | undefined} playTime The stated playing time.
 * @param {number | null | undefined} minPlayTime The shortest playing time.
 * @param {number | null | undefined} maxPlayTime The longest playing time.
 * @returns {string | null} The text, or null when no time is known.
 */
export function playTimeText(playTime, minPlayTime, maxPlayTime) {
  if (isKnown(minPlayTime) && isKnown(maxPlayTime) && minPlayTime !== maxPlayTime) {
    return rangeText(Math.min(minPlayTime, maxPlayTime), Math.max(minPlayTime, maxPlayTime));
  }

  for (const value of [playTime, minPlayTime, maxPlayTime]) {
    if (isKnown(value)) {
      return String(value);
    }
  }

  return null;
}

/**
 * Decides the weight band from the one-decimal value the card shows, so the word always agrees with the number beside it.
 * @param {number | null | undefined} weight The weight from 1 to 5.
 * @returns {number | null} The band from 0 (light) to 4 (heavy), or null when the weight is missing.
 */
export function weightBand(weight) {
  if (!isKnown(weight)) {
    return null;
  }

  const shown = Number(oneDecimal(weight, 'en'));

  return WEIGHT_BAND_STARTS.filter((start) => shown >= start).length;
}

/**
 * Writes the rating as a score out of ten in the page language.
 * @param {number | null | undefined} rating The rating, already rounded to one decimal.
 * @param {{ numberLocale: string }} copy The visitor-facing strings.
 * @returns {string | null} The text, or null when the rating is missing.
 */
export function ratingText(rating, copy) {
  return isKnown(rating) ? oneDecimal(rating, copy.numberLocale) + ' / 10' : null;
}

/**
 * Writes the minimum age as a number with a plus sign.
 * @param {number | null | undefined} age The minimum age in years.
 * @returns {string | null} The text, or null when the age is missing.
 */
export function ageText(age) {
  return isKnown(age) ? String(age) + '+' : null;
}

/**
 * Builds the entries of the game-night strip in their fixed order: players, play time, weight and minimum age. An entry whose
 * value is missing is left out.
 * @param {object} record The card record.
 * @param {object} copy The visitor-facing strings of the page language.
 * @returns {{ icon: string, value: string, label: string }[]}
 */
export function factsFor(record, copy) {
  const facts = [];
  const players = playersText(record.minPlayers, record.maxPlayers);

  if (players !== null) {
    const largest = isKnown(record.maxPlayers) ? record.maxPlayers : record.minPlayers;
    facts.push({ icon: 'i-players', value: players, label: copy.playersLabel(largest) });
  }

  const minutes = playTimeText(record.playTime, record.minPlayTime, record.maxPlayTime);

  if (minutes !== null) {
    facts.push({ icon: 'i-time', value: minutes + ' ' + copy.minutesShort, label: copy.playTimeLabel });
  }

  const band = weightBand(record.weight);

  if (band !== null) {
    facts.push({
      icon: 'i-weight',
      value: copy.weightWords[band],
      label: copy.weightLabel(oneDecimal(record.weight, copy.numberLocale)),
    });
  }

  const age = ageText(record.minAge);

  if (age !== null) {
    facts.push({ icon: 'i-age', value: age, label: copy.ageLabel });
  }

  return facts;
}

/**
 * Tells whether the record holds at least one of the seven details the card shows besides the location: players, play time,
 * weight, minimum age, rating, designers and mechanics.
 * @param {object} record The card record.
 * @returns {boolean}
 */
export function hasAnyDetail(record) {
  const known = [
    isKnown(record.minPlayers) || isKnown(record.maxPlayers),
    isKnown(record.playTime) || isKnown(record.minPlayTime) || isKnown(record.maxPlayTime),
    isKnown(record.weight),
    isKnown(record.minAge),
    isKnown(record.rating),
    Array.isArray(record.designers) && record.designers.length > 0,
    Array.isArray(record.mechanics) && record.mechanics.length > 0,
  ];

  return known.some(Boolean);
}
