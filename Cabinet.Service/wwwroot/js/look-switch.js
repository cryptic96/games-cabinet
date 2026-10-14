/**
 * Applies a look the server pushed to a page that is already open. The status carries the five look values and whether the wall is
 * a light one. A look the page already shows changes nothing. A change of the lights alone sets the attributes and lets the lamp fade
 * run by itself. Any other change is applied inside a short cross-fade of the whole page where the browser has one, and at once
 * where it has none or the visitor prefers less motion. The footer logo follows the wall, and the script that decides when the lights
 * are lit is loaded first when the page was opened with the lights off and never loaded it.
 */
import { refreshLamp } from './look.js';
import { isOutdatedStatus } from './status.js';

const LOOK_FIELDS = ['design', 'wood', 'backs', 'lights', 'depth'];
const MAX_VALUE_LENGTH = 32;
const LIGHTS_FIELD = 'lights';
const LIGHTS_OFF = 'off';
const SWITCH_MARKER = 'data-look-switch';
const REDUCED_MOTION_QUERY = '(prefers-reduced-motion: reduce)';
const EVENING_META = 'meta[name="cabinet-evening-script"]';
const LOGO_SELECTOR = 'footer .bgg-credit img';

const newestSeen = new WeakMap();
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
 * Writes the look on the root, then lights or darkens the lamp. The layout is read in between so a light layer that was not shown
 * before has its unlit style on screen when it is lit, and the lamp fades on instead of appearing.
 * @param {Element} root The root element.
 * @param {{ values: string[], lightWall: boolean }} look The look to show.
 */
function writeLook(root, look) {
  LOOK_FIELDS.forEach((field, index) => root.setAttribute('data-' + field, look.values[index]));
  swapLogo(root, look.lightWall);
  root.getBoundingClientRect?.();
  refreshLamp(root);
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
 * Applies the look a status carries to the page. It does nothing for a status without a look, for one older than the newest this
 * page has seen, and for a look the page already shows.
 * @param {object} status A pushed or fetched status.
 * @param {Element} [root] The element that carries the look attributes; the page's root by default.
 * @returns {Promise<void>} Resolves when the look is on the page, or at once when there was nothing to do.
 */
export async function applyLookStatus(status, root = document.documentElement) {
  const look = readLook(status);

  if (look === null || isStale(root, status)) {
    return;
  }

  const changed = LOOK_FIELDS.filter((field, index) => root.getAttribute('data-' + field) !== look.values[index]);

  if (changed.length === 0) {
    return;
  }

  const doc = root.ownerDocument;
  const turnsLightsOn = look.values[LOOK_FIELDS.indexOf(LIGHTS_FIELD)] !== LIGHTS_OFF && window.cabinetEvening === undefined;

  if (turnsLightsOn) {
    await loadEveningScript(doc);
  }

  const update = () => writeLook(root, look);
  const lightsOnly = changed.length === 1 && changed[0] === LIGHTS_FIELD;

  if (lightsOnly || prefersReducedMotion() || typeof doc?.startViewTransition !== 'function') {
    update();

    return;
  }

  crossFade(root, update);
}
