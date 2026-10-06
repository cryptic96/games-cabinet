/**
 * Every visitor-facing string the cabinet scripts show lives here, so wording changes and translation touch one file.
 */
export const COPY = Object.freeze({
  loading: 'Loading the cabinet...',
  errorHeading: 'The cabinet could not be loaded.',
  errorBody: 'Check your connection and try again.',
  retry: 'Try again',
  untitled: 'Untitled game',

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
