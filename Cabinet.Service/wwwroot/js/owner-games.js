/**
 * The script side of the games list on the locations page, apart from ticking and moving many games at once: each row's picker,
 * which saves the value it settles on and not every key press, and the toolbar above the list, which finds games as the owner
 * types, shows only the games that are not placed yet, ticks everything that is shown, and puts the rows in collator order.
 * Every title and every query is written with `textContent`, never as markup.
 */
import { OWNER_TEXT, effectiveLocationIds, resultText, unplacedLabel } from './owner-format.js';
import { gameStates } from './owner-placing.js';
import {
  announce,
  createFinder,
  postOwner,
  saveSettledValue,
  setPickerValue,
  showRowStatus,
  sortByCollator,
} from './owner-shared.js';

/**
 * Reads the choice a picker was drawn on by the server, which is what the owner data holds.
 * @param {HTMLSelectElement} select The picker.
 * @returns {string} The value of the option that was selected in the markup, or an empty string.
 */
function drawnValue(select) {
  return Array.from(select.options).find((option) => option.defaultSelected)?.value ?? '';
}

/**
 * Makes every picker of the page save the value it settles on. A pointer or touch choice saves at once; stepping through the
 * options with the keyboard saves once, after a pause, when focus leaves, or on Enter or Tab. A saved choice says `Saved` with
 * Undo in the row's own status; a failed one puts the picker back on its previous value and says so.
 * @param {Document} doc The page.
 * @param {{
 *   enqueue: (task: () => Promise<void>) => void,
 *   showProblem: (region: HTMLElement, result: object) => void,
 *   undoer: (region: HTMLElement, apply: () => void, focusAfter: () => HTMLElement) => () => void,
 *   gamesChanged: () => void,
 * }} hooks What the page offers: one request at a time, the wording of a failure, the Undo button and the recount after a change.
 */
export function initPickers(doc, hooks) {
  for (const select of doc.querySelectorAll('select[data-picker]')) {
    const form = select.closest('form');
    const region = form?.parentElement?.querySelector('.row-status');

    if (!form || !region) {
      continue;
    }

    /**
     * Saves one value for this picker.
     * @param {string} value The value the picker settled on.
     * @param {{ keepalive?: boolean }} [options] Whether the request has to outlive the page.
     */
    const commit = (value, options = {}) =>
      hooks.enqueue(async () => {
        const previous = select.dataset.saved ?? '';

        if (value === previous) {
          return;
        }

        const result = await postOwner(
          form.getAttribute('action'),
          { gameId: form.elements.gameId.value, locationId: value },
          { keepalive: options.keepalive === true },
        );

        if (result.outcome === 'saved') {
          select.dataset.saved = value;
          hooks.gamesChanged();
          showRowStatus(region, OWNER_TEXT.saved, {
            undoLabel: OWNER_TEXT.undo,
            onUndo: hooks.undoer(
              region,
              () => {
                setPickerValue(select, previous);
                hooks.gamesChanged();
              },
              () => select,
            ),
          });
        } else if (result.outcome === 'noChange') {
          select.dataset.saved = value;
        } else {
          setPickerValue(select, previous);
          hooks.showProblem(region, result);
        }
      });

    setPickerValue(select, drawnValue(select));
    form.querySelector('[data-save]').hidden = true;
    saveSettledValue(select, commit);
    form.addEventListener('submit', (event) => {
      event.preventDefault();
      commit(select.value);
    });
  }
}

/**
 * Wires the toolbar of the games list and puts the rows in collator order.
 * @param {Document} doc The page.
 * @returns {{ refreshCount: () => void, syncSelectAll: () => void } | null} What the rest of the page calls when games moved or
 * the ticks changed, or null when the page has no games list.
 */
export function initGamesToolbar(doc) {
  const list = doc.querySelector('.game-list');
  const toolbar = doc.getElementById('games-toolbar');

  if (!list || !toolbar) {
    return null;
  }

  const input = doc.getElementById('game-find');
  const unplacedSwitch = doc.getElementById('unplaced-switch');
  const unplacedText = doc.getElementById('unplaced-label');
  const selectAll = doc.getElementById('select-all');
  const countLine = doc.getElementById('games-count');
  const emptyNote = doc.getElementById('games-empty');
  let unplacedIds = new Set();

  /**
   * Lists every game row.
   * @returns {HTMLElement[]}
   */
  function gameRows() {
    return Array.from(list.querySelectorAll('.game-row'));
  }

  /**
   * Lists the rows that are on screen, a base game shown only as context included.
   * @returns {HTMLElement[]}
   */
  function shownRows() {
    return gameRows().filter((row) => !row.hidden);
  }

  /**
   * Brings the select-all checkbox in line with the ticks of the rows that are shown: ticked when all are, in between when some are.
   */
  function syncSelectAll() {
    const checks = shownRows().map((row) => row.querySelector('.game-check'));
    const ticked = checks.filter((check) => check.checked).length;

    selectAll.checked = checks.length > 0 && ticked === checks.length;
    selectAll.indeterminate = ticked > 0 && ticked < checks.length;
  }

  /**
   * Counts the games that are shown in no location and writes the count into the switch's label. The rows that are shown do not
   * change here, so a game the owner just placed stays in the list until the owner searches or switches again.
   */
  function refreshCount() {
    const shown = effectiveLocationIds(gameStates(doc));

    unplacedIds = new Set(Array.from(shown).filter(([, id]) => id === '').map(([gameId]) => gameId));
    unplacedText.textContent = unplacedLabel(unplacedIds.size);
  }

  /**
   * Writes what the page shows now: the count line, or the note that says why the list is empty, and the same words for the
   * screen reader when the owner caused the change.
   * @param {{ shown: number, total: number, query: string, announce: boolean }} report What the finder did.
   */
  function onApply(report) {
    const text = resultText({
      shown: report.shown,
      total: report.total,
      query: report.query,
      unplacedOnly: unplacedSwitch.checked,
    });
    const empty = report.shown === 0;

    countLine.hidden = empty;
    countLine.textContent = empty ? '' : text;
    emptyNote.hidden = !empty;
    emptyNote.textContent = empty ? (report.query === '' ? OWNER_TEXT.everyPlaced : OWNER_TEXT.noMatch) : '';

    if (report.announce) {
      announce(text, doc);
    }

    syncSelectAll();
  }

  const finder = createFinder(input, gameRows, {
    debounceMs: 250,
    accepts: (row) => !unplacedSwitch.checked || unplacedIds.has(row.dataset.gameId),
    onApply,
  });

  sortByCollator(list);
  refreshCount();
  unplacedSwitch.addEventListener('change', () => finder.apply());
  selectAll.addEventListener('change', () => {
    const checks = shownRows().map((row) => row.querySelector('.game-check'));

    for (const check of checks) {
      check.checked = selectAll.checked;
    }

    checks.at(-1)?.dispatchEvent(new doc.defaultView.Event('change', { bubbles: true }));
    syncSelectAll();
  });
  list.addEventListener('change', (event) => {
    if (event.target.matches?.('.game-check')) {
      syncSelectAll();
    }
  });
  finder.apply({ announce: false });
  toolbar.hidden = false;

  return { refreshCount, syncSelectAll };
}
