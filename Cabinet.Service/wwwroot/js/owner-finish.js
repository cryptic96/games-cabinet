/**
 * The script side of the cabinet finish panel on the owner's settings page. The page works with a real form and a Save finish
 * button; this script takes over its submission so choosing a tile saves at once without leaving the page, with the panel's own
 * `Saved` line. A tile that is already chosen can be chosen again and saves with `Saved`, because nothing changed and there is
 * nothing to undo. A save that fails puts the tile of the stored finish back and says so in the panel's status line. Every text is written with `textContent`, never as markup.
 */
import { OWNER_TEXT } from './owner-format.js';
import { initRevision, postOwner, setChecked, showProblem as showSharedProblem, showRowProblem, showRowStatus } from './owner-shared.js';

/** The address of the finish handler, relative to the page the script runs on. */
const FINISH_HANDLER = '?handler=Finish';

/**
 * Tells the owner that a request did not go through, in the panel's status line: the server's own words when it refused the
 * change, otherwise what every owner page says.
 * @param {HTMLElement} region The status region.
 * @param {{ outcome: string, message?: string }} result What the request came to.
 */
function showProblem(region, result) {
  if (result.outcome === 'refused' && typeof result.message === 'string' && result.message !== '') {
    showRowProblem(region, result.message);
  } else {
    showSharedProblem(region, result);
  }
}

/**
 * Makes the finish form save the finish a tap on a tile settles on, one save at a time. A tile chosen while a save is under way
 * is saved when that one is done, so the stored finish is the one the panel shows; a save that fails puts the tile of the
 * stored finish back.
 * @param {HTMLFormElement} form The finish form.
 * @param {HTMLElement} region The panel's status line.
 */
function initForm(form, region) {
  const fieldset = form.querySelector('fieldset');
  const current = () => fieldset.querySelector('input[type="radio"]:checked');
  let saving = false;
  let chosenMeanwhile = false;
  let savedValue = current()?.value ?? '';

  form.querySelector('[data-save]').hidden = true;

  /**
   * Saves the finish that is checked now, then the one chosen while that save was under way, until the stored finish is the one
   * that is shown.
   * @returns {Promise<void>}
   */
  async function save() {
    if (saving) {
      chosenMeanwhile = true;

      return;
    }

    saving = true;

    try {
      let checked = current();

      while (checked) {
        chosenMeanwhile = false;

        const result = await postOwner(form.getAttribute('action') ?? FINISH_HANDLER, { finish: checked.value });

        if (result.outcome !== 'saved' && result.outcome !== 'noChange') {
          setChecked(fieldset, savedValue);
          showProblem(region, result);

          return;
        }

        savedValue = checked.value;
        showRowStatus(region, OWNER_TEXT.saved);
        checked = chosenMeanwhile && current()?.value !== savedValue ? current() : null;
      }
    } finally {
      saving = false;
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
