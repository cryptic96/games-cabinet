/**
 * The script side of the owner's pictures page. The page works with real forms and a Save picture button; this script takes over
 * their submission so a tap on a tile saves the choice at once without leaving the page, with the row's own `Saved` line and an
 * Undo button. Stepping through a row's radios with the arrow keys saves once, when the choice settles, so one keyboard pass makes
 * one save and one change-log entry. It also draws the generated cover on tiles that have no picture to show, puts the rows of the
 * all-games view in collator order, and works the worklist: `Looks right` records a game as checked, every save or undo carries
 * the counts the counter above the games shows, a resolved row stays where it is with a `Checked` mark until the next load, and
 * a search box narrows the rows. Every title is written with `textContent`, never as markup.
 */
import { drawGeneratedCover } from './card-view.js';
import {
  OWNER_TEXT,
  allGamesLinkText,
  counterText,
  noMatchText,
  showingText,
  worthALookLinkText,
} from './owner-format.js';
import {
  announce,
  createFinder,
  initRevision,
  postOwner,
  reloadButton,
  showRowProblem,
  showRowStatus,
  sortByCollator,
} from './owner-shared.js';

/** The handler addresses, relative to the page the script runs on. */
const HANDLERS = Object.freeze({
  choose: '?handler=Choose',
  looksRight: '?handler=LooksRight',
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
 * Gives a row the `Picture won't load` mark and its note, once, when a picture on it could not be shown.
 * @param {Element} row The row.
 */
function markWontLoad(row) {
  const marks = row.querySelector('.pic-marks');
  const notes = row.querySelector('.pic-notes');

  if (!marks || marks.querySelector('[data-mark="wont-load"]')) {
    return;
  }

  const doc = row.ownerDocument;
  const tag = doc.createElement('li');

  tag.className = 'pic-tag pic-tag-strong';
  tag.dataset.mark = 'wont-load';
  tag.textContent = OWNER_TEXT.wontLoad;
  marks.append(tag);

  if (notes) {
    const note = doc.createElement('p');

    note.className = 'pic-note';
    note.textContent = OWNER_TEXT.wontLoadNote;
    notes.append(note);
  }
}

/**
 * Shows how a row stands after the server answered: the `Checked` mark and the button follow whether the game is resolved, and
 * the marks and notes that a resolution clears are hidden while it holds and shown again when an undo takes it back. An answer
 * that does not say leaves the row as it is.
 * @param {Element} row The row.
 * @param {{ resolved?: boolean }} result What the server answered.
 */
function applyRowState(row, result) {
  if (typeof result.resolved !== 'boolean') {
    return;
  }

  const resolved = result.resolved;
  const mark = row.querySelector('[data-mark="checked"]');
  const button = row.querySelector('[data-looks-right]');

  if (mark) {
    mark.hidden = !resolved;
  }

  for (const cleared of row.querySelectorAll('[data-clears]')) {
    cleared.hidden = resolved;
  }

  if (button) {
    button.textContent = resolved ? OWNER_TEXT.checked : OWNER_TEXT.looksRight;

    if (resolved) {
      button.setAttribute('aria-disabled', 'true');
    } else {
      button.removeAttribute('aria-disabled');
    }
  }
}

/**
 * Draws the generated cover on every tile that has no picture, and swaps a tile whose picture the browser cannot show to the
 * generated cover without a word, as the cabinet does. The row it is on gets the `Picture won't load` mark.
 * @param {Document} doc The page.
 */
function initCovers(doc) {
  for (const art of doc.querySelectorAll('.pic-art[data-art="cover"]')) {
    drawCover(art, titleOf(art.closest('.pic-row')));
  }

  for (const image of doc.querySelectorAll('.pic-art[data-art="picture"] > img')) {
    const art = image.parentElement;
    const swap = () => {
      drawCover(art, titleOf(art.closest('.pic-row')));
      markWontLoad(art.closest('.pic-row'));
    };

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
 * @param {(result: object) => void} showCounts Writes the counts an answer carries into the counter and the view links.
 */
function initRow(row, enqueue, showCounts) {
  const form = row.querySelector('form.pic-form');
  const looksRight = row.querySelector('[data-looks-right]');
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
   * @param {boolean} [refocus] Whether focus goes to the checked radio afterwards; a button that was pressed keeps its focus instead.
   * @returns {() => void} The click handler of the Undo button.
   */
  const undoer = (previous, refocus = true) => () =>
    enqueue(async () => {
      const result = await postOwner(HANDLERS.undo, { gameId: form.elements.gameId.value });

      if (result.outcome === 'saved') {
        setChecked(fieldset, previous);
        fieldset.dataset.saved = previous;
        applyRowState(row, result);
        showCounts(result);
        showRowStatus(region, OWNER_TEXT.undone);

        if (refocus) {
          fieldset.querySelector('input[type="radio"]:checked')?.focus();
        } else {
          looksRight?.focus();
        }
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
        applyRowState(row, result);
        showCounts(result);
        showRowStatus(region, OWNER_TEXT.saved, { undoLabel: OWNER_TEXT.undo, onUndo: undoer(previous) });
      } else if (result.outcome === 'noChange') {
        fieldset.dataset.saved = value;
      } else {
        setChecked(fieldset, previous);
        showProblem(region, result);
      }
    });

  /**
   * Records that the game's pictures look right. The button stays where it is, keeps focus and turns into the disabled `Checked`;
   * the row keeps its place until the next load.
   */
  const recordLooksRight = () =>
    enqueue(async () => {
      if (looksRight.getAttribute('aria-disabled') === 'true') {
        return;
      }

      const previous = fieldset.dataset.saved ?? '';
      const result = await postOwner(HANDLERS.looksRight, { gameId: form.elements.gameId.value });

      if (result.outcome === 'saved') {
        applyRowState(row, result);
        showCounts(result);
        showRowStatus(region, OWNER_TEXT.markedChecked, { undoLabel: OWNER_TEXT.undo, onUndo: undoer(previous, false) });
      } else if (result.outcome === 'noChange') {
        applyRowState(row, { resolved: true });
      } else {
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
  looksRight?.addEventListener('click', (event) => {
    event.preventDefault();
    recordLooksRight();
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

  const counter = doc.getElementById('pic-counter');
  const worthLink = doc.getElementById('pic-view-worth');
  const allLink = doc.getElementById('pic-view-all');
  let chain = Promise.resolve();

  /**
   * Writes the counts an answer carries into the counter and the two view links.
   * @param {{ toCheck?: number, candidates?: number, total?: number }} result What the server answered.
   */
  const showCounts = (result) => {
    if (!Number.isInteger(result.toCheck) || !Number.isInteger(result.candidates) || !Number.isInteger(result.total)) {
      return;
    }

    if (counter) {
      counter.textContent = counterText(result.toCheck, result.candidates);
    }

    if (worthLink) {
      worthLink.textContent = worthALookLinkText(result.toCheck);
    }

    if (allLink) {
      allLink.textContent = allGamesLinkText(result.total);
    }
  };

  /**
   * Runs a task after every earlier one has finished, so only one change is ever in flight.
   * @param {() => Promise<void>} task The work.
   */
  const enqueue = (task) => {
    chain = chain.then(task, task);
  };

  initRevision(doc);

  if (list.dataset.view === 'all') {
    sortByCollator(list);
  }

  initCovers(doc);

  for (const row of list.querySelectorAll('.pic-row')) {
    initRow(row, enqueue, showCounts);
  }

  initFind(doc, list);
  list.dataset.enhanced = '';
}

/**
 * Wires the search box: after the owner stops typing, the rows whose title does not contain the query are hidden, and what is
 * shown is written to the page's announcer, and, while a query is typed, to a visible line above the games.
 * @param {Document} doc The page.
 * @param {HTMLElement} list The list of rows.
 */
function initFind(doc, list) {
  const input = doc.getElementById('pic-find');
  const box = doc.getElementById('pic-find-box');
  const line = doc.getElementById('pic-count');

  if (!input || !box || !line) {
    return;
  }

  const finder = createFinder(input, () => Array.from(list.querySelectorAll('.pic-row')), {
    onApply: (report) => {
      const text =
        report.shown === 0 && report.query !== ''
          ? noMatchText(report.query)
          : showingText(report.shown, report.total);

      line.hidden = report.query === '';
      line.textContent = report.query === '' ? '' : text;

      if (report.announce) {
        announce(text, doc);
      }
    },
  });

  finder.apply({ announce: false });
  box.hidden = false;
}

initPictures();
