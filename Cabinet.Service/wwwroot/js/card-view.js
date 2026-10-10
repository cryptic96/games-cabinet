/**
 * Builds the detail card of one game as DOM. The card is text and elements only: every value from the collection reaches the page
 * through text content or an attribute the script checks first, so nothing a source writes can become markup.
 */

import { factsFor, hasAnyDetail, ratingText } from './format.js';

/** The only address shape a card picture may have: a stored file on the site's own origin. */
const ART_PATH = /^\/art\/[0-9a-f]{16}-[0-9]{1,4}\.webp$/;

/** The fits a card picture may state. */
const ART_FITS = ['width', 'height', 'exact'];

/** The cover pattern names in the order of the pattern index the records carry. */
const PATTERN_NAMES = ['stripes', 'chevrons', 'dots', 'rings', 'diagonal', 'plain'];

/** A colour as the data writes it: a hexadecimal triple. */
const HEX_COLOUR = /^#[0-9a-fA-F]{6}$/;

/** A colour taken from box art as the data writes it: lowercase hexadecimal only. */
const ART_COLOUR = /^#[0-9a-f]{6}$/;

/** The only two text colours a colour taken from box art may carry. */
const ART_TEXT_COLOURS = ['#ffffff', '#000000'];

/** The custom properties the four edge colours of a picture are set on, as pairs of side name and property. */
const EDGE_PROPERTIES = [
  ['top', '--edge-t'],
  ['right', '--edge-r'],
  ['bottom', '--edge-b'],
  ['left', '--edge-l'],
];

/** The colour pair a cover takes when neither the picture nor the colour table gives a valid one. */
const FALLBACK_TONE = Object.freeze({ background: '#8c5d38', text: '#ffffff' });

/** The narrowest cover proportions a card draws, so an extreme box does not make a card taller than the screen. */
const MIN_COVER_RATIO = 0.6;

/** The widest cover proportions a card draws. */
const MAX_COVER_RATIO = 2.0;

/** The namespace of the icon elements. */
const SVG_NAMESPACE = 'http://www.w3.org/2000/svg';

/** A game identifier as it may appear in a link: digits only. */
const GAME_ID = /^[0-9]+$/;

/** The address every game page on BoardGameGeek starts with. */
const BGG_ORIGIN = 'https://boardgamegeek.com/';

/**
 * Returns the title, or the fallback text when the title is missing or blank.
 * @param {unknown} title The title from the record.
 * @param {string} fallback The text to use instead.
 * @returns {string}
 */
function titleOrFallback(title, fallback) {
  return typeof title === 'string' && title.trim() !== '' ? title : fallback;
}

/**
 * Returns the picture of a record when it is a stored file on the site's own origin, and null otherwise.
 * @param {object} record The card record.
 * @returns {object | null}
 */
function usableCover(record) {
  const cover = record.cover;

  if (cover === null || typeof cover !== 'object') {
    return null;
  }

  return typeof cover.url === 'string' && ART_PATH.test(cover.url) ? cover : null;
}

/**
 * Tells whether a value is a whole number from 1 to 4096, the sizes a stored picture can have.
 * @param {unknown} value The value from the data.
 * @returns {boolean}
 */
function isPictureSize(value) {
  return Number.isInteger(value) && value >= 1 && value <= 4096;
}

/**
 * Returns the colour pair of a cover: the pair taken from the box art when the record carries a valid one, otherwise the entry of
 * the colour table the record points at, otherwise a neutral wood pair.
 * @param {object} record The card record.
 * @param {object[]} palette The colour table of the layout.
 * @returns {{ background: string, text: string }}
 */
function toneOf(record, palette) {
  const colour = record.colour;

  if (colour !== null && typeof colour === 'object' && typeof colour.background === 'string'
    && ART_COLOUR.test(colour.background) && ART_TEXT_COLOURS.includes(colour.text)) {
    return colour;
  }

  const tone = palette[record.toneIndex];
  const valid = tone !== undefined && tone !== null && HEX_COLOUR.test(tone.background) && HEX_COLOUR.test(tone.text);

  return valid ? tone : FALLBACK_TONE;
}

/**
 * Returns the proportions of the box front, kept between the narrowest and widest a card draws.
 * @param {object} record The card record.
 * @returns {number}
 */
function coverRatio(record) {
  const ratio = Number.isFinite(record.ratio) && record.ratio > 0 ? record.ratio : 1;

  return Math.min(MAX_COVER_RATIO, Math.max(MIN_COVER_RATIO, ratio));
}

/**
 * Draws the generated cover into the cover spot: the pattern chosen from the game's hash and a solid title plate. The plate is
 * hidden from screen readers because the title is the next thing they read. It is the cover of every game without a usable
 * picture and what stands in when a picture fails to load.
 * @param {HTMLElement} art The cover spot.
 * @param {string} title The title to put on the plate.
 * @param {number} patternIndex The pattern index of the record.
 */
function drawGeneratedCover(art, title, patternIndex) {
  art.dataset.pattern = PATTERN_NAMES[patternIndex] ?? 'plain';

  const label = document.createElement('span');
  label.className = 'card-plate-label';
  label.setAttribute('dir', 'auto');
  label.textContent = title;

  const plate = document.createElement('span');
  plate.className = 'card-plate';
  plate.setAttribute('aria-hidden', 'true');
  plate.append(label);
  art.append(plate);
}

/**
 * Sets the four edge colours of a picture on the cover spot when the data gives all four as valid colours.
 * @param {HTMLElement} art The cover spot.
 * @param {object | undefined} edges The edge colours from the record.
 */
function setEdgeColours(art, edges) {
  if (edges === null || typeof edges !== 'object') {
    return;
  }

  if (!EDGE_PROPERTIES.every(([side]) => typeof edges[side] === 'string' && HEX_COLOUR.test(edges[side]))) {
    return;
  }

  for (const [side, property] of EDGE_PROPERTIES) {
    art.style.setProperty(property, edges[side]);
  }
}

/**
 * Draws the picture into the cover spot, shown whole on the colours along its edges. When the browser cannot show the file the
 * picture is taken away and the generated cover is drawn instead, silently.
 * @param {HTMLElement} art The cover spot.
 * @param {object} cover The valid cover of the record.
 * @param {string} title The title the generated cover would carry.
 * @param {number} patternIndex The pattern index of the record.
 */
function drawPicture(art, cover, title, patternIndex) {
  art.dataset.fit = ART_FITS.includes(cover.fit) ? cover.fit : 'exact';
  setEdgeColours(art, cover.edges);

  const image = document.createElement('img');
  image.alt = '';
  image.draggable = false;
  image.src = cover.url;

  if (isPictureSize(cover.width) && isPictureSize(cover.height)) {
    image.width = cover.width;
    image.height = cover.height;
  }

  image.addEventListener(
    'error',
    () => {
      image.remove();
      delete art.dataset.fit;

      for (const [, property] of EDGE_PROPERTIES) {
        art.style.removeProperty(property);
      }

      drawGeneratedCover(art, title, patternIndex);
    },
    { once: true },
  );
  art.append(image);
}

/**
 * Builds the cover frame: the picture shown whole inside a white photo border, or the generated cover when the game has no usable
 * picture. The spot reserves its final proportions before the picture decodes and shows the edge colours until it does.
 * @param {object} record The card record.
 * @param {string} title The title shown on the card.
 * @param {object[]} palette The colour table of the layout.
 * @returns {HTMLDivElement}
 */
function buildCover(record, title, palette) {
  const frame = document.createElement('div');
  frame.className = 'card-cover';

  const art = document.createElement('div');
  art.className = 'card-art';

  const tone = toneOf(record, palette);
  art.style.setProperty('--bg', tone.background);
  art.style.setProperty('--fg', tone.text);
  frame.append(art);

  const cover = usableCover(record);

  if (cover !== null) {
    drawPicture(art, cover, title, record.patternIndex);
  } else {
    drawGeneratedCover(art, title, record.patternIndex);
  }

  return frame;
}

/**
 * Builds one icon from the sprite.
 * @param {string} iconsUrl The address of the icon sprite.
 * @param {string} id The symbol id.
 * @returns {SVGSVGElement}
 */
function buildIcon(iconsUrl, id) {
  const icon = document.createElementNS(SVG_NAMESPACE, 'svg');
  icon.setAttribute('aria-hidden', 'true');
  icon.setAttribute('focusable', 'false');

  const use = document.createElementNS(SVG_NAMESPACE, 'use');
  use.setAttribute('href', iconsUrl + '#' + id);
  icon.append(use);

  return icon;
}

/**
 * Builds the link to the game's page on BoardGameGeek, or returns null when the game id is not made of digits only. The address
 * is a fixed host and path with the id appended, never a string taken from the data.
 * @param {object} record The card record.
 * @param {object} copy The visitor-facing strings.
 * @param {string} iconsUrl The address of the icon sprite.
 * @returns {HTMLParagraphElement | null}
 */
function buildLink(record, copy, iconsUrl) {
  const id = String(record.gameId);

  if (!GAME_ID.test(id)) {
    return null;
  }

  const anchor = document.createElement('a');
  anchor.setAttribute('href', BGG_ORIGIN + (record.isExpansion === true ? 'boardgameexpansion/' : 'boardgame/') + id);
  anchor.setAttribute('target', '_blank');
  anchor.setAttribute('rel', 'noopener');

  const hint = document.createElement('span');
  hint.className = 'visually-hidden';
  hint.textContent = copy.newTabHint;
  anchor.append(document.createTextNode(copy.bggLink + ' '), hint, buildIcon(iconsUrl, 'i-out'));

  const paragraph = document.createElement('p');
  paragraph.className = 'card-link';
  paragraph.append(anchor);

  return paragraph;
}

/**
 * Builds one entry of the game-night strip: a decorative icon, the bold value and the quiet label that screen readers read.
 * @param {{ icon: string, value: string, label: string }} fact The entry from the format functions.
 * @param {string} iconsUrl The address of the icon sprite.
 * @returns {HTMLLIElement}
 */
function buildFact(fact, iconsUrl) {
  const icon = buildIcon(iconsUrl, fact.icon);
  icon.classList.add('fact-icon');

  const value = document.createElement('span');
  value.className = 'v';
  value.textContent = fact.value;

  const label = document.createElement('span');
  label.className = 'l';
  label.textContent = fact.label;

  const item = document.createElement('li');
  item.className = 'fact';
  item.append(icon, value, document.createTextNode(' '), label);

  return item;
}

/**
 * Builds the quiet note that stands in the strip's place when no detail is known.
 * @param {object} copy The visitor-facing strings.
 * @returns {HTMLParagraphElement}
 */
function buildNote(copy) {
  const note = document.createElement('p');
  note.className = 'card-note';
  note.textContent = copy.noDetails;

  return note;
}

/**
 * Builds the game-night strip, the quiet note when no detail is known or the card data did not arrive, or nothing when details
 * are known but none of them belongs in the strip.
 * @param {object} record The card record.
 * @param {object} copy The visitor-facing strings.
 * @param {string} iconsUrl The address of the icon sprite.
 * @returns {HTMLElement | null}
 */
function buildStripOrNote(record, copy, iconsUrl) {
  if (record.incomplete === true || !hasAnyDetail(record)) {
    return buildNote(copy);
  }

  const facts = factsFor(record, copy);

  if (facts.length === 0) {
    return null;
  }

  const list = document.createElement('ul');
  list.className = 'facts';
  list.append(...facts.map((fact) => buildFact(fact, iconsUrl)));

  return list;
}

/**
 * Tells whether a value is a string with something other than white space in it.
 * @param {unknown} value The value from the data.
 * @returns {boolean}
 */
function isFilledText(value) {
  return typeof value === 'string' && value.trim() !== '';
}

/**
 * Keeps the filled strings of a list from the data.
 * @param {unknown} list The list from the record.
 * @returns {string[]}
 */
function filledTexts(list) {
  return Array.isArray(list) ? list.filter(isFilledText) : [];
}

/**
 * Builds one row of a label-and-value list: the label in the first column, the value beside it.
 * @param {string} label The quiet label.
 * @param {HTMLElement} value The value element, a dd.
 * @param {HTMLElement} list The list the row is added to.
 */
function appendRow(label, value, list) {
  const term = document.createElement('dt');
  term.textContent = label;
  list.append(term, value);
}

/**
 * Builds the stored-in group, or returns null when the record has no usable location. The location is shown exactly as it was
 * given, with its own text direction and no language mark.
 * @param {object} record The card record.
 * @param {object} copy The visitor-facing strings.
 * @returns {HTMLDListElement | null}
 */
function buildWhere(record, copy) {
  if (!isFilledText(record.location)) {
    return null;
  }

  const value = document.createElement('dd');
  value.setAttribute('dir', 'auto');
  value.textContent = record.location;

  const list = document.createElement('dl');
  list.className = 'card-where';
  appendRow(copy.storedIn, value, list);

  return list;
}

/**
 * Builds the mechanics list: every mechanic in full, marked as English because BoardGameGeek names them in English, with the
 * separator drawn by the style sheet.
 * @param {string[]} mechanics The mechanic names.
 * @returns {HTMLUListElement}
 */
function buildMechanics(mechanics) {
  const list = document.createElement('ul');
  list.className = 'mech';
  list.setAttribute('role', 'list');
  list.setAttribute('lang', 'en');

  for (const name of mechanics) {
    const item = document.createElement('li');
    item.setAttribute('role', 'listitem');
    item.textContent = name;

    if (list.childElementCount > 0) {
      list.append(document.createTextNode(' '));
    }

    list.append(item);
  }

  return list;
}

/**
 * Builds the quiet rows below the strip: the rating, the designers and the mechanics, each only when it has data. Returns null
 * when none has.
 * @param {object} record The card record.
 * @param {object} copy The visitor-facing strings.
 * @returns {HTMLDListElement | null}
 */
function buildMeta(record, copy) {
  const list = document.createElement('dl');
  list.className = 'card-meta';

  const rating = ratingText(record.rating, copy);

  if (rating !== null) {
    const value = document.createElement('dd');
    value.textContent = rating;
    appendRow(copy.ratingLabel, value, list);
  }

  const designers = filledTexts(record.designers);

  if (designers.length > 0) {
    const value = document.createElement('dd');
    value.setAttribute('dir', 'auto');
    value.textContent = designers.join(', ');
    appendRow(copy.designersLabel(designers.length), value, list);
  }

  const mechanics = filledTexts(record.mechanics);

  if (mechanics.length > 0) {
    const value = document.createElement('dd');
    value.append(buildMechanics(mechanics));
    appendRow(copy.mechanicsLabel(mechanics.length), value, list);
  }

  return list.childElementCount === 0 ? null : list;
}

/**
 * Builds the card of one game: the grip strip of the phone sheet, the close button, the header with cover, title and year, and the
 * ruled area with the game-night strip or the quiet note, the location, the rating, designers and mechanics, and the link to
 * BoardGameGeek last. Whatever is missing is left out.
 * @param {object} record The card record, or the least a card can show when the data did not arrive.
 * @param {object} copy The visitor-facing strings.
 * @param {{ iconsUrl?: string, palette?: object[] }} [options] The sprite address and the colour table of the layout on screen.
 * @returns {HTMLDivElement} The card element.
 */
export function buildCard(record, copy, options = {}) {
  const iconsUrl = options.iconsUrl ?? '';
  const palette = options.palette ?? [];
  const titleText = titleOrFallback(record.title, copy.untitled);

  const card = document.createElement('div');
  card.className = 'card';

  const grip = document.createElement('span');
  grip.className = 'card-grip';
  grip.setAttribute('aria-hidden', 'true');

  const close = document.createElement('button');
  close.type = 'button';
  close.className = 'card-close';
  close.setAttribute('aria-label', copy.close);
  close.append(buildIcon(iconsUrl, 'i-close'));

  const closeWrap = document.createElement('div');
  closeWrap.className = 'card-close-wrap';
  closeWrap.append(close);

  const title = document.createElement('h2');
  title.id = 'card-title';
  title.className = 'card-title';
  title.tabIndex = -1;
  title.setAttribute('dir', 'auto');
  title.textContent = titleText;

  const titles = document.createElement('div');
  titles.className = 'card-titles';
  titles.append(title);

  if (Number.isInteger(record.year) && record.year > 0) {
    const year = document.createElement('p');
    year.className = 'card-year';
    year.textContent = String(record.year);
    titles.append(year);
  }

  const head = document.createElement('header');
  head.className = 'card-head';
  head.style.setProperty('--cover-ratio', String(coverRatio(record)));
  head.append(buildCover(record, titleText, palette), titles);

  const ruled = document.createElement('div');
  ruled.className = 'card-ruled';

  for (const part of [buildStripOrNote(record, copy, iconsUrl), buildWhere(record, copy), buildMeta(record, copy)]) {
    if (part !== null) {
      ruled.append(part);
    }
  }

  const link = buildLink(record, copy, iconsUrl);

  if (link !== null) {
    ruled.append(link);
  }

  card.append(grip, closeWrap, head, ruled);

  return card;
}
