/**
 * The script side of the picture link form on the owner's pictures page. The owner pastes the address of a picture from BGG's
 * gallery and presses `Check link`; the server fetches it, and the answer is drawn as a preview: the face-out box with the
 * picture and the spine in the picture's colour, side by side on a small shelf, using the cabinet's own box rules and the
 * server's numbers, so the preview cannot drift from the cabinet. The preview picture is served by the site itself. `Use this`
 * stores the link and fills the Your link tile; `Discard preview` throws the preview away. Without script the same form posts and
 * the server draws the preview, and this script then only wires the buttons that are already on the page. Every value that came
 * from the server is checked before it is used, sizes are set only through custom properties, and every text is written with
 * `textContent`.
 */
import { OWNER_TEXT, fill, leadAndRest, shortAddress } from './owner-format.js';
import { postOwner, showRowProblem, showRowStatus } from './owner-shared.js';

/** The handler addresses, relative to the page the script runs on. */
const HANDLERS = Object.freeze({
  check: '?handler=CheckLink',
  use: '?handler=UseLink',
  discard: '?handler=DiscardPreview',
  remove: '?handler=RemoveLink',
  undo: '?handler=Undo',
});

/** The only address a previewed picture may have: the site's own preview route and a token of 32 lowercase hexadecimal characters. */
const PREVIEW_PATH = /^\/owner\/preview\/[0-9a-f]{32}$/;

/** The only address a stored picture may have: a file of the site's own art directory. */
const ART_PATH = /^\/art\/[0-9a-f]{16}-[0-9]{1,4}\.webp$/;

/** The shape of a colour the preview may be set to. */
const HEX_COLOUR = /^#[0-9a-fA-F]{6}$/;

/** The two text colours a spine may carry. */
const TEXT_COLOURS = ['#ffffff', '#000000'];

/** The fits a picture may have in its box front. */
const FITS = ['width', 'height', 'exact'];

/** The custom properties the four edge colours of a picture are set on, as pairs of side name and property. */
const EDGE_PROPERTIES = [
  ['top', '--edge-t'],
  ['right', '--edge-r'],
  ['bottom', '--edge-b'],
  ['left', '--edge-l'],
];

/**
 * Tells whether a value is a whole number from 1 to the limit.
 * @param {unknown} value The value.
 * @param {number} limit The largest allowed value.
 * @returns {boolean}
 */
function isCount(value, limit) {
  return Number.isInteger(value) && value >= 1 && value <= limit;
}

/**
 * Checks the preview the server answered with and returns what the script may draw, or null when anything is not what the server
 * writes. Nothing the script draws comes from a value that was not checked here.
 * @param {{ previewUrl?: string, token?: string, preview?: object }} result The server's answer.
 * @returns {object | null}
 */
export function checkedPreview(result) {
  const preview = result?.preview;

  if (preview === null || typeof preview !== 'object' || !PREVIEW_PATH.test(result.previewUrl ?? '')) {
    return null;
  }

  if (result.token !== result.previewUrl.split('/').pop()) {
    return null;
  }

  const sizesAreValid = [preview.boxWidthMm, preview.boxHeightMm, preview.spineWidthMm, preview.spineHeightMm].every((value) =>
    isCount(value, 2000)) && isCount(preview.boxDepthMm, 2000) && isCount(preview.artWidth, 4096) && isCount(preview.artHeight, 4096);
  const coloursAreValid = HEX_COLOUR.test(preview.spineBackground ?? '') && TEXT_COLOURS.includes(preview.spineText);
  const textsAreValid = ['title', 'spineLabel', 'verdictText', 'shapeText'].every((name) => typeof preview[name] === 'string');

  if (!sizesAreValid || !coloursAreValid || !textsAreValid || !FITS.includes(preview.fit)) {
    return null;
  }

  return { ...preview, previewUrl: result.previewUrl, token: result.token };
}

/**
 * Sets one custom property to a whole number.
 * @param {HTMLElement} element The element to style.
 * @param {string} name The custom property name.
 * @param {number} value The value.
 */
function setNumber(element, name, value) {
  element.style.setProperty(name, String(value));
}

/**
 * Makes an element with a class name and, optionally, its text.
 * @param {Document} doc The document.
 * @param {string} tag The tag name.
 * @param {string} [className] The class name.
 * @param {string} [text] The text, written as text.
 * @returns {HTMLElement}
 */
function make(doc, tag, className = '', text = '') {
  const element = doc.createElement(tag);

  if (className) {
    element.className = className;
  }

  if (text) {
    element.textContent = text;
  }

  return element;
}

/**
 * Sets the four edge colours of a picture on the box when the preview gives all four as valid colours.
 * @param {HTMLElement} box The face-out box.
 * @param {object | null} edges The edge colours from the server.
 */
function setEdgeColours(box, edges) {
  if (edges === null || typeof edges !== 'object' || !EDGE_PROPERTIES.every(([side]) => HEX_COLOUR.test(edges[side] ?? ''))) {
    return;
  }

  for (const [side, property] of EDGE_PROPERTIES) {
    box.style.setProperty(property, edges[side]);
  }
}

/**
 * Builds the face-out box of the preview: the cabinet's `.placement` of a cover with a picture, one image shown whole.
 * @param {Document} doc The document.
 * @param {object} preview The checked preview.
 * @returns {HTMLElement}
 */
function buildBox(doc, preview) {
  const box = make(doc, 'div', 'placement');
  const image = make(doc, 'img', 'cover-art');

  box.setAttribute('role', 'img');
  box.setAttribute('aria-label', fill(OWNER_TEXT.boxFrontOf, { title: preview.title }));
  box.dataset.kind = 'cover';
  box.dataset.art = 'true';
  box.dataset.fit = preview.fit;
  box.style.setProperty('--bg', preview.spineBackground);
  box.style.setProperty('--fg', preview.spineText);
  setEdgeColours(box, preview.edges);
  image.src = preview.previewUrl;
  image.width = preview.artWidth;
  image.height = preview.artHeight;
  image.alt = '';
  image.draggable = false;
  box.append(image);

  return box;
}

/**
 * Builds the spine of the preview: the cabinet's `.placement` of a spine, in the picture's colour with the shortened title.
 * @param {Document} doc The document.
 * @param {object} preview The checked preview.
 * @returns {HTMLElement}
 */
function buildSpine(doc, preview) {
  const spine = make(doc, 'div', 'placement');
  const label = make(doc, 'span', 'placement-label', preview.spineLabel);

  spine.setAttribute('role', 'img');
  spine.setAttribute('aria-label', fill(OWNER_TEXT.spineOf, { title: preview.title }));
  spine.dataset.kind = 'spine';
  spine.style.setProperty('--bg', preview.spineBackground);
  spine.style.setProperty('--fg', preview.spineText);
  label.setAttribute('dir', 'auto');
  spine.append(label);

  return spine;
}

/**
 * Builds the small shelf: the cabinet's cubby back, a board with a lit top edge, and the box and the spine standing on it at
 * cabinet scale, with a caption on the board under each.
 * @param {Document} doc The document.
 * @param {object} preview The checked preview.
 * @returns {HTMLElement}
 */
function buildShelf(doc, preview) {
  const shelf = make(doc, 'div', 'preview-shelf');
  const back = make(doc, 'span', 'preview-back');
  const board = make(doc, 'span', 'preview-board');
  const boxSlot = make(doc, 'div', 'preview-slot');
  const spineSlot = make(doc, 'div', 'preview-slot');

  back.setAttribute('aria-hidden', 'true');
  board.setAttribute('aria-hidden', 'true');
  setNumber(shelf, '--shelf-w', preview.boxWidthMm + preview.spineWidthMm);
  boxSlot.dataset.slot = 'box';
  setNumber(boxSlot, '--w', preview.boxWidthMm);
  setNumber(boxSlot, '--h', preview.boxHeightMm);
  boxSlot.append(buildBox(doc, preview));
  spineSlot.dataset.slot = 'spine';
  setNumber(spineSlot, '--w', preview.spineWidthMm);
  setNumber(spineSlot, '--h', preview.spineHeightMm);
  spineSlot.append(buildSpine(doc, preview));

  const boxCaption = make(doc, 'span', 'preview-caption', OWNER_TEXT.boxFront);
  const spineCaption = make(doc, 'span', 'preview-caption', OWNER_TEXT.spine);

  boxCaption.dataset.slot = 'box';
  spineCaption.dataset.slot = 'spine';
  shelf.append(back, board, boxSlot, spineSlot, boxCaption, spineCaption);

  return shelf;
}

/**
 * Builds the whole preview section for a game from a checked preview: heading, the shelf, the verdict and where the box's shape
 * came from, the note for a picture that is not a flat cover, the disclaimer, and the two buttons.
 * @param {Document} doc The document.
 * @param {string} gameId The game.
 * @param {object} preview The checked preview.
 * @returns {HTMLElement}
 */
function buildPreview(doc, gameId, preview) {
  const section = make(doc, 'section', 'pic-preview');
  const heading = make(doc, 'h4', '', OWNER_TEXT.previewHeading);
  const facts = make(doc, 'p', 'pic-preview-facts');
  const actions = make(doc, 'div', 'pic-preview-actions');
  const use = make(doc, 'button', 'owner-btn-primary', OWNER_TEXT.useThis);
  const discard = make(doc, 'button', 'owner-btn', OWNER_TEXT.discardPreview);

  section.id = `pic-preview-${gameId}`;
  section.dataset.token = preview.token;
  section.dataset.gameId = gameId;
  heading.id = `pic-preview-heading-${gameId}`;
  heading.tabIndex = -1;
  section.setAttribute('aria-labelledby', heading.id);
  use.type = 'button';
  use.dataset.usePreview = '';
  discard.type = 'button';
  discard.dataset.discardPreview = '';

  const verdict = make(doc, 'span', 'pic-tag', preview.verdictText);
  const shape = make(doc, 'span', 'pic-preview-shape', preview.shapeText);

  verdict.dataset.previewVerdict = '';
  shape.dataset.previewShape = '';
  facts.append(verdict, shape);
  actions.append(use, discard);
  section.append(heading, make(doc, 'p', 'pic-preview-sub', OWNER_TEXT.previewSub), buildShelf(doc, preview), facts);

  if (typeof preview.note === 'string' && preview.note !== '') {
    const note = make(doc, 'p', 'pic-note', preview.note);

    note.dataset.previewNote = '';
    section.append(note);
  }

  const disclaimer = make(doc, 'p', 'pic-why', OWNER_TEXT.previewDisclaimer);

  disclaimer.dataset.previewDisclaimer = '';
  section.append(disclaimer, actions);

  return section;
}

/**
 * Reads which choice a group of radios has checked.
 * @param {Element} fieldset The group.
 * @returns {string} The value of the checked radio, or an empty string when none is.
 */
function checkedValue(fieldset) {
  return fieldset.querySelector('input[type="radio"]:checked')?.value ?? '';
}

/** The states the Your link tile can be drawn in. */
const TILE_STATES = ['picture', 'fetching', 'none'];

/**
 * Draws the Your link tile as the server describes it: with the stored copy, its verdict and the short address and the radio
 * available; while the link's picture is being fetched, with the text `Fetching...`; or with no link, with `None yet` and the
 * radio unavailable. Nothing is drawn from a description that is not valid.
 * @param {Element} tile The Your link tile.
 * @param {{ state?: string, url?: string, width?: number, height?: number, verdictText?: string | null, address?: string | null, fullAddress?: string | null }} data The tile as the server knows it.
 * @param {(image: HTMLImageElement) => void} watch Swaps a picture the browser cannot show to the generated cover.
 */
function renderLinkTile(tile, data, watch) {
  const doc = tile.ownerDocument;
  const art = tile.querySelector('.pic-art');
  const radio = tile.querySelector('input[type="radio"]');
  const state = data.state ?? 'picture';

  if (!art || !radio || !TILE_STATES.includes(state)) {
    return;
  }

  if (state === 'picture' && (!ART_PATH.test(data.url ?? '') || !isCount(data.width, 4096) || !isCount(data.height, 4096))) {
    return;
  }

  art.replaceChildren();
  art.dataset.art = state;
  tile.dataset.state = state;
  tile.querySelector('.pic-sub')?.remove();
  tile.querySelector('.pic-verdict')?.remove();
  tile.querySelector('.pic-address')?.remove();

  if (state === 'picture') {
    const image = make(doc, 'img');

    image.src = data.url;
    image.width = data.width;
    image.height = data.height;
    image.alt = '';
    image.decoding = 'async';
    art.append(image);
    watch(image);
  }

  radio.disabled = state === 'none';

  if (state === 'none') {
    radio.setAttribute('aria-disabled', 'true');
    radio.checked = false;
  } else {
    radio.removeAttribute('aria-disabled');
  }

  const caption = tile.querySelector('.pic-caption');

  if (state === 'picture' && typeof data.verdictText === 'string' && data.verdictText !== '') {
    caption?.after(make(doc, 'span', 'pic-verdict', data.verdictText));
  }

  if (state !== 'picture') {
    const text = state === 'fetching' ? OWNER_TEXT.tileFetching : OWNER_TEXT.tileNoLinkYet;

    caption?.after(make(doc, 'span', 'pic-sub', text));
  }

  const address = storedAddress(data);

  if (state !== 'none' && address !== '') {
    const line = make(doc, 'span', 'pic-address', address);

    line.setAttribute('dir', 'ltr');
    line.title = typeof data.fullAddress === 'string' ? data.fullAddress : '';
    tile.append(line);
  }
}

/**
 * The short address of a stored link as the server describes it: the one the server made, or one made from the whole address.
 * @param {{ address?: string | null, fullAddress?: string | null }} data The tile as the server knows it.
 * @returns {string} The short address, or an empty text when the link has none.
 */
function storedAddress(data) {
  if (typeof data.address === 'string' && data.address !== '') {
    return data.address;
  }

  return typeof data.fullAddress === 'string' ? shortAddress(data.fullAddress) : '';
}

/**
 * Wires the link form of one row: `Check link`, the preview it draws, `Use this` and `Discard preview`. A preview the server
 * drew on the page is wired the same way as one the script draws.
 * @param {HTMLElement} row The row.
 * @param {{
 *   enqueue: (task: () => Promise<void>) => void,
 *   showCounts: (result: object) => void,
 *   applyRowState: (row: Element, result: object) => void,
 *   showProblem: (region: HTMLElement, result: object) => void,
 *   watchPicture: (image: HTMLImageElement) => void,
 * }} helpers What the pictures page already does for its rows.
 */
export function initLinkBox(row, helpers) {
  const box = row.querySelector('details.pic-linkbox');
  const form = box?.querySelector('form.pic-link-form');
  const fieldset = row.querySelector('fieldset.pic-choices');
  const region = row.querySelector(':scope > .row-status');
  const field = form?.elements.link;
  const button = form?.querySelector('[data-check-link]');
  const error = form?.querySelector('.pic-link-error');

  if (!box || !form || !fieldset || !region || !field || !button || !error) {
    return;
  }

  const doc = row.ownerDocument;
  const gameId = form.elements.gameId.value;
  const hint = field.getAttribute('aria-describedby') ?? '';
  let busy = false;

  const clearError = () => {
    error.hidden = true;
    error.textContent = '';
    field.removeAttribute('aria-invalid');
    field.setAttribute('aria-describedby', hint.split(' ')[0]);
  };

  const showError = (text) => {
    const { lead, rest } = leadAndRest(text);
    const leadElement = make(doc, 'strong', 'pic-link-lead', lead);

    error.replaceChildren(leadElement);

    if (rest !== '') {
      error.append(doc.createTextNode(` ${rest}`));
    }

    error.hidden = false;
    field.setAttribute('aria-invalid', 'true');
    field.setAttribute('aria-describedby', `${hint.split(' ')[0]} ${error.id}`);
    field.focus();
  };

  const currentPreview = () => box.querySelector('.pic-preview');
  const current = form.querySelector('[data-current-link]');
  const removeButton = form.querySelector('[data-remove-link]');
  const linkTile = () => row.querySelector('.pic-tile[data-kind="Link"]');

  /**
   * Makes the line `Current link` and the `Remove link` button follow the address the Your link tile shows: they are there while
   * a link is stored and gone when it is not.
   */
  const showStoredLink = () => {
    const line = linkTile()?.querySelector('.pic-address');
    const address = line?.textContent ?? '';
    const named = current?.querySelector('.pic-current-address');

    if (current && named) {
      named.textContent = address;
      named.title = line?.title ?? '';
      current.hidden = address === '';
    }

    if (removeButton) {
      removeButton.hidden = address === '';
    }
  };

  /**
   * Captures how the row stands before a link is used, so Undo can put the Your link tile, the choice and the mark back.
   * @returns {() => void} Puts the row back as it was.
   */
  const captureRow = () => {
    const tile = row.querySelector('.pic-tile[data-kind="Link"]');
    const copy = tile.cloneNode(true);
    const choice = checkedValue(fieldset);
    const hadMark = Boolean(row.querySelector('[data-mark="yours"]'));

    return () => {
      row.querySelector('.pic-tile[data-kind="Link"]').replaceWith(copy);

      for (const radio of fieldset.querySelectorAll('input[type="radio"]')) {
        radio.checked = radio.value === choice;
      }

      fieldset.dataset.saved = choice;

      if (!hadMark) {
        row.querySelector('[data-mark="yours"]')?.remove();
      }
    };
  };

  const markYours = () => {
    const marks = row.querySelector('.pic-marks');

    if (marks && !marks.querySelector('[data-mark="yours"]')) {
      const tag = make(doc, 'li', 'pic-tag pic-tag-strong', OWNER_TEXT.yours);

      tag.dataset.mark = 'yours';
      marks.prepend(tag);
    }
  };

  const undoUse = (restore) => (saved) =>
    helpers.enqueue(async () => {
      const result = await postOwner(HANDLERS.undo, { gameId }, { revision: saved });

      if (result.outcome === 'saved') {
        restore();
        showStoredLink();
        helpers.applyRowState(row, result);
        helpers.showCounts(result);
        showRowStatus(region, OWNER_TEXT.undone);
        fieldset.querySelector('input[type="radio"]:checked')?.focus();
      } else if (result.outcome === 'outOfDate' && result.status === 'cant-undo') {
        showRowProblem(region, OWNER_TEXT.cantUndo);
      } else {
        helpers.showProblem(region, result);
      }
    });

  const use = (section) => {
    const button = section.querySelector('[data-use-preview]');

    if (button.getAttribute('aria-disabled') === 'true') {
      return;
    }

    button.setAttribute('aria-disabled', 'true');

    helpers.enqueue(async () => {
      const restore = captureRow();
      const result = await postOwner(HANDLERS.use, { gameId, token: section.dataset.token });

      button.removeAttribute('aria-disabled');

      if (result.outcome === 'saved') {
        renderLinkTile(row.querySelector('.pic-tile[data-kind="Link"]'), result.linkTile ?? {}, helpers.watchPicture);

        const radio = row.querySelector('.pic-tile[data-kind="Link"] input[type="radio"]');

        for (const other of fieldset.querySelectorAll('input[type="radio"]')) {
          other.checked = other === radio;
        }

        fieldset.dataset.saved = 'Link';
        markYours();
        showStoredLink();
        helpers.applyRowState(row, result);
        helpers.showCounts(result);
        section.remove();
        field.value = '';
        clearError();
        box.open = false;
        showRowStatus(region, OWNER_TEXT.saved, { undoLabel: OWNER_TEXT.undo, onUndo: undoUse(restore) });
        radio.focus();
      } else if (result.outcome === 'refused' && result.status === 'link-expired') {
        section.remove();
        showError(result.message ?? OWNER_TEXT.linkExpired);
      } else {
        helpers.showProblem(region, result);
      }
    });
  };

  const undoRemove = (previousChoice) => (saved) =>
    helpers.enqueue(async () => {
      const result = await postOwner(HANDLERS.undo, { gameId }, { revision: saved });

      if (result.outcome === 'saved') {
        renderLinkTile(linkTile(), result.linkTile ?? {}, helpers.watchPicture);

        for (const radio of fieldset.querySelectorAll('input[type="radio"]')) {
          radio.checked = radio.value === previousChoice;
        }

        fieldset.dataset.saved = previousChoice;
        showStoredLink();
        helpers.applyRowState(row, result);
        helpers.showCounts(result);
        showRowStatus(region, OWNER_TEXT.undone);
        (fieldset.querySelector('input[type="radio"]:checked') ?? removeButton)?.focus();
      } else if (result.outcome === 'outOfDate' && result.status === 'cant-undo') {
        showRowProblem(region, OWNER_TEXT.cantUndo);
      } else {
        helpers.showProblem(region, result);
      }
    });

  const remove = () =>
    helpers.enqueue(async () => {
      const previousChoice = checkedValue(fieldset);

      removeButton.setAttribute('aria-disabled', 'true');

      const result = await postOwner(HANDLERS.remove, { gameId });

      removeButton.removeAttribute('aria-disabled');

      if (result.outcome === 'saved' || result.outcome === 'noChange') {
        renderLinkTile(linkTile(), { state: 'none' }, helpers.watchPicture);

        if (typeof result.choice === 'string') {
          for (const radio of fieldset.querySelectorAll('input[type="radio"]')) {
            radio.checked = radio.value === result.choice;
          }

          fieldset.dataset.saved = result.choice;
        }

        currentPreview()?.remove();
        clearError();
        field.value = '';
        showStoredLink();
        helpers.applyRowState(row, result);
        helpers.showCounts(result);

        if (result.outcome === 'saved') {
          showRowStatus(region, OWNER_TEXT.linkRemoved, { undoLabel: OWNER_TEXT.undo, onUndo: undoRemove(previousChoice) });
        }

        button.focus();
      } else {
        helpers.showProblem(region, result);
      }
    });

  const discard = (section) => {
    postOwner(HANDLERS.discard, { gameId, token: section.dataset.token });
    section.remove();
    field.focus();
  };

  const wire = (section) => {
    section.querySelector('[data-use-preview]')?.addEventListener('click', (event) => {
      event.preventDefault();

      if (event.currentTarget.getAttribute('aria-disabled') !== 'true') {
        use(section);
      }
    });
    section.querySelector('[data-discard-preview]')?.addEventListener('click', (event) => {
      event.preventDefault();
      discard(section);
    });
  };

  const check = async () => {
    if (busy) {
      return;
    }

    busy = true;
    clearError();
    currentPreview()?.remove();
    button.textContent = OWNER_TEXT.checking;
    button.setAttribute('aria-disabled', 'true');
    field.readOnly = true;

    let result;

    try {
      result = await postOwner(HANDLERS.check, { gameId, link: field.value });
    } finally {
      busy = false;
      button.textContent = OWNER_TEXT.checkLink;
      button.removeAttribute('aria-disabled');
      field.readOnly = false;
    }

    const preview = result.outcome === 'ready' ? checkedPreview(result) : null;

    if (preview === null) {
      showError(result.outcome === 'expired' ? OWNER_TEXT.expired : result.message ?? OWNER_TEXT.linkFailed);

      return;
    }

    const section = buildPreview(doc, gameId, preview);

    box.append(section);
    wire(section);
    section.querySelector('h4').focus();
  };

  removeButton?.addEventListener('click', (event) => {
    event.preventDefault();

    if (removeButton.getAttribute('aria-disabled') !== 'true') {
      remove();
    }
  });
  field.addEventListener('input', clearError);
  form.addEventListener('submit', (event) => {
    event.preventDefault();
    check();
  });

  const present = currentPreview();

  if (present) {
    wire(present);
  }
}
