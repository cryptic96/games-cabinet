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
const SECONDS_PER_CHECK = 60;
const BOUNDARY_SLACK_MS = 250;
const followedRoots = new WeakSet();

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
 * Moves the lamp to what the attributes now say. Nothing happens when the lamp already is that way. Otherwise the fading hint
 * is set before the lit marker changes, so the opacity transition runs from the state the layer is in; a fade in is
 * remembered for this tab. With a fade of nothing (reduced motion) no hint is set.
 * @param {Element} root The root element.
 * @returns {boolean} Whether the lamp changed.
 */
function changeLamp(root) {
  const lit = isLit(root.dataset.lights, root.hasAttribute('data-evening'));

  if (root.hasAttribute('data-lit') === lit) {
    return false;
  }

  const fades = fadeMilliseconds(root) > 0;

  if (fades) {
    root.setAttribute('data-fading', '');
  }

  refreshLamp(root);

  if (lit) {
    rememberFadeInThisTab();
  }

  if (fades) {
    endFadingWhenDone(root);
  }

  return true;
}

/**
 * Starts the markers for the page's first draw. Lights that are off only settle the page. Lights that are lit and not yet
 * marked fade on: settled, fading and lit are set in one task, so the transition runs from the unlit state. Lights the head
 * script already lit before the first paint are left lit and only settle.
 * @param {Element} root The root element.
 */
function start(root) {
  root.setAttribute('data-settled', '');
  changeLamp(root);
}

/**
 * Brings data-evening and the lamp in line with the visitor's clock. Nothing happens without the clock the head script
 * provides; with the lights off the evening marker is removed.
 * @param {Element} root The root element.
 */
function applyClock(root) {
  const clock = window.cabinetEvening;

  if (typeof clock?.isEvening !== 'function') {
    return;
  }

  if (root.dataset.lights !== 'off' && clock.isEvening(new Date())) {
    root.setAttribute('data-evening', '');
  } else {
    root.removeAttribute('data-evening');
  }

  changeLamp(root);
}

/**
 * Keeps an open page right about the evening. It checks the clock at once, every minute while the page is visible, when the
 * page becomes visible again, and with one timer set for the next sunrise or lamp-on moment (set again each time it fires), so
 * a page left open across sunset or sunrise fades at that moment. Calling it again for the same page does nothing. Only
 * evenings only needs the timer; always on and off never change with the clock.
 * @param {Document} [doc] The page; the current document by default.
 */
export function followClock(doc = document) {
  const root = doc.documentElement;

  if (followedRoots.has(root)) {
    return;
  }

  followedRoots.add(root);

  let boundary;
  const check = () => {
    window.clearTimeout(boundary);
    applyClock(root);

    const clock = window.cabinetEvening;

    if (root.dataset.lights === 'evenings' && typeof clock?.msToNextChange === 'function') {
      boundary = window.setTimeout(check, clock.msToNextChange(new Date()) + BOUNDARY_SLACK_MS);
    }
  };
  const checkWhenVisible = () => {
    if (doc.visibilityState === 'visible') {
      check();
    }
  };

  check();
  window.setInterval(checkWhenVisible, MILLISECONDS_PER_SECOND * SECONDS_PER_CHECK);
  doc.addEventListener('visibilitychange', checkWhenVisible);
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
    followClock(doc);
  }
}
