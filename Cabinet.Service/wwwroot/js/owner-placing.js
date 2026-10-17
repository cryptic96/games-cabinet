/**
 * The script side of the games list on the locations page: ticking rows, the selection dock that slides up while any row is
 * ticked, and the dialog of buttons that asks where the ticked games go. A move is one request for every ticked copy and one
 * change on the server, so one Undo takes all of it back. The dock then shows what happened, with Undo and Dismiss, until the
 * owner ticks a row again. Every name and title is written with `textContent`, never as markup.
 */
import {
  OWNER_TEXT,
  clearedGamesText,
  effectiveLocationIds,
  movedGamesText,
  moveTitleText,
  selectedText,
} from './owner-format.js';
import { HEX_COLOUR, announce, dismissOnBackdrop, postOwner, setPickerValue, showRowProblem, showRowStatus } from './owner-shared.js';

/** The handler addresses, relative to the page the script runs on. */
const HANDLERS = Object.freeze({
  bulkMove: '?handler=BulkMove',
  undo: '?handler=Undo',
});

/** How long the count of ticked games waits for a further tick before it is announced. */
const ANNOUNCE_DELAY_MS = 300;

/** The prefix of a target that names a location. */
const LOCATION_TARGET = 'location:';

/**
 * Reads where every game of the list is kept now and which base game each expansion follows.
 * @param {Document} doc The page.
 * @returns {Array<{ id: string, own: string, baseId: string | null }>} The games in list order.
 */
export function gameStates(doc) {
  return Array.from(doc.querySelectorAll('.game-row')).map((row) => ({
    id: row.dataset.entryId,
    own: row.querySelector('select[name="locationId"]')?.value ?? '',
    baseId: row.dataset.baseEntryId || null,
  }));
}

/**
 * Builds the dialog that asks where the ticked games go. It is built once; its buttons are filled in each time it opens.
 * @param {Document} doc The page.
 * @returns {{ element: HTMLDialogElement, title: HTMLElement, options: HTMLElement, keep: HTMLButtonElement }}
 */
function buildMoveDialog(doc) {
  const element = doc.createElement('dialog');
  const title = doc.createElement('h3');
  const options = doc.createElement('div');
  const actions = doc.createElement('div');
  const keep = doc.createElement('button');

  element.className = 'owner-dialog';
  element.dataset.variant = 'move';
  element.setAttribute('aria-labelledby', 'move-dialog-title');
  title.id = 'move-dialog-title';
  title.dir = 'auto';
  options.className = 'move-options';
  actions.className = 'dialog-actions';
  keep.type = 'button';
  keep.className = 'owner-btn';
  keep.textContent = OWNER_TEXT.keepSelection;
  actions.append(keep);
  element.append(title, options, actions);
  doc.body.append(element);

  return { element, title, options, keep };
}

/**
 * Wires the ticked rows of the games list, the selection dock and the dialog that asks where the ticked games go.
 * @param {Document} doc The page.
 * @param {{
 *   enqueue: (task: () => Promise<void>) => void,
 *   locations: () => Array<{ id: string, name: string }>,
 *   showProblem: (region: HTMLElement, result: object) => void,
 *   gamesChanged: () => void,
 *   selectionChanged?: () => void,
 * }} hooks What the list of locations offers: one request at a time, the locations in order, the way a failure is worded, the
 * call that brings the counts of every location in line after games moved, and the call that tells the toolbar the ticks changed.
 * @returns {{ refreshMeta: () => void } | null} The part other code needs, or null when the page has no games list.
 */
export function initPlacing(doc, hooks) {
  const list = doc.querySelector('.game-list');
  const dock = doc.getElementById('select-dock');

  if (!list || !dock) {
    return null;
  }

  const countText = doc.getElementById('dock-count');
  const status = doc.getElementById('dock-status');
  const moveButton = doc.getElementById('dock-move');
  const clearButton = doc.getElementById('dock-clear');
  const heading = doc.getElementById('games-heading');
  const panel = list.closest('.owner-panel') ?? list;
  const move = buildMoveDialog(doc);
  let announceTimer = null;
  let chosen = false;

  /**
   * Lists every game row.
   * @returns {HTMLElement[]}
   */
  function gameRows() {
    return Array.from(list.querySelectorAll('.game-row'));
  }

  /**
   * Lists the rows whose checkbox is ticked.
   * @returns {HTMLElement[]}
   */
  function tickedRows() {
    return gameRows().filter((row) => row.querySelector('.game-check')?.checked);
  }

  /**
   * Tells whether a row is an expansion that follows an owned base game.
   * @param {HTMLElement} row The row.
   * @returns {boolean}
   */
  function followsBase(row) {
    return Boolean(row.dataset.baseEntryId);
  }

  /**
   * Gives the picker of a row.
   * @param {HTMLElement} row The row.
   * @returns {HTMLSelectElement}
   */
  function pickerOf(row) {
    return row.querySelector('select[name="locationId"]');
  }

  /**
   * Tells whether the copies of the base game an expansion row follows are shown in more than one place.
   * @param {HTMLElement} row The expansion's row.
   * @param {Map<string, string>} shown Where each copy is shown now, by collection entry.
   * @returns {boolean}
   */
  function baseCopiesDiffer(row, shown) {
    const places = gameRows()
      .filter((candidate) => candidate.dataset.gameId === row.dataset.baseId)
      .map((candidate) => shown.get(candidate.dataset.entryId) ?? '');

    return new Set(places).size > 1;
  }

  /**
   * Shows the dock. A dock that was hidden first takes its place below the screen, then slides up.
   * @param {string} state `selecting`, `done` or `error`.
   */
  function show(state) {
    if (dock.hidden) {
      dock.hidden = false;
      void dock.offsetHeight;
    }

    dock.dataset.state = state;
  }

  /**
   * Hides the dock.
   */
  function hide() {
    dock.hidden = true;
    delete dock.dataset.state;
  }

  /**
   * Empties the dock's status.
   */
  function resetStatus() {
    status.replaceChildren();
    status.removeAttribute('data-problem');
  }

  /**
   * Closes the dock's done state and puts focus back at the top of the games.
   */
  function dismiss() {
    resetStatus();
    hide();
    heading?.focus();
  }

  /**
   * Makes the Dismiss button of the done state.
   * @returns {HTMLButtonElement}
   */
  function dismissButton() {
    const button = doc.createElement('button');

    button.type = 'button';
    button.className = 'owner-btn';
    button.dataset.dismiss = '';
    button.textContent = OWNER_TEXT.dismiss;
    button.addEventListener('click', dismiss);

    return button;
  }

  /**
   * Leaves the done state without moving focus, as a search or a filter change does.
   */
  function leaveDone() {
    if (dock.dataset.state === 'done') {
      resetStatus();
      hide();
    }
  }

  /**
   * Brings the dock in line with the ticked rows: hidden at none, the count from the first tick on, and the count announced once
   * the ticking has settled.
   */
  function onTick() {
    const count = tickedRows().length;

    globalThis.clearTimeout(announceTimer);
    resetStatus();
    hooks.selectionChanged?.();

    if (count === 0) {
      hide();

      return;
    }

    countText.textContent = selectedText(count);
    show('selecting');
    announceTimer = globalThis.setTimeout(() => announce(selectedText(count), doc), ANNOUNCE_DELAY_MS);
  }

  /**
   * Unticks every row.
   */
  function untickAll() {
    for (const row of tickedRows()) {
      row.querySelector('.game-check').checked = false;
    }

    hooks.selectionChanged?.();
  }

  /**
   * Rewrites where each row says its game is kept, from the pickers and the current names of the locations.
   */
  function refreshMeta() {
    const shown = effectiveLocationIds(gameStates(doc));
    const names = new Map(hooks.locations().map((location) => [location.id, location.name]));

    for (const row of gameRows()) {
      const id = shown.get(row.dataset.entryId) ?? '';
      const where = row.querySelector('.game-where');
      const follow = row.querySelector('.game-follow');

      if (names.has(id)) {
        where.textContent = names.get(id);
        where.removeAttribute('data-unplaced');
      } else {
        where.textContent = OWNER_TEXT.notPlaced;
        where.dataset.unplaced = 'true';
      }

      const hint = row.querySelector('[data-follow-hint]');

      if (follow) {
        follow.hidden = pickerOf(row).value !== '';
      }

      if (hint) {
        hint.hidden = pickerOf(row).value !== '' || !baseCopiesDiffer(row, shown);
      }
    }
  }

  /**
   * Shows what a move came to in the dock, with Undo when there is something to take back and always Dismiss, and moves focus to it.
   * @param {string} text What happened.
   * @param {(() => void) | null} onUndo Takes the move back; none when nothing changed.
   */
  function showDone(text, onUndo) {
    globalThis.clearTimeout(announceTimer);
    untickAll();
    showRowStatus(status, text, onUndo ? { undoLabel: OWNER_TEXT.undo, onUndo } : {});
    status.append(dismissButton());
    show('done');
    status.focus();
  }

  /**
   * Shows a failure in the dock's status. The done state has no other way out, so it keeps Dismiss; a failed move keeps the
   * selection and the buttons that go with it.
   * @param {{ outcome: string, status?: string }} result What the request came to.
   * @param {boolean} inDone Whether the dock is showing the done state.
   */
  function showFailure(result, inDone) {
    if (result.outcome === 'outOfDate' && result.status === 'cant-undo') {
      showRowProblem(status, OWNER_TEXT.cantUndo);
    } else {
      hooks.showProblem(status, result);
    }

    if (inDone) {
      status.append(dismissButton());
    } else {
      show('error');
    }
  }

  /**
   * Takes back the move that was just made and puts every game's picker back where it was.
   * @param {Array<{ row: HTMLElement, own: string }>} previous Where each moved game was kept before.
   * @returns {(saved?: number | null) => void} The click handler of the Undo button; it gets the revision the change was saved at.
   */
  function undoMove(previous) {
    return (saved) =>
      hooks.enqueue(async () => {
        const result = await postOwner(HANDLERS.undo, {}, { revision: saved });

        if (result.outcome !== 'saved') {
          showFailure(result, true);

          return;
        }

        for (const { row, own } of previous) {
          setPickerValue(pickerOf(row), own);
        }

        hooks.gamesChanged();
        showDone(OWNER_TEXT.undone, null);
      });
  }

  /**
   * Moves the ticked games and shows the outcome in the dock.
   * @param {string} target `location:{id}`, `not-placed` or `same-as-base`.
   */
  function startMove(target) {
    const moving = tickedRows();
    const eligible = target === 'same-as-base' ? moving.filter(followsBase) : moving;
    const previous = moving.map((row) => ({ row, own: pickerOf(row).value }));

    hooks.enqueue(async () => {
      const result = await postOwner(HANDLERS.bulkMove, { collectionIds: moving.map((row) => row.dataset.entryId), target });

      if (result.outcome === 'noChange') {
        showDone(OWNER_TEXT.nothingToChange, null);

        return;
      }

      if (result.outcome !== 'saved') {
        showFailure(result, false);

        return;
      }

      const own = target.startsWith(LOCATION_TARGET) ? target.slice(LOCATION_TARGET.length) : '';

      for (const row of eligible) {
        setPickerValue(pickerOf(row), own);
      }

      hooks.gamesChanged();

      const following = eligible.filter(followsBase).length;
      const name = hooks.locations().find((location) => location.id === own)?.name ?? '';
      const text = own === '' ? clearedGamesText(eligible.length - following, following) : movedGamesText(moving.length, name);

      showDone(text, undoMove(previous));
    });
  }

  /**
   * Makes one button of the move dialog.
   * @param {string} text What it says.
   * @param {string} target What choosing it asks for.
   * @returns {HTMLButtonElement}
   */
  function optionButton(text, target) {
    const button = doc.createElement('button');

    button.type = 'button';
    button.className = 'owner-btn move-option';
    button.dir = 'auto';
    button.textContent = text;
    button.addEventListener('click', () => {
      chosen = true;
      move.element.close();
      startMove(target);
    });

    return button;
  }

  /**
   * Opens the dialog with one button per location, then Not placed, then Same as base game when the selection holds an
   * expansion that has an owned base game, and puts focus on the first.
   */
  function openMoveDialog() {
    const ticked = tickedRows();

    if (ticked.length === 0) {
      return;
    }

    const buttons = hooks.locations().map((location) => optionButton(location.name, LOCATION_TARGET + location.id));

    buttons.push(optionButton(OWNER_TEXT.notPlaced, 'not-placed'));

    if (ticked.some(followsBase)) {
      const same = optionButton(OWNER_TEXT.sameAsBase, 'same-as-base');

      same.classList.add('move-separate');
      buttons.push(same);
    }

    chosen = false;
    move.title.textContent = moveTitleText(ticked.length);
    move.options.replaceChildren(...buttons);
    move.element.showModal();
    buttons[0].focus();
  }

  for (const swatch of list.querySelectorAll('[data-swatch]')) {
    if (HEX_COLOUR.test(swatch.dataset.swatch)) {
      swatch.style.backgroundColor = swatch.dataset.swatch;
    }
  }

  list.addEventListener('change', (event) => {
    if (event.target.matches?.('.game-check')) {
      onTick();
    }
  });
  panel.addEventListener('input', (event) => {
    if (event.target.matches?.('.owner-find input, input[type="search"]')) {
      leaveDone();
    }
  });
  panel.addEventListener('change', (event) => {
    if (event.target.matches?.('input[role="switch"]')) {
      leaveDone();
    }
  });
  moveButton.addEventListener('click', openMoveDialog);
  clearButton.addEventListener('click', () => {
    untickAll();
    onTick();
    heading?.focus();
  });
  move.keep.addEventListener('click', () => move.element.close());
  dismissOnBackdrop(move.element);
  move.element.addEventListener('close', () => {
    if (!chosen) {
      moveButton.focus();
    }
  });

  list.dataset.enhanced = '';

  return { refreshMeta };
}
