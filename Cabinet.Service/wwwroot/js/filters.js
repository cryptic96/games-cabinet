/**
 * The filter bar above the cabinet: the search field and the count of games that stand in the light. Typing puts every box that is not
 * part of a matching family into shadow where it stands, in the same task as the keystroke; nothing moves and nothing is removed. The
 * rules live in the pure filter-rules module; this module only reads the field, toggles an attribute on the boxes and writes text.
 */
import { EMPTY_FILTERS, buildModel, countLit, evaluate, fold, listEntries } from './filter-rules.js';

/** How long the count must stay unchanged before it is announced to a screen reader, in milliseconds. */
export const FILTER_ANNOUNCE_MS = 600;

/**
 * The controller of a page that has no filter bar: it dims nothing and ignores every call, so the cabinet script can use one
 * controller whether or not the bar exists and a page without the bar still draws every box in the light.
 */
export const NO_FILTERS = Object.freeze({
  dimOf: () => false,
  rebuild: () => {},
  apply: () => {},
  state: EMPTY_FILTERS,
});

/**
 * Starts the filter bar.
 * @param {object} parts What the bar needs from the page.
 * @param {HTMLElement} parts.bar The filter bar element.
 * @param {HTMLElement} parts.mount The element that holds the drawn cabinet.
 * @param {object} parts.copy The visitor-facing strings of the page language.
 * @param {string} parts.language The page language, for the order of the games list the count follows.
 * @param {() => object[]} parts.getRecords Gives the records of the collection: the card data when it is held, otherwise records made
 * from the drawn boxes.
 * @returns {{ dimOf: (placement: object) => boolean, rebuild: (records: object[]) => void, apply: () => void, state: object }} The
 * controls: whether a box is drawn in shadow, a new model of the collection, a fresh pass over the drawn boxes and the current state.
 */
export function initFilters({ bar, mount, copy, language, getRecords }) {
  const field = bar.querySelector('#filter-q');
  const count = bar.querySelector('.filter-count');
  const clear = bar.querySelector('.filter-clear');

  let state = { ...EMPTY_FILTERS };
  let model = buildModel(getRecords());
  let entries = listEntries(model, { language });
  let result = evaluate(state, model);

  /**
   * Tells whether an entry is drawn in shadow. An entry the model does not hold is judged from its own title.
   * @param {string} entry The entry id as text.
   * @param {string} title The title of the box, for an entry the model does not hold.
   * @returns {boolean}
   */
  function isDim(entry, title) {
    if (!result.active) {
      return false;
    }

    if (model.byEntry.has(entry)) {
      return !result.lit.has(entry);
    }

    return !fold(title).includes(fold(state.q));
  }

  /**
   * Writes the count and the state of the bar for the current result.
   * @param {{ lit: number, total: number }} tally How many entries are lit out of how many there are.
   */
  function showTally(tally) {
    const none = result.active && tally.lit === 0;

    count.textContent = !result.active ? '' : none ? copy.noMatch : copy.filterCount(tally.lit, tally.total);
    bar.toggleAttribute('data-active', result.active);
    clear.toggleAttribute('data-highlighted', none);
  }

  /**
   * Decides again which games are lit, puts the boxes in shadow or in the light, writes the count and tells the page.
   */
  function apply() {
    result = evaluate(state, model);

    for (const box of mount.querySelectorAll('.placement')) {
      box.toggleAttribute('data-dim', isDim(box.dataset.entryId, box.getAttribute('aria-label') ?? ''));
    }

    const tally = countLit(entries, result.lit);

    showTally(tally);
    document.dispatchEvent(new CustomEvent('cabinet:filtered', { detail: { active: result.active, lit: result.lit, count: tally, state } }));
  }

  field.addEventListener('input', () => {
    state = { ...state, q: field.value };
    apply();
  });

  return {
    dimOf: (placement) => isDim(String(placement.entryId), placement.title),

    rebuild(records) {
      model = buildModel(records);
      entries = listEntries(model, { language });
      apply();
    },

    apply,

    get state() {
      return state;
    },
  };
}
