/**
 * Head script of the cabinet page that reserves the filter dock's column before the first paint, so the cabinet never shifts
 * sideways when the dock's buttons appear. It reads one value from the device's local storage, whether the visitor left the filter
 * panel open, and sets one attribute on the root element: the panel open beside the cabinet when it was left open and the window is
 * wide enough for the panel to stand beside the cabinet, otherwise the folded rail. A panel that would lie over the cabinet is never
 * opened here. Storage that is missing or blocked gives the rail. It reads nothing else, writes nothing, and adds no global name.
 */
(function reserveTheFilterColumn() {
  'use strict';

  const PANEL_KEY = 'cabinet-filter-panel';
  const PANEL_OPEN = 'open';
  const PUSH_QUERY = '(min-width: 90rem)';
  const PUSH = 'push';
  const RAIL = 'rail';

  let remembered = null;

  try {
    remembered = window.localStorage.getItem(PANEL_KEY);
  } catch {
    remembered = null;
  }

  const pushed = remembered === PANEL_OPEN && window.matchMedia(PUSH_QUERY).matches;

  document.documentElement.dataset.filterDock = pushed ? PUSH : RAIL;
})();
