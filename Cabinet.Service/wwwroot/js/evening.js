/**
 * Head script of the cabinet page. It runs before the first paint, only when the stored lights choice is not off, and does one
 * thing: when this browser tab has already faded the lights on once, it lights the page at once so a later load in the tab starts
 * lit instead of fading on again. The only name it adds to the page is window.cabinetEvening, which carries the key of the tab
 * flag and a helper that marks a root element lit from the attributes it carries.
 */
(function lightIfAlreadyFadedIn() {
  'use strict';

  const TAB_FLAG_KEY = 'cabinet-lit';
  const TAB_FLAG_VALUE = '1';
  const root = document.documentElement;

  /**
   * Tells whether the attributes of an element say the lights are lit: always on, or evenings while it is evening.
   * @param {Element} element The element that carries the look attributes.
   * @returns {boolean}
   */
  function litByAttributes(element) {
    const lights = element.getAttribute('data-lights');

    return lights === 'on' || (lights === 'evenings' && element.hasAttribute('data-evening'));
  }

  /**
   * Marks an element lit when its attributes say the lights are lit.
   * @param {Element} element The element that carries the look attributes.
   * @returns {boolean} Whether the element is lit.
   */
  function markLit(element) {
    const lit = litByAttributes(element);

    if (lit) {
      element.setAttribute('data-lit', '');
    }

    return lit;
  }

  /**
   * Tells whether this tab has already faded the lights on. Storage that is not available counts as not.
   * @returns {boolean}
   */
  function tabAlreadyFadedIn() {
    try {
      return window.sessionStorage.getItem(TAB_FLAG_KEY) === TAB_FLAG_VALUE;
    } catch {
      return false;
    }
  }

  window.cabinetEvening = { key: TAB_FLAG_KEY, markLit };

  if (root.getAttribute('data-lights') !== 'off' && tabAlreadyFadedIn()) {
    markLit(root);
  }
}());
