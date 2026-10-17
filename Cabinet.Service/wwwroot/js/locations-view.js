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

/** How long, in milliseconds, the box that was asked for stays outlined after Show on shelf took the visitor to it. */
export const FOUND_HOLD_MS = 2000;

/** The most times the page measures and scrolls again while it settles on the box Show on shelf is taking the visitor to. */
const LANDING_PASSES = 3;

/** The attribute that outlines the box Show on shelf landed on. */
const FOUND_MARKER = 'data-found';

/**
 * Tells where to scroll so the box of Show on shelf is in view below the bar. When the location's heading and the box both fit below
 * the bar with a gap above and below, the heading is put a little under the bar so the visitor sees which cabinet it is. Otherwise
 * the box is centred in the space below the bar, or put at its top when even that space is too small to hold it. All edges are
 * measured in the viewport; the result is a scroll position, never below zero.
 * @param {object} edges What was measured.
 * @param {number} edges.headingTop The top edge of the location's heading.
 * @param {number} edges.boxTop The top edge of the box.
 * @param {number} edges.boxBottom The bottom edge of the box.
 * @param {number} edges.barBottom The bottom edge of the bar while it is pinned, otherwise zero.
 * @param {number} edges.viewportHeight The height of the viewport.
 * @param {number} edges.scrollY The current vertical scroll position of the page.
 * @returns {number} The scroll position to go to.
 */
export function shelfScrollTarget({ headingTop, boxTop, boxBottom, barBottom, viewportHeight, scrollY }) {
  const room = viewportHeight - barBottom;
  const boxHeight = boxBottom - boxTop;
  let target;

  if (boxBottom - headingTop <= room - 2 * BAR_GAP_PX) {
    target = scrollY + headingTop - barBottom - BAR_GAP_PX;
  } else if (boxHeight <= room - 2 * BAR_GAP_PX) {
    target = scrollY + (boxTop + boxBottom) / 2 - (barBottom + room / 2);
  } else {
    target = scrollY + boxTop - barBottom - BAR_GAP_PX;
  }

  return Math.max(0, target);
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
   * Tells where the bottom edge of the bar is when it is pinned to the top of the page, and zero when it scrolls away with the page.
   * @returns {number}
   */
  function pinnedBarBottom() {
    if (bar === null || window.getComputedStyle(bar).position !== 'sticky') {
      return 0;
    }

    return Math.max(0, bar.getBoundingClientRect().bottom);
  }

  /**
   * Finds the box of an entry in the cabinet on screen, never the marker that counts hidden expansions.
   * @param {string} entryId The entry id as text.
   * @returns {Element | null}
   */
  function findBox(entryId) {
    return cabinet?.querySelector(`.placement[data-entry-id="${CSS.escape(entryId)}"]:not([data-kind="moreMarker"])`) ?? null;
  }

  let found = null;
  let foundTimer = 0;

  /**
   * Takes the outline off the box Show on shelf landed on, if there is one, and stops listening for the press that would do it.
   */
  function clearFound() {
    window.clearTimeout(foundTimer);
    document.removeEventListener('pointerdown', clearFound, true);
    document.removeEventListener('keydown', clearFoundOnKey, true);

    if (found !== null) {
      found.removeAttribute(FOUND_MARKER);
      found = null;
    }
  }

  /**
   * Takes the outline off when a key is pressed, but not when the press is a key held down from the one that asked for Show on shelf.
   * @param {KeyboardEvent} event The key press.
   */
  function clearFoundOnKey(event) {
    if (!event.repeat) {
      clearFound();
    }
  }

  /**
   * Scrolls the page at once to where the box of Show on shelf belongs. A scroll can change what is above the box, as shelves that
   * come into view are laid out for real, so the page measures again after each scroll and scrolls once more until the box is where
   * it should be, a few times at most.
   * @param {Element} box The box to land on.
   * @param {Element} heading The heading of the box's location, or the box itself when it has none.
   */
  function settleOn(box, heading) {
    for (let pass = 0; pass < LANDING_PASSES; pass += 1) {
      const boxRect = box.getBoundingClientRect();
      const target = shelfScrollTarget({
        headingTop: heading.getBoundingClientRect().top,
        boxTop: boxRect.top,
        boxBottom: boxRect.bottom,
        barBottom: pinnedBarBottom(),
        viewportHeight: window.innerHeight,
        scrollY: window.scrollY,
      });

      if (Math.abs(target - window.scrollY) < 1) {
        return;
      }

      window.scrollTo({ top: target, behavior: 'instant' });
    }
  }

  /**
   * Scrolls the page at once to the box of an entry and outlines it. The outline stays for a short while and goes at the next press
   * of the pointer or a key, never by moving the pointer. Without the box the page only makes sure the cabinet is not above the bar.
   * @param {string} entryId The entry id as text.
   */
  function landOn(entryId) {
    const box = findBox(entryId);

    if (box === null) {
      keepCabinetInReach();

      return;
    }

    const heading = box.closest('.location-group')?.querySelector('.location-heading') ?? box;

    settleOn(box, heading);
    clearFound();
    found = box;
    box.setAttribute(FOUND_MARKER, '');
    foundTimer = window.setTimeout(clearFound, FOUND_HOLD_MS);
    document.addEventListener('pointerdown', clearFound, true);
    document.addEventListener('keydown', clearFoundOnKey, true);
    window.requestAnimationFrame(() => {
      if (found === box) {
        settleOn(box, heading);
      }
    });
  }

  /**
   * Takes the visitor's choice. An open card is closed first. The location view waits for its document and the current view stays
   * until it arrives; a later choice made while one is waiting wins. Once the new view is drawn it is announced and, when the choice
   * is the visitor's own, remembered on the device.
   * @param {'all' | 'location'} next The view chosen.
   * @param {boolean} [remember] Whether to remember the view on the device after it is drawn.
   * @param {(() => void) | null} [land] What to do inside the swap in place of keeping the cabinet in reach, once the view is drawn.
   * @returns {Promise<void>}
   */
  async function choose(next, remember = true, land = null) {
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

      if (land === null) {
        keepCabinetInReach();
      } else {
        land();
      }
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

  /**
   * Takes the visitor to the box of an entry on its shelf: the open card is closed, the location view is shown when it is not on
   * screen, and the page scrolls to the box and outlines it. A location view that cannot be had says so and outlines nothing. No
   * filter is touched.
   * @param {string} entryId The entry id as text.
   * @returns {Promise<void>}
   */
  async function showOnShelf(entryId) {
    clearFound();

    if (view === 'location') {
      request += 1;
      const mine = request;

      await closeCard();

      if (mine === request && view === 'location') {
        landOn(entryId);
      }

      return;
    }

    check('location');
    await choose('location', true, () => landOn(entryId));
  }

  document.addEventListener('cabinet:show-on-shelf', (event) => {
    const entryId = event.detail?.entryId;

    if (typeof entryId === 'string' && entryId !== '') {
      showOnShelf(entryId);
    }
  });

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
