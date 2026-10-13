/**
 * The script every owner page loads for the backup: it rewrites each time on the page into the viewer's own clock, shows
 * `Preparing...` on the download button while the file is made, and keeps the reminder banner, the line that says when the last
 * download was and the line in the change log at the last download up to date without a reload. The page works without it:
 * the download is a real form post and every time has a readable UTC text. The state comes from the server as numbers and a
 * time, and every text is written with `textContent`.
 */
import { OWNER_TEXT, bannerTexts, fill, whenText } from './owner-format.js';
import { announce, readToken } from './owner-shared.js';

/** How long after a download starts the page asks the server how the backup stands. */
const REFRESH_DELAY_MS = 1500;

/** How long after a saved change the page waits before asking, so a burst of changes asks once. */
const SAVED_DELAY_MS = 300;

/** The buttons that are showing the busy text, with the children and name they get back. */
const busyButtons = new WeakMap();

/**
 * Rewrites every `time` element that carries a moment in the viewer's own clock, and keeps its `datetime` attribute.
 * @param {ParentNode} root The part of the page to rewrite.
 * @param {Date} [now] The current moment, replaceable in tests.
 */
export function localiseTimes(root, now = new Date()) {
  for (const element of root.querySelectorAll('time[data-owner-when]')) {
    const text = whenText(element.getAttribute('datetime') ?? '', now, { capitalise: element.hasAttribute('data-capitalise') });

    if (text !== '') {
      element.textContent = text;
    }
  }
}

/**
 * Asks the server how the backup stands.
 * @param {string} url The address of the state.
 * @returns {Promise<{ readable: boolean, hasData: boolean, changes: number, lastDownloadAtUtc: string | null, neverDownloaded: boolean } | null>}
 * The state, or null when the answer is missing or is not a state, so the page stays as it is.
 */
export async function fetchBackupState(url) {
  try {
    const response = await globalThis.fetch(url, { credentials: 'same-origin', cache: 'no-store', headers: { Accept: 'application/json' } });

    if (!response.ok) {
      return null;
    }

    const state = await response.json();

    return Number.isInteger(state?.changes) && state.changes >= 0 ? state : null;
  } catch {
    return null;
  }
}

/**
 * Writes the banner's two texts, the sentence and the short line, and shows or hides it.
 * @param {Document} doc The page.
 * @param {{ changes: number, neverDownloaded: boolean }} state The state of the backup.
 */
function applyBanner(doc, state) {
  const banner = doc.getElementById('owner-banner');

  if (!banner) {
    return;
  }

  const texts = bannerTexts(state.changes, state.neverDownloaded);

  for (const element of banner.querySelectorAll('.owner-banner-text [data-banner]')) {
    element.textContent = element.dataset.banner === 'full' ? texts.full : texts.compact;
  }

  banner.hidden = state.changes === 0;
}

/**
 * Shows the quiet download line of the backup panel when nothing is waiting to be downloaded, and hides it while the banner shows.
 * @param {Document} doc The page.
 * @param {{ changes: number, lastDownloadAtUtc: string | null }} state The state of the backup.
 * @param {Date} now The current moment.
 */
function applyQuietLine(doc, state, now) {
  const quiet = doc.getElementById('backup-quiet');
  const line = doc.getElementById('backup-line');
  const time = doc.getElementById('backup-time');

  if (!quiet || !line || !time) {
    return;
  }

  quiet.hidden = state.changes > 0;
  line.hidden = state.lastDownloadAtUtc === null;

  if (state.lastDownloadAtUtc !== null) {
    time.setAttribute('datetime', state.lastDownloadAtUtc);
    time.textContent = whenText(state.lastDownloadAtUtc, now);
  }
}

/**
 * Moves the line that marks the last download in the change log: it goes after as many entries as there are changes since the
 * download, and is left out when nothing was downloaded or the download lies further back than the entries shown.
 * @param {Document} doc The page.
 * @param {{ changes: number, lastDownloadAtUtc: string | null }} state The state of the backup.
 * @param {Date} now The current moment.
 */
function applyDivider(doc, state, now) {
  const list = doc.getElementById('change-log');

  if (!list) {
    return;
  }

  list.querySelector('[data-divider]')?.remove();

  const entries = [...list.querySelectorAll('.change-entry')];

  if (state.lastDownloadAtUtc === null || state.changes > entries.length) {
    return;
  }

  const divider = doc.createElement('li');
  const time = doc.createElement('time');

  divider.className = 'change-divider';
  divider.dataset.divider = '';
  time.setAttribute('datetime', state.lastDownloadAtUtc);
  time.dataset.ownerWhen = '';
  time.textContent = whenText(state.lastDownloadAtUtc, now);
  divider.append(`${OWNER_TEXT.downloaded} `, time);
  list.insertBefore(divider, entries[state.changes] ?? null);
}

/**
 * Brings every part of the page that shows the backup up to date with a state from the server.
 * @param {Document} doc The page.
 * @param {{ changes: number, lastDownloadAtUtc: string | null, neverDownloaded: boolean }} state The state of the backup.
 * @param {Date} [now] The current moment, replaceable in tests.
 */
export function applyBackupState(doc, state, now = new Date()) {
  applyBanner(doc, state);
  applyQuietLine(doc, state, now);
  applyDivider(doc, state, now);
}

/**
 * Puts a download button into its busy state: the text `Preparing...`, `aria-disabled`, and no fixed name, so the busy text is
 * what a screen reader reads.
 * @param {HTMLButtonElement} button The download button.
 */
function setBusy(button, text = OWNER_TEXT.preparing) {
  busyButtons.set(button, { children: [...button.childNodes].map((node) => node.cloneNode(true)), label: button.getAttribute('aria-label') });
  button.setAttribute('aria-disabled', 'true');
  button.removeAttribute('aria-label');
  button.replaceChildren(button.ownerDocument.createTextNode(text));
}

/**
 * Gives a busy download button its text and name back.
 * @param {HTMLButtonElement} button The download button.
 */
function clearBusy(button) {
  const saved = busyButtons.get(button);

  if (!saved) {
    return;
  }

  busyButtons.delete(button);
  button.replaceChildren(...saved.children);
  button.removeAttribute('aria-disabled');

  if (saved.label !== null) {
    button.setAttribute('aria-label', saved.label);
  }
}

/**
 * Takes away the Undo buttons of the page: a download ends the chance to undo the latest change, so a button that is left would
 * only be refused.
 * @param {Document} doc The page.
 */
function dropUndoButtons(doc) {
  for (const button of doc.querySelectorAll('button[data-undo], .owner-notice form')) {
    button.remove();
  }
}

/** The shape of the token a checked file is held under: 32 lowercase hexadecimal characters. */
const RESTORE_TOKEN = /^[0-9a-f]{32}$/;

/**
 * Shows a problem with the file the owner chose in the alert line of the restore panel, or hides the line when there is none.
 * @param {Document} doc The page.
 * @param {string | null} text The words, written with `textContent`.
 */
function showRestoreProblem(doc, text) {
  const line = doc.getElementById('restore-problem');

  if (!line) {
    return;
  }

  line.textContent = text ?? '';
  line.hidden = text === null;
}

/**
 * Sends the chosen file to the server to be checked, with the antiforgery token in the header and a JSON answer asked for. A file
 * that passes brings the page to its preview, which the server renders from the token it made; a file that does not shows its
 * words in the alert line and changes nothing.
 * @param {HTMLFormElement} form The check form.
 * @param {Document} doc The page.
 * @param {{ send?: Function, go?: (address: string) => void, body?: BodyInit }} [options] The browser's fetch, how the browser is taken
 * to the preview, and the body to send, each replaceable in tests.
 * @returns {Promise<boolean>} Whether the page is being taken to the preview.
 */
export async function submitRestoreCheck(form, doc, options = {}) {
  const send = options.send ?? ((address, init) => globalThis.fetch(address, init));
  const go = options.go ?? ((address) => globalThis.location.assign(address));

  showRestoreProblem(doc, null);

  try {
    const response = await send(form.action, {
      method: 'POST',
      credentials: 'same-origin',
      headers: { Accept: 'application/json', RequestVerificationToken: readToken(doc) },
      body: options.body ?? new globalThis.FormData(form),
    });

    if (response.status === 413) {
      showRestoreProblem(doc, OWNER_TEXT.fileTooLarge);

      return false;
    }

    const answer = await response.json();

    if (response.ok && typeof answer?.token === 'string' && RESTORE_TOKEN.test(answer.token)) {
      const page = new URL(form.action, doc.baseURI).pathname;

      go(`${page}?restore=${answer.token}#restore-preview-heading`);

      return true;
    }

    showRestoreProblem(doc, typeof answer?.message === 'string' && answer.message !== '' ? answer.message : OWNER_TEXT.checkFailed);
  } catch {
    showRestoreProblem(doc, OWNER_TEXT.checkFailed);
  }

  return false;
}

/**
 * Takes over the check form of the restore panel: the check is made without leaving the page, the button shows `Checking...`
 * while it runs, and focus moves to the preview or to the restored line when the page shows one. Without the script the form is
 * a plain post and the page answers it.
 * @param {Document} doc The page.
 */
function initRestore(doc) {
  doc.getElementById('restore-preview-heading')?.focus();
  doc.getElementById('restore-done-heading')?.focus();
  focusFileInputAfterKeep(doc);

  const form = doc.querySelector('form[data-restore-form]');
  const button = form?.querySelector('button[data-check-button]');

  if (!form || !button) {
    return;
  }

  form.addEventListener('submit', async (event) => {
    event.preventDefault();

    if (button.getAttribute('aria-disabled') === 'true') {
      return;
    }

    setBusy(button, OWNER_TEXT.checking);
    await submitRestoreCheck(form, doc);
    clearBusy(button);
  });

}

/** The address fragment the page is sent to after a check is thrown away, which names the file input. */
const FILE_INPUT_FRAGMENT = '#restore-file';

/**
 * Puts focus on the file input when the page was reached by throwing a check away. `Keep current data` is a plain post that the
 * server answers with a redirect to the file input's fragment, so a reader who chose it lands where they can choose another file,
 * with or without the script.
 * @param {Document} doc The page.
 * @param {string} [fragment] The fragment of the address, replaceable in tests.
 */
export function focusFileInputAfterKeep(doc, fragment = globalThis.location?.hash ?? '') {
  if (fragment === FILE_INPUT_FRAGMENT) {
    doc.getElementById('restore-file')?.focus();
  }
}

/**
 * Starts the script on a page: localises its times, takes over the download forms' busy state and keeps the banner current.
 * @param {Document} [doc] The page; the browser's own by default.
 */
export function initBackup(doc = globalThis.document) {
  if (!doc) {
    return;
  }

  localiseTimes(doc);
  initRestore(doc);

  const url = doc.getElementById('owner-main')?.dataset.backupState ?? '';
  let timer = null;

  /**
   * Asks the server for the state and writes it to the page; a missing answer leaves the page as it is.
   * @returns {Promise<object | null>} The state the page now shows, or null when the page was left as it was.
   */
  async function refresh() {
    const state = url === '' ? null : await fetchBackupState(url);

    if (state !== null) {
      applyBackupState(doc, state);
    }

    return state;
  }

  /**
   * Asks for the state once, a moment from now, however many calls come in between.
   * @param {number} delay How long to wait.
   */
  function refreshSoon(delay) {
    globalThis.clearTimeout(timer);
    timer = globalThis.setTimeout(refresh, delay);
  }

  for (const form of doc.querySelectorAll('form[data-owner-download]')) {
    const button = form.querySelector('button[type="submit"]');

    form.addEventListener('submit', (event) => {
      if (button.getAttribute('aria-disabled') === 'true') {
        event.preventDefault();

        return;
      }

      setBusy(button);
      dropUndoButtons(doc);
      globalThis.setTimeout(async () => {
        const state = await refresh();

        clearBusy(button);

        if (state?.lastDownloadAtUtc) {
          announce(fill(OWNER_TEXT.downloadedWhen, { when: whenText(state.lastDownloadAtUtc) }), doc);
        }
      }, REFRESH_DELAY_MS);
    });
  }

  doc.addEventListener('owner-saved', () => refreshSoon(SAVED_DELAY_MS));
  globalThis.addEventListener('focus', () => refreshSoon(SAVED_DELAY_MS));
}

initBackup();
