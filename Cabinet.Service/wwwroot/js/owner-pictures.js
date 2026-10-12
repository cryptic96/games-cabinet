/**
 * The script side of the owner's pictures page. The page works with real forms and a Save picture button; this script takes over
 * their submission so a tap on a tile saves the choice at once without leaving the page, with the row's own `Saved` line and an
 * Undo button. Stepping through a row's radios with the arrow keys saves once, when the choice settles, so one keyboard pass makes
 * one save and one change-log entry. It also draws the generated cover on tiles that have no picture to show and puts the rows in
 * collator order. Every title is written with `textContent`, never as markup.
 */
import { drawGeneratedCover } from './card-view.js';
import { OWNER_TEXT } from './owner-format.js';
import { initRevision, postOwner, reloadButton, showRowProblem, showRowStatus, sortByCollator } from './owner-shared.js';

/** The handler addresses, relative to the page the script runs on. */
const HANDLERS = Object.freeze({
  choose: '?handler=Choose',
  undo: '?handler=Undo',
});

/** The shape of a colour the generated cover may be set to. */
const HEX_COLOUR = /^#[0-9a-fA-F]{6}$/;

/** The colour pair the generated cover falls back to when the page gives none that is valid. */
const FALLBACK_TONE = Object.freeze({ background: '#8c5d38', text: '#ffffff' });

/** How long stepping through the radios with the keyboard must pause before the choice is saved. */
const SETTLE_MS = 600;

/** How close after a key press a change has to come to count as made by the keyboard. */
const KEY_WINDOW_MS = 100;

/**
 * Reads which choice a group of radios has checked.
 * @param {Element} fieldset The group.
 * @returns {string} The value of the checked radio, or an empty string when none is.
 */
function checkedValue(fieldset) {
  return fieldset.querySelector('input[type="radio"]:checked')?.value ?? '';
}

/**
 * Checks the radio with a value and unchecks the others.
 * @param {Element} fieldset The group.
 * @param {string} value The value to check.
 */
function setChecked(fieldset, value) {
  for (const radio of fieldset.querySelectorAll('input[type="radio"]')) {
    radio.checked = radio.value === value;
  }
}

/**
 * Reads the title of a row.
 * @param {Element} row The row.
 * @returns {string}
 */
function titleOf(row) {
  return row.querySelector('.game-title')?.textContent ?? '';
}

/**
 * Draws the generated cover into a picture area, in the game's spine colours, as the detail card draws it. The text the page
 * wrote for the no-script case is replaced.
 * @param {HTMLElement} art The picture area.
 * @param {string} title The title to put on the cover.
 */
function drawCover(art, title) {
  const background = HEX_COLOUR.test(art.dataset.bg ?? '') ? art.dataset.bg : FALLBACK_TONE.background;
  const text = HEX_COLOUR.test(art.dataset.fg ?? '') ? art.dataset.fg : FALLBACK_TONE.text;
  const cover = art.ownerDocument.createElement('span');

  cover.className = 'card-art pic-cover';
  cover.style.setProperty('--bg', background);
  cover.style.setProperty('--fg', text);
  drawGeneratedCover(cover, title, Number.parseInt(art.dataset.pattern ?? '', 10));
  art.replaceChildren(cover);
  art.dataset.art = 'cover';
}

/**
 * Draws the generated cover on every tile that has no picture, and swaps a tile whose picture the browser cannot show to the
 * generated cover without a word, as the cabinet does.
 * @param {Document} doc The page.
 */
function initCovers(doc) {
  for (const art of doc.querySelectorAll('.pic-art[data-art="cover"]')) {
    drawCover(art, titleOf(art.closest('.pic-row')));
  }

  for (const image of doc.querySelectorAll('.pic-art[data-art="picture"] > img')) {
    const art = image.parentElement;
    const swap = () => drawCover(art, titleOf(art.closest('.pic-row')));

    if (image.complete && image.naturalWidth === 0) {
      swap();
    } else {
      image.addEventListener('error', swap, { once: true });
    }
  }
}

/**
 * Tells the owner that a request did not go through, in the region of the row it was about.
 * @param {HTMLElement} region The status region.
 * @param {{ outcome: string }} result What the request came to.
 */
function showProblem(region, result) {
  if (result.outcome === 'outOfDate') {
    reloadButton(region, OWNER_TEXT.outOfDate, OWNER_TEXT.reload);
  } else if (result.outcome === 'expired') {
    reloadButton(region, OWNER_TEXT.expired, OWNER_TEXT.reload);
  } else if (result.outcome === 'unreadable') {
    showRowProblem(region, OWNER_TEXT.unreadable);
  } else {
    showRowProblem(region, OWNER_TEXT.failed);
  }
}

/**
 * Makes one row save the choice it settles on. A tap saves at once; keyboard stepping saves after a pause, on Enter or Tab, or
 * when focus leaves the group; a page that goes to the background saves what is waiting with the keepalive flag.
 * @param {HTMLElement} row The row.
 * @param {(task: () => Promise<void>) => void} enqueue Runs a task after every earlier one, so only one change is in flight.
 */
function initRow(row, enqueue) {
  const form = row.querySelector('form');
  const fieldset = row.querySelector('fieldset.pic-choices');
  const region = row.querySelector('.row-status');
  const doc = row.ownerDocument;
  let lastKeyAt = Number.NEGATIVE_INFINITY;
  let timer = null;
  let pending = false;

  if (!form || !fieldset || !region) {
    return;
  }

  fieldset.dataset.saved = checkedValue(fieldset);
  form.querySelector('[data-save]').hidden = true;

  /**
   * Takes back the latest change on the server and, when that worked, on the page too.
   * @param {string} previous The choice the row had before the change.
   * @returns {() => void} The click handler of the Undo button.
   */
  const undoer = (previous) => () =>
    enqueue(async () => {
      const result = await postOwner(HANDLERS.undo, {});

      if (result.outcome === 'saved') {
        setChecked(fieldset, previous);
        fieldset.dataset.saved = previous;
        showRowStatus(region, OWNER_TEXT.undone);
        fieldset.querySelector('input[type="radio"]:checked')?.focus();
      } else if (result.outcome === 'outOfDate' && result.status === 'cant-undo') {
        showRowProblem(region, OWNER_TEXT.cantUndo);
      } else {
        showProblem(region, result);
      }
    });

  /**
   * Saves one choice.
   * @param {string} value The choice the group settled on.
   * @param {boolean} keepalive Whether the request has to outlive the page.
   */
  const commit = (value, keepalive) =>
    enqueue(async () => {
      const previous = fieldset.dataset.saved ?? '';

      if (value === previous) {
        return;
      }

      const result = await postOwner(
        form.getAttribute('action') ?? HANDLERS.choose,
        { gameId: form.elements.gameId.value, choice: value },
        { keepalive },
      );

      if (result.outcome === 'saved') {
        fieldset.dataset.saved = value;
        showRowStatus(region, OWNER_TEXT.saved, { undoLabel: OWNER_TEXT.undo, onUndo: undoer(previous) });
      } else if (result.outcome === 'noChange') {
        fieldset.dataset.saved = value;
      } else {
        setChecked(fieldset, previous);
        showProblem(region, result);
      }
    });

  /**
   * Saves the choice that is waiting, if there is one.
   * @param {boolean} keepalive Whether the request has to outlive the page.
   */
  function flush(keepalive) {
    globalThis.clearTimeout(timer);
    timer = null;

    if (pending) {
      pending = false;
      commit(checkedValue(fieldset), keepalive);
    }
  }

  fieldset.addEventListener('keydown', (event) => {
    lastKeyAt = event.timeStamp;

    if (event.key === 'Enter' || event.key === 'Tab') {
      flush(false);
    }
  });
  fieldset.addEventListener('change', (event) => {
    if (!event.target.matches?.('input[type="radio"]')) {
      return;
    }

    pending = true;
    globalThis.clearTimeout(timer);

    if (event.timeStamp - lastKeyAt <= KEY_WINDOW_MS) {
      timer = globalThis.setTimeout(() => flush(false), SETTLE_MS);
    } else {
      flush(false);
    }
  });
  fieldset.addEventListener('focusout', (event) => {
    if (!fieldset.contains(event.relatedTarget)) {
      flush(false);
    }
  });
  form.addEventListener('submit', (event) => {
    event.preventDefault();
    flush(false);
  });
  doc.addEventListener('visibilitychange', () => {
    if (doc.visibilityState === 'hidden') {
      flush(true);
    }
  });
}

/**
 * Wires the rows of the pictures page and puts them in collator order.
 * @param {Document} [doc] The page; the browser's own by default.
 */
export function initPictures(doc = globalThis.document) {
  const list = doc?.getElementById('pic-list');

  if (!list) {
    return;
  }

  let chain = Promise.resolve();

  /**
   * Runs a task after every earlier one has finished, so only one change is ever in flight.
   * @param {() => Promise<void>} task The work.
   */
  const enqueue = (task) => {
    chain = chain.then(task, task);
  };

  initRevision(doc);
  sortByCollator(list);
  initCovers(doc);

  for (const row of list.querySelectorAll('.pic-row')) {
    initRow(row, enqueue);
  }

  list.dataset.enhanced = '';
}

initPictures();
