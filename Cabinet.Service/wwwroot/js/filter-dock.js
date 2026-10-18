/**
 * The filter dock of a desktop: from 64rem the filters live in a column at the left edge of the screen instead of a bar above the
 * cabinet. The column is folded to a narrow rail of symbols, or open as a panel that holds the search field, the count and the
 * choices. From 90rem the open panel stands beside the cabinet and pushes it aside; below that it lies over the cabinet. The state is
 * one attribute on the root element, which the style sheet reads; this module only sets that attribute and moves focus. Below 64rem
 * the style sheet ignores the attribute and the slim bar and its sheet are in use.
 */

/** The media query from which the dock is in use; below it the page has the slim bar and the Filters sheet. */
const DOCK_QUERY = '(min-width: 64rem)';

/** The media query from which the open panel stands beside the cabinet; below it the open panel lies over the cabinet. */
export const PUSH_QUERY = '(min-width: 90rem)';

/** The media query that says the visitor prefers less motion. */
const REDUCED_MOTION = '(prefers-reduced-motion: reduce)';

/** The state of a folded dock: the rail of symbols. */
const RAIL = 'rail';

/** The state of a panel open beside the cabinet. */
const PUSH = 'push';

/** The state of a panel open over the cabinet. */
const OVER = 'over';

/**
 * Gives the length of a timing token of the page in milliseconds, so the script and the style sheet never disagree.
 * @param {HTMLElement} root The element the token is read from.
 * @param {string} name The custom property, such as `--fallback-close`.
 * @returns {number}
 */
function tokenMs(root, name) {
  const raw = window.getComputedStyle(root).getPropertyValue(name).trim();
  const value = Number.parseFloat(raw);

  if (!Number.isFinite(value)) {
    return 0;
  }

  return raw.endsWith('ms') ? value : value * 1000;
}

/**
 * Starts the filter dock.
 * @param {object} parts What the dock needs from the page.
 * @param {HTMLElement} parts.rail The folded rail.
 * @param {HTMLElement} parts.panel The open panel.
 * @param {HTMLElement} [parts.root] The element that carries the dock's state; the root element of the page by default.
 * @param {HTMLInputElement | null} [parts.field] The search field, which takes focus when the panel opens for a search.
 * @param {HTMLElement | null} [parts.fallbackFocus] Where focus goes when the panel had it and the dock goes away with the width.
 * @returns {{ open: (how?: { focus?: string | null }) => void, fold: (how?: { restoreFocus?: boolean, instant?: boolean }) => void, isOpen: () => boolean, state: () => string | null, attach: () => void, detach: () => void }}
 * The controls: open the panel (moving focus to the head button, the search field or one group's control), fold it back to the
 * rail, whether it is open, the current state, and reserving or giving up the dock's column when the page has a cabinet or not.
 */
export function initFilterDock({ rail, panel, root = document.documentElement, field = null, fallbackFocus = null }) {
  const railMenu = rail.querySelector('.filter-menu');
  const headMenu = panel.querySelector('.filter-menu');
  const docked = window.matchMedia(DOCK_QUERY);
  const pushed = window.matchMedia(PUSH_QUERY);

  let foldTimer = 0;

  /**
   * Tells the state of the dock.
   * @returns {string | null} The rail, push or over state, or null while the page has no dock.
   */
  function state() {
    return root.dataset.filterDock ?? null;
  }

  /**
   * Tells whether the panel is open, beside or over the cabinet.
   * @returns {boolean}
   */
  function isOpen() {
    const current = state();

    return current === PUSH || current === OVER;
  }

  /**
   * Stops a fold that is still sliding out, so an open that follows it starts from a clean panel.
   */
  function cancelFold() {
    window.clearTimeout(foldTimer);
    foldTimer = 0;
    delete panel.dataset.folding;
  }

  /**
   * Finds the control that takes focus when the panel opens for a given reason.
   * @param {string | null} focus Why the panel opens: `menu` for the three-line button, `search` or the name of a group.
   * @returns {HTMLElement | null}
   */
  function focusTarget(focus) {
    if (focus === 'menu') {
      return state() === PUSH ? headMenu : field;
    }

    if (focus === 'search') {
      return field;
    }

    if (focus === 'players' || focus === 'time') {
      return panel.querySelector(`.filter-group[data-filter="${focus}"] input:checked`);
    }

    if (focus === 'location') {
      return panel.querySelector('#filter-loc');
    }

    return null;
  }

  /**
   * Opens the panel: beside the cabinet from 90rem, over it below that. Focus moves to the control the reason names; for a search
   * the field's text is selected so typing replaces it. Nothing happens below 64rem or while the page has no dock.
   * @param {{ focus?: string | null }} [how] Why the panel opens.
   */
  function open({ focus = null } = {}) {
    if (!docked.matches || state() === null) {
      return;
    }

    cancelFold();

    if (!isOpen()) {
      root.dataset.filterDock = pushed.matches ? PUSH : OVER;
    }

    const target = focusTarget(focus);

    if (target === null || target.disabled) {
      return;
    }

    target.focus();

    if (target === field && focus === 'search') {
      field.select();
    }
  }

  /**
   * Ends a fold: the dock is the rail again, and focus goes to the rail's three-line button when asked.
   * @param {boolean} restoreFocus Whether focus moves to the rail's three-line button.
   */
  function finishFold(restoreFocus) {
    cancelFold();
    root.dataset.filterDock = RAIL;

    if (restoreFocus) {
      railMenu.focus();
    }
  }

  /**
   * Folds the panel back to the rail. A panel over the cabinet slides out over the closing time first, unless the visitor prefers
   * less motion or the fold is asked to be instant; a panel beside the cabinet always folds at once.
   * @param {{ restoreFocus?: boolean, instant?: boolean }} [how] Whether focus moves to the rail's three-line button, and whether
   * the fold skips the slide.
   */
  function fold({ restoreFocus = true, instant = false } = {}) {
    if (!isOpen() || panel.hasAttribute('data-folding')) {
      return;
    }

    if (state() !== OVER || instant || window.matchMedia(REDUCED_MOTION).matches) {
      finishFold(restoreFocus);

      return;
    }

    panel.dataset.folding = '';

    if (restoreFocus) {
      railMenu.focus();
    }

    foldTimer = window.setTimeout(() => finishFold(false), tokenMs(root, '--fallback-close'));
  }

  /**
   * Reserves the dock's column: the page has a cabinet, so the rail stands at the left edge.
   */
  function attach() {
    if (state() === null) {
      root.dataset.filterDock = RAIL;
    }
  }

  /**
   * Gives up the dock's column: the page shows a message instead of a cabinet, so the column collapses.
   */
  function detach() {
    cancelFold();
    delete root.dataset.filterDock;
  }

  /**
   * Folds an open panel at once when the width crosses 64rem or 90rem and the open state no longer fits the width: a panel beside
   * the cabinet below 90rem, one over the cabinet from 90rem, or any panel below 64rem. Focus that was inside the panel goes to the
   * rail's three-line button while the dock stays, or to the given fallback when the dock goes; anywhere else it stays where it is.
   */
  function followWidth() {
    const current = state();
    const fits = docked.matches && (current === PUSH) === pushed.matches;

    if (!isOpen() || fits) {
      return;
    }

    const hadFocus = panel.contains(document.activeElement);

    finishFold(hadFocus && docked.matches);

    if (hadFocus && !docked.matches) {
      fallbackFocus?.focus();
    }
  }

  attach();

  railMenu.addEventListener('click', () => open({ focus: 'menu' }));
  headMenu.addEventListener('click', () => fold());

  for (const button of rail.querySelectorAll('.filter-rail-button')) {
    button.addEventListener('click', () => open({ focus: button.dataset.group ?? null }));
  }

  docked.addEventListener('change', followWidth);
  pushed.addEventListener('change', followWidth);

  return { open, fold, isOpen, state, attach, detach };
}
