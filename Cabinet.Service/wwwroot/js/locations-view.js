/**
 * The view switch at the start of the filter bar: all games together in one cabinet, or one cabinet per storage location. This module
 * owns the choice of view and the by-location document. The document is fetched the first time the visitor asks for the location view
 * and kept while it still belongs to the layout on screen, and the view on screen stays as it is until the document has arrived. The
 * page draws; this module only tells it which view to draw and hands it the document.
 */

/** The address of the by-location document. */
const ROUTE = '/cabinet/layout-by-location';

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
 * Starts the view switch.
 * @param {object} parts What the switch needs from the page.
 * @param {HTMLElement} parts.toggle The fieldset that holds the two radios named filter-view.
 * @param {object} parts.copy The visitor-facing strings of the page language.
 * @param {() => string} parts.getProfile Gives the profile name the layout endpoints expect for the viewport.
 * @param {() => string | undefined} parts.getSample Gives the invented collection the page shows, when it shows one.
 * @param {() => string | null} parts.getLayoutEtag Gives the entity tag of the combined layout on screen.
 * @param {(view: 'all' | 'location', doc: object | null) => void} parts.onSwitch Draws the view the visitor chose; the document is
 * given for the location view.
 * @returns {{ view: 'all' | 'location', fetchDocument: (layoutEtag: string | null) => Promise<object | null>, setView: (view: 'all' | 'location') => void, fallBack: () => void }}
 * The controls: the view on screen, a way to fetch the document for a layout, a way to show a view that was set from elsewhere and a
 * way to give up on the location view with the polite message.
 */
export function initLocationsView({ toggle, copy, getProfile, getSample, getLayoutEtag, onSwitch }) {
  const radios = [...toggle.querySelectorAll('input[name="filter-view"]')];
  const live = document.getElementById('filter-live');

  let view = 'all';
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

  /**
   * Fetches the by-location document for the profile on screen, or returns the one already held for the same profile and layout.
   * @param {string | null} layoutEtag The entity tag of the combined layout the document must belong to.
   * @returns {Promise<object | null>} The document, or null when the request failed, the answer was not usable or it belongs to another
   * version of the collection than the layout on screen.
   */
  async function fetchDocument(layoutEtag) {
    const profile = getProfile();

    if (held !== null && held.profile === profile && held.sample === getSample() && held.doc.layout === layoutEtag) {
      return held.doc;
    }

    const sample = getSample();

    try {
      const response = await fetch(`${ROUTE}?profile=${profile}${sample ? '&sample=' + encodeURIComponent(sample) : ''}`);

      if (!response.ok) {
        return null;
      }

      const doc = await response.json();

      if (!isLocationDocument(doc) || (layoutEtag !== null && doc.layout !== layoutEtag)) {
        return null;
      }

      held = { profile, sample, doc };

      return doc;
    } catch {
      return null;
    }
  }

  /**
   * Gives up on the location view: the all-together radio is checked again and the message says why.
   */
  function fallBack() {
    view = 'all';
    check('all');
    say(copy.locationViewFailed);
  }

  /**
   * Takes the visitor's choice. The location view waits for its document and the current view stays until it arrives; a later choice
   * made while one is waiting wins.
   * @param {'all' | 'location'} next The view chosen.
   * @returns {Promise<void>}
   */
  async function choose(next) {
    request += 1;
    const mine = request;

    if (next === view) {
      return;
    }

    if (next === 'all') {
      view = 'all';
      onSwitch('all', null);
      say(copy.viewAllAnnounce);

      return;
    }

    const doc = await fetchDocument(getLayoutEtag());

    if (mine !== request) {
      return;
    }

    if (doc === null) {
      fallBack();

      return;
    }

    view = 'location';
    onSwitch('location', doc);
    say(copy.viewLocationAnnounce);
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
      choose('all');
    }
  });

  return {
    get view() {
      return view;
    },

    fetchDocument,

    setView(next) {
      view = next;
      check(next);
    },

    fallBack,
  };
}
