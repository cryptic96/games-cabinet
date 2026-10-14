/**
 * The script side of the owner's cabinet look panel. The panel works as a plain form; this script adds the preview: a real cabinet
 * section drawn by the cabinet's own renderer from a fixed sample layout inside a wrapper that carries the five look attributes.
 * Every choice changes only the trial, which is the wrapper's attributes, the summary line and the buttons. Nothing reaches
 * visitors until `Use for everyone`, which posts the five values of the trial in one request, and `Back to current` drops the
 * trial. The design and wood tiles draw themselves in the trial: a design tile in the trial wood, a wood tile on the trial design.
 * `Preview time` only moves the preview's evening marker, and the preview's lamp follows the trial. Every text is written with
 * `textContent`, and the values written as attributes come from the panel's own radios.
 */
import { COPY_BY_LANGUAGE } from './copy.js';
import { LOOK_FIELDS, OWNER_TEXT, fill, lookSummary } from './owner-format.js';
import { refreshLamp } from './look.js';
import { announce, initRevision, postOwner, showProblem, showRowProblem, showRowStatus } from './owner-shared.js';
import { renderCabinet } from './render.js';

/** The handler that publishes a look, relative to the page the script runs on. */
const LOOK_HANDLER = '?handler=Look';

/** The handler that takes the latest change back. */
const UNDO_HANDLER = '?handler=Undo';

/** The name of the field that holds the painted backs switch. */
const BACKS_FIELD = 'backs';

/** The value of the painted backs switch when it is on. */
const PAINTED = 'painted';

/** The value stored for backs that show the wood. */
const WOOD_BACKS = 'wood';

/** The name of the radios that set the time of day of the preview; the field is not part of a look and is never posted. */
const PREVIEW_TIME_FIELD = 'preview-time';

/** The value of the preview time radio that shows the evening. */
const EVENING = 'evening';

/** The choice of lights that follows the visitor's clock. */
const EVENINGS_ONLY = 'evenings';

/** The fields of a look a design tile takes from the trial; its own design stays its own. */
const DESIGN_TILE_FIELDS = Object.freeze(['wood', 'backs', 'depth']);

/** The fields of a look a wood tile takes from the trial; its own wood stays its own. */
const WOOD_TILE_FIELDS = Object.freeze(['design', 'backs', 'depth']);

/**
 * Reads which look the panel's controls show.
 * @param {ParentNode} root The part of the page that holds the controls.
 * @returns {Record<string, string>} The five values, by field; a group without a checked choice gives an empty value.
 */
export function readLook(root) {
  const look = {};

  for (const field of LOOK_FIELDS) {
    const inputs = [...root.querySelectorAll(`input[name="${field}"]`)];

    look[field] =
      field === BACKS_FIELD
        ? inputs.some((input) => input.checked) ? PAINTED : WOOD_BACKS
        : (inputs.find((input) => input.checked)?.value ?? '');
  }

  return look;
}

/**
 * Makes the panel's controls show a look.
 * @param {ParentNode} root The part of the page that holds the controls.
 * @param {Record<string, string>} look The five values, by field.
 */
export function writeLook(root, look) {
  for (const field of LOOK_FIELDS) {
    for (const input of root.querySelectorAll(`input[name="${field}"]`)) {
      input.checked = field === BACKS_FIELD ? look[field] === PAINTED : input.value === look[field];
    }
  }
}

/**
 * Tells whether two looks are the same in all five fields.
 * @param {Record<string, string>} first One look.
 * @param {Record<string, string>} second The other look.
 * @returns {boolean}
 */
export function sameLook(first, second) {
  return LOOK_FIELDS.every((field) => first[field] === second[field]);
}

/**
 * Writes a look as the five attributes of an element, and nowhere else.
 * @param {Element} element The preview wrapper.
 * @param {Record<string, string>} look The five values, by field.
 */
export function applyLookAttributes(element, look) {
  for (const field of LOOK_FIELDS) {
    element.setAttribute(`data-${field}`, look[field]);
  }
}

/**
 * Reads the look an element carries as attributes.
 * @param {Element} element The preview wrapper.
 * @returns {Record<string, string>}
 */
export function readLookAttributes(element) {
  const look = {};

  for (const field of LOOK_FIELDS) {
    look[field] = element.getAttribute(`data-${field}`) ?? '';
  }

  return look;
}

/**
 * Writes the trial on the swatches of the tiles: every design tile takes the trial's wood, backs and depth, and every wood tile
 * takes the trial's design, backs and depth. A swatch keeps its own design or wood and never takes the lights.
 * @param {ParentNode} root The part of the page that holds the tiles.
 * @param {Record<string, string>} look The trial.
 */
export function applySwatchAttributes(root, look) {
  for (const swatch of root.querySelectorAll('.look-swatch-design')) {
    for (const field of DESIGN_TILE_FIELDS) {
      swatch.setAttribute(`data-${field}`, look[field]);
    }
  }

  for (const swatch of root.querySelectorAll('.look-swatch-wood')) {
    for (const field of WOOD_TILE_FIELDS) {
      swatch.setAttribute(`data-${field}`, look[field]);
    }
  }
}

/**
 * Fetches the sample layout the preview draws.
 * @param {string} address The address of the sample, on the page's own origin.
 * @param {Function} [send] The browser's fetch, replaceable in tests.
 * @returns {Promise<object | null>} The layout, or null when it could not be loaded or is not a layout.
 */
export async function loadSampleLayout(address, send = (url, init) => globalThis.fetch(url, init)) {
  try {
    const response = await send(address, { credentials: 'same-origin', headers: { Accept: 'application/json' } });

    if (!response.ok) {
      return null;
    }

    const layout = await response.json();

    return Array.isArray(layout?.sections) && layout.sections.length > 0 ? layout : null;
  } catch {
    return null;
  }
}

/**
 * Finds the parts of the panel the script works on.
 * @param {Document} doc The page.
 * @returns {object | null} The parts, or null when the page has no look panel with a preview.
 */
function findParts(doc) {
  const parts = {
    preview: doc.getElementById('look-preview'),
    mount: doc.getElementById('look-preview-mount'),
    current: doc.getElementById('look-current'),
    trying: doc.getElementById('look-trying'),
    form: doc.getElementById('look-form'),
    publish: doc.getElementById('look-publish'),
    discard: doc.getElementById('look-discard'),
    status: doc.getElementById('finish-status'),
    evening: doc.getElementById('look-evening'),
    eveningHint: doc.getElementById('look-evening-hint'),
  };

  return Object.values(parts).every(Boolean) ? parts : null;
}

/**
 * Wires the look panel: the preview, the trial, the tiles, Preview time, publish, discard and Undo.
 * @param {Document} [doc] The page; the browser's own by default.
 * @param {{ post?: Function, load?: Function, render?: Function, lamp?: Function }} [deps] How a change is posted, how the sample is
 * loaded, how it is drawn and how the preview's lamp is brought in line with its attributes, each replaceable in tests.
 * @returns {{ trial: () => Record<string, string>, current: () => Record<string, string>, ready: Promise<void> } | null} What a test can
 * look at, or null when the page has no look panel.
 */
export function initLookPanel(doc = globalThis.document, deps = {}) {
  const parts = doc ? findParts(doc) : null;

  if (parts === null) {
    return null;
  }

  const { preview, mount, current: currentLine, trying, form, publish, discard, status, evening, eveningHint } = parts;
  const post = deps.post ?? postOwner;
  const load = deps.load ?? loadSampleLayout;
  const render = deps.render ?? renderCabinet;
  const lamp = deps.lamp ?? refreshLamp;
  let current = readLookAttributes(preview);
  let trial = readLook(form);
  let earlier = null;
  let busy = false;

  initRevision(doc);
  discard.hidden = false;
  const noScriptNote = doc.getElementById('look-no-script');

  if (noScriptNote) {
    noScriptNote.hidden = true;
  }

  /**
   * Tells whether the Evening choice of Preview time is checked.
   * @returns {boolean}
   */
  function eveningChosen() {
    return [...form.querySelectorAll(`input[name="${PREVIEW_TIME_FIELD}"]`)].some((input) => input.checked && input.value === EVENING);
  }

  /**
   * Brings Preview time and the preview's lamp in line with the trial. The group only matters with Evenings only, so it is
   * disabled otherwise and its hint says so; the evening marker goes on the preview wrapper and nowhere else.
   */
  function refreshPreviewTime() {
    const matters = trial.lights === EVENINGS_ONLY;

    evening.disabled = !matters;
    eveningHint.textContent = matters ? OWNER_TEXT.previewTimeHint : OWNER_TEXT.previewTimeHintDisabled;

    if (eveningChosen()) {
      preview.setAttribute('data-evening', '');
    } else {
      preview.removeAttribute('data-evening');
    }

    lamp(preview);
  }

  /**
   * Brings the preview, the tiles, the summary lines and the buttons up to date with the trial.
   */
  function refresh() {
    const differs = !sameLook(trial, current);

    applyLookAttributes(preview, trial);
    applySwatchAttributes(doc, trial);
    refreshPreviewTime();
    currentLine.textContent = fill(OWNER_TEXT.visitorsSee, { summary: lookSummary(current) });
    trying.textContent = differs ? fill(OWNER_TEXT.trying, { summary: lookSummary(trial) }) : '';
    trying.hidden = !differs;

    for (const button of [publish, discard]) {
      if (differs) {
        button.removeAttribute('aria-disabled');
      } else {
        button.setAttribute('aria-disabled', 'true');
      }
    }
  }

  /**
   * Empties the status line.
   */
  function clearStatus() {
    status.replaceChildren();
    status.removeAttribute('data-problem');
  }

  /**
   * Takes away the Undo form the server wrote for a look saved without script: once a script changes the status, that form no
   * longer describes the latest change.
   */
  function dropServerUndo() {
    for (const element of doc.querySelectorAll('form.look-undo')) {
      element.remove();
    }
  }

  /**
   * Takes the earlier look back on the server and, when that worked, on the page.
   * @param {number | null} saved The revision the publish produced.
   */
  async function undo(saved) {
    const result = await post(UNDO_HANDLER, {}, { revision: saved });

    if (result.outcome === 'saved' && earlier !== null) {
      current = earlier;
      earlier = null;
      trial = { ...current };
      writeLook(form, trial);
      refresh();
      showRowStatus(status, OWNER_TEXT.lookUndone);
      publish.focus();
    } else if (result.outcome === 'outOfDate' && result.status === 'cant-undo') {
      showRowProblem(status, OWNER_TEXT.cantUndo);
    } else {
      showProblem(status, result);
    }
  }

  /**
   * Publishes the trial as the look every visitor sees. A failed or refused post keeps the trial and says what happened.
   */
  async function publishTrial() {
    if (busy || publish.getAttribute('aria-disabled') === 'true') {
      return;
    }

    busy = true;
    dropServerUndo();

    const sent = { ...trial };

    try {
      const result = await post(LOOK_HANDLER, sent);

      if (result.outcome === 'saved' || result.outcome === 'noChange') {
        earlier = result.outcome === 'saved' ? current : null;
        current = sent;
        refresh();

        if (result.outcome === 'saved') {
          showRowStatus(status, OWNER_TEXT.lookSaved, { onUndo: undo, undoLabel: OWNER_TEXT.undo });
        } else {
          showRowStatus(status, OWNER_TEXT.saved);
        }

        publish.focus();
      } else if (result.outcome === 'refused') {
        showRowProblem(status, typeof result.message === 'string' && result.message !== '' ? result.message : OWNER_TEXT.failed);
      } else {
        showProblem(status, result);
      }
    } finally {
      busy = false;
    }
  }

  /**
   * Drops the trial and shows the current look again, with focus on the checked design.
   */
  function discardTrial() {
    if (discard.getAttribute('aria-disabled') === 'true') {
      return;
    }

    trial = { ...current };
    writeLook(form, trial);
    refresh();
    showRowStatus(status, OWNER_TEXT.lookDiscarded);
    [...form.querySelectorAll('input[name="design"]')].find((input) => input.checked)?.focus();
  }

  form.addEventListener('change', (event) => {
    if (event?.target?.name === PREVIEW_TIME_FIELD) {
      refreshPreviewTime();

      return;
    }

    trial = readLook(form);
    dropServerUndo();
    refresh();

    if (sameLook(trial, current)) {
      clearStatus();
    } else {
      showRowStatus(status, OWNER_TEXT.lookTrying);
    }
  });
  form.addEventListener('submit', (event) => {
    event.preventDefault();
    publishTrial();
  });
  discard.addEventListener('click', discardTrial);
  refresh();

  const ready = (async () => {
    const layout = await load(preview.dataset.sample ?? '');

    preview.setAttribute('data-settled', '');

    if (layout === null) {
      const problem = doc.createElement('p');

      problem.className = 'look-preview-problem';
      problem.textContent = OWNER_TEXT.previewFailed;
      mount.replaceChildren(problem);
      preview.dataset.state = 'failed';
      announce(OWNER_TEXT.previewFailed, doc);

      return;
    }

    render(mount, layout, COPY_BY_LANGUAGE.en);
    preview.dataset.state = 'ready';
  })();

  return { trial: () => ({ ...trial }), current: () => ({ ...current }), ready };
}

initLookPanel();
