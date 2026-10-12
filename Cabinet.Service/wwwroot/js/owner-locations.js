/**
 * The script side of the owner's list of locations. The page works with real forms; this script takes over their submission so a
 * change is saved without leaving the page: rename in place, move up and down with focus kept on the same button, delete with a
 * question that names how many games become not placed, add at the end of the list, and Undo for the latest change. Every answer
 * of the server goes through one request at a time, so a quick second press never works from an old revision, and every name
 * and title is written with `textContent`, never as markup.
 */
import {
  OWNER_TEXT,
  deleteDialogParts,
  deletedText,
  fill,
  gameCount,
  headingText,
  movedText,
  refusalText,
} from './owner-format.js';
import {
  announce,
  getRevision,
  initRevision,
  postOwner,
  reloadButton,
  setRevision,
  showRowProblem,
  showRowStatus,
} from './owner-shared.js';

/** The handler addresses, relative to the page the script runs on. */
const HANDLERS = Object.freeze({
  rename: '?handler=Rename',
  moveUp: '?handler=MoveUp',
  moveDown: '?handler=MoveDown',
  remove: '?handler=Delete',
  removeConfirmed: '?handler=DeleteConfirmed',
  add: '?handler=AddLocation',
  undo: '?handler=Undo',
});

/**
 * Finds the parts of the list the script works on.
 * @param {Document} doc The page.
 * @returns {object | null} The parts, or null when the page has no list of locations.
 */
function findParts(doc) {
  const panel = doc.getElementById('your-locations');
  const list = doc.getElementById('loc-list');
  const template = doc.getElementById('loc-item-template');
  const addForm = doc.getElementById('add-location-form');
  const dialog = doc.getElementById('loc-delete-dialog');

  if (!panel || !list || !template || !addForm || !dialog) {
    return null;
  }

  return {
    panel,
    list,
    template,
    addForm,
    dialog,
    empty: doc.getElementById('loc-empty'),
    heading: doc.getElementById('locations-heading'),
    addField: doc.getElementById('new-location'),
    addError: doc.getElementById('new-location-error'),
    addStatus: doc.getElementById('new-location-status'),
    title: doc.getElementById('loc-delete-title'),
    body: doc.getElementById('loc-delete-body'),
    confirmButton: doc.getElementById('loc-delete-confirm'),
    keepButton: doc.getElementById('loc-delete-keep'),
  };
}

/**
 * Wires the list of locations, the add form and the delete dialog of the page.
 * @param {Document} [doc] The page; the browser's own by default.
 */
export function initLocationList(doc = globalThis.document) {
  const parts = doc ? findParts(doc) : null;

  if (parts === null) {
    return;
  }

  const { panel, list, template, addForm, dialog, empty, heading, addField, addError, addStatus } = parts;
  let chain = Promise.resolve();
  let opener = null;
  let confirmed = false;
  let pendingDelete = null;

  initRevision(doc);

  /**
   * Runs a task after every earlier one has finished, so only one change is ever in flight.
   * @param {() => Promise<void>} task The work.
   */
  function enqueue(task) {
    chain = chain.then(task, task);
  }

  /**
   * Lists the items of the list in their order.
   * @returns {HTMLElement[]}
   */
  function items() {
    return Array.from(list.children);
  }

  /**
   * Gives the name field of an item.
   * @param {HTMLElement} item The item.
   * @returns {HTMLInputElement}
   */
  function nameField(item) {
    return item.querySelector('.loc-name');
  }

  /**
   * Gives one of the buttons of an item.
   * @param {HTMLElement} item The item.
   * @param {string} selector What to look for.
   * @returns {HTMLButtonElement}
   */
  function button(item, selector) {
    return item.querySelector(selector);
  }

  /**
   * Gives the status region of an item.
   * @param {HTMLElement} item The item.
   * @returns {HTMLElement}
   */
  function statusOf(item) {
    return item.querySelector('.row-status');
  }

  /**
   * Shows or clears the inline name error of an item or of the add form.
   * @param {HTMLInputElement} field The name field.
   * @param {HTMLElement} region The element that holds the message.
   * @param {string | null} text The message, or null to clear it.
   * @param {string} [baseDescribedBy] The description the field has besides the error.
   */
  function setNameError(field, region, text, baseDescribedBy = '') {
    region.textContent = text ?? '';

    if (text === null) {
      field.removeAttribute('aria-invalid');

      if (baseDescribedBy === '') {
        field.removeAttribute('aria-describedby');
      } else {
        field.setAttribute('aria-describedby', baseDescribedBy);
      }

      return;
    }

    field.setAttribute('aria-invalid', 'true');
    field.setAttribute('aria-describedby', (baseDescribedBy + ' ' + region.id).trim());
  }

  /**
   * Brings the names, labels, counts and disabled ends of every item in line with the list's order and content.
   */
  function refresh() {
    const all = items();

    all.forEach((item, index) => {
      const name = item.dataset.saved ?? '';
      const up = button(item, '[data-step="up"]');
      const down = button(item, '[data-step="down"]');
      const field = nameField(item);

      field.setAttribute('aria-label', fill(OWNER_TEXT.nameLabel, { position: index + 1 }));
      up.setAttribute('aria-label', fill(OWNER_TEXT.moveLabel, { name, direction: 'up' }));
      down.setAttribute('aria-label', fill(OWNER_TEXT.moveLabel, { name, direction: 'down' }));
      button(item, '.loc-delete').setAttribute('aria-label', fill(OWNER_TEXT.deleteLabel, { name }));
      setAriaDisabled(up, index === 0);
      setAriaDisabled(down, index === all.length - 1);
      button(item, '.loc-save').hidden = true;
      item.querySelector('.loc-count').textContent = gameCount(Number.parseInt(item.dataset.games ?? '0', 10));

    });

    heading.textContent = headingText(all.length);
    list.hidden = all.length === 0;

    if (empty) {
      empty.hidden = all.length > 0;
    }
  }

  /**
   * Sets or clears the disabled look and state of a button that must keep focus.
   * @param {HTMLElement} element The button.
   * @param {boolean} disabled Whether it is disabled.
   */
  function setAriaDisabled(element, disabled) {
    if (disabled) {
      element.setAttribute('aria-disabled', 'true');
    } else {
      element.removeAttribute('aria-disabled');
    }
  }

  /**
   * Rebuilds the options of every game's location picker from the list, keeping each picker on its place when that place still
   * exists, so a game picked a moment ago never offers a location that is gone or an old name.
   */
  function syncPickers() {
    for (const select of doc.querySelectorAll('.game-row select[name="locationId"]')) {
      const previous = select.value;

      for (const option of Array.from(select.options)) {
        if (option.value !== '') {
          option.remove();
        }
      }

      for (const item of items()) {
        const option = doc.createElement('option');

        option.value = item.dataset.locationId;
        option.textContent = item.dataset.saved;
        select.append(option);
      }

      select.value = Array.from(select.options).some((option) => option.value === previous) ? previous : '';
    }
  }

  /**
   * Tells the owner that a request did not go through, in the region of the row it was about.
   * @param {HTMLElement} region The status region.
   * @param {{ outcome: string, status?: string }} result What the request came to.
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
   * Takes back the latest change on the server and, when that worked, takes it back on the page too.
   * @param {HTMLElement} region The status region the Undo button was in.
   * @param {() => void} apply Takes the change back on the page.
   * @param {() => HTMLElement} focusAfter Gives the element that takes focus once the Undo button is gone.
   * @returns {() => void} The click handler of the Undo button.
   */
  function undoer(region, apply, focusAfter) {
    return () =>
      enqueue(async () => {
        const result = await postOwner(HANDLERS.undo, {});

        if (result.outcome === 'saved') {
          apply();
          refresh();
          syncPickers();
          showRowStatus(region, OWNER_TEXT.undone);
          focusAfter().focus();
        } else if (result.outcome === 'outOfDate' && result.status === 'cant-undo') {
          showRowProblem(region, OWNER_TEXT.cantUndo);
        } else {
          showProblem(region, result);
        }
      });
  }

  /**
   * Saves a new name for an item when it differs from the saved one.
   * @param {HTMLElement} item The item.
   */
  async function commitRename(item) {
    const field = nameField(item);
    const saved = item.dataset.saved;
    const entered = field.value.trim();
    const region = statusOf(item);
    const errorRegion = item.querySelector('.field-error');

    if (entered === saved) {
      field.value = saved;
      setNameError(field, errorRegion, null);

      return;
    }

    const result = await postOwner(HANDLERS.rename, { id: item.dataset.locationId, name: field.value });

    if (result.outcome === 'saved') {
      item.dataset.saved = entered;
      field.value = entered;
      setNameError(field, errorRegion, null);
      refresh();
      syncPickers();
      showRowStatus(region, fill(OWNER_TEXT.renamed, { name: entered }), {
        undoLabel: OWNER_TEXT.undo,
        onUndo: undoer(
          region,
          () => {
            item.dataset.saved = saved;
            field.value = saved;
          },
          () => field,
        ),
      });
    } else if (result.outcome === 'noChange') {
      field.value = saved;
      setNameError(field, errorRegion, null);
    } else if (result.outcome === 'refused' && refusalText(result.status, entered) !== null) {
      setNameError(field, errorRegion, refusalText(result.status, entered));
    } else {
      field.value = saved;
      showProblem(region, result);
    }
  }

  /**
   * Moves an item one place and keeps focus on the button that was pressed.
   * @param {HTMLElement} item The item.
   * @param {HTMLElement} pressed The Up or Down button.
   */
  async function commitMove(item, pressed) {
    const up = pressed.dataset.step === 'up';
    const region = statusOf(item);
    const result = await postOwner(up ? HANDLERS.moveUp : HANDLERS.moveDown, { id: item.dataset.locationId });

    if (result.outcome === 'noChange') {
      return;
    }

    if (result.outcome !== 'saved') {
      showProblem(region, result);

      return;
    }

    shift(item, up);
    pressed.focus();

    const text = movedText(item.dataset.saved, up ? 'up' : 'down', items().indexOf(item) + 1, items().length);

    showRowStatus(region, text, {
      undoLabel: OWNER_TEXT.undo,
      onUndo: undoer(
        region,
        () => {
          shift(item, !up);
          announce(OWNER_TEXT.undone, doc);
        },
        () => pressed,
      ),
    });
    announce(text, doc);
  }

  /**
   * Moves an item one place in the list and brings every label in line.
   * @param {HTMLElement} item The item.
   * @param {boolean} up Whether it moves up.
   */
  function shift(item, up) {
    if (up) {
      list.insertBefore(item, item.previousElementSibling);
    } else if (item.nextElementSibling) {
      list.insertBefore(item.nextElementSibling, item);
    }

    refresh();
    syncPickers();
  }

  /**
   * Starts deleting an item: at once when no game would become not placed, otherwise after the question.
   * @param {HTMLElement} item The item.
   * @param {HTMLElement} pressed The Delete button.
   */
  function startDelete(item, pressed) {
    const games = Number.parseInt(item.dataset.notPlaced ?? '0', 10);

    if (games === 0) {
      enqueue(() => commitDelete(item, HANDLERS.remove, 0, pressed));

      return;
    }

    const { lead, rest } = deleteDialogParts(games);

    opener = pressed;
    confirmed = false;
    pendingDelete = { item, games };
    parts.title.textContent = fill(OWNER_TEXT.dialogTitle, { name: item.dataset.saved });
    parts.body.querySelector('strong').textContent = lead;
    parts.body.querySelector('span').textContent = rest;
    dialog.showModal();

    if (doc.activeElement !== parts.keepButton) {
      parts.keepButton.focus();
    }
  }

  /**
   * Deletes an item on the server and, when that worked, on the page.
   * @param {HTMLElement} item The item.
   * @param {string} handler Which delete handler to ask.
   * @param {number} games How many games become not placed.
   * @param {HTMLElement} pressed The Delete button, which keeps focus when the delete fails.
   */
  async function commitDelete(item, handler, games, pressed) {
    const result = await postOwner(handler, { id: item.dataset.locationId });

    if (result.outcome !== 'saved') {
      showProblem(statusOf(item), result);
      pressed.focus();

      return;
    }

    const index = items().indexOf(item);
    const name = item.dataset.saved;
    const placed = Array.from(doc.querySelectorAll('.game-row select[name="locationId"]')).filter(
      (select) => select.value === item.dataset.locationId,
    );

    item.remove();
    refresh();
    syncPickers();
    showRowStatus(addStatus, deletedText(name, games), {
      undoLabel: OWNER_TEXT.undo,
      onUndo: undoer(
        addStatus,
        () => {
          list.insertBefore(item, items()[index] ?? null);
          refresh();
          syncPickers();

          for (const select of placed) {
            select.value = item.dataset.locationId;
          }
        },
        () => nameField(item),
      ),
    });

    const next = items()[index];

    (next ? nameField(next) : addField).focus();
  }

  /**
   * Adds a location at the end of the list.
   */
  async function commitAdd() {
    const entered = addField.value;
    const result = await postOwner(HANDLERS.add, { name: entered });

    if (result.outcome === 'saved' && typeof result.id === 'string') {
      const name = entered.trim();
      const item = buildItem(result.id, name);

      list.append(item);
      panel.open = true;
      addField.value = '';
      addField.focus();
      setNameError(addField, addError, null, 'new-location-hint');
      refresh();
      syncPickers();
      showRowStatus(addStatus, fill(OWNER_TEXT.added, { name }), {
        undoLabel: OWNER_TEXT.undo,
        onUndo: undoer(
          addStatus,
          () => {
            item.remove();
          },
          () => addField,
        ),
      });
    } else if (result.outcome === 'refused' && refusalText(result.status, entered.trim()) !== null) {
      setNameError(addField, addError, refusalText(result.status, entered.trim()), 'new-location-hint');
    } else {
      showProblem(addStatus, result);
    }
  }

  /**
   * Makes the item of a location the server just added from the page's template.
   * @param {string} id The new location's identifier.
   * @param {string} name Its name.
   * @returns {HTMLElement}
   */
  function buildItem(id, name) {
    const item = template.content.firstElementChild.cloneNode(true);

    item.id = 'loc-' + id;
    item.dataset.locationId = id;
    item.dataset.saved = name;
    item.dataset.games = '0';
    item.dataset.notPlaced = '0';
    item.querySelector('input[name="id"]').value = id;
    nameField(item).value = name;
    item.querySelector('.field-error').id = 'loc-error-' + id;
    setRevision(getRevision(), doc);

    return item;
  }

  list.addEventListener('submit', (event) => {
    const form = event.target;

    if (!form.matches?.('.loc-form')) {
      return;
    }

    event.preventDefault();

    const item = form.closest('.loc-item');
    const pressed = event.submitter;

    if (pressed?.getAttribute('aria-disabled') === 'true') {
      return;
    }

    if (pressed?.matches('[data-step]')) {
      enqueue(() => commitMove(item, pressed));
    } else if (pressed?.matches('.loc-delete')) {
      startDelete(item, pressed);
    } else {
      enqueue(() => commitRename(item));
    }
  });

  list.addEventListener('change', (event) => {
    if (event.target.matches?.('.loc-name')) {
      enqueue(() => commitRename(event.target.closest('.loc-item')));
    }
  });

  list.addEventListener('keydown', (event) => {
    if (event.key === 'Escape' && event.target.matches?.('.loc-name')) {
      const item = event.target.closest('.loc-item');

      event.target.value = item.dataset.saved;
      setNameError(event.target, item.querySelector('.field-error'), null);
    }
  });

  addForm.addEventListener('submit', (event) => {
    event.preventDefault();
    enqueue(commitAdd);
  });

  dialog.addEventListener('click', (event) => {
    if (event.target !== dialog) {
      return;
    }

    const box = dialog.getBoundingClientRect();
    const outside =
      event.clientX < box.left || event.clientX > box.right || event.clientY < box.top || event.clientY > box.bottom;

    if (outside) {
      dialog.close();
    }
  });

  dialog.addEventListener('close', () => {
    if (!confirmed) {
      opener?.focus();
    }
  });

  parts.keepButton.addEventListener('click', () => dialog.close());

  parts.confirmButton.addEventListener('click', () => {
    const { item, games } = pendingDelete;
    const pressed = opener;

    confirmed = true;
    dialog.close();
    enqueue(() => commitDelete(item, HANDLERS.removeConfirmed, games, pressed));
  });

  for (const item of items()) {
    item.dataset.saved = nameField(item).value;
  }

  refresh();
  panel.dataset.enhanced = '';
}

initLocationList();
