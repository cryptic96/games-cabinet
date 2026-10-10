/**
 * The detail card's dialog: a tap on any box opens one native modal dialog holding that game's card, and closing it puts focus back
 * on the box that opened it. The card is built from data already in memory, so opening it never waits for the network.
 */
import { buildCard } from './card-view.js';
import { choosePath, createCloseGate, createHistoryStep, dragOutcome, isMostlyOnScreen, pullKind, resist, sourceEntry } from './card-flow.js';

/** The media query that says the visitor prefers less motion. */
const REDUCED_MOTION = '(prefers-reduced-motion: reduce)';

/** The media query that makes the card a sheet at the bottom of the screen. */
const PHONE = '(max-width: 40rem)';

/** How far back, in milliseconds, the speed of a drag is measured. */
const SPEED_WINDOW_MS = 80;

/** The parts of the sheet a drag may start on. */
const DRAG_ZONES = '.card-grip, .card-head';

/** The elements inside a drag zone that keep their own press. */
const PRESSABLE = 'a, button, input, select, textarea, summary, [role="button"], [role="link"]';

/** The longest the pull-out waits for the cover picture to decode, in milliseconds. */
const COVER_WAIT_MS = 200;

/** The timing token each fade the dialog can carry runs for. */
const FADES = {
  open: '--fallback-open',
  close: '--fallback-close',
  'reduced-open': '--card-fade-reduced',
  'reduced-close': '--card-fade-reduced',
};

/**
 * Wires the dialog to the cabinet.
 * @param {object} parts What the card needs from the page.
 * @param {HTMLDialogElement} parts.dialog The one dialog of the page.
 * @param {HTMLElement} parts.mount The element that holds the drawn cabinet.
 * @param {(entryId: string) => object | null} parts.getRecord Gives the card record of an entry, or null when nothing is known.
 * @param {() => object} parts.getCopy Gives the visitor-facing strings.
 * @param {() => object[]} [parts.getPalette] Gives the colour table of the layout on screen.
 * @param {string} [parts.iconsUrl] The address of the icon sprite.
 * @returns {{ openCard: Function, closeCard: Function, swapTo: Function, showsOtherGame: Function, isOpen: Function, whenClosed: Function }}
 * The controls.
 */
export function initCardDialog({ dialog, mount, getRecord, getCopy, getPalette = () => [], iconsUrl = '' }) {
  const steps = createHistoryStep({
    history: window.history,
    setTimer: (callback, delay) => window.setTimeout(callback, delay),
    clearTimer: (timer) => window.clearTimeout(timer),
  });
  let opener = null;
  let shownEntryId = null;
  let swapping = false;
  let opening = false;
  let moving = false;
  let closeWhenSettled = false;
  let pressedOnFrame = false;
  let warmedCover = null;
  let forcedPath = null;
  let drag = null;
  const gate = createCloseGate();

  steps.clearStale(window.history.state);

  /**
   * Finds the box that opened the card: the element itself while it is still on the page, otherwise the box with the same entry id
   * and kind in the cabinet as it was redrawn.
   * @returns {HTMLElement | null}
   */
  function findOpener() {
    if (opener === null) {
      return null;
    }

    if (opener.element !== undefined && opener.element.isConnected) {
      return opener.element;
    }

    return mount.querySelector('.placement[data-entry-id="' + CSS.escape(opener.entryId) + '"][data-kind="' + CSS.escape(opener.kind) + '"]');
  }

  /**
   * Gives what a card is built with: the sprite, the colour table on screen and the swap a row asks for.
   * @returns {{ iconsUrl: string, palette: object[], onSwap: Function }}
   */
  function cardOptions() {
    return { iconsUrl, palette: getPalette(), onSwap: swapTo };
  }

  /**
   * Gives the length of a timing token of the page in milliseconds, so the script and the style sheet never disagree.
   * @param {string} name The custom property, such as `--swap-fade`.
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
   * Waits for a number of milliseconds.
   * @param {number} ms How long to wait.
   * @returns {Promise<void>}
   */
  function pause(ms) {
    return new Promise((resolve) => window.setTimeout(resolve, ms));
  }

  /**
   * Shows another game in the card that is open, in place: the header and the ruled area fade out, are rebuilt from the other
   * game's record, scroll back to the top and fade in, and focus moves to the new title, which the dialog's name follows. No second
   * pull-out and no new history step, so Back still closes the whole card. Only entries the card data holds are shown.
   * @param {string | number} entryId The collection entry to show.
   * @returns {Promise<void>}
   */
  async function swapTo(entryId) {
    const card = dialog.querySelector('.card');
    const id = String(entryId);

    if (!dialog.open || swapping || card === null || id === shownEntryId) {
      return;
    }

    const record = getRecord(id);

    if (record === null || record === undefined) {
      return;
    }

    swapping = true;

    try {
      card.dataset.swap = '';
      await pause(tokenMs('--swap-fade'));

      if (!dialog.open || !card.isConnected) {
        return;
      }

      const next = buildCard(record, getCopy(), cardOptions());

      card.querySelector('.card-head').replaceWith(next.querySelector('.card-head'));
      card.querySelector('.card-ruled').replaceWith(next.querySelector('.card-ruled'));
      card.scrollTop = 0;
      shownEntryId = id;
      void getComputedStyle(card.querySelector('.card-ruled')).opacity;
      delete card.dataset.swap;
      card.querySelector('.card-title').focus({ preventScroll: true });
    } finally {
      swapping = false;

      if (card.isConnected) {
        delete card.dataset.swap;
      }
    }
  }

  /**
   * Tells whether the card shows another game than the box it was opened from, which happens after a swap.
   * @returns {boolean}
   */
  function showsOtherGame() {
    return dialog.open && opener !== null && shownEntryId !== null && shownEntryId !== opener.entryId;
  }

  /**
   * Puts the card on screen as it stands: shows the dialog, takes the history step and moves focus to the title, or to the heading
   * of the owned expansions, scrolled to the top of the card at once, when the card opens at them.
   * @param {HTMLElement} card The card in the dialog.
   * @param {boolean} [atExpansions] Whether the card opens at its owned expansions.
   */
  function showCard(card, atExpansions = false) {
    if (!dialog.open) {
      dialog.showModal();
    }

    steps.opened();

    const heading = atExpansions ? card.querySelector('.card-expansions-title') : null;

    if (heading === null) {
      card.querySelector('.card-title').focus({ preventScroll: true });
    } else {
      heading.scrollIntoView({ block: 'start', behavior: 'instant' });
      heading.focus({ preventScroll: true });
    }
  }

  /**
   * Removes the marks a transition leaves on the page.
   * @param {HTMLElement | null} box The box that was pulled.
   * @param {HTMLElement | null} art The cover spot of the card.
   */
  function clearPullMarks(box, art) {
    const root = document.documentElement;

    box?.removeAttribute('data-pulling');
    art?.removeAttribute('data-pulling');
    delete root.dataset.pullKind;
    delete root.dataset.pullDir;
  }

  /**
   * Runs one view transition and waits until it has settled, whatever way it ends. The update step always runs, so a skipped or
   * failed transition still leaves the page in its final state; every promise the transition makes is observed so none of them
   * can report an unhandled rejection.
   * @param {() => void} update Puts the page into its new state.
   * @returns {Promise<void>}
   */
  async function runTransition(update) {
    let transition;

    try {
      transition = document.startViewTransition(update);
    } catch {
      update();

      return;
    }

    transition.ready.catch(() => {});
    transition.updateCallbackDone.catch(() => {});
    await transition.finished.catch(() => {});
  }

  /**
   * Starts decoding the cover picture of the game a box stands for, so it is ready by the time the tap completes. It is called on
   * press and on the keys that open a card, never on hover.
   * @param {HTMLElement} box The box that is being pressed.
   */
  function warmCover(box) {
    const { entryId } = sourceEntry(box.dataset.kind, box.dataset.entryId);
    const url = getRecord(String(entryId))?.cover?.url;

    if (typeof url !== 'string' || !url.startsWith('/art/') || warmedCover?.src.endsWith(url)) {
      return;
    }

    warmedCover = new Image();
    warmedCover.src = url;
    warmedCover.decode().catch(() => {});
  }

  /**
   * Waits until the cover picture of a card has decoded, but never longer than a moment: a picture that is not ready is left to
   * arrive on its own, with the edge colours showing meanwhile.
   * @param {HTMLElement} card The card in the closed dialog.
   * @returns {Promise<void>}
   */
  async function waitForCover(card) {
    const image = card.querySelector('.card-art > img');

    if (image === null) {
      return;
    }

    await Promise.race([image.decode().catch(() => {}), pause(COVER_WAIT_MS)]);
  }

  /**
   * Gives the size of the viewport the boxes are measured against.
   * @returns {{ width: number, height: number }}
   */
  function viewportSize() {
    return { width: document.documentElement.clientWidth, height: window.innerHeight };
  }

  /**
   * Finds the box a card is pulled out of and returned to: the box that was tapped while it is on the page, otherwise the box of
   * the same entry in the cabinet as it was redrawn. The marker that counts hidden expansions is never that box; its base game's
   * own box is, and so is the box of a card that was opened from the games list.
   * @returns {HTMLElement | null}
   */
  function findPullBox() {
    if (opener === null) {
      return null;
    }

    const { element, entryId } = opener;

    if (element !== undefined && element.isConnected && element.classList.contains('placement') && element.dataset.kind !== 'moreMarker') {
      return element;
    }

    return mount.querySelector('.placement[data-entry-id="' + CSS.escape(entryId) + '"]:not([data-kind="moreMarker"])');
  }

  /**
   * Chooses how the card opens or closes right now: the visitor's motion preference is read here, at every tap and every close.
   * @param {object} situation What is known now.
   * @param {HTMLElement | null} situation.box The box to fly from or to.
   * @param {HTMLElement | null} situation.art The cover spot to fly to or from, or null when it is not on screen yet.
   * @param {boolean} situation.swapped Whether the card shows another game than its source box.
   * @returns {'reduced' | 'view-transition' | 'fade'}
   */
  function decidePath({ box, art, swapped }) {
    const size = viewportSize();
    const boxOnScreen = box !== null && isMostlyOnScreen(box.getBoundingClientRect(), size)
      && (art === null || isMostlyOnScreen(art.getBoundingClientRect(), size));

    return choosePath({
      reducedMotion: window.matchMedia(REDUCED_MOTION).matches,
      hasViewTransition: typeof document.startViewTransition === 'function',
      boxOnScreen,
      swapped,
    });
  }

  /**
   * Opens the card as a pull-out: the box lifts, turns to its cover and becomes the card's cover spot, while the box itself stays
   * hidden so its empty slot shows behind the dim.
   * @param {HTMLElement} box The box that was tapped.
   * @param {HTMLElement} card The card, already in the closed dialog.
   * @returns {Promise<void>}
   */
  async function openWithPull(box, card) {
    const art = card.querySelector('.card-art');
    const root = document.documentElement;

    root.dataset.pullKind = pullKind(box.dataset.kind);
    root.dataset.pullDir = 'open';
    box.dataset.pulling = 'source';

    await runTransition(() => {
      box.removeAttribute('data-pulling');
      box.dataset.out = '';
      art?.setAttribute('data-pulling', 'target');
      showCard(card);
    });

    if (!dialog.open) {
      box.dataset.out = '';
      showCard(card);
    }

    clearPullMarks(box, art);
  }

  /**
   * Opens the card with a plain fade of the dialog and the dim. The box is taken out of its slot at once, or, for a visitor who
   * prefers less motion, stays where it is with a clear outline.
   * @param {HTMLElement | null} box The box the card belongs to, if it is on the page.
   * @param {HTMLElement} card The card, already in the closed dialog.
   * @param {boolean} reduced Whether the visitor prefers less motion.
   * @param {boolean} atExpansions Whether the card opens at its owned expansions.
   * @returns {Promise<void>}
   */
  async function openWithFade(box, card, reduced, atExpansions) {
    box?.setAttribute(reduced ? 'data-open' : 'data-out', '');
    dialog.dataset.fade = reduced ? 'reduced-open' : 'open';
    showCard(card, atExpansions);
    await pause(tokenMs(FADES[dialog.dataset.fade]));
    delete dialog.dataset.fade;
  }

  /**
   * Opens the card of an entry over the cabinet, takes the one history step and moves focus to its title, or to the heading of the
   * owned expansions when the card is opened at them. The box flies out of the shelf where the browser can show that, and the card
   * fades in where it cannot or where the visitor prefers less motion. A card that is open or opening is left alone, and so is a
   * second tap while one pull-out runs. A close that just happened is given a moment to finish removing its own step first.
   * @param {string | number} entryId The collection entry to show.
   * @param {{ opener?: HTMLElement, atExpansions?: boolean }} [from] The box that was tapped, so focus can return to it, and whether
   * the card opens at its list of owned expansions.
   * @returns {Promise<void>}
   */
  async function openCard(entryId, { opener: element, atExpansions = false } = {}) {
    if (dialog.open || opening || moving) {
      return;
    }

    opening = true;
    gate.opened();

    try {
      await steps.beforeOpen();

      const record = getRecord(String(entryId));

      if (dialog.open || record === null || record === undefined) {
        return;
      }

      opener = { element, entryId: String(entryId), kind: element?.dataset.kind ?? '' };

      const card = buildCard(record, getCopy(), cardOptions());
      let path = 'fade';

      shownEntryId = String(entryId);
      dialog.replaceChildren(card);
      moving = true;

      try {
        if (!window.matchMedia(REDUCED_MOTION).matches && typeof document.startViewTransition === 'function') {
          await waitForCover(card);
        }

        const box = findPullBox();

        path = decidePath({ box, art: null, swapped: false });

        if (path === 'view-transition') {
          await openWithPull(box, card);
        } else {
          await openWithFade(box, card, path === 'reduced', atExpansions);
        }
      } finally {
        moving = false;
      }

      const heading = atExpansions && path === 'view-transition' ? card.querySelector('.card-expansions-title') : null;

      if (heading !== null) {
        heading.scrollIntoView({ block: 'start', behavior: 'smooth' });
        heading.focus({ preventScroll: true });
      }
    } finally {
      opening = false;

      if (!dialog.open) {
        gate.closed();
      }
    }

    if (closeWhenSettled) {
      closeWhenSettled = false;
      closeCard();
    }
  }

  /**
   * Ends a close once the box is back in its slot and the dialog is gone: gives focus back to the box that opened the card and
   * lets anyone waiting for the card to be gone go on.
   * @param {HTMLElement | null} returnTo The element that gets focus back.
   */
  function finishClose(returnTo) {
    returnTo?.focus({ preventScroll: true });
    opener = null;
    shownEntryId = null;
    moving = false;
    closeWhenSettled = false;
    forcedPath = null;
    gate.closed();
  }

  /**
   * Closes the card the way it came out: the cover turns away and the box slides back into its slot, and focus is back on the box
   * the moment the dialog is gone.
   * @param {HTMLElement} box The box the card came out of.
   * @param {HTMLElement} art The cover spot of the card.
   * @param {HTMLElement | null} returnTo The element that gets focus back.
   * @returns {Promise<void>}
   */
  async function closeWithPull(box, art, returnTo) {
    const root = document.documentElement;

    root.dataset.pullKind = pullKind(box.dataset.kind);
    root.dataset.pullDir = 'close';
    art.dataset.pulling = 'target';

    await runTransition(() => {
      art.removeAttribute('data-pulling');
      dialog.close();
      box.removeAttribute('data-out');
      box.dataset.pulling = 'source';
      returnTo?.focus({ preventScroll: true });
    });

    if (dialog.open) {
      dialog.close();
      box.removeAttribute('data-out');
    }

    clearPullMarks(box, art);
  }

  /**
   * Closes the card with a plain fade of the dialog and the dim, then puts its box back, takes away the outline a visitor who
   * prefers less motion saw, and returns focus.
   * @param {HTMLElement | null} box The box the card belongs to, if it is on the page.
   * @param {HTMLElement | null} returnTo The element that gets focus back.
   * @param {boolean} reduced Whether the visitor prefers less motion.
   * @returns {Promise<void>}
   */
  async function closeWithFade(box, returnTo, reduced) {
    dialog.dataset.fade = reduced ? 'reduced-close' : 'close';
    await pause(tokenMs(FADES[dialog.dataset.fade]));
    dialog.close();
    delete dialog.dataset.fade;
    box?.removeAttribute('data-out');
    box?.removeAttribute('data-open');
    returnTo?.focus({ preventScroll: true });
  }

  /**
   * Closes the open card by whichever way suits now: the pull-out in reverse, or a plain fade.
   * @returns {Promise<void>}
   */
  async function runClose() {
    const returnTo = findOpener();
    const box = findPullBox();
    const art = dialog.querySelector('.card-art');
    const decided = decidePath({ box, art, swapped: showsOtherGame() });
    const path = forcedPath === 'fade' && decided === 'view-transition' ? 'fade' : decided;

    forcedPath = null;
    moving = true;

    try {
      if (path === 'view-transition' && art !== null) {
        await closeWithPull(box, art, returnTo);
      } else {
        await closeWithFade(box, returnTo, path === 'reduced');
      }
    } finally {
      finishClose(returnTo);
    }
  }

  /**
   * Closes the card. A close asked for while the card is still opening waits until it has finished opening. With the path `fade` the
   * card always closes with the plain fade and the box does not fly, which is how a card closes when the page is about to be drawn
   * again for another profile.
   * @param {{ path?: 'fade' }} [how] How to close; leave out for the way that suits now.
   * @returns {Promise<void>} Settled once no card is open and its box is back, at once when no card is open.
   */
  function closeCard({ path } = {}) {
    const pending = opening || moving;

    if (!dialog.open && !(path === 'fade' && pending)) {
      return Promise.resolve();
    }

    if (path === 'fade') {
      forcedPath = 'fade';
    }

    if (pending) {
      closeWhenSettled = true;

      return gate.whenClosed();
    }

    runClose();

    return gate.whenClosed();
  }

  /**
   * Starts a drag of the sheet when a press lands on its grip strip or its header on a phone and nothing is moving. The content
   * below the header, the close button, links and rows never start one, so reading and scrolling never close the sheet.
   * @param {PointerEvent} event The press.
   */
  function startDrag(event) {
    const card = dialog.querySelector('.card');

    if (drag !== null || !dialog.open || moving || opening || card === null || !event.isPrimary || event.button !== 0) {
      return;
    }

    const zone = event.target.closest(DRAG_ZONES);

    if (zone === null || !card.contains(zone) || event.target.closest(PRESSABLE) !== null) {
      return;
    }

    if (!window.matchMedia(PHONE).matches || card.hasAttribute('data-springing')) {
      return;
    }

    try {
      zone.setPointerCapture(event.pointerId);
    } catch {
      return;
    }

    card.dataset.moving = '';
    drag = {
      pointerId: event.pointerId,
      card,
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
    drag.card.style.setProperty('--sheet-dy', drag.offset + 'px');
    drag.samples.push({ at: event.timeStamp, y: event.clientY });
    drag.samples = drag.samples.filter((sample) => event.timeStamp - sample.at <= SPEED_WINDOW_MS);
  }

  /**
   * Gives how fast the finger was moving down over the last moments before it lifted, in pixels per millisecond; zero when it had
   * rested for longer than the measured window.
   * @param {PointerEvent} event The release.
   * @returns {number}
   */
  function releaseSpeed(event) {
    const recent = drag.samples.filter((sample) => event.timeStamp - sample.at <= SPEED_WINDOW_MS);

    if (recent.length === 0 || !(event.timeStamp > recent[0].at)) {
      return 0;
    }

    return (event.clientY - recent[0].y) / (event.timeStamp - recent[0].at);
  }

  /**
   * Lets the sheet go back to rest: over the spring token's length, or at once for a visitor who prefers less motion.
   * @param {HTMLElement} card The sheet.
   */
  function springBack(card) {
    if (window.matchMedia(REDUCED_MOTION).matches) {
      card.style.setProperty('--sheet-dy', '0px');
      delete card.dataset.moving;

      return;
    }

    card.dataset.springing = '';
    delete card.dataset.moving;
    card.style.setProperty('--sheet-dy', '0px');
    window.setTimeout(() => {
      delete card.dataset.springing;
    }, tokenMs('--sheet-spring'));
  }

  /**
   * Ends a drag: a long or fast enough pull down closes the card the normal way, from where the finger left the sheet, and
   * anything else, including a cancelled press, lets the sheet spring back.
   * @param {PointerEvent} event The release or the cancel.
   */
  function endDrag(event) {
    if (drag === null || event.pointerId !== drag.pointerId) {
      return;
    }

    const { card, offset } = drag;
    const outcome = event.type === 'pointerup'
      ? dragOutcome({ dy: offset, height: card.getBoundingClientRect().height, velocity: releaseSpeed(event) })
      : 'spring';

    drag = null;

    if (!card.isConnected || !dialog.open) {
      return;
    }

    if (outcome === 'close') {
      delete card.dataset.moving;
      closeCard();
    } else {
      springBack(card);
    }
  }

  /**
   * Tells whether a card is open.
   * @returns {boolean}
   */
  function isOpen() {
    return dialog.open;
  }

  /**
   * Gives a promise that is settled when no card is open and no pull-out is running: at once when none is, otherwise when the one
   * that is has finished and the box is back in its slot.
   * @returns {Promise<void>}
   */
  function whenClosed() {
    return gate.whenClosed();
  }

  dialog.addEventListener('close', () => {
    steps.closedHere();

    if (!moving && !opening) {
      gate.closed();
    }
  });

  dialog.addEventListener('cancel', (event) => {
    event.preventDefault();
    closeCard();
  });

  window.addEventListener('popstate', () => {
    if (steps.popped() === 'close') {
      closeCard();
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
    if (event.target.closest('.card-close') !== null || (event.target === dialog && pressedOnFrame)) {
      closeCard();
    }

    pressedOnFrame = false;
  });

  mount.addEventListener('pointerdown', (event) => {
    const box = event.target.closest('.placement');

    if (box !== null) {
      warmCover(box);
    }
  });

  mount.addEventListener('keydown', (event) => {
    const box = event.target.closest('.placement');

    if (box !== null && (event.key === 'Enter' || event.key === ' ')) {
      warmCover(box);
    }
  });

  mount.addEventListener('click', (event) => {
    const box = event.target.closest('.placement');

    if (box === null || !mount.contains(box)) {
      return;
    }

    const { entryId, atExpansions } = sourceEntry(box.dataset.kind, box.dataset.entryId);

    openCard(entryId, { opener: box, atExpansions });
  });

  return { openCard, closeCard, swapTo, showsOtherGame, isOpen, whenClosed };
}
