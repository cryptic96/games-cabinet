/**
 * Applies the markers that turn the cabinet's lights on. The server writes the stored choice on the root element; this script
 * adds the three markers that depend on the visitor: data-lit while the lights are lit, data-settled once the first frames are
 * on screen so transitions may run, and data-fading while the lamp fades in. The first load in a tab waits until the cabinet is
 * drawn and two frames have passed, so the lamp fades on in front of the visitor; a page that the head script already marked lit
 * before its first paint only becomes settled.
 */

const TAB_FLAG_KEY_FALLBACK = 'cabinet-lit';
const TAB_FLAG_VALUE = '1';
const FRAMES_BEFORE_FADE = 2;
const FADE_GRACE_MS = 150;
const MILLISECONDS_PER_SECOND = 1000;
const DRAWN_EVENT = 'cabinet:drawn';

/**
 * Tells whether the lights are lit for a stored choice and a clock reading.
 * @param {string | undefined} lights The stored choice: off, on or evenings.
 * @param {boolean} evening Whether the visitor's clock is between shortly before sunset and sunrise.
 * @returns {boolean} True for always on, and for evenings only while it is evening.
 */
export function isLit(lights, evening) {
  return lights === 'on' || (lights === 'evenings' && evening === true);
}

/**
 * Sets or removes data-lit on a root element from the attributes it carries now, so the evening rule and a live change of the
 * look reach the same decision as the first load.
 * @param {Element} root The element that carries the look attributes, such as the page's root or a preview wrapper.
 * @returns {boolean} Whether the lights are lit on that root.
 */
export function refreshLamp(root) {
  const lit = isLit(root.dataset.lights, root.hasAttribute('data-evening'));

  if (lit) {
    root.setAttribute('data-lit', '');
  } else {
    root.removeAttribute('data-lit');
  }

  return lit;
}

/**
 * Reads the length of the lamp fade from the root's custom property.
 * @param {Element} root The root element.
 * @returns {number} The fade in milliseconds, 0 when it is not a time.
 */
function fadeMilliseconds(root) {
  const text = window.getComputedStyle(root).getPropertyValue('--lamp-fade').trim();
  const amount = Number.parseFloat(text);

  if (Number.isNaN(amount)) {
    return 0;
  }

  return text.endsWith('ms') ? amount : amount * MILLISECONDS_PER_SECOND;
}

/**
 * Remembers in this tab that the lights have faded on once, so a later load in the same tab starts lit. Storage that is not
 * available is ignored, and every load then fades.
 */
function rememberFadeInThisTab() {
  const key = window.cabinetEvening?.key ?? TAB_FLAG_KEY_FALLBACK;

  try {
    window.sessionStorage.setItem(key, TAB_FLAG_VALUE);
  } catch {
    return;
  }
}

/**
 * Waits for the given number of animation frames.
 * @param {number} count How many frames to wait.
 * @returns {Promise<void>}
 */
function frames(count) {
  return new Promise((resolve) => {
    const step = (remaining) => {
      if (remaining === 0) {
        resolve();
      } else {
        window.requestAnimationFrame(() => step(remaining - 1));
      }
    };

    step(count);
  });
}

/**
 * Waits until the cabinet has been drawn, so there is a light layer to fade.
 * @param {Document} doc The page.
 * @returns {Promise<void>}
 */
function whenDrawn(doc) {
  if (doc.querySelector('.section-light') !== null) {
    return Promise.resolve();
  }

  return new Promise((resolve) => {
    doc.addEventListener(DRAWN_EVENT, () => resolve(), { once: true });
  });
}

/**
 * Takes the compositor hint away when the fade is over: when the layer's opacity transition ends, or a moment after the fade
 * should have ended if the browser never reports it.
 * @param {Element} root The root element.
 */
function endFadingWhenDone(root) {
  const finish = () => {
    root.removeAttribute('data-fading');
    root.removeEventListener('transitionend', onEnd);
    window.clearTimeout(timer);
  };
  const onEnd = (event) => {
    if (event.propertyName === 'opacity') {
      finish();
    }
  };
  const timer = window.setTimeout(finish, fadeMilliseconds(root) + FADE_GRACE_MS);

  root.addEventListener('transitionend', onEnd);
}

/**
 * Starts the markers for the page's first draw. Lights that are off only settle the page. Lights that are lit and not yet
 * marked fade on: settled, fading and lit are set in one task, so the transition runs from the unlit state. Lights the head
 * script already lit before the first paint are left lit and only settle.
 * @param {Element} root The root element.
 */
function start(root) {
  const alreadyLit = root.hasAttribute('data-lit');
  const lit = isLit(root.dataset.lights, root.hasAttribute('data-evening'));

  root.setAttribute('data-settled', '');

  if (alreadyLit || !lit) {
    return;
  }

  const fades = fadeMilliseconds(root) > 0;

  if (fades) {
    root.setAttribute('data-fading', '');
  }

  refreshLamp(root);
  rememberFadeInThisTab();

  if (fades) {
    endFadingWhenDone(root);
  }
}

/**
 * Prepares the lights of a page that shows the cabinet. It does nothing visible until the cabinet is drawn and two frames
 * have passed; then it applies the markers once. Calling it again on a settled page does nothing.
 * @param {Document} [doc] The page; the current document by default.
 * @returns {Promise<void>} Resolves when the markers have been applied.
 */
export async function initLook(doc = document) {
  const root = doc.documentElement;

  if (root.hasAttribute('data-settled')) {
    return;
  }

  await whenDrawn(doc);
  await frames(FRAMES_BEFORE_FADE);

  if (!root.hasAttribute('data-settled')) {
    start(root);
  }
}
