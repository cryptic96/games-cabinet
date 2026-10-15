/**
 * Applies a look the server pushed to a page that is already open. The status carries the five look values and whether the wall is
 * a light one. A look the page already shows changes nothing. A change of the lights alone sets the attributes and lets the lamp fade
 * run by itself. Any other change is applied inside a short cross-fade of the whole page where the browser has one, and at once
 * where it has none or the visitor prefers less motion. The footer logo follows the wall, and the script that decides when the lights
 * are lit is loaded first when the page was opened with the lights off and never loaded it. A new design gives the sections other
 * heights, so the section in the middle of the screen is noted before the change and put back where it was after it, with the
 * browser's own scroll anchoring off meanwhile so that one rule alone moves the page. A look that arrives while a box is being
 * pulled out waits until the pull-out is over, and only the latest one that arrived is then applied. Every look that is accepted
 * becomes the newest one the page was asked to show, and a look that is no longer the newest never writes: it is checked again
 * after the wait for the evening script and inside the cross-fade's update, so a slower, older look cannot overwrite a newer one.
 */
import * as lamp from './look.js';
import { isOutdatedStatus } from './status.js';

const LOOK_FIELDS = ['design', 'wood', 'backs', 'lights', 'depth'];
const MAX_VALUE_LENGTH = 32;
const DESIGN_FIELD = 'design';
const LIGHTS_FIELD = 'lights';
const LIGHTS_OFF = 'off';
const SWITCH_MARKER = 'data-look-switch';
const EVENING_MARKER = 'data-evening';
const REDUCED_MOTION_QUERY = '(prefers-reduced-motion: reduce)';
const EVENING_META = 'meta[name="cabinet-evening-script"]';
const LOGO_SELECTOR = 'footer .bgg-credit img';
const SECTION_SELECTOR = '#cabinet .section';
const PULL_MARKER = 'data-pull-kind';
const ANCHOR_PROPERTY = 'overflow-anchor';

const newestSeen = new WeakMap();
const newestAsked = new WeakMap();
const heldLooks = new WeakMap();
let eveningLoading = null;

/**
 * Reads the look out of a status.
 * @param {unknown} status A pushed or fetched status.
 * @returns {{ values: string[], lightWall: boolean } | null} The five values in field order and the wall tone, or null when the status
 * carries no usable look, such as a partial status that only reports a cooldown.
 */
function readLook(status) {
  const look = status?.look;

  if (look === null || typeof look !== 'object') {
    return null;
  }

  const values = LOOK_FIELDS.map((field) => look[field]);

  if (!values.every((value) => typeof value === 'string' && value.length > 0 && value.length <= MAX_VALUE_LENGTH)) {
    return null;
  }

  return { values, lightWall: look.lightWall === true };
}

/**
 * Reads when the server rendered the page, so a status older than the page itself is never applied.
 * @param {Element} root The root element.
 * @returns {number} The moment in milliseconds, or negative infinity when the page does not say.
 */
function renderedAtMs(root) {
  const stamp = root.querySelector?.('.sync')?.dataset?.serverTime;
  const at = Date.parse(stamp ?? '');

  return Number.isFinite(at) ? at : Number.NEGATIVE_INFINITY;
}

/**
 * Tells whether a status is older than the newest one this page has seen, and remembers the newest.
 * @param {Element} root The root element.
 * @param {object} status The status.
 * @returns {boolean} True when the status must be ignored.
 */
function isStale(root, status) {
  const newest = newestSeen.get(root) ?? renderedAtMs(root);

  if (isOutdatedStatus(newest, status)) {
    return true;
  }

  const at = Date.parse(status.serverTimeUtc);

  newestSeen.set(root, Number.isFinite(at) ? Math.max(newest, at) : newest);

  return false;
}

/**
 * Makes a look the newest one the page was asked to show, which every older look still on its way to the page then gives way to.
 * @param {Element} root The root element.
 * @returns {number} The rank of the look; a later look has a higher one.
 */
function askForLook(root) {
  const rank = (newestAsked.get(root) ?? 0) + 1;

  newestAsked.set(root, rank);

  return rank;
}

/**
 * Tells whether no newer look was asked for since a look got its rank.
 * @param {Element} root The root element.
 * @param {number} rank The rank the look got when it was accepted.
 * @returns {boolean} True while the look is still the newest one asked for.
 */
function isNewest(root, rank) {
  return newestAsked.get(root) === rank;
}

/**
 * Points the footer logo at the file that suits the wall: the dark one on a light wall, the white one otherwise.
 * @param {Element} root The root element.
 * @param {boolean} lightWall Whether the wall is a light one.
 */
function swapLogo(root, lightWall) {
  const logo = root.ownerDocument?.querySelector(LOGO_SELECTOR) ?? null;
  const address = logo?.getAttribute(lightWall ? 'data-logo-dark' : 'data-logo-light');

  if (logo !== null && address && logo.getAttribute('src') !== address) {
    logo.setAttribute('src', address);
  }
}

/**
 * Loads the script that tells when the lights are lit, once, and waits until it has run. A script that fails to load does not hold
 * the look back.
 * @param {Document} doc The page.
 * @returns {Promise<void>}
 */
function loadEveningScript(doc) {
  const address = doc.querySelector(EVENING_META)?.getAttribute('content');

  if (!address) {
    return Promise.resolve();
  }

  eveningLoading ??= new Promise((resolve) => {
    const script = doc.createElement('script');

    script.addEventListener('load', () => resolve(), { once: true });
    script.addEventListener('error', () => {
      eveningLoading = null;
      resolve();
    }, { once: true });
    script.setAttribute('src', address);
    doc.head.append(script);
  });

  return eveningLoading;
}

/**
 * Picks the section that holds the vertical centre of the viewport.
 * @param {{ top: number, bottom: number }[]} rects The sections' rectangles measured from the top of the viewport.
 * @param {number} viewportHeight The height of the viewport.
 * @returns {number} The index of the section that contains the centre, or of the one whose edge is nearest when the centre lies in a
 * gap between sections; -1 when there are no sections.
 */
export function sectionAtCentre(rects, viewportHeight) {
  const centre = viewportHeight / 2;
  let nearest = -1;
  let nearestDistance = Number.POSITIVE_INFINITY;

  rects.forEach((rect, index) => {
    const distance = centre < rect.top ? rect.top - centre : Math.max(0, centre - rect.bottom);

    if (distance < nearestDistance) {
      nearest = index;
      nearestDistance = distance;
    }
  });

  return nearest;
}

/**
 * Tells how far the page must scroll to bring a section back to where it was.
 * @param {{ top: number }} before Where the section was, measured from the top of the viewport.
 * @param {{ top: number }} after Where the section is now.
 * @returns {number} The distance to scroll down; negative to scroll up.
 */
export function scrollCorrection(before, after) {
  return after.top - before.top;
}

/**
 * Notes the section in the middle of the viewport and where it stands.
 * @param {Document} doc The page.
 * @param {Element} scroller The element that scrolls the page.
 * @returns {{ section: Element, top: number } | null} The section and its distance from the viewport top, or null when the page has no
 * sections drawn.
 */
function noteSection(doc, scroller) {
  const sections = [...(doc.querySelectorAll?.(SECTION_SELECTOR) ?? [])];
  const rects = sections.map((section) => section.getBoundingClientRect());
  const index = sectionAtCentre(rects, scroller.clientHeight);

  return index < 0 ? null : { section: sections[index], top: rects[index].top };
}

/**
 * Runs the update that changes the design and puts the section that was in the middle of the viewport back where it was. The
 * browser's own scroll anchoring is switched off while this happens and restored afterwards, so this is the only thing that moves
 * the page.
 * @param {Element} root The root element.
 * @param {() => void} update Puts the page into its new look.
 */
function keepPlace(root, update) {
  const doc = root.ownerDocument;
  const scroller = doc?.scrollingElement;
  const noted = scroller ? noteSection(doc, scroller) : null;

  if (noted === null) {
    update();

    return;
  }

  scroller.style.setProperty(ANCHOR_PROPERTY, 'none');

  try {
    update();

    const correction = scrollCorrection({ top: noted.top }, noted.section.getBoundingClientRect());

    if (correction !== 0) {
      scroller.scrollBy({ top: correction, behavior: 'instant' });
    }
  } finally {
    scroller.style.removeProperty(ANCHOR_PROPERTY);
  }
}

/**
 * Writes the look on the root, then lights or darkens the lamp. The layout is read in between so a light layer that was not shown
 * before has its unlit style on screen when it is lit, and the lamp fades on instead of appearing. Lights that are off also drop
 * the evening marker, and lights that are not off hand the clock to the lamp script's follower when the lamp module has one, so
 * evenings only is decided by the visitor's own clock from this moment on.
 * @param {Element} root The root element.
 * @param {{ values: string[], lightWall: boolean }} look The look to show.
 */
function writeLook(root, look) {
  LOOK_FIELDS.forEach((field, index) => root.setAttribute('data-' + field, look.values[index]));
  swapLogo(root, look.lightWall);
  root.getBoundingClientRect?.();

  if (look.values[LOOK_FIELDS.indexOf(LIGHTS_FIELD)] === LIGHTS_OFF) {
    root.removeAttribute(EVENING_MARKER);
  }

  lamp.refreshLamp(root);

  if (root.getAttribute('data-' + LIGHTS_FIELD) !== LIGHTS_OFF) {
    lamp.followClock?.(root.ownerDocument);
  }
}

/**
 * Runs the update inside a view transition, with the root marked for the duration so its own length applies. The update runs
 * whether or not the transition does, and exactly once.
 * @param {Element} root The root element.
 * @param {() => void} update Puts the page into its new look.
 */
function crossFade(root, update) {
  let applied = false;
  const apply = () => {
    if (!applied) {
      applied = true;
      update();
    }
  };

  root.setAttribute(SWITCH_MARKER, '');

  let transition;

  try {
    transition = root.ownerDocument.startViewTransition(apply);
  } catch {
    apply();
    root.removeAttribute(SWITCH_MARKER);

    return;
  }

  transition.ready.catch(() => {});
  transition.updateCallbackDone.catch(() => {});
  transition.finished.catch(() => {}).then(() => {
    apply();
    root.removeAttribute(SWITCH_MARKER);
  });
}

/**
 * Tells whether the visitor asked for less motion.
 * @returns {boolean}
 */
function prefersReducedMotion() {
  return window.matchMedia?.(REDUCED_MOTION_QUERY)?.matches === true;
}

/**
 * Puts a look on the page when it differs from what the page shows now.
 * @param {Element} root The element that carries the look attributes.
 * @param {{ values: string[], lightWall: boolean }} look The look to show.
 * @param {number} rank The rank the look got when it was accepted; the look is not written once a newer one was asked for.
 * @returns {Promise<void>} Resolves when the look is on the page, or at once when there was nothing to do or a newer look took over.
 */
async function showLook(root, look, rank) {
  const changed = LOOK_FIELDS.filter((field, index) => root.getAttribute('data-' + field) !== look.values[index]);

  if (changed.length === 0) {
    return;
  }

  const doc = root.ownerDocument;
  const turnsLightsOn = look.values[LOOK_FIELDS.indexOf(LIGHTS_FIELD)] !== LIGHTS_OFF && window.cabinetEvening === undefined;

  if (turnsLightsOn) {
    await loadEveningScript(doc);

    if (!isNewest(root, rank)) {
      return;
    }
  }

  const write = () => {
    if (isNewest(root, rank)) {
      writeLook(root, look);
    }
  };
  const update = changed.includes(DESIGN_FIELD) ? () => keepPlace(root, write) : write;
  const lightsOnly = changed.length === 1 && changed[0] === LIGHTS_FIELD;

  if (lightsOnly || prefersReducedMotion() || typeof doc?.startViewTransition !== 'function') {
    update();

    return;
  }

  crossFade(root, update);
}

/**
 * Keeps the newest look that arrives while a box is being pulled out, and shows it once the pull-out marker is gone. A look that
 * arrives later replaces the one held, and every caller is told when the held look is on the page.
 * @param {Element} root The root element.
 * @param {{ values: string[], lightWall: boolean }} look The look to show once the pull-out is over.
 * @param {number} rank The rank the look got when it was accepted.
 * @returns {Promise<void>} Resolves when the look held at that time has been shown.
 */
function holdUntilPulledOut(root, look, rank) {
  const held = heldLooks.get(root);

  if (held !== undefined) {
    held.look = look;
    held.rank = rank;

    return new Promise((resolve) => held.waiting.push(resolve));
  }

  const entry = { look, rank, waiting: [] };
  const observer = new MutationObserver(() => {
    if (root.hasAttribute(PULL_MARKER)) {
      return;
    }

    observer.disconnect();
    heldLooks.delete(root);
    showLook(root, entry.look, entry.rank).finally(() => entry.waiting.forEach((resolve) => resolve()));
  });

  heldLooks.set(root, entry);
  observer.observe(root, { attributes: true, attributeFilter: [PULL_MARKER] });

  return new Promise((resolve) => entry.waiting.push(resolve));
}

/**
 * Applies the look a status carries to the page. It does nothing for a status without a look, for one older than the newest this
 * page has seen, and for a look the page already shows. While a box is being pulled out the look is held back and applied when the
 * pull-out has ended.
 * @param {object} status A pushed or fetched status.
 * @param {Element} [root] The element that carries the look attributes; the page's root by default.
 * @returns {Promise<void>} Resolves when the look is on the page, or at once when there was nothing to do.
 */
export async function applyLookStatus(status, root = document.documentElement) {
  const look = readLook(status);

  if (look === null || isStale(root, status)) {
    return;
  }

  const rank = askForLook(root);

  if (root.hasAttribute(PULL_MARKER) || heldLooks.has(root)) {
    await holdUntilPulledOut(root, look, rank);

    return;
  }

  await showLook(root, look, rank);
}
