/**
 * The Filters sheet of a phone or tablet: below 64rem the bar is a slim bar with the search field and a Filters button, and the
 * choices (the view switch, players, time and location) live in a bottom sheet over a clear backdrop, so the dimmed cabinet stays
 * visible above them while the visitor chooses. There is one set of controls; this module moves each one between its slot in the
 * full bar and its slot in the sheet when the width crosses 64rem. The sheet closes the way the detail card's sheet does: its
 * primary button, Escape, a tap on the cabinet, a drag down from its grip strip, and Back.
 */
import { createHistoryStep, dragOutcome, recentSamples, releaseSpeed, resist } from './card-flow.js';

/** The media query that says the full bar is in use; below it the controls live in the sheet. */
const FULL_BAR = '(min-width: 64rem)';

/** The media query that says the visitor prefers less motion. */
const REDUCED_MOTION = '(prefers-reduced-motion: reduce)';

/** The gap, in pixels, between the bottom of the bar and the top of the box the page scrolls to. */
const REVEAL_GAP_PX = 16;

/** How far, in pixels, the page must travel in one direction before the slim bar slides away or comes back. */
export const SLIDE_TRAVEL_PX = 8;

/** The boxes that can be lit: every box but the marker that counts hidden expansions. */
const LIT_BOXES = '.placement:not([data-dim]):not([data-kind="moreMarker"])';

/**
 * Picks the box the page scrolls to when the sheet closes with its matches out of sight: the first lit box in reading order that
 * lies below the bar at the current scroll position, otherwise the first lit box of all.
 * @param {Array<{ top: number }>} rects The lit boxes in reading order, each with its top edge in page coordinates.
 * @param {number} scrollTop The vertical scroll position of the page.
 * @param {number} barBottom The bottom edge of the bar, in pixels from the top of the viewport.
 * @returns {{ top: number } | null} The box to scroll to, or null when no box is lit.
 */
export function nextLitTarget(rects, scrollTop, barBottom) {
  if (rects.length === 0) {
    return null;
  }

  const edge = scrollTop + barBottom;

  return rects.find((rect) => rect.top >= edge) ?? rects[0];
}

/**
 * Decides what the slim bar does after the page scrolled: it slides away after more than a few pixels of travel down, comes back
 * after more than a few pixels of travel up, and otherwise stays as it is. It never slides away while something blocks it, or while
 * the page is within one bar height of the top, and a bar that is away comes back as soon as one of those holds.
 * @param {object} state What the decision depends on.
 * @param {number} state.lastY The scroll position the travel is measured from.
 * @param {number} state.y The scroll position now.
 * @param {boolean} state.away Whether the bar is away now.
 * @param {boolean} state.blocked Whether a filter is in use, focus is in the bar, or the sheet or a card is open.
 * @param {number} state.barHeight The height of the bar.
 * @returns {'away' | 'back' | 'stay'} What the bar does next.
 */
export function slideDecision({ lastY, y, away, blocked, barHeight }) {
  if (blocked || y < barHeight) {
    return away ? 'back' : 'stay';
  }

  const travel = y - lastY;

  if (travel > SLIDE_TRAVEL_PX) {
    return 'away';
  }

  return travel < -SLIDE_TRAVEL_PX ? 'back' : 'stay';
}

/**
 * Starts the Filters sheet.
 * @param {object} parts What the sheet needs from the page.
 * @param {HTMLElement} parts.bar The filter bar.
 * @param {HTMLDialogElement} parts.dialog The sheet's dialog.
 * @param {{ view: HTMLElement | null, players: HTMLElement | null, time: HTMLElement | null, location: HTMLElement | null, note: HTMLElement | null }} parts.units
 * The movable units of the control set.
 * @param {HTMLElement} parts.openButton The Filters button of the slim bar.
 * @param {HTMLElement | null} parts.cabinet The element the cabinet is drawn in, whose lit boxes the page scrolls to.
 * @param {object} parts.copy The visitor-facing strings of the page language.
 * @param {() => { lit: number, total: number, active: boolean }} parts.getCount Gives how many games stand in the light, how many
 * there are, and whether a filter is in use.
 * @returns {{ open: () => Promise<void>, close: (how?: { instant?: boolean }) => void, isOpen: () => boolean, refresh: () => void, showBar: () => void }}
 * The controls: open the sheet, close it, whether it is open, write its button and Clear for the current result, and bring the slim
 * bar back into view if it slid away.
 */
export function initFilterSheet({ bar, dialog, units, openButton, cabinet, copy, getCount }) {
  const sheet = dialog.querySelector('.filter-sheet');
  const slot = dialog.querySelector('.filter-sheet-groups');
  const done = dialog.querySelector('.filter-done');
  const sheetClear = dialog.querySelector('.filter-sheet-clear');
  const rows = [...bar.querySelectorAll('.filter-row')];
  const status = bar.querySelector('.filter-status');
  const full = window.matchMedia(FULL_BAR);
  const steps = createHistoryStep({
    history: window.history,
    setTimer: (callback, delay) => window.setTimeout(callback, delay),
    clearTimer: (timer) => window.clearTimeout(timer),
    marker: 'filters',
  });

  let shown = false;
  let closing = false;
  let revealPending = false;
  let pressedOnFrame = false;
  let drag = null;

  steps.clearStale(window.history.state);

  /**
   * Gives the length of a timing token of the page in milliseconds, so the script and the style sheet never disagree.
   * @param {string} name The custom property, such as `--fallback-close`.
   * @returns {number}
   */
  function tokenMs(name) {
    const raw = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
    const value = Number.parseFloat(raw);

    if (!Number.isFinite(value)) {
      return 0;
    }

    return raw.endsWith('ms') ? value : value * 1000;
  }

  /**
   * Tells whether the visitor prefers less motion.
   * @returns {boolean}
   */
  function reducedMotion() {
    return window.matchMedia(REDUCED_MOTION).matches;
  }

  /**
   * Puts every unit where the current width wants it: each one on its own into the sheet's slot in the order view, players, time,
   * location, note, or back into its place in the bar against an anchor that never moves, so the bar's order holds after any
   * number of moves.
   */
  function place() {
    const { view, players, time, location, note } = units;

    if (!full.matches) {
      slot.append(...[view, players, time, location, note].filter((unit) => unit !== null));

      return;
    }

    const [first, second] = rows;

    if (view !== null) {
      first.prepend(view);
    }

    if (players !== null) {
      first.append(players);
    }

    if (time !== null) {
      second.prepend(time);
    }

    if (location !== null) {
      second.insertBefore(location, status);
    }

    if (note !== null) {
      bar.after(note);
    }
  }

  /**
   * Writes the sheet's primary button and its Clear for the current result: the number of games that stay in the light, or the
   * close string when nothing matches, in which case the button is outlined and Clear is the highlighted action.
   */
  function refresh() {
    const { lit, total, active } = getCount();
    const none = active && lit === 0;

    done.textContent = none ? copy.close : copy.showGames(active ? lit : total);
    done.toggleAttribute('data-outlined', none);
    sheetClear.hidden = !active;
    sheetClear.toggleAttribute('data-highlighted', none);
  }

  /**
   * Finds the control that takes focus when the sheet opens: the view switch's chosen radio when it is shown, otherwise the chosen
   * radio of the first group that can be used, and the primary button when none can.
   * @returns {HTMLElement}
   */
  function firstControl() {
    const select = units.location?.querySelector('select') ?? null;
    const candidates = [
      units.view !== null && !units.view.hidden ? units.view.querySelector('input:checked') : null,
      units.players?.querySelector('input:checked') ?? null,
      units.time?.querySelector('input:checked') ?? null,
      units.location !== null && !units.location.hidden ? select : null,
    ];

    return candidates.find((control) => control !== null && !control.disabled) ?? done;
  }

  /**
   * Opens the sheet over the cabinet, takes the one history step and moves focus to the first control. Focus is released first, so
   * the browser has nothing to scroll back to when the dialog closes; the sheet returns focus itself. Nothing happens in the full
   * bar, where there is no sheet, or while the sheet is already open.
   * @returns {Promise<void>}
   */
  async function open() {
    if (dialog.open || full.matches) {
      return;
    }

    await steps.beforeOpen();

    if (dialog.open || full.matches) {
      return;
    }

    refresh();
    document.activeElement?.blur();
    dialog.showModal();
    shown = true;
    steps.opened();
    firstControl().focus({ preventScroll: true });
  }

  /**
   * Scrolls the page, instantly, so the next lit box in reading order sits a little under the bar, but only when a filter is in use,
   * something matches and no lit box is in view below the bar already. Boxes never move; only the page scrolls.
   */
  function revealMatches() {
    const { lit, active } = getCount();

    if (!active || lit === 0 || cabinet === null) {
      return;
    }

    const barBottom = Math.max(0, bar.getBoundingClientRect().bottom);
    const boxes = [...cabinet.querySelectorAll(LIT_BOXES)].map((element) => ({ element, rect: element.getBoundingClientRect() }));
    const visible = boxes.some(({ rect }) => rect.height > 0 && rect.bottom > barBottom && rect.top < window.innerHeight);

    if (visible) {
      return;
    }

    const target = nextLitTarget(boxes.map(({ element, rect }) => ({ element, top: rect.top + window.scrollY })), window.scrollY, barBottom);

    if (target === null) {
      return;
    }

    const pinned = window.getComputedStyle(bar).position === 'sticky' ? bar.offsetHeight : 0;

    window.scrollTo({ top: Math.max(0, target.top - pinned - REVEAL_GAP_PX), behavior: 'instant' });
  }

  /**
   * Ends a close: takes away every mark the close or a drag left on the sheet, removes the history step when the close did not come
   * from Back, gives focus back to the Filters button and, after a close through the primary button, shows the matches. It does
   * its work once per open, whichever way the dialog closed. The matches are shown on the next frame, after the browser has finished
   * returning focus to where it was, which can move the page.
   */
  function settle() {
    if (!shown) {
      return;
    }

    shown = false;
    closing = false;
    drag = null;
    delete dialog.dataset.closing;
    delete sheet.dataset.moving;
    delete sheet.dataset.springing;
    sheet.style.removeProperty('--sheet-dy');
    steps.closedHere();
    openButton.focus({ preventScroll: true });

    if (revealPending) {
      revealPending = false;
      window.requestAnimationFrame(revealMatches);
    }
  }

  /**
   * Closes the dialog and settles.
   */
  function finish() {
    if (dialog.open) {
      dialog.close();
    }

    settle();
  }

  /**
   * Closes the sheet. By default it slides out over the card sheet's closing time and then closes; with `instant`, and for a visitor
   * who prefers less motion, it closes at once. Asking again while it slides out changes nothing.
   * @param {{ instant?: boolean, reveal?: boolean }} [how] Whether to close at once, and whether the page then shows the matches.
   */
  function close({ instant = false, reveal = false } = {}) {
    if (!dialog.open || closing) {
      return;
    }

    revealPending = revealPending || reveal;

    if (instant || reducedMotion()) {
      finish();

      return;
    }

    closing = true;
    dialog.dataset.closing = '';
    window.setTimeout(finish, tokenMs('--fallback-close'));
  }

  /**
   * Starts a drag of the sheet when a press lands on its grip strip and nothing else is moving.
   * @param {PointerEvent} event The press.
   */
  function startDrag(event) {
    if (drag !== null || closing || !dialog.open || !event.isPrimary || event.button !== 0 || event.target.closest('.filter-grip') === null) {
      return;
    }

    const grip = event.target.closest('.filter-grip');

    if (sheet.hasAttribute('data-springing')) {
      return;
    }

    try {
      grip.setPointerCapture(event.pointerId);
    } catch {
      return;
    }

    sheet.dataset.moving = '';
    drag = {
      pointerId: event.pointerId,
      startY: event.clientY,
      offset: 0,
      samples: [{ at: event.timeStamp, y: event.clientY }],
    };
  }

  /**
   * Moves the sheet with the finger and keeps the last moments of the movement, so the speed at release can be told.
   * @param {PointerEvent} event The movement.
   */
  function moveDrag(event) {
    if (drag === null || event.pointerId !== drag.pointerId) {
      return;
    }

    drag.offset = resist(event.clientY - drag.startY);
    sheet.style.setProperty('--sheet-dy', drag.offset + 'px');
    drag.samples.push({ at: event.timeStamp, y: event.clientY });
    drag.samples = recentSamples(drag.samples, event.timeStamp);
  }

  /**
   * Lets the sheet go back to rest: over the spring token's length, or at once for a visitor who prefers less motion.
   */
  function springBack() {
    if (reducedMotion()) {
      sheet.style.setProperty('--sheet-dy', '0px');
      delete sheet.dataset.moving;

      return;
    }

    sheet.dataset.springing = '';
    delete sheet.dataset.moving;
    sheet.style.setProperty('--sheet-dy', '0px');
    window.setTimeout(() => {
      delete sheet.dataset.springing;
    }, tokenMs('--sheet-spring'));
  }

  /**
   * Ends a drag: a long or fast enough pull down closes the sheet from where the finger left it, and anything else, including a
   * cancelled press, lets it spring back.
   * @param {PointerEvent} event The release or the cancel.
   */
  function endDrag(event) {
    if (drag === null || event.pointerId !== drag.pointerId) {
      return;
    }

    const { offset } = drag;
    const outcome = event.type === 'pointerup'
      ? dragOutcome({ dy: offset, height: sheet.getBoundingClientRect().height, velocity: releaseSpeed(drag.samples, { at: event.timeStamp, y: event.clientY }) })
      : 'spring';

    drag = null;

    if (!dialog.open) {
      return;
    }

    if (outcome === 'close') {
      delete sheet.dataset.moving;
      close();
    } else {
      springBack();
    }
  }

  let slideAnchor = 0;

  /**
   * Tells whether something keeps the slim bar in view: a filter in use, focus inside the bar, or a sheet or card on screen.
   * @returns {boolean}
   */
  function slideBlocked() {
    return getCount().active || bar.contains(document.activeElement) || document.querySelector('dialog[open]') !== null;
  }

  /**
   * Brings the slim bar back into view and measures later travel from where the page is now.
   */
  function showBar() {
    bar.removeAttribute('data-away');
    slideAnchor = window.scrollY;
  }

  /**
   * Slides the slim bar away or back after the page scrolled. Travel is measured from the highest point reached while the bar is in
   * view and from the lowest while it is away, so many small scroll steps in one direction add up.
   */
  function followScroll() {
    const y = Math.max(0, window.scrollY);
    const away = bar.hasAttribute('data-away');
    const decision = slideDecision({ lastY: slideAnchor, y, away, blocked: slideBlocked(), barHeight: bar.offsetHeight });

    if (decision === 'stay') {
      slideAnchor = away ? Math.max(slideAnchor, y) : Math.min(slideAnchor, y);

      return;
    }

    slideAnchor = y;
    bar.toggleAttribute('data-away', decision === 'away');
  }

  /**
   * Listens for scrolling while the slim bar is in use, with the one passive listener, and lets go of it in the full bar, which stays
   * pinned and never slides.
   */
  function watchScroll() {
    if (full.matches) {
      window.removeEventListener('scroll', followScroll);
      bar.removeAttribute('data-away');

      return;
    }

    slideAnchor = window.scrollY;
    window.addEventListener('scroll', followScroll, { passive: true });
  }

  place();
  refresh();
  watchScroll();

  full.addEventListener('change', () => {
    close({ instant: true });
    place();
    watchScroll();
  });

  openButton.addEventListener('click', open);
  done.addEventListener('click', () => close({ reveal: true }));

  dialog.addEventListener('cancel', (event) => {
    event.preventDefault();
    close();
  });

  dialog.addEventListener('close', settle);

  window.addEventListener('popstate', () => {
    if (steps.popped() === 'close' && dialog.open) {
      close();
    }
  });

  dialog.addEventListener('pointerdown', (event) => {
    pressedOnFrame = event.target === dialog;
    startDrag(event);
  });

  dialog.addEventListener('pointermove', moveDrag);
  dialog.addEventListener('pointerup', endDrag);
  dialog.addEventListener('pointercancel', endDrag);

  dialog.addEventListener('click', (event) => {
    if (event.target === dialog && pressedOnFrame) {
      close();
    }

    pressedOnFrame = false;
  });

  document.addEventListener('cabinet:filtered', () => {
    refresh();

    if (getCount().active) {
      showBar();
    }
  });

  return { open, close, isOpen: () => dialog.open, refresh, showBar };
}
