/**
 * The filter bar above the cabinet: the search field, the chip groups and the count of games that stand in the light. Typing or
 * choosing puts every box that is not part of a matching family into shadow where it stands, in the same task as the keystroke or tap;
 * nothing moves and nothing is removed. The rules live in the pure filter-rules module; this module only reads the controls, toggles
 * an attribute on the boxes and writes text.
 */
import {
  EMPTY_FILTERS,
  PLAYER_CHOICES,
  TIME_CHOICES,
  UNKNOWN_LOCATION,
  buildModel,
  countLit,
  effectiveLocation,
  evaluate,
  fold,
  listEntries,
  locationChoices,
  withAlternates,
} from './filter-rules.js';
import { createFilterWriter } from './filter-state.js';

/** How long the count must stay unchanged before it is announced to a screen reader, in milliseconds. */
export const FILTER_ANNOUNCE_MS = 600;

/**
 * How many veils one pass may change before the fade is switched off for that pass. A pass that changes more boxes than this puts
 * them in shadow or in the light at once, so a cheap phone never runs hundreds of fades together.
 */
export const BULK_VEIL_COUNT = 120;

/** The address of the other titles of the owned games, which the search reads once on first use. */
const NAMES_ROUTE = '/cabinet/search-names';

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
  showBar: () => {},
  disableCardFilters: () => {},
  enableCardFilters: () => {},
  state: EMPTY_FILTERS,
});

/**
 * Keeps the page in step with the pinned bar without a scroll handler. One observer on the zero-height marker that sits right before
 * the bar sets the stuck attribute while the marker is above the top of the viewport, which is when the bar is pinned, and a second
 * observer writes the bar's height on the root while the bar is pinned, and zero while it is not, so the page can keep focused and
 * scrolled-to boxes clear of it.
 * @param {HTMLElement} bar The filter bar element.
 */
function watchPinning(bar) {
  const sentinel = bar.previousElementSibling;

  if (sentinel instanceof HTMLElement && sentinel.classList.contains('filter-sentinel') && 'IntersectionObserver' in window) {
    new IntersectionObserver(
      (observed) => {
        const latest = observed[observed.length - 1];

        bar.toggleAttribute('data-stuck', !latest.isIntersecting && latest.boundingClientRect.top < 0);
      },
      { root: null, threshold: 0 },
    ).observe(sentinel);
  }

  if ('ResizeObserver' in window) {
    const publish = () => {
      const pinned = window.getComputedStyle(bar).position === 'sticky';

      document.documentElement.style.setProperty('--filter-bar-h', `${pinned ? bar.offsetHeight : 0}px`);
    };

    const observer = new ResizeObserver(publish);

    observer.observe(bar);
    observer.observe(document.documentElement);
  }
}

/**
 * Starts the filter bar.
 * @param {object} parts What the bar needs from the page.
 * @param {HTMLElement} parts.bar The filter bar element.
 * @param {HTMLElement} parts.mount The element that holds the drawn cabinet.
 * @param {object} parts.copy The visitor-facing strings of the page language.
 * @param {string} parts.language The page language, for the order of the games list the count follows.
 * @param {() => object[]} parts.getRecords Gives the records of the collection: the card data when it is held, otherwise records made
 * from the drawn boxes.
 * @param {{ q: string, players: number | null, time: number | null, location: string | null }} [parts.initial] The filters the tab
 * kept; the controls start from them while the bar is still hidden.
 * @param {{ setItem: Function, removeItem: Function } | null} [parts.storage] Where the filters are kept for the tab; null keeps them
 * in memory only.
 * @returns {{ dimOf: (placement: object) => boolean, rebuild: (records: object[], locations?: object[], held?: { layoutEtag?: string | null, quiet?: boolean }) => void, apply: () => void, showBar: (visible: boolean) => void, disableCardFilters: () => void, enableCardFilters: () => void, state: object }}
 * The controls: whether a box is drawn in shadow, a new model of the collection (with the owner's locations when the card data brings
 * them, and the entity tag of the layout the records belong to, so the other titles are read again when the collection changed; a
 * quiet one only replaces the model and leaves the boxes on screen alone, for a draw that follows at once), a
 * fresh pass over the drawn boxes, showing or hiding the bar, switching off and on the controls that need the card data, and the
 * current state.
 */
export function initFilters({ bar, mount, copy, language, getRecords, initial = EMPTY_FILTERS, storage = null }) {
  const field = bar.querySelector('#filter-q');
  const count = bar.querySelector('.filter-count');
  const clear = bar.querySelector('.filter-clear');
  const live = document.getElementById('filter-live');
  const place = bar.querySelector('#filter-loc');
  const placeLabel = bar.querySelector('label[for="filter-loc"]');
  const groups = CHIP_GROUPS.map((group) => ({ ...group, element: bar.querySelector(`.filter-group[data-filter="${group.name}"]`) })).filter((group) => group.element !== null);

  const note = document.querySelector('.filter-note');
  const writer = createFilterWriter({
    storage,
    setTimer: (callback, delay) => window.setTimeout(callback, delay),
    clearTimer: (timer) => window.clearTimeout(timer),
  });

  let state = { ...initial };
  let cardFiltersOff = false;
  let locations = [];
  let names = null;
  let namesState = 'unasked';
  let namesGeneration = 0;
  let heldLayout = null;
  let model = buildModel(getRecords(), locations);
  let entries = listEntries(model, { language });
  let result = evaluate(state, model);
  let tally = countLit(entries, result.lit);
  let announceTimer = null;
  let bulkFrame = 0;

  watchPinning(bar);

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

    if (state.players !== null || state.time !== null || state.location !== null) {
      return true;
    }

    return !fold(title).includes(fold(state.q));
  }

  /**
   * Fills the location select from the model: the first option is Any, then the owner's locations that hold a game in the owner's
   * order, then the unknown choice when some game has no location. Names go in as text. The select is shown only while the owner has
   * locations. The list is only rewritten when it differs, so an open list is not closed by a refresh that changes nothing.
   */
  function fillPlaces() {
    if (place === null) {
      return;
    }

    const { places, unknown } = locationChoices(model);
    const wanted = [['', copy.anyLocation], ...places.map((entry) => [entry.id, entry.name]), ...(unknown ? [[UNKNOWN_LOCATION, copy.unknownLocation]] : [])];
    const present = [...place.options].map((option) => [option.value, option.textContent]);

    if (JSON.stringify(wanted) !== JSON.stringify(present)) {
      place.replaceChildren(
        ...wanted.map(([value, text]) => {
          const option = document.createElement('option');

          option.value = value;
          option.textContent = text;
          option.dir = 'auto';

          return option;
        }),
      );
    }

    const shown = model.locations.length > 0;

    place.hidden = !shown;

    if (placeLabel !== null) {
      placeLabel.hidden = !shown;
    }

    place.value = effectiveLocation(state.location, model) ?? '';
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
    const location = effectiveLocation(state.location, model);

    if (location !== state.location) {
      state = { ...state, location };
      writer.write(state);
    }

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
   * Builds the model of the collection from the records given, with the other titles of its games when they are held.
   * @param {object[]} records The records of the collection.
   * @returns {ReturnType<typeof buildModel>}
   */
  function modelOf(records) {
    return withAlternates(buildModel(records, locations), names);
  }

  /**
   * Reads the other titles of the owned games, once, the first time the visitor uses the search field. The request is made at low
   * priority, shows nothing while it runs and nothing when it fails: title search keeps working and is not asked again until the
   * collection changes. When the titles arrive the families are matched again and the pass is applied; a count that changed is
   * announced like any other change. An answer that belongs to a collection the page has since left is dropped.
   */
  async function fetchNames() {
    if (namesState !== 'unasked') {
      return;
    }

    namesState = 'asking';
    const generation = namesGeneration;
    const sample = mount.dataset.sample;

    try {
      const response = await fetch(NAMES_ROUTE + (sample ? '?sample=' + encodeURIComponent(sample) : ''), { priority: 'low' });
      const body = response.ok ? await response.json() : null;

      if (generation !== namesGeneration) {
        return;
      }

      if (body === null || typeof body.names !== 'object' || body.names === null) {
        namesState = 'failed';
        return;
      }

      const before = tally.lit;

      names = body.names;
      namesState = 'held';
      model = modelOf(getRecords());
      apply();

      if (tally.lit !== before) {
        announceSoon();
      }
    } catch {
      if (generation === namesGeneration) {
        namesState = 'failed';
      }
    }
  }

  /**
   * Takes the search the visitor typed, shows its result at once and queues the announcement.
   */
  function search() {
    fetchNames();
    state = { ...state, q: field.value };
    apply();
    writer.write(state);
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
    writer.write(state);
    announceSoon();
  }

  /**
   * Takes the choice in the location select, shows its result at once and queues the announcement.
   */
  function choosePlace() {
    const value = place.value === '' ? null : place.value;

    if (state.location === value) {
      return;
    }

    state = { ...state, location: value };
    apply();
    writer.write(state);
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

    if (place !== null) {
      place.value = '';
    }

    state = { ...EMPTY_FILTERS };
    apply();
    writer.write(state);
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

  /**
   * Shows the state the tab kept in the controls: the text in the field and the chip each group stands on. The location select is
   * filled from the card data, so it takes its value when that arrives.
   */
  function showRestoredState() {
    field.value = state.q;

    for (const group of groups) {
      const chosen = state[group.name];
      const chip = group.element.querySelector(`input[value="${chosen === null ? '' : chosen}"]`);

      if (chip !== null) {
        chip.checked = true;
      }
    }
  }

  /**
   * Shows or hides the bar, and the note about the card details with it: the note only shows while the bar does.
   * @param {boolean} visible Whether the bar is shown.
   */
  function showBar(visible) {
    bar.toggleAttribute('hidden', !visible);

    if (note !== null) {
      note.hidden = !visible || !cardFiltersOff;
    }
  }

  /**
   * Switches off the players, time and location controls because the card data they need did not load: they are put to Any, disabled,
   * and the note says why. Search keeps working on the titles alone.
   */
  function disableCardFilters() {
    cardFiltersOff = true;
    state = { ...state, players: null, time: null, location: null };
    resetChips();

    for (const input of bar.querySelectorAll('.filter-group input')) {
      input.disabled = true;
    }

    if (place !== null) {
      place.value = '';
      place.disabled = true;
    }

    if (note !== null) {
      note.textContent = copy.detailsFailed;
      note.hidden = bar.hidden;
    }

    apply();
    writer.write(state);
  }

  /**
   * Switches the controls that need the card data back on once the data is held.
   */
  function enableCardFilters() {
    if (!cardFiltersOff) {
      return;
    }

    cardFiltersOff = false;

    for (const input of bar.querySelectorAll('.filter-group input')) {
      input.disabled = false;
    }

    if (place !== null) {
      place.disabled = false;
    }

    if (note !== null) {
      note.hidden = true;
    }
  }

  field.addEventListener('input', search);
  field.addEventListener('focus', fetchNames);
  place?.addEventListener('change', choosePlace);
  showRestoredState();
  fillPlaces();
  window.addEventListener('pagehide', writer.flush);

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

    rebuild(records, owned, held) {
      const layoutEtag = typeof held?.layoutEtag === 'string' ? held.layoutEtag : null;

      const collectionChanged = layoutEtag !== null && heldLayout !== null && layoutEtag !== heldLayout;

      if (collectionChanged) {
        namesState = 'unasked';
        namesGeneration += 1;
      }

      heldLayout = layoutEtag ?? heldLayout;
      locations = Array.isArray(owned) ? owned : locations;
      model = modelOf(records);
      entries = listEntries(model, { language });

      if (held?.quiet !== true) {
        fillPlaces();
        apply();
      }

      if (collectionChanged && field.value !== '') {
        fetchNames();
      }
    },

    apply,
    showBar,
    disableCardFilters,
    enableCardFilters,

    get state() {
      return state;
    },
  };
}
