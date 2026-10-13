/**
 * Owner additions to the cabinet page, loaded only at the owner address. It marks every box whose game has no place with a small
 * corner tag and gives that box a longer accessible name, using the card data the cabinet script announces on the document. It adds
 * no request and moves no box: the tag is one absolutely placed element inside the box.
 */
import { COPY } from './copy.js';

const TAG_CLASS = 'placement-tag';

let records = new Map();

/**
 * Tells whether a card record says its game has no place: the location is missing or blank.
 * @param {object} record The card record.
 * @returns {boolean}
 */
function isUnplaced(record) {
  return typeof record.location !== 'string' || record.location.trim() === '';
}

/**
 * Builds the corner tag: an empty decorative element the stylesheet draws.
 * @returns {HTMLSpanElement}
 */
function buildTag() {
  const tag = document.createElement('span');
  tag.className = TAG_CLASS;
  tag.setAttribute('aria-hidden', 'true');

  return tag;
}

/**
 * Marks one box as not placed: the attribute, the tag and the longer accessible name. The tooltip and the visible label stay as
 * they are, and the name is always built from the tooltip, which holds the full name, so doing it twice changes nothing.
 * @param {HTMLElement} box The box.
 */
function markUnplaced(box) {
  box.dataset.unplaced = '';

  if (box.querySelector('.' + TAG_CLASS) === null) {
    box.append(buildTag());
  }

  box.setAttribute('aria-label', COPY.notPlacedName(box.title));
}

/**
 * Takes the mark off one box and gives its accessible name back.
 * @param {HTMLElement} box The box.
 */
function clearUnplaced(box) {
  delete box.dataset.unplaced;
  box.querySelector('.' + TAG_CLASS)?.remove();
  box.setAttribute('aria-label', box.title);
}

/**
 * Applies the marks to every drawn box from the card data held now. A box whose entry the data does not hold is left as it is, so
 * a box is only ever called unplaced when the data says so; before the data arrives, and when it fails, nothing is marked.
 */
function apply() {
  if (records.size === 0) {
    return;
  }

  for (const box of document.querySelectorAll('#cabinet .placement:not([data-kind="moreMarker"])')) {
    const record = records.get(box.dataset.entryId);

    if (record === undefined) {
      continue;
    }

    if (isUnplaced(record)) {
      markUnplaced(box);
    } else {
      clearUnplaced(box);
    }
  }
}

document.addEventListener('cabinet:cards', (event) => {
  records = new Map(event.detail.records.map((record) => [String(record.entryId), record]));
  apply();
});

document.addEventListener('cabinet:drawn', apply);
