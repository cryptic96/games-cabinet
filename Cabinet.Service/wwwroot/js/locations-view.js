/**
 * The view switch at the start of the filter bar: all games together in one cabinet, or one cabinet per storage location. This module
 * owns the choice of view and the by-location document. The document is fetched the first time the visitor asks for the location view
 * and kept while it still belongs to the layout on screen, and the view on screen stays as it is until the document has arrived. The
 * page draws; this module only tells it which view to draw and hands it the document.
 *
 * The view the visitor chose is remembered on the device, and only once that view has been drawn: never before its document is in hand
 * and never when the page fell back to another view. A switch is a short cross-fade of the cabinet area alone, started only when the
 * document is in hand, instant when the visitor prefers less motion or the browser has no view transitions, and an open card is closed
 * before it starts. Afterwards the page scrolls up when the cabinet's top has gone above the bar, and never down.
 */
import { writeView } from './filter-state.js';

/** The address of the by-location document. */
const ROUTE = '/cabinet/layout-by-location';

/** The attribute on the root that names the cabinet area for the view transition of a switch. */
const SWITCH_MARKER = 'data-switching-view';

/** The query that tells the visitor wants less motion. */
const REDUCED_MOTION_QUERY = '(prefers-reduced-motion: reduce)';

/** The gap, in pixels, between the bottom of the bar and the top of the cabinet after the page scrolled up. */
const BAR_GAP_PX = 16;

/**
 * Tells where to scroll after a switch so the cabinet is not left above the bar. Both edges are measured in the viewport, after the
 * swap. The page scrolls only when the cabinet's top has gone above the bar's bottom, to the position that puts the cabinet's top a
 * little under the bar; that position is always above the current one, so the page never scrolls down.
 * @param {number} cabinetTop The cabinet's top edge, in pixels from the top of the viewport.
 * @param {number} barBottom The bar's bottom edge, in pixels from the top of the viewport.
 * @param {number} scrollY The current vertical scroll position of the page.
 * @returns {number | null} The scroll position to go to, or null when the page stays where it is.
 */
export function scrollAfterSwitch(cabinetTop, barBottom, scrollY) {
  return cabinetTop < barBottom ? scrollY + cabinetTop - barBottom - BAR_GAP_PX : null;
}

/**
 * Tells whether a value is text or missing.
 * @param {unknown} value The value to test.
 * @returns {boolean}
 */
function isTextOrMissing(value) {
  return value === undefined || typeof value === 'string';
}

/**
 * Tells whether a value is a usable by-location document: the entity tag of the combined layout it belongs to and a list of
 * locations, each with its sections and a name and id that are text when they are there.
 * @param {unknown} doc The parsed answer.
 * @returns {boolean}
 */
export function isLocationDocument(doc) {
  return doc !== null
    && typeof doc === 'object'
    && typeof doc.layout === 'string'
    && Array.isArray(doc.locations)
    && doc.locations.every(
      (location) => location !== null
        && typeof location === 'object'
        && isTextOrMissing(location.id)
        && isTextOrMissing(location.name)
        && Array.isArray(location.sections),
    );
}

/**
 * Tells whether the visitor asked for less motion.
 * @returns {boolean}
 */
function prefersReducedMotion() {
  return window.matchMedia?.(REDUCED_MOTION_QUERY)?.matches === true;
}

/**
 * Runs the swap inside a view transition of the cabinet area when the browser has one and the visitor allows motion, and at once
 * otherwise. The swap runs exactly once either way, and the root is marked while the transition lasts so the style sheet can name the
 * cabinet area. Settles when the swap is done.
 * @param {() => void} swap Puts the new view on screen.
 * @returns {Promise<void>}
 */
async function swapWithFade(swap) {
  const root = document.documentElement;
  let applied = false;
  const apply = () => {
    if (!applied) {
      applied = true;
      swap();
    }
  };

  if (prefersReducedMotion() || typeof document.startViewTransition !== 'function') {
    apply();

    return;
  }

  root.setAttribute(SWITCH_MARKER, '');

  let transition;

  try {
    transition = document.startViewTransition(apply);
  } catch {
    apply();
    root.removeAttribute(SWITCH_MARKER);

    return;
  }

  transition.ready.catch(() => {});
  transition.finished.catch(() => {}).then(() => {
    apply();
    root.removeAttribute(SWITCH_MARKER);
  });

  await transition.updateCallbackDone.catch(() => {});
  apply();
}

/**
 * Starts the view switch.
 * @param {object} parts What the switch needs from the page.
 * @param {HTMLElement} parts.toggle The fieldset that holds the two radios named filter-view.
 * @param {object} parts.copy The visitor-facing strings of the page language.
 * @param {() => string} parts.getProfile Gives the profile name the layout endpoints expect for the viewport.
 * @param {() => string | undefined} parts.getSample Gives the invented collection the page shows, when it shows one.
 * @param {() => string | null} parts.getLayoutEtag Gives the entity tag of the combined layout on screen.
 * @param {(view: 'all' | 'location', doc: object | null) => void} parts.onSwitch Draws the view the visitor chose; the document is
 * given for the location view.
 * @param {'all' | 'location'} [parts.initialView] The view remembered on the device, which the page draws first.
 * @param {{ setItem: (key: string, value: string) => void } | null} [parts.storage] The device's local storage, where the chosen view
 * is remembered.
 * @param {() => Promise<void>} [parts.closeCard] Closes an open card and settles once it is closed.
 * @param {Element | null} [parts.cabinet] The element the cabinet is drawn in.
 * @param {Element | null} [parts.bar] The filter bar, whose bottom edge the cabinet must not slip above.
 * @returns {{ view: 'all' | 'location', requestDocument: () => Promise<object | null>, fetchDocument: (layoutEtag: string | null, started?: Promise<object | null> | null) => Promise<object | null>, setView: (view: 'all' | 'location') => void, fallBack: () => void }}
 * The controls: the view on screen, a way to start asking for the document, a way to get the document for a layout, a way to show a
 * view that was set from elsewhere and a way to give up on the location view with the polite message.
 */
export function initLocationsView({
  toggle,
  copy,
  getProfile,
  getSample,
  getLayoutEtag,
  onSwitch,
  initialView = 'all',
  storage = null,
  closeCard = () => Promise.resolve(),
  cabinet = null,
  bar = null,
}) {
  const radios = [...toggle.querySelectorAll('input[name="filter-view"]')];
  const live = document.getElementById('filter-live');

  let view = initialView;
  let held = null;
  let request = 0;

  /**
   * Writes a message for screen readers.
   * @param {string} text The message.
   */
  function say(text) {
    if (live !== null) {
      live.textContent = text;
    }
  }

  /**
   * Checks the radio of a view.
   * @param {string} value The view.
   */
  function check(value) {
    for (const radio of radios) {
      radio.checked = radio.value === value;
    }
  }

  check(view);

  /**
   * Asks for the by-location document of the profile on screen. The answer is checked for its shape only; whether it belongs to the
   * layout on screen is decided by whoever uses it. A request that fails in any way gives null instead of an error.
   * @returns {Promise<object | null>} The document, or null when the request failed or the answer was not usable.
   */
  async function requestDocument() {
    const sample = getSample();

    try {
      const response = await fetch(`${ROUTE}?profile=${getProfile()}${sample ? '&sample=' + encodeURIComponent(sample) : ''}`);

      if (!response.ok) {
        return null;
      }

      const doc = await response.json();

      return isLocationDocument(doc) ? doc : null;
    } catch {
      return null;
    }
  }

  /**
   * Gets the by-location document for the profile on screen: the one already held for the same profile and layout, or the answer of a
   * request already under way, or a new request.
   * @param {string | null} layoutEtag The entity tag of the combined layout the document must belong to.
   * @param {Promise<object | null> | null} [started] A request for the document that is already under way.
   * @returns {Promise<object | null>} The document, or null when the request failed, the answer was not usable or it belongs to another
   * version of the collection than the layout on screen.
   */
  async function fetchDocument(layoutEtag, started = null) {
    const profile = getProfile();
    const sample = getSample();

    if (held !== null && held.profile === profile && held.sample === sample && held.doc.layout === layoutEtag) {
      return held.doc;
    }

    const doc = await (started ?? requestDocument());

    if (doc === null || (layoutEtag !== null && doc.layout !== layoutEtag)) {
      return null;
    }

    held = { profile, sample, doc };

    return doc;
  }

  /**
   * Gives up on the location view: the all-together radio is checked again and the message says why. Nothing is remembered.
   */
  function fallBack() {
    view = 'all';
    check('all');
    say(copy.locationViewFailed);
  }

  /**
   * Scrolls the page up when the swap left the cabinet's top above the bar, and never down.
   */
  function keepCabinetInReach() {
    if (cabinet === null || bar === null) {
      return;
    }

    const target = scrollAfterSwitch(cabinet.getBoundingClientRect().top, bar.getBoundingClientRect().bottom, window.scrollY);

    if (target !== null) {
      window.scrollTo({ top: target, behavior: 'instant' });
    }
  }

  /**
   * Takes the visitor's choice. An open card is closed first. The location view waits for its document and the current view stays
   * until it arrives; a later choice made while one is waiting wins. Once the new view is drawn it is announced and, when the choice
   * is the visitor's own, remembered on the device.
   * @param {'all' | 'location'} next The view chosen.
   * @param {boolean} [remember] Whether to remember the view on the device after it is drawn.
   * @returns {Promise<void>}
   */
  async function choose(next, remember = true) {
    request += 1;
    const mine = request;

    if (next === view) {
      return;
    }

    await closeCard();

    if (mine !== request) {
      return;
    }

    let doc = null;

    if (next === 'location') {
      doc = await fetchDocument(getLayoutEtag());

      if (mine !== request) {
        return;
      }

      if (doc === null) {
        fallBack();

        return;
      }
    }

    await swapWithFade(() => {
      if (mine !== request) {
        return;
      }

      view = next;
      onSwitch(next, doc);
      keepCabinetInReach();
    });

    if (mine !== request || view !== next) {
      return;
    }

    say(next === 'location' ? copy.viewLocationAnnounce : copy.viewAllAnnounce);

    if (remember) {
      writeView(storage, next);
    }
  }

  for (const radio of radios) {
    radio.addEventListener('change', () => {
      if (radio.checked) {
        choose(radio.value === 'location' ? 'location' : 'all');
      }
    });
  }

  document.addEventListener('cabinet:filtered', (event) => {
    const has = event.detail?.hasLocations === true;

    toggle.hidden = !has;

    if (!has && view === 'location') {
      check('all');
      choose('all', false);
    }
  });

  return {
    get view() {
      return view;
    },

    requestDocument,

    fetchDocument,

    setView(next) {
      view = next;
      check(next);
    },

    fallBack,
  };
}
