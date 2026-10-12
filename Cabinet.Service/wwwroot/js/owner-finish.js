/**
 * The script side of the cabinet finish panel on the owner's settings page. The page works with a real form and a Save finish
 * button; this script takes over its submission so choosing a tile saves at once without leaving the page, with the panel's own
 * `Saved` line. A tile that is already chosen can be chosen again and saves with `Saved`, because nothing changed and there is
 * nothing to undo. A save that fails leaves the tile checked, because the stored finish did not change, and says so in the
 * panel's status line. Every text is written with `textContent`, never as markup.
 */
import { OWNER_TEXT } from './owner-format.js';
import { initRevision, postOwner, reloadButton, showRowProblem, showRowStatus } from './owner-shared.js';

/** The address of the finish handler, relative to the page the script runs on. */
const FINISH_HANDLER = '?handler=Finish';

/**
 * Tells the owner that a request did not go through, in the panel's status line.
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
  } else if (result.outcome === 'refused' && typeof result.message === 'string' && result.message !== '') {
    showRowProblem(region, result.message);
  } else {
    showRowProblem(region, OWNER_TEXT.failed);
  }
}

/**
 * Makes the finish form save the finish a tap on a tile settles on, one save at a time.
 * @param {HTMLFormElement} form The finish form.
 * @param {HTMLElement} region The panel's status line.
 */
function initForm(form, region) {
  const fieldset = form.querySelector('fieldset');
  let saving = false;

  form.querySelector('[data-save]').hidden = true;

  /**
   * Saves the finish that is checked now.
   * @returns {Promise<void>}
   */
  async function save() {
    const checked = fieldset.querySelector('input[type="radio"]:checked');

    if (saving || !checked) {
      return;
    }

    saving = true;

    const result = await postOwner(form.getAttribute('action') ?? FINISH_HANDLER, { finish: checked.value });

    saving = false;

    if (result.outcome === 'saved' || result.outcome === 'noChange') {
      showRowStatus(region, OWNER_TEXT.saved);
    } else {
      checked.checked = true;
      showProblem(region, result);
    }
  }

  fieldset.addEventListener('click', (event) => {
    if (event.target.matches?.('input[type="radio"]')) {
      save();
    }
  });
  form.addEventListener('submit', (event) => {
    event.preventDefault();
    save();
  });
}

/**
 * Starts the script on the settings page.
 * @param {Document} [doc] The page; the browser's own by default.
 */
export function initFinish(doc = globalThis.document) {
  const form = doc?.querySelector?.('form[data-finish-form]');
  const region = doc?.getElementById?.('finish-status');

  if (!form || !region) {
    return;
  }

  initRevision(doc);
  initForm(form, region);
}

initFinish();
