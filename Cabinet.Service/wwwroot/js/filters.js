/**
 * The filter bar above the cabinet: the search field, the chip groups and the count of games that stand in the light. Typing or
 * choosing puts every box that is not part of a matching family into shadow where it stands, in the same task as the keystroke or tap;
 * nothing moves and nothing is removed. The rules live in the pure filter-rules module; this module only reads the controls, toggles
 * an attribute on the boxes and writes text.
 */
import { EMPTY_FILTERS, PLAYER_CHOICES, TIME_CHOICES, buildModel, countLit, evaluate, fold, listEntries } from './filter-rules.js';

/** How long the count must stay unchanged before it is announced to a screen reader, in milliseconds. */
export const FILTER_ANNOUNCE_MS = 600;

/**
 * How many veils one pass may change before the fade is switched off for that pass. A pass that changes more boxes than this puts
 * them in shadow or in the light at once, so a cheap phone never runs hundreds of fades together.
 */
export const BULK_VEIL_COUNT = 120;

/**
 * The chip groups of the bar: which state member each group sets, and the values it may take. A chip value outside the set, or an
 * empty one, means Any.
 */
const CHIP_GROUPS = Object.freeze([
  Object.freeze({ name: 'players', choices: PLAYER_CHOICES }),
  Object.freeze({ name: 'time', choices: TIME_CHOICES }),
]);

/**
 * Reads the value a chip stands for, against the fixed set of its group.
 * @param {string} raw The value attribute of the chip's radio.
 * @param {readonly number[]} choices The values the group offers.
 * @returns {number | null} The value, or null for Any and for anything that is not one of the choices.
 */
function chipValue(raw, choices) {
  const value = Number(raw);

  return raw !== '' && Number.isInteger(value) && choices.includes(value) ? value : null;
}

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
  const live = document.getElementById('filter-live');
  const groups = CHIP_GROUPS.map((group) => ({ ...group, element: bar.querySelector(`.filter-group[data-filter="${group.name}"]`) })).filter((group) => group.element !== null);

  let state = { ...EMPTY_FILTERS };
  let model = buildModel(getRecords());
  let entries = listEntries(model, { language });
  let result = evaluate(state, model);
  let tally = countLit(entries, result.lit);
  let announceTimer = null;
  let bulkFrame = 0;

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

    if (state.players !== null || state.time !== null) {
      return true;
    }

    return !fold(title).includes(fold(state.q));
  }

  /**
   * Writes the count and the state of the bar for the current result.
   */
  function showTally() {
    const none = result.active && tally.lit === 0;

    count.textContent = !result.active ? '' : none ? copy.noMatch : copy.filterCount(tally.lit, tally.total);
    bar.toggleAttribute('data-active', result.active);
    clear.toggleAttribute('data-highlighted', none);
  }

  /**
   * Ends a pass that switched the fade off. The attribute stays through one drawn frame, so the browser has worked out the new
   * shadows with no fade before the attribute goes, and a later pass cancels the pending end so two passes never cut each other short.
   */
  function endBulkPass() {
    window.cancelAnimationFrame(bulkFrame);
    bulkFrame = window.requestAnimationFrame(() => {
      bulkFrame = window.requestAnimationFrame(() => {
        mount.removeAttribute('data-bulk');
      });
    });
  }

  /**
   * Decides again which games are lit, puts the boxes in shadow or in the light, writes the count and tells the page. A pass that
   * changes more than BULK_VEIL_COUNT veils marks the mount first, so those veils change at once instead of fading.
   */
  function apply() {
    result = evaluate(state, model);

    const changes = [];

    for (const box of mount.querySelectorAll('.placement')) {
      const dim = isDim(box.dataset.entryId, box.getAttribute('aria-label') ?? '');

      if (dim !== box.hasAttribute('data-dim')) {
        changes.push([box, dim]);
      }
    }

    if (changes.length > BULK_VEIL_COUNT) {
      mount.setAttribute('data-bulk', '');
      endBulkPass();
    }

    for (const [box, dim] of changes) {
      box.toggleAttribute('data-dim', dim);
    }

    tally = countLit(entries, result.lit);
    showTally();
    document.dispatchEvent(new CustomEvent('cabinet:filtered', { detail: { active: result.active, lit: result.lit, count: tally, state } }));
  }

  /**
   * Gives the text a screen reader hears for the current result: the full count, the none-found sentence, or the cleared sentence.
   * @returns {string}
   */
  function announcement() {
    if (!result.active) {
      return copy.filtersCleared(tally.total);
    }

    return tally.lit === 0 ? copy.noMatch : copy.filterCount(tally.lit, tally.total);
  }

  /**
   * Writes the announcement once the count has stayed the same for a moment, so a burst of keystrokes is heard as one message.
   */
  function announceSoon() {
    window.clearTimeout(announceTimer);
    announceTimer = window.setTimeout(() => {
      announceTimer = null;
      live.textContent = announcement();
    }, FILTER_ANNOUNCE_MS);
  }

  /**
   * Takes the search the visitor typed, shows its result at once and queues the announcement.
   */
  function search() {
    state = { ...state, q: field.value };
    apply();
    announceSoon();
  }

  /**
   * Takes the choice of one chip group, shows its result at once and queues the announcement. A choice that is already held changes
   * nothing.
   * @param {{ name: string, choices: readonly number[], element: HTMLElement }} group The group the visitor used.
   */
  function choose(group) {
    const checked = group.element.querySelector('input:checked');
    const value = checked === null ? null : chipValue(checked.value, group.choices);

    if (state[group.name] === value) {
      return;
    }

    state = { ...state, [group.name]: value };
    apply();
    announceSoon();
  }

  /**
   * Returns every chip group to Any without telling anyone; the caller applies the state.
   */
  function resetChips() {
    for (const group of groups) {
      const any = group.element.querySelector('input[value=""]');

      if (any !== null) {
        any.checked = true;
      }
    }
  }

  /**
   * Empties the search, puts every box back in the light and announces it at once. Focus ends in the search field.
   */
  function clearAll() {
    window.clearTimeout(announceTimer);
    announceTimer = null;
    field.value = '';
    resetChips();
    state = { ...EMPTY_FILTERS };
    apply();
    live.textContent = announcement();
    field.focus();
  }

  /**
   * Tells whether a key press target is somewhere the visitor types, so a slash typed there stays a slash.
   * @param {EventTarget | null} target The element the key was pressed on.
   * @returns {boolean}
   */
  function isTypingPlace(target) {
    return target instanceof HTMLElement && (target.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(target.tagName));
  }

  field.addEventListener('input', search);

  for (const group of groups) {
    group.element.addEventListener('change', () => choose(group));

    group.element.addEventListener('click', (event) => {
      const radio = event.target;

      if (!(radio instanceof HTMLInputElement) || radio.type !== 'radio') {
        return;
      }

      const value = chipValue(radio.value, group.choices);

      if (value !== null && state[group.name] === value) {
        resetChips();
        choose(group);
      }
    });
  }

  field.addEventListener('keydown', (event) => {
    if (event.key === 'Escape' && field.value !== '') {
      event.preventDefault();
      clearAll();
    } else if (event.key === 'Enter' && window.matchMedia('(pointer: coarse)').matches) {
      field.blur();
    }
  });

  clear.addEventListener('click', clearAll);

  document.addEventListener('keydown', (event) => {
    const plain = !event.ctrlKey && !event.altKey && !event.metaKey;

    if (event.key !== '/' || !plain || event.defaultPrevented || bar.hidden || isTypingPlace(event.target) || document.querySelector('dialog[open]') !== null) {
      return;
    }

    event.preventDefault();
    field.focus();
    field.select();
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
