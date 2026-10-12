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
 * @param {{ fetch?: Function, token?: string, revision?: number | null }} [options] The browser's fetch, the token and the revision, replaceable in tests.
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
    });
    text = await response.text();
  } catch {
    return { outcome: 'failed' };
  }

  return mapAnswer(response.status, text);
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
 * @param {{ onUndo?: () => void, undoLabel?: string }} [options] The action of the Undo button and its label; no button when there is no action.
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
    button.addEventListener('click', options.onUndo);
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
