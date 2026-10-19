/**
 * The filter dock of a desktop: from 64rem the filters live in a column at the left edge of the screen instead of a bar above the
 * cabinet. The column is folded to a narrow rail of symbols, or open as a panel that holds the search field, the count and the
 * choices. From 90rem the open panel stands beside the cabinet and pushes it aside, as a plain neighbour that keeps the reader's
 * place; below that it lies over the cabinet as a modal fly-over that every close route puts away in one step. The state is one
 * attribute on the root element, which the style sheet reads; this module sets that attribute, the fly-over's dialog semantics and
 * focus. A small head script sets the attribute before the first paint from the choice the device remembers, so the dock's column is
 * there in the first frame; this module keeps that state and remembers the visitor's own open or fold at widths where the panel
 * stands beside the cabinet. Below 64rem the style sheet ignores the attribute and the slim bar and its sheet are in use.
 */
import { createHistoryStep } from './card-flow.js';
import { PANEL_FOLDED, PANEL_OPEN, readPanel, writePanel } from './filter-state.js';

/** The media query from which the dock is in use; below it the page has the slim bar and the Filters sheet. */
const DOCK_QUERY = '(min-width: 64rem)';

/**
 * The media query from which the open panel stands beside the cabinet; below it the open panel lies over the cabinet. The head
 * script holds the same query, and a test keeps the two equal.
 */
export const PUSH_QUERY = '(min-width: 90rem)';

/** The media query that says the visitor prefers less motion. */
const REDUCED_MOTION = '(prefers-reduced-motion: reduce)';

/** The state of a folded dock: the rail of symbols. */
const RAIL = 'rail';

/** The state of a panel open beside the cabinet. */
const PUSH = 'push';

/** The state of a panel open over the cabinet. */
const OVER = 'over';

/** The remembered choice of a visitor who left the panel open at a push width. */
export const REMEMBER_OPEN = PANEL_OPEN;

/** The remembered choice of a visitor who folded the panel, and the choice of a first visit. */
export const REMEMBER_FOLDED = PANEL_FOLDED;

/**
 * How long, in milliseconds, a press outside the fly-over keeps waiting for the click of its own gesture before it gives up, so a
 * gesture that ends without a click never leaves anything that could eat a later tap.
 */
export const CLICK_SWALLOW_MS = 400;

/** The parts of the page outside the dock that a fly-over makes inert. */
const OUTSIDE_PARTS = 'body > .skip-link, body > header, body > main, body > footer';

/** The elements inside the panel that can take focus from the keyboard. */
const FOCUSABLE = 'a[href], button, input, select, textarea, [tabindex]';

/**
 * Gives the device's local storage, where the open or folded panel is remembered, or null when the browser does not let the page
 * have it.
 * @returns {Storage | null}
 */
function deviceStorage() {
  try {
    return window.localStorage;
  } catch {
    return null;
  }
}

/**
 * Tells which state the dock lands in for a width: none below 64rem, the rail at fly-over widths whatever is remembered, and from
 * 90rem the panel pushed open when the visitor left it open there, otherwise the rail. A fly-over is never a landing state: it only
 * exists because the visitor opened it.
 * @param {{ wide: boolean, push: boolean, remembered: string | null }} width Whether the 64rem and 90rem queries match, and the
 * remembered choice.
 * @returns {'rail' | 'push' | null}
 */
export function dockStateFor({ wide, push, remembered }) {
  if (!wide) {
    return null;
  }

  if (!push) {
    return RAIL;
  }

  return remembered === REMEMBER_OPEN ? PUSH : RAIL;
}

/**
 * Picks the thing whose place on screen is kept while the pushed panel resizes the cabinet: the focused box when it is in view,
 * otherwise the box nearest below the viewport's top edge (its top at or below that edge), otherwise the cabinet itself.
 * @template {{ top: number, bottom: number }} T
 * @param {object} reading Where things are, in viewport coordinates.
 * @param {T | null} [reading.focused] The focused box, or null when focus is not on a box.
 * @param {T[]} reading.boxes The boxes of the cabinet.
 * @param {number} [reading.viewportTop] The top edge of the viewport.
 * @param {number} [reading.viewportBottom] The bottom edge of the viewport.
 * @param {T | null} [reading.cabinet] The cabinet, the answer when no box will do.
 * @returns {T | null}
 */
export function pickAnchor({ focused = null, boxes, viewportTop = 0, viewportBottom = Number.POSITIVE_INFINITY, cabinet = null }) {
  if (focused !== null && focused.bottom > viewportTop && focused.top < viewportBottom) {
    return focused;
  }

  let best = null;

  for (const box of boxes) {
    if (box.top >= viewportTop && (best === null || box.top < best.top)) {
      best = box;
    }
  }

  return best ?? cabinet;
}

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
 * Tells whether an element is drawn and can take focus from the keyboard: not disabled, not taken out of the tab order, rendered and
 * visible, and for a radio the one stop of its group (the checked radio, or the first of a group with none checked).
 * @param {HTMLElement} element The element.
 * @returns {boolean}
 */
function isTabStop(element) {
  if (element.disabled || element.tabIndex < 0 || element.getClientRects().length === 0) {
    return false;
  }

  if (window.getComputedStyle(element).visibility === 'hidden') {
    return false;
  }

  if (element instanceof HTMLInputElement && element.type === 'radio' && !element.checked) {
    const group = [...document.querySelectorAll(`input[type="radio"][name="${CSS.escape(element.name)}"]`)];

    return group[0] === element && !group.some((radio) => radio.checked);
  }

  return true;
}

/**
 * Tells whether two controls are the same tab stop: the same element, or two radios of one group.
 * @param {Element | null} one The first control.
 * @param {Element | null} other The second control.
 * @returns {boolean}
 */
function sameStop(one, other) {
  if (one === other) {
    return true;
  }

  return one instanceof HTMLInputElement && other instanceof HTMLInputElement && one.type === 'radio' && other.type === 'radio' && one.name === other.name;
}

/**
 * Starts the filter dock.
 * @param {object} parts What the dock needs from the page.
 * @param {HTMLElement} parts.rail The folded rail.
 * @param {HTMLElement} parts.panel The open panel.
 * @param {HTMLElement} [parts.root] The element that carries the dock's state; the root element of the page by default.
 * @param {HTMLInputElement | null} [parts.field] The search field, which takes focus when the panel opens for a search.
 * @param {HTMLElement | null} [parts.fallbackFocus] Where focus goes when the panel had it and the dock goes away with the width.
 * @param {HTMLElement | null} [parts.cabinet] The element the cabinet is drawn in, whose boxes keep their place while the pushed
 * panel resizes it.
 * @param {Storage | null} [parts.storage] The device's local storage, where the open or folded panel is remembered.
 * @returns {{ open: (how?: { focus?: string | null }) => void, fold: (how?: { restoreFocus?: boolean, instant?: boolean }) => void, isOpen: () => boolean, state: () => string | null, show: () => void, hide: () => void }}
 * The controls: open the panel (moving focus to the head button, the search field or one group's control), fold it back to the
 * rail, whether it is open, the current state, and reserving or giving up the dock's column when the page has a cabinet or not.
 */
export function initFilterDock({ rail, panel, root = document.documentElement, field = null, fallbackFocus = null, cabinet = document.getElementById('cabinet'), storage = deviceStorage() }) {
  const railMenu = rail.querySelector('.filter-menu');
  const headMenu = panel.querySelector('.filter-menu');
  const docked = window.matchMedia(DOCK_QUERY);
  const pushed = window.matchMedia(PUSH_QUERY);
  const steps = createHistoryStep({
    history: window.history,
    setTimer: (callback, delay) => window.setTimeout(callback, delay),
    clearTimer: (timer) => window.clearTimeout(timer),
    marker: 'dock',
  });

  let foldTimer = 0;
  let remembered = readPanel(storage);
  let modal = false;
  let madeInert = [];
  let disarmSwallow = () => {};

  steps.clearStale(window.history.state);

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
   * Remembers the visitor's own open or fold at a push width: in memory, and on the device when its storage lets the page write. A
   * write the storage refuses still changes the choice in memory, so the panel keeps working for the rest of the visit.
   * @param {'open' | 'folded'} choice The choice.
   */
  function remember(choice) {
    remembered = choice;
    writePanel(storage, choice);
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
   * Reads where the anchor of the reader's place is: the focused box in view, the box nearest below the viewport top, or the cabinet.
   * @returns {{ element: Element, top: number, bottom: number } | null}
   */
  function readAnchor() {
    if (cabinet === null) {
      return null;
    }

    const measure = (element) => {
      const rect = element.getBoundingClientRect();

      return { element, top: rect.top, bottom: rect.bottom, height: rect.height };
    };
    const focusedBox = document.activeElement?.closest?.('.placement') ?? null;
    const boxes = [...cabinet.querySelectorAll('.placement')].map(measure).filter((box) => box.height > 0);

    return pickAnchor({
      focused: focusedBox !== null && cabinet.contains(focusedBox) ? measure(focusedBox) : null,
      boxes,
      viewportTop: 0,
      viewportBottom: window.innerHeight,
      cabinet: measure(cabinet),
    });
  }

  /**
   * Makes a change that resizes the cabinet while keeping the reader's place: the anchor is read before the change and the page is
   * scrolled at once, in the same task, by how far the anchor's top edge moved, so it keeps its offset in the viewport.
   * @param {() => void} change The change to the dock.
   */
  function keepingPlace(change) {
    const anchor = readAnchor();

    change();

    if (anchor === null) {
      return;
    }

    const moved = anchor.element.getBoundingClientRect().top - anchor.top;

    if (Math.abs(moved) >= 0.5) {
      window.scrollBy({ top: moved, behavior: 'instant' });
    }
  }

  /**
   * Lists the panel's tab stops in document order.
   * @returns {HTMLElement[]}
   */
  function tabStops() {
    return [...panel.querySelectorAll(FOCUSABLE)].filter(isTabStop);
  }

  /**
   * Keeps Tab and Shift+Tab inside the fly-over: from its last stop Tab goes to its first, and from its first Shift+Tab goes to its
   * last. Focus found outside the panel is brought back into it.
   * @param {KeyboardEvent} event The Tab key.
   */
  function wrapTab(event) {
    const stops = tabStops();
    const active = document.activeElement;

    if (stops.length === 0) {
      event.preventDefault();

      return;
    }

    const first = stops[0];
    const last = stops[stops.length - 1];

    if (!panel.contains(active)) {
      event.preventDefault();
      (event.shiftKey ? last : first).focus();
    } else if (event.shiftKey && sameStop(active, first)) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && sameStop(active, last)) {
      event.preventDefault();
      first.focus();
    }
  }

  /**
   * Handles the keys of an open fly-over: Escape folds it, unless something inside already used the key (the search field empties
   * its text first), and Tab stays inside it.
   * @param {KeyboardEvent} event The key.
   */
  function onModalKey(event) {
    if (event.key === 'Escape') {
      if (!event.defaultPrevented) {
        event.preventDefault();
        fold();
      }

      return;
    }

    if (event.key === 'Tab' && !event.altKey && !event.ctrlKey && !event.metaKey) {
      wrapTab(event);
    }
  }

  /**
   * Swallows the click of the gesture whose press folded the fly-over, so the box under the pointer opens no card. It waits for one
   * click only: it gives up when the gesture is cancelled, one task after the pointer is released (the click of a tap comes right
   * after the release, a drag that ends without a click leaves nothing behind), and in any case after a short while.
   */
  function armSwallow() {
    disarmSwallow();

    let releaseTimer = 0;

    const swallow = (event) => {
      event.preventDefault();
      event.stopPropagation();
      disarm();
    };
    const released = () => {
      releaseTimer = window.setTimeout(disarm, 0);
    };
    const giveUpTimer = window.setTimeout(() => disarm(), CLICK_SWALLOW_MS);

    /**
     * Removes every listener of the swallower and stops its timers.
     */
    function disarm() {
      window.clearTimeout(giveUpTimer);
      window.clearTimeout(releaseTimer);
      document.removeEventListener('click', swallow, true);
      document.removeEventListener('pointerup', released, true);
      document.removeEventListener('pointercancel', disarm, true);
      disarmSwallow = () => {};
    }

    document.addEventListener('click', swallow, true);
    document.addEventListener('pointerup', released, true);
    document.addEventListener('pointercancel', disarm, true);
    disarmSwallow = disarm;
  }

  /**
   * Folds the fly-over on a press anywhere outside the panel and keeps that press from reaching the page. A press on the page's own
   * scrollbars is left alone, so the visitor can scroll the cabinet behind the panel.
   * @param {PointerEvent} event The press.
   */
  function onPressOutside(event) {
    if (event.target instanceof Node && panel.contains(event.target)) {
      return;
    }

    if (event.clientX >= root.clientWidth || event.clientY >= root.clientHeight) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    armSwallow();
    fold();
  }

  /**
   * Makes the open fly-over a modal dialog: the panel takes the dialog role, everything outside the dock and the rail becomes inert,
   * and the keys, the press outside and Back are listened for. The dock itself keeps its search role and is never inert.
   */
  function becomeModal() {
    if (modal) {
      return;
    }

    modal = true;
    panel.setAttribute('role', 'dialog');
    panel.setAttribute('aria-modal', 'true');
    panel.setAttribute('aria-labelledby', 'filter-panel-title');
    madeInert = [...document.querySelectorAll(OUTSIDE_PARTS), rail].filter((element) => !element.hasAttribute('inert'));

    for (const element of madeInert) {
      element.setAttribute('inert', '');
    }

    document.addEventListener('keydown', onModalKey);
    document.addEventListener('pointerdown', onPressOutside, true);
    steps.beforeOpen().then(() => {
      if (modal) {
        steps.opened();
      }
    });
  }

  /**
   * Ends the fly-over's modality: the dialog role, `aria-modal` and every `inert` it set are removed, its listeners go, and its
   * history step is removed unless Back already did that.
   */
  function endModal() {
    if (!modal) {
      return;
    }

    modal = false;
    panel.removeAttribute('role');
    panel.removeAttribute('aria-modal');
    panel.removeAttribute('aria-labelledby');

    for (const element of madeInert) {
      element.removeAttribute('inert');
    }

    madeInert = [];
    document.removeEventListener('keydown', onModalKey);
    document.removeEventListener('pointerdown', onPressOutside, true);
    steps.closedHere();
  }

  /**
   * Finds the control that takes focus when the panel opens for a given reason.
   * @param {string | null} focus Why the panel opens: `menu` for the three-line button, `search` or the name of a group.
   * @param {string} next The state the panel opens in.
   * @returns {HTMLElement | null}
   */
  function focusTarget(focus, next) {
    if (focus === 'menu') {
      return next === PUSH ? headMenu : field;
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
   * Opens the panel: beside the cabinet from 90rem, keeping the reader's place, or over it below that as a modal fly-over. Focus
   * moves to the control the reason names; for a search the field's text is selected so typing replaces it. When that control
   * cannot take focus (switched off because the card details failed) focus goes into the panel all the same: to the search field
   * over the cabinet, to the head button beside it. An open at a push width is remembered on the device; an open over the cabinet is
   * not. Nothing happens below 64rem or while the page has no dock.
   * @param {{ focus?: string | null }} [how] Why the panel opens.
   */
  function open({ focus = null } = {}) {
    if (!docked.matches || state() === null) {
      return;
    }

    cancelFold();

    const next = pushed.matches ? PUSH : OVER;

    if (next === PUSH) {
      remember(REMEMBER_OPEN);
    }

    if (state() !== next) {
      if (next === PUSH) {
        keepingPlace(() => {
          root.dataset.filterDock = PUSH;
        });
      } else {
        root.dataset.filterDock = OVER;
      }
    }

    if (next === OVER) {
      becomeModal();
    }

    const wanted = focusTarget(focus, next);
    const usable = wanted !== null && !wanted.disabled && wanted.getClientRects().length > 0;
    let target = usable ? wanted : null;

    if (target === null && (next === OVER || focus !== null)) {
      target = next === OVER ? field : headMenu;
    }

    if (target === null) {
      return;
    }

    target.focus({ preventScroll: target === headMenu });

    if (target === field && focus === 'search') {
      field.select();
    }
  }

  /**
   * Ends a fold: the dock is the rail again.
   */
  function finishFold() {
    cancelFold();
    root.dataset.filterDock = RAIL;
  }

  /**
   * Folds the panel back to the rail. A pushed panel folds at once, keeping the reader's place, and the fold is remembered on the
   * device. A fly-over first stops being modal, then slides out over the closing time, unless the visitor prefers less motion or the
   * fold is asked to be instant. Focus goes to the rail's three-line button.
   * @param {{ restoreFocus?: boolean, instant?: boolean }} [how] Whether focus moves to the rail's three-line button, and whether
   * the fold skips the slide.
   */
  function fold({ restoreFocus = true, instant = false } = {}) {
    if (!isOpen() || panel.hasAttribute('data-folding')) {
      return;
    }

    if (state() === PUSH) {
      if (pushed.matches) {
        remember(REMEMBER_FOLDED);
      }

      keepingPlace(finishFold);

      if (restoreFocus) {
        railMenu.focus({ preventScroll: true });
      }

      return;
    }

    endModal();

    if (restoreFocus) {
      railMenu.focus({ preventScroll: true });
    }

    if (instant || window.matchMedia(REDUCED_MOTION).matches) {
      finishFold();

      return;
    }

    panel.dataset.folding = '';
    foldTimer = window.setTimeout(finishFold, tokenMs(root, '--fallback-close'));
  }

  /**
   * Reserves the dock's column: the page has a cabinet, so the dock stands at the left edge in the state the width and the remembered
   * choice ask for. A state already set is kept, so a live redraw never folds an open panel.
   */
  function show() {
    if (state() === null) {
      root.dataset.filterDock = dockStateFor({ wide: docked.matches, push: pushed.matches, remembered }) ?? RAIL;
    }
  }

  /**
   * Gives up the dock's column: the page shows a message instead of a cabinet (before the first sync, an empty collection, a failed
   * load), so the column collapses in the same task that draws the message. An open fly-over stops being modal first.
   */
  function hide() {
    endModal();
    cancelFold();
    delete root.dataset.filterDock;
  }

  /**
   * Lands the dock in the state the new width asks for when the window crosses 64rem or 90rem: an open fly-over closes at once and
   * its history step is removed, a pushed panel folds on narrowing, and from 90rem a panel the visitor left open there opens again.
   * The remembered choice is never changed here. Focus that was inside a panel that folded goes to the rail's three-line button
   * while the dock stays, or to the given fallback when the dock goes; anywhere else it stays where it is. This runs before the
   * Filters sheet moves the controls on the same width change (its query list is created first), so focus is read before a moved
   * control loses it.
   */
  function followWidth() {
    const current = state();

    if (current === null) {
      return;
    }

    const next = dockStateFor({ wide: docked.matches, push: pushed.matches, remembered }) ?? RAIL;
    const folding = panel.hasAttribute('data-folding');

    if (current === next && !folding) {
      return;
    }

    const hadFocus = panel.contains(document.activeElement);

    endModal();
    cancelFold();
    root.dataset.filterDock = next;

    if (!hadFocus || next === PUSH) {
      return;
    }

    if (docked.matches) {
      railMenu.focus({ preventScroll: true });
    } else {
      fallbackFocus?.focus({ preventScroll: true });
    }
  }

  /**
   * Lands the dock at start. The head script has already set the state before the first paint; it is kept when it is the state the
   * width and the remembered choice ask for, and put right otherwise, such as when the head script did not run.
   */
  function land() {
    const next = dockStateFor({ wide: docked.matches, push: pushed.matches, remembered }) ?? RAIL;

    if (state() !== next) {
      root.dataset.filterDock = next;
    }
  }

  land();

  railMenu.addEventListener('click', () => open({ focus: 'menu' }));
  headMenu.addEventListener('click', () => fold());

  for (const button of rail.querySelectorAll('.filter-rail-button')) {
    button.addEventListener('click', () => open({ focus: button.dataset.group ?? null }));
  }

  window.addEventListener('popstate', () => {
    if (steps.popped() === 'close' && modal) {
      fold();
    }
  });

  docked.addEventListener('change', followWidth);
  pushed.addEventListener('change', followWidth);

  return { open, fold, isOpen, state, show, hide };
}
