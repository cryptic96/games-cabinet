/**
 * The detail card's dialog: a tap on any box opens one native modal dialog holding that game's card, and closing it puts focus back
 * on the box that opened it. The card is built from data already in memory, so opening it never waits for the network.
 */
import { buildCard } from './card-view.js';
import { sourceEntry } from './card-flow.js';

/**
 * Wires the dialog to the cabinet.
 * @param {object} parts What the card needs from the page.
 * @param {HTMLDialogElement} parts.dialog The one dialog of the page.
 * @param {HTMLElement} parts.mount The element that holds the drawn cabinet.
 * @param {(entryId: string) => object | null} parts.getRecord Gives the card record of an entry, or null when nothing is known.
 * @param {() => object} parts.getCopy Gives the visitor-facing strings.
 * @param {() => object[]} [parts.getPalette] Gives the colour table of the layout on screen.
 * @param {string} [parts.iconsUrl] The address of the icon sprite.
 * @returns {{ openCard: Function, closeCard: Function, isOpen: Function, whenClosed: Function }} The controls.
 */
export function initCardDialog({ dialog, mount, getRecord, getCopy, getPalette = () => [], iconsUrl = '' }) {
  let opener = null;
  let closedPromise = null;
  let resolveClosed = null;

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
   * Opens the card of an entry over the cabinet and moves focus to its title.
   * @param {string | number} entryId The collection entry to show.
   * @param {{ opener?: HTMLElement }} [from] The box that was tapped, so focus can return to it.
   */
  function openCard(entryId, { opener: element } = {}) {
    if (dialog.open) {
      return;
    }

    const record = getRecord(String(entryId));

    if (record === null || record === undefined) {
      return;
    }

    opener = { element, entryId: String(entryId), kind: element?.dataset.kind ?? '' };

    const card = buildCard(record, getCopy(), { iconsUrl, palette: getPalette() });

    dialog.replaceChildren(card);
    dialog.showModal();
    card.querySelector('.card-title').focus({ preventScroll: true });
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
    const box = findOpener();

    if (box !== null) {
      box.focus({ preventScroll: true });
    }

    opener = null;

    if (resolveClosed !== null) {
      resolveClosed();
      closedPromise = null;
      resolveClosed = null;
    }
  });

  dialog.addEventListener('click', (event) => {
    if (event.target.closest('.card-close') !== null) {
      closeCard();
    }
  });

  mount.addEventListener('click', (event) => {
    const box = event.target.closest('.placement');

    if (box === null || !mount.contains(box)) {
      return;
    }

    const { entryId } = sourceEntry(box.dataset.kind, box.dataset.entryId);

    openCard(entryId, { opener: box });
  });

  return { openCard, closeCard, isOpen, whenClosed };
}
