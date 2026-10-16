/**
 * Entry point of the cabinet page: fetches the layout for the viewport's profile (and the mount's sample when it carries one), hands it to the
 * renderer, and shows loading and error states. The profile follows one media query; nothing else listens to resize.
 */
import './language.js';
import { renderCabinet, renderLocations } from './render.js';
import { COPY, pageLanguage } from './copy.js';
import { initSyncStatus, fetchStatus } from './sync.js';
import { startLive } from './live.js';
import { initCardDialog } from './detail.js';
import { indexPlacements, recordFromPlacement } from './card-flow.js';
import { initRoving } from './keys.js';
import { initGamesList } from './games-list.js';
import { initFilters, NO_FILTERS } from './filters.js';
import { isActive } from './filter-rules.js';
import { readFilters, readView } from './filter-state.js';
import { initLocationsView } from './locations-view.js';
import { initLook } from './look.js';
import { applyLookStatus } from './look-switch.js';

const mount = document.getElementById('cabinet');
const syncRoot = document.querySelector('.sync');
const dialog = document.querySelector('.card-dialog');
const phoneQuery = window.matchMedia('(max-width: 40rem)');

const roving = mount === null ? null : initRoving(mount);
const listSection = document.querySelector('.games-list');
const skipLink = document.querySelector('.skip-link');
let cardControls = null;

const gamesList = listSection === null
  ? null
  : initGamesList({
    section: listSection,
    copy: COPY,
    language: pageLanguage(),
    openCard: (entryId, from) => cardControls?.openCard(entryId, from),
  });

let latestLoad = 0;
let cardRecords = new Map();
let drawnPlacements = new Map();
let drawnPalette = [];
let cardsFailed = false;
let cardLocations = [];
let drawnLayout = null;
let drawnEtag = null;

const filterBar = document.querySelector('.filter-bar');

/**
 * Gives the records the filters judge: every card record while the card data is held, plus a least record for each drawn box the
 * card data does not hold. A box carries the id of its family, which the least record keeps so the family can still be found.
 * @returns {object[]}
 */
function filterRecords() {
  const records = [...cardRecords.values()];

  for (const [key, placement] of drawnPlacements) {
    if (!cardRecords.has(key)) {
      records.push({ ...recordFromPlacement(placement), familyId: placement.familyId });
    }
  }

  return records;
}

/**
 * Gives the tab's session storage, where the filters are kept, or null when the browser does not let the page have it.
 * @returns {Storage | null}
 */
function tabStorage() {
  try {
    return window.sessionStorage;
  } catch {
    return null;
  }
}

/**
 * Gives the device's local storage, where the chosen view is remembered, or null when the browser does not let the page have it.
 * @returns {Storage | null}
 */
function deviceStorage() {
  try {
    return window.localStorage;
  } catch {
    return null;
  }
}

const tabStore = tabStorage();
const deviceStore = deviceStorage();

const filters = filterBar === null || mount === null
  ? NO_FILTERS
  : initFilters({ bar: filterBar, mount, copy: COPY, language: pageLanguage(), getRecords: filterRecords, initial: readFilters(tabStore), storage: tabStore });

const viewToggle = document.querySelector('.filter-view');

/**
 * Gives the view the visitor chose: the one big cabinet, or one cabinet per storage location.
 * @returns {'all' | 'location'}
 */
function currentView() {
  return locationsView === null ? 'all' : locationsView.view;
}

/** The waits before the second and third request for the card data after a failed one, in milliseconds. */
const CARD_RETRY_DELAYS_MS = [1500, 4000];
const CARD_ATTEMPTS = CARD_RETRY_DELAYS_MS.length + 1;

/**
 * Tells the rest of the page something about the cabinet through a document event, so a script that only some pages load can react
 * without this one knowing about it.
 * @param {string} name The event name.
 * @param {object} [detail] What the event carries.
 */
function announce(name, detail = {}) {
  document.dispatchEvent(new CustomEvent(name, { detail }));
}

/**
 * Gives the invented collection the mount shows, when it shows one.
 * @returns {string | undefined}
 */
function currentSample() {
  return mount === null ? undefined : mount.dataset.sample;
}

/**
 * Returns the profile name the layout endpoint expects for the current viewport.
 * @returns {string}
 */
function currentProfile() {
  return phoneQuery.matches ? 'phone' : 'desktop';
}

/**
 * Builds the layout address for the current profile and the mount's sample, if it carries one.
 * @returns {string}
 */
function layoutUrl() {
  const sample = mount.dataset.sample;

  return '/cabinet/layout?profile=' + currentProfile() + (sample ? '&sample=' + encodeURIComponent(sample) : '');
}

/**
 * Builds the card data address for the current profile and the mount's sample, if it carries one.
 * @returns {string}
 */
function cardsUrl() {
  const sample = mount.dataset.sample;

  return '/cabinet/cards?profile=' + currentProfile() + (sample ? '&sample=' + encodeURIComponent(sample) : '');
}

/**
 * Gives the card record of an entry: the record the card data holds, or the least the drawn box knows when the data did not
 * arrive or does not hold that entry. The least record says so when every request for the data failed.
 * @param {string} entryId The collection entry id as text.
 * @returns {object | null} The record, or null when the entry is not on screen either.
 */
function recordFor(entryId) {
  const record = cardRecords.get(entryId);

  if (record !== undefined) {
    return record;
  }

  const placement = drawnPlacements.get(entryId);

  return placement === undefined ? null : recordFromPlacement(placement, cardsFailed);
}

/**
 * Waits the given time without blocking anything.
 * @param {number} milliseconds How long to wait.
 * @returns {Promise<void>}
 */
function pause(milliseconds) {
  return new Promise((resolve) => window.setTimeout(resolve, milliseconds));
}

/**
 * Starts a request for the card data at normal priority, for a draw that waits for it. A network failure gives null instead of an
 * error, so a request nobody has asked about yet never ends as an unhandled rejection.
 * @returns {Promise<Response | null>}
 */
function startCardsRequest() {
  return fetch(cardsUrl()).catch(() => null);
}

/**
 * Asks for the card data once and keeps it when it belongs to the layout on screen.
 * @param {number} epoch The load counter when the layout was drawn.
 * @param {string | null} etag The entity tag of the layout on screen, when the response carried one.
 * @param {Promise<Response | null> | null} [started] A request that is already under way, used instead of a new one.
 * @param {boolean} [beforeDraw] Whether a draw follows, which applies the filters itself; the boxes on screen are then left alone
 * until that draw swaps them.
 * @returns {Promise<'loaded' | 'superseded' | 'stale' | 'failed'>} Loaded when the data is kept, superseded when a newer load took
 * over, stale when the data belongs to a different layout, failed when the request or its answer was not usable.
 */
async function requestCards(epoch, etag, started = null, beforeDraw = false) {
  try {
    const response = started === null ? await fetch(cardsUrl(), { priority: 'low' }) : await started;

    if (epoch !== latestLoad) {
      return 'superseded';
    }

    if (response === null || !response.ok) {
      return 'failed';
    }

    const body = await response.json();

    if (epoch !== latestLoad) {
      return 'superseded';
    }

    if (etag !== null && body.layout !== etag) {
      return 'stale';
    }

    cardRecords = new Map(body.cards.map((record) => [String(record.entryId), record]));
    cardLocations = body.locations ?? [];
    filters.enableCardFilters();
    gamesList?.showCards(body.cards);
    filters.rebuild(filterRecords(), body.locations ?? [], { layoutEtag: etag, quiet: beforeDraw });
    announce('cabinet:cards', { records: body.cards });

    return 'loaded';
  } catch {
    return epoch === latestLoad ? 'failed' : 'superseded';
  }
}

/**
 * Fetches the card data of the layout just drawn and keeps it in memory so a tap finds it ready. It tries again when the
 * data belongs to a different layout than the one on screen, and again after a short wait when the request failed. It never
 * shows anything: a card without its data still opens with what the drawn box knows, and says that the details could not be
 * loaded once every attempt has failed, which is also when the filters that need the details step aside. A newer load or redraw
 * supersedes it.
 * @param {number} epoch The load counter when the layout was drawn.
 * @param {string | null} etag The entity tag of the layout on screen, when the response carried one.
 * @param {Promise<Response | null> | null} [started] A request for the card data that is already under way; the first attempt uses it.
 * @returns {Promise<'loaded' | 'superseded' | 'failed'>} How it ended.
 */
async function fetchCards(epoch, etag, started = null) {
  const beforeDraw = started !== null;
  let failures = 0;

  for (let attempt = 0; attempt < CARD_ATTEMPTS; attempt += 1) {
    const outcome = await requestCards(epoch, etag, attempt === 0 ? started : null, beforeDraw);

    if (outcome === 'superseded') {
      return 'superseded';
    }

    if (outcome === 'loaded') {
      return 'loaded';
    }

    if (outcome === 'failed' && attempt < CARD_ATTEMPTS - 1) {
      await pause(CARD_RETRY_DELAYS_MS[failures]);
      failures += 1;

      if (epoch !== latestLoad) {
        return 'superseded';
      }
    }
  }

  cardsFailed = true;
  filters.disableCardFilters();

  return 'failed';
}

/**
 * Tells the filters what the layout about to be drawn holds, so the boxes are built already in the light or in shadow.
 * @param {object} layout The layout about to be drawn, or the sections of every location put together.
 * @param {string | null} etag The entity tag of the layout response, which tells the filters when the collection changed.
 * @param {object | null} doc The by-location document about to be drawn, or null when the combined layout is.
 */
function prepareFilters(layout, etag, doc) {
  drawnPlacements = indexPlacements(layout);
  filters.setView(doc === null ? 'all' : 'location', doc);
  filters.rebuild(filterRecords(), undefined, { layoutEtag: etag });
}

/**
 * Puts the sections of every location of a by-location document in one layout, which is what the boxes are indexed by.
 * @param {{ palette?: object[], locations: { sections: object[] }[] }} doc The by-location document.
 * @returns {{ sections: object[], palette: object[] }}
 */
function layoutOfLocations(doc) {
  return { sections: doc.locations.flatMap((location) => location.sections), palette: doc.palette ?? [] };
}

/**
 * Tells whether a draw has to wait for the card data: any filter set, the search included, needs the details to be judged, so the
 * first frame is already in shadow instead of showing every box in the light first. The location view waits too, because only the card
 * data says whether the owner still has any location to show a cabinet for.
 * @returns {boolean}
 */
function drawWaitsForCards() {
  return isActive(filters.state) || currentView() === 'location';
}

/**
 * Puts the cabinet on screen in place of what is there and shows the bar in the same task, with no await between, so nothing shifts
 * under the reader and no frame shows a box in the light that is about to go into shadow. The bar is shown when at least one box was
 * drawn. With a by-location document the cabinet is drawn as one group per location, otherwise as the one big cabinet.
 * @param {object} layout The combined layout, kept so the view can be switched back without asking for it again.
 * @param {string | null} etag The entity tag of the layout response.
 * @param {object | null} doc The by-location document to draw, or null to draw the combined layout.
 * @returns {object} The layout that was drawn, which is the combined one or the sections of every location put together.
 */
function paint(layout, etag, doc) {
  const drawn = doc === null ? layout : layoutOfLocations(doc);

  drawnLayout = layout;
  drawnEtag = etag;
  prepareFilters(drawn, etag, doc);

  if (doc === null) {
    renderCabinet(mount, layout, COPY, filters.dimOf);
  } else {
    renderLocations(mount, doc, COPY, filters.dimOf);
    filters.showHeadings();
  }

  filters.showBar(drawnPlacements.size > 0);
  roving.refresh();
  announce('cabinet:drawn');

  return drawn;
}

/**
 * Draws the cabinet and then does what follows a draw.
 * @param {object} layout The layout to draw.
 * @param {string | null} etag The entity tag of the layout response.
 * @param {'loaded' | 'failed' | null} settled How the wait for the card data ended, or null when the draw did not wait for it.
 * @param {object | null} [doc] The by-location document to draw instead of the combined layout.
 */
function drawCabinet(layout, etag, settled, doc = null) {
  afterDraw(paint(layout, etag, doc), etag, settled);
}

/**
 * Draws the view the visitor switched to from the layout already held, with the filters as they are. The card data is held already or
 * its request is under way, so nothing is asked for again.
 * @param {'all' | 'location'} view The view chosen.
 * @param {object | null} doc The by-location document for the location view.
 */
function switchView(view, doc) {
  if (drawnLayout !== null) {
    paint(drawnLayout, drawnEtag, view === 'location' ? doc : null);
  }
}

const locationsView = viewToggle === null || mount === null
  ? null
  : initLocationsView({
    toggle: viewToggle,
    copy: COPY,
    getProfile: currentProfile,
    getSample: currentSample,
    getLayoutEtag: () => drawnEtag,
    onSwitch: switchView,
    initialView: readView(deviceStore),
    storage: deviceStore,
    closeCard: () => (cardControls === null ? Promise.resolve() : cardControls.closeCard({ path: 'fade' })),
    cabinet: mount,
    bar: filterBar,
  });

/**
 * Puts the page back on the all-together view without a word and without touching what the device remembers, when the location view
 * is on but the card data lists no location: the owner has none, or deleted the last one. The switch is hidden by the filters.
 */
function leaveLocationViewWithoutLocations() {
  if (currentView() === 'location' && cardLocations.length === 0) {
    locationsView.setView('all');
  }
}

/**
 * Gives the by-location document a draw needs while the location view is on, or null in the other view. When the document cannot be
 * had the draw goes on with the all-together cabinet and the caller says so after drawing, unless the request was quiet.
 * @param {string | null} etag The entity tag of the layout the document must belong to.
 * @param {boolean} quiet Whether to leave the view alone and draw nothing when the document cannot be had.
 * @param {Promise<object | null> | null} [started] A request for the document that is already under way.
 * @returns {Promise<{ doc: object | null, usable: boolean, failed: boolean }>} The document, whether the draw can go on (false only
 * when a quiet request failed), and whether the location view could not be had and the all-together cabinet is drawn instead.
 */
async function documentForView(etag, quiet, started = null) {
  if (currentView() !== 'location') {
    return { doc: null, usable: true, failed: false };
  }

  const doc = await locationsView.fetchDocument(etag, started);

  if (doc !== null) {
    return { doc, usable: true, failed: false };
  }

  return { doc: null, usable: !quiet, failed: !quiet };
}

/**
 * Remembers what the layout just drawn holds and, unless the draw already waited for the card data, starts fetching it.
 * @param {object} layout The layout drawn.
 * @param {string | null} etag The entity tag of the layout response.
 * @param {'loaded' | 'failed' | null} settled How the wait for the card data ended, or null when the draw did not wait for it.
 */
function afterDraw(layout, etag, settled) {
  drawnPlacements = indexPlacements(layout);
  drawnPalette = layout.palette ?? [];
  cardsFailed = settled === 'failed';

  if (settled !== 'loaded') {
    gamesList?.showTitles(drawnPlacements);
  }

  if (settled === null) {
    fetchCards(latestLoad, etag);
  }
}

/**
 * Replaces the mount's content with the loading line.
 */
function showLoading() {
  const message = document.createElement('p');
  message.className = 'cabinet-message cabinet-loading';
  message.textContent = COPY.loading;
  mount.replaceChildren(message);
}

/**
 * Shows the load error with a button that repeats the load.
 */
function showError() {
  const heading = document.createElement('p');
  heading.className = 'cabinet-message-heading';
  heading.textContent = COPY.errorHeading;

  const body = document.createElement('p');
  body.textContent = COPY.errorBody;

  const retry = document.createElement('button');
  retry.type = 'button';
  retry.className = 'cabinet-retry';
  retry.textContent = COPY.retry;
  retry.addEventListener('click', load);

  filters.showBar(false);

  const message = document.createElement('div');
  message.className = 'cabinet-message';
  message.append(heading, body, retry);
  mount.replaceChildren(message);
}

/**
 * Fetches and draws the cabinet, showing the loading line first and the error state on any failure. A newer load
 * supersedes an older one that is still in flight, so a slow earlier response never overwrites a later one.
 * @returns {Promise<void>}
 */
async function load() {
  latestLoad += 1;
  const thisLoad = latestLoad;
  cardRecords = new Map();
  cardLocations = [];
  cardsFailed = false;
  drawnLayout = null;
  showLoading();

  try {
    const startedCards = drawWaitsForCards() ? startCardsRequest() : null;
    const startedDocument = currentView() === 'location' ? locationsView.requestDocument() : null;
    const response = await fetch(layoutUrl());

    if (thisLoad !== latestLoad) {
      return;
    }

    if (!response.ok) {
      showError();
      return;
    }

    const layout = await response.json();

    if (thisLoad !== latestLoad) {
      return;
    }

    const etag = response.headers.get('ETag');
    const settled = startedCards === null ? null : await fetchCards(thisLoad, etag, startedCards);

    if (settled === 'superseded') {
      return;
    }

    leaveLocationViewWithoutLocations();

    const { doc, failed } = await documentForView(etag, false, startedDocument);

    if (thisLoad !== latestLoad) {
      return;
    }

    drawCabinet(layout, etag, settled, doc);

    if (failed) {
      locationsView.fallBack();
    }
  } catch {
    if (thisLoad === latestLoad) {
      showError();
    }
  }
}

/**
 * Fetches the layout for the current profile and the mount's sample, if it carries one.
 * @returns {Promise<{ layout: object, etag: string | null } | null>} The layout and its entity tag, or null when the answer is not a success.
 */
async function fetchLayout() {
  const response = await fetch(layoutUrl());

  return response.ok ? { layout: await response.json(), etag: response.headers.get('ETag') } : null;
}

/**
 * Waits until the tab is visible, so a hidden tab redraws once when it comes back instead of while nobody looks.
 * @returns {Promise<void>}
 */
function whenVisible() {
  if (!document.hidden) {
    return Promise.resolve();
  }

  return new Promise((resolve) => {
    document.addEventListener('visibilitychange', () => resolve(), { once: true });
  });
}

/**
 * Redraws the cabinet quietly after the collection changed: no loading line, no error state, no animation. The old cabinet stays
 * until the new layout is ready and is swapped for it in one step, together with the being-filled message. Keyboard focus returns
 * to the box with the same entry id and kind when it still exists. Any failure leaves the old cabinet and says nothing. A newer load or
 * redraw supersedes this one.
 * @returns {Promise<boolean>} True when the new cabinet is on screen; false when nothing changed on screen.
 */
async function redraw() {
  await whenVisible();

  latestLoad += 1;
  const thisRedraw = latestLoad;

  try {
    const startedCards = drawWaitsForCards() ? startCardsRequest() : null;
    const fetched = await fetchLayout();

    if (thisRedraw !== latestLoad) {
      return false;
    }

    if (fetched === null) {
      return abandonRedraw();
    }

    const { layout, etag } = fetched;
    const settled = startedCards === null ? null : await fetchCards(thisRedraw, etag, startedCards);

    if (settled === 'superseded') {
      return false;
    }

    leaveLocationViewWithoutLocations();

    const { doc, usable } = await documentForView(etag, true);

    if (thisRedraw !== latestLoad) {
      return false;
    }

    if (!usable) {
      return abandonRedraw();
    }

    const active = document.activeElement;
    const focused = mount.contains(active) ? { entryId: active.dataset.entryId, kind: active.dataset.kind } : undefined;
    const scrolledTo = window.scrollY;

    drawCabinet(layout, etag, settled, doc);
    keepScroll(scrolledTo);

    const filling = document.querySelector('.cabinet-filling');

    if (filling !== null) {
      filling.remove();
    }

    if (focused !== undefined) {
      const same = mount.querySelector('[data-entry-id="' + CSS.escape(focused.entryId) + '"][data-kind="' + CSS.escape(focused.kind) + '"]');

      if (same !== null) {
        same.focus({ preventScroll: true });
      } else {
        document.activeElement.blur();
      }
    }

    return true;
  } catch {
    return thisRedraw === latestLoad ? abandonRedraw() : false;
  }
}

/**
 * Puts the page back where it was scrolled to after the cabinet was swapped, when the new page is tall enough to hold that place.
 * @param {number} top The scroll position before the swap, in pixels.
 */
function keepScroll(top) {
  const reachable = document.documentElement.scrollHeight - window.innerHeight >= top;

  if (reachable && Math.abs(window.scrollY - top) > 1) {
    window.scrollTo({ top, left: 0, behavior: 'instant' });
  }
}

/**
 * Ends a redraw that could not get a layout without saying anything. If it had superseded a first load that was still showing
 * the loading line, that load is repeated so the line cannot stay on screen for good.
 * @returns {boolean} False: nothing changed on screen.
 */
function abandonRedraw() {
  if (mount.querySelector('.cabinet-loading') !== null) {
    load();
  }

  return false;
}

let redrawWaitingForCard = null;

/**
 * Redraws the cabinet quietly once no card is open. While a card is open nothing under it is redrawn, refetched or replaced; the
 * redraw waits until the card has closed and its box is back in its slot. Changes that arrive during that wait share the one
 * pending redraw, which fetches the newest layout when the wait is over.
 * @returns {Promise<boolean>} What the redraw reports: true when the new cabinet is on screen.
 */
function redrawWhenCardIsClosed() {
  if (redrawWaitingForCard !== null) {
    return redrawWaitingForCard;
  }

  const closed = cardControls === null ? Promise.resolve() : cardControls.whenClosed();

  redrawWaitingForCard = closed.then(() => {
    redrawWaitingForCard = null;

    return redraw();
  });

  return redrawWaitingForCard;
}

/**
 * Draws the cabinet again for the profile the viewport now matches. A card that is open is closed at once with the plain fade, with
 * no box flying, before the cabinet is loaded, so no card is left floating over a cabinet drawn for the other profile.
 * @returns {Promise<void>}
 */
async function reloadForNewProfile() {
  if (cardControls !== null) {
    await cardControls.closeCard({ path: 'fade' });
  }

  await load();
}

if (syncRoot !== null) {
  const sync = initSyncStatus(syncRoot, { onCollectionChanged: redrawWhenCardIsClosed });

  startLive({
    applyStatus: (status) => {
      applyLookStatus(status);
      sync.applyStatus(status);
    },
    fetchStatus,
  });
}

if (mount !== null && dialog !== null) {
  cardControls = initCardDialog({
    dialog,
    mount,
    getRecord: recordFor,
    getCopy: () => COPY,
    getPalette: () => drawnPalette,
    iconsUrl: dialog.dataset.icons ?? '',
  });
}

if (skipLink !== null && gamesList !== null) {
  skipLink.addEventListener('click', (event) => {
    event.preventDefault();
    gamesList.focusStart();
  });
}

if (mount !== null) {
  initLook(document);
  phoneQuery.addEventListener('change', reloadForNewProfile);
  load();
}
