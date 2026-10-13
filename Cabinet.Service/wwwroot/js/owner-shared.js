/**
 * What every owner page script shares: reading the antiforgery token, posting a change and mapping the answer to an outcome the
 * page can act on, the revision of the data the page was built from, the per-row status with its Undo button, and the page's
 * announcer. Nothing here touches the document until a function that needs it is called, so the module imports in a plain Node test.
 */

/** The name of the header the server reads the antiforgery token from. */
const TOKEN_HEADER = 'RequestVerificationToken';

/** The page revision the next post is made from. */
let currentRevision = null;

/** The row status that currently offers Undo, so the next change can take the button away. */
let undoRegion = null;

/**
 * Reads the antiforgery token the owner layout carries.
 * @param {Document} [doc] The document to read; the page's own by default.
 * @returns {string} The token, or an empty string when the page has none.
 */
export function readToken(doc = globalThis.document) {
  return doc?.querySelector?.('meta[name="request-verification-token"]')?.getAttribute('content') ?? '';
}

/**
 * Gives the revision of the owner data the page last knew.
 * @returns {number | null}
 */
export function getRevision() {
  return currentRevision;
}

/**
 * Sets the revision of the owner data the page knows, and writes it into every form of the page that carries the page revision,
 * so a form posted without script is never older than what the script saved. Forms that carry another revision, such as the
 * one an Undo button of a server-rendered notice posts, are left alone.
 * @param {number} revision The revision.
 * @param {Document} [doc] The document whose forms follow; none when it is left out in a test.
 */
export function setRevision(revision, doc = globalThis.document) {
  currentRevision = revision;

  for (const input of doc?.querySelectorAll?.('input[name="revision"][data-page-revision]') ?? []) {
    input.value = String(revision);
  }
}

/**
 * Reads the revision the page was built from out of its forms and keeps it as the page's revision.
 * @param {Document} [doc] The document to read; the page's own by default.
 * @returns {number | null} The revision, or null when the page has no form that carries it.
 */
export function initRevision(doc = globalThis.document) {
  const input = doc?.querySelector?.('input[name="revision"][data-page-revision]');
  const revision = input ? Number.parseInt(input.value, 10) : Number.NaN;

  currentRevision = Number.isFinite(revision) ? revision : null;

  return currentRevision;
}

/**
 * Posts one change to an owner handler as a script does: the fields and the revision form-encoded, the token in the header, and
 * a JSON answer asked for. The answer is mapped to an outcome: a 200 gives the server's JSON, a 409 gives `outOfDate`
 * or `unreadable`, a 422 gives `refused` with the server's words and status key, a 400 without a body gives `expired`, and any
 * other answer or a network error gives `failed`. A saved, unchanged or refused answer updates the page's revision; an out-of-date
 * answer does not, so the page keeps saying it is out of date until it is reloaded.
 * @param {string} url The handler's address.
 * @param {Record<string, string | number | Array<string | number>>} [fields] The form fields to send; a list is sent as one field per item.
 * @param {{ fetch?: Function, token?: string, revision?: number | null, keepalive?: boolean }} [options] The browser's fetch, the token
 * and the revision, replaceable in tests, and whether the request has to outlive the page (the antiforgery header still goes along).
 * @returns {Promise<{ outcome: string, status?: string, message?: string, revision?: number, canUndo?: boolean }>}
 */
export async function postOwner(url, fields = {}, options = {}) {
  const send = options.fetch ?? ((address, init) => globalThis.fetch(address, init));
  const token = options.token ?? readToken();
  const revision = options.revision ?? currentRevision;
  const body = new URLSearchParams();

  for (const [name, value] of Object.entries(fields)) {
    for (const each of Array.isArray(value) ? value : [value]) {
      body.append(name, String(each));
    }
  }

  if (revision !== null && revision !== undefined) {
    body.append('revision', String(revision));
  }

  let response;
  let text;

  try {
    response = await send(url, {
      method: 'POST',
      credentials: 'same-origin',
      headers: { Accept: 'application/json', [TOKEN_HEADER]: token },
      body,
      ...(options.keepalive ? { keepalive: true } : {}),
    });
    text = await response.text();
  } catch {
    return { outcome: 'failed' };
  }

  const result = mapAnswer(response.status, text);

  if (result.outcome === 'saved') {
    announceSaved();
  }

  return result;
}

/**
 * Tells the page that a change was saved, so what depends on the owner data, such as the backup reminder, can bring itself up to
 * date. It does nothing outside a page.
 */
function announceSaved() {
  const doc = globalThis.document;

  if (doc && typeof CustomEvent === 'function') {
    doc.dispatchEvent(new CustomEvent('owner-saved'));
  }
}

/**
 * Maps one answer of an owner handler to an outcome and keeps the page revision up to date.
 * @param {number} status The HTTP status.
 * @param {string} text The body as text.
 * @returns {{ outcome: string, status?: string, message?: string, revision?: number, canUndo?: boolean }}
 */
function mapAnswer(status, text) {
  if (status === 400 && text.trim() === '') {
    return { outcome: 'expired' };
  }

  let json;

  try {
    json = JSON.parse(text);
  } catch {
    return { outcome: 'failed' };
  }

  if (status === 200) {
    keepRevision(json);

    return json;
  }

  if (status === 409) {
    return { ...json, outcome: json.outcome === 'unreadable' ? 'unreadable' : 'outOfDate' };
  }

  if (status === 422) {
    keepRevision(json);

    return { ...json, outcome: 'refused' };
  }

  return { outcome: 'failed' };
}

/**
 * Adopts the revision an answer carries, when it carries a real one.
 * @param {{ revision?: number }} json The answer.
 */
function keepRevision(json) {
  if (Number.isInteger(json.revision) && json.revision > 0) {
    setRevision(json.revision);
  }
}

/**
 * Empties a status region and returns the document it belongs to.
 * @param {Element} region The status region.
 * @returns {Document}
 */
function reset(region) {
  region.replaceChildren();
  region.removeAttribute('data-problem');

  return region.ownerDocument;
}

/**
 * Takes the Undo button out of a status region and keeps its text.
 * @param {Element | null} region The status region, or null.
 */
function dropUndo(region) {
  region?.querySelector?.('button[data-undo]')?.remove();
}

/**
 * Removes what the server wrote about the post that brought the browser to this page: the notice at the top and the Undo forms
 * in the rows. Once a script saves another change they no longer describe the latest change, and their Undo would be refused.
 * @param {Document} doc The page.
 */
function dropServerNotices(doc) {
  doc.querySelector('.owner-notice')?.remove();

  for (const form of doc.querySelectorAll('.row-status form')) {
    form.remove();
  }
}

/**
 * Writes the status of a change that was saved into a row's own status region, with an Undo button when the change can be taken
 * back. Only the latest change can be taken back, so the Undo button of any other row goes away at the same moment. The text is
 * written as text, never as markup.
 * @param {Element} region The row's status region.
 * @param {string} text What happened.
 * The Undo button belongs to the change that was just saved: it remembers the revision of that save and hands it to the action, which
 * sends it with the undo, so the server refuses the undo when another change was saved since. A second press while the first is
 * still being handled does nothing.
 * @param {{ onUndo?: (saved: number | null) => void, undoLabel?: string }} [options] The action of the Undo button and its label; no button when there is no action.
 */
export function showRowStatus(region, text, options = {}) {
  const doc = reset(region);

  dropServerNotices(doc);

  if (undoRegion !== null && undoRegion !== region) {
    dropUndo(undoRegion);
  }

  undoRegion = null;

  const message = doc.createElement('span');
  message.textContent = text;
  region.append(message);

  if (typeof options.onUndo === 'function') {
    const button = doc.createElement('button');
    button.type = 'button';
    button.className = 'owner-btn';
    button.dataset.undo = '';
    button.textContent = options.undoLabel ?? '';
    const saved = currentRevision;

    button.addEventListener('click', () => {
      if (button.getAttribute('aria-disabled') === 'true') {
        return;
      }

      button.setAttribute('aria-disabled', 'true');
      options.onUndo(saved);
    });
    region.append(button);
    undoRegion = region;
  }
}

/**
 * Writes a failure into a row's status region without touching which change can be taken back, because nothing was changed.
 * @param {Element} region The row's status region.
 * @param {string} text What went wrong and what to do.
 * @param {{ onReload?: () => void, reloadLabel?: string }} [options] The action of a Reload button and its label.
 */
export function showRowProblem(region, text, options = {}) {
  const doc = reset(region);
  const message = doc.createElement('span');

  region.dataset.problem = 'true';
  message.textContent = text;
  region.append(message);

  if (typeof options.onReload === 'function') {
    const button = doc.createElement('button');
    button.type = 'button';
    button.className = 'owner-btn';
    button.textContent = options.reloadLabel ?? '';
    button.addEventListener('click', options.onReload);
    region.append(button);
  }
}

/**
 * Writes the out-of-date or expired message into a region with a Reload button that reloads the page.
 * @param {Element} region The row's status region.
 * @param {string} text The message.
 * @param {string} label The label of the button.
 * @param {() => void} [reload] Reloads the page; the browser's own reload by default.
 */
export function reloadButton(region, text, label, reload = () => globalThis.location.reload()) {
  showRowProblem(region, text, { onReload: reload, reloadLabel: label });
}

/**
 * Writes a text into the page's polite announcer, which every owner page carries as `#owner-status`.
 * @param {string} text What to announce.
 * @param {Document} [doc] The document that holds the announcer; the page's own by default.
 */
export function announce(text, doc = globalThis.document) {
  const region = doc?.getElementById?.('owner-status');

  if (region) {
    region.textContent = text;
  }
}

/** How close after a key press a change has to come to count as made by the keyboard. */
const KEY_WINDOW_MS = 100;

/**
 * Sets a picker's value and remembers it as the value that is saved, so a later failed save knows what to go back to.
 * @param {HTMLSelectElement} select The picker.
 * @param {string} value The value it now holds and has saved.
 */
export function setPickerValue(select, value) {
  select.value = value;
  select.dataset.saved = value;
}

/**
 * Saves a select's value once it has settled. A closed select fires a change for every arrow key, so stepping through the options
 * with the keyboard waits for a pause; a choice made with a pointer or a finger fires one change and saves at once. Leaving the
 * control, Enter and Tab save whatever is waiting, and a page that goes to the background saves it with the keepalive flag so
 * the request survives.
 * @param {HTMLSelectElement} select The select.
 * @param {(value: string, options: { keepalive: boolean }) => void} save Saves the value.
 * @param {number} [settleMs] How long keyboard stepping must pause before it saves.
 * @returns {{ flush: () => void }} A way to save what is waiting without delay.
 */
export function saveSettledValue(select, save, settleMs = 600) {
  let lastKeyAt = Number.NEGATIVE_INFINITY;
  let timer = null;
  let pending = false;

  /**
   * Saves the waiting value, if there is one.
   * @param {boolean} keepalive Whether the request has to outlive the page.
   */
  function flushNow(keepalive) {
    globalThis.clearTimeout(timer);
    timer = null;

    if (pending) {
      pending = false;
      save(select.value, { keepalive });
    }
  }

  select.addEventListener('keydown', (event) => {
    lastKeyAt = event.timeStamp;

    if (event.key === 'Enter' || event.key === 'Tab') {
      flushNow(false);
    }
  });
  select.addEventListener('change', (event) => {
    pending = true;
    globalThis.clearTimeout(timer);

    if (event.timeStamp - lastKeyAt <= KEY_WINDOW_MS) {
      timer = globalThis.setTimeout(() => flushNow(false), settleMs);
    } else {
      flushNow(false);
    }
  });
  select.addEventListener('focusout', () => flushNow(false));

  const doc = select.ownerDocument;

  doc?.addEventListener?.('visibilitychange', () => {
    if (doc.visibilityState === 'hidden') {
      flushNow(true);
    }
  });

  return { flush: () => flushNow(false) };
}

/**
 * Folds a text for searching: accents removed, lower case. `Café` and `cafe` fold to the same text.
 * @param {string} text The text.
 * @returns {string}
 */
export function foldForSearch(text) {
  return text.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
}

/**
 * Reads the title of a game row.
 * @param {Element} row The row.
 * @returns {string}
 */
function titleOfRow(row) {
  return row.querySelector('.game-title')?.textContent ?? '';
}

/**
 * Wires a search box to a list of rows. After the owner stops typing the rows whose title does not contain the query are hidden;
 * a base game whose expansion matches stays visible and is marked `data-context`, so the nesting stays readable. An extra rule
 * can narrow the rows further. Every run reports how many rows are shown out of how many there are.
 * @param {HTMLInputElement} input The search box.
 * @param {() => Element[]} rows Gives the rows to filter.
 * @param {{
 *   debounceMs?: number,
 *   accepts?: (row: Element) => boolean,
 *   titleOf?: (row: Element) => string,
 *   onApply?: (report: { shown: number, total: number, query: string, announce: boolean }) => void,
 * }} [options] The pause after the last key, an extra rule, how to read a title, and what to do with each report.
 * @returns {{ apply: (options?: { announce?: boolean }) => { shown: number, total: number, query: string, announce: boolean } }}
 */
export function createFinder(input, rows, options = {}) {
  const { debounceMs = 250, accepts = () => true, titleOf = titleOfRow, onApply = () => {} } = options;
  let timer = null;

  /**
   * Hides the rows that do not match and reports what is shown.
   * @param {{ announce?: boolean }} [run] Whether the report is meant to be announced.
   * @returns {{ shown: number, total: number, query: string, announce: boolean }}
   */
  function apply(run = {}) {
    globalThis.clearTimeout(timer);
    timer = null;

    const all = rows();
    const query = input.value.trim();
    const folded = foldForSearch(query);
    const passes = new Map(all.map((row) => [row, foldForSearch(titleOf(row)).includes(folded) && accepts(row)]));
    const parents = new Map(all.map((row) => [row.dataset.gameId, row]));
    const context = new Set();

    for (const row of all) {
      const parent = row.dataset.nested === 'true' && row.dataset.baseId ? parents.get(row.dataset.baseId) : undefined;

      if (passes.get(row) && parent && !passes.get(parent)) {
        context.add(parent);
      }
    }

    let shown = 0;

    for (const row of all) {
      const visible = passes.get(row) || context.has(row);

      row.hidden = !visible;

      if (context.has(row)) {
        row.dataset.context = 'true';
      } else {
        delete row.dataset.context;
      }

      shown += visible ? 1 : 0;
    }

    const report = { shown, total: all.length, query, announce: run.announce ?? true };

    onApply(report);

    return report;
  }

  input.addEventListener('input', () => {
    globalThis.clearTimeout(timer);
    timer = globalThis.setTimeout(() => apply(), debounceMs);
  });
  input.addEventListener('search', () => apply());

  return { apply };
}

/**
 * Puts the rows of a games list in the order the English collator gives, with numbers read as numbers, so `Game 2` comes before
 * `Game 10`: every game by title, each base game followed by its expansions, also by title.
 * @param {Element} container The list that holds the rows.
 */
export function sortByCollator(container) {
  const collator = new Intl.Collator('en', { numeric: true, sensitivity: 'base' });
  const current = Array.from(container.children);
  const position = new Map(current.map((row, index) => [row, index]));
  const present = new Set(current.map((row) => row.dataset.gameId));
  const byTitle = (first, second) =>
    collator.compare(titleOfRow(first), titleOfRow(second)) || position.get(first) - position.get(second);
  const isChild = (row) => row.dataset.nested === 'true' && Boolean(row.dataset.baseId) && present.has(row.dataset.baseId);
  const children = new Map();

  for (const row of current.filter(isChild)) {
    const siblings = children.get(row.dataset.baseId) ?? [];

    siblings.push(row);
    children.set(row.dataset.baseId, siblings);
  }

  const ordered = [];

  for (const row of current.filter((candidate) => !isChild(candidate)).sort(byTitle)) {
    ordered.push(row, ...(children.get(row.dataset.gameId) ?? []).sort(byTitle));
  }

  if (ordered.some((row, index) => row !== current[index])) {
    container.append(...ordered);
  }
}
