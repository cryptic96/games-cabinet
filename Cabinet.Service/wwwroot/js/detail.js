/**
 * The detail card's dialog: a tap on any box opens one native modal dialog holding that game's card, and closing it puts focus back
 * on the box that opened it. The card is built from data already in memory, so opening it never waits for the network.
 */
import { buildCard } from './card-view.js';
import { createHistoryStep, sourceEntry } from './card-flow.js';

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
  let pressedOnFrame = false;
  let closedPromise = null;
  let resolveClosed = null;

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
   * Gives the length of the swap fade in milliseconds, read from the page's own timing token so the script and the style sheet
   * never disagree.
   * @returns {number}
   */
  function swapFadeMs() {
    const raw = getComputedStyle(document.documentElement).getPropertyValue('--swap-fade').trim();
    const value = Number.parseFloat(raw);

    if (!Number.isFinite(value)) {
      return 0;
    }

    return raw.endsWith('ms') ? value : value * 1000;
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
      await new Promise((resolve) => window.setTimeout(resolve, swapFadeMs()));

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
   * Opens the card of an entry over the cabinet, takes the one history step and moves focus to its title, or to the heading of the
   * owned expansions when the card is opened at them. A card that is open or opening is left alone. A close that just happened is
   * given a moment to finish removing its own step first.
   * @param {string | number} entryId The collection entry to show.
   * @param {{ opener?: HTMLElement, atExpansions?: boolean }} [from] The box that was tapped, so focus can return to it, and whether
   * the card opens at its list of owned expansions.
   * @returns {Promise<void>}
   */
  async function openCard(entryId, { opener: element, atExpansions = false } = {}) {
    if (dialog.open || opening) {
      return;
    }

    opening = true;

    try {
      await steps.beforeOpen();

      const record = getRecord(String(entryId));

      if (dialog.open || record === null || record === undefined) {
        return;
      }

      opener = { element, entryId: String(entryId), kind: element?.dataset.kind ?? '' };

      const card = buildCard(record, getCopy(), cardOptions());

      shownEntryId = String(entryId);
      dialog.replaceChildren(card);
      dialog.showModal();
      steps.opened();

      const heading = atExpansions ? card.querySelector('.card-expansions-title') : null;

      if (heading === null) {
        card.querySelector('.card-title').focus({ preventScroll: true });
      } else {
        heading.scrollIntoView({ block: 'start', behavior: 'instant' });
        heading.focus({ preventScroll: true });
      }
    } finally {
      opening = false;
    }
  }

  /**
   * Closes the card.
   */
  function closeCard() {
    if (dialog.open) {
      dialog.close();
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
   * Gives a promise that is settled when no card is open: at once when none is, otherwise when the open one has closed.
   * @returns {Promise<void>}
   */
  function whenClosed() {
    if (!dialog.open) {
      return Promise.resolve();
    }

    closedPromise ??= new Promise((resolve) => {
      resolveClosed = resolve;
    });

    return closedPromise;
  }

  dialog.addEventListener('close', () => {
    steps.closedHere();

    const box = findOpener();

    if (box !== null) {
      box.focus({ preventScroll: true });
    }

    opener = null;
    shownEntryId = null;

    if (resolveClosed !== null) {
      resolveClosed();
      closedPromise = null;
      resolveClosed = null;
    }
  });

  window.addEventListener('popstate', () => {
    if (steps.popped() === 'close') {
      closeCard();
    }
  });

  dialog.addEventListener('pointerdown', (event) => {
    pressedOnFrame = event.target === dialog;
  });

  dialog.addEventListener('click', (event) => {
    if (event.target.closest('.card-close') !== null || (event.target === dialog && pressedOnFrame)) {
      closeCard();
    }

    pressedOnFrame = false;
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
