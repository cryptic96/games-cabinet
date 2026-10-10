/**
 * Builds the detail card of one game as DOM. The card is text and elements only: every value from the collection reaches the page
 * through text content or an attribute the script checks first, so nothing a source writes can become markup.
 */

/** The only address shape a card picture may have: a stored file on the site's own origin. */
const ART_PATH = /^\/art\/[0-9a-f]{16}-[0-9]{1,4}\.webp$/;

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
 * Builds the cover frame: the picture shown whole inside its frame.
 * @param {object} record The card record.
 * @returns {HTMLDivElement}
 */
function buildCover(record) {
  const frame = document.createElement('div');
  frame.className = 'card-cover';

  const art = document.createElement('div');
  art.className = 'card-art';
  frame.append(art);

  const cover = usableCover(record);

  if (cover !== null) {
    const image = document.createElement('img');
    image.alt = '';
    image.draggable = false;
    image.src = cover.url;
    art.append(image);
  }

  return frame;
}

/**
 * Builds the link to the game's page on BoardGameGeek, or returns null when the game id is not made of digits only. The address
 * is a fixed host and path with the id appended, never a string taken from the data.
 * @param {object} record The card record.
 * @param {object} copy The visitor-facing strings.
 * @returns {HTMLParagraphElement | null}
 */
function buildLink(record, copy) {
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
  anchor.append(document.createTextNode(copy.bggLink + ' '), hint);

  const paragraph = document.createElement('p');
  paragraph.className = 'card-link';
  paragraph.append(anchor);

  return paragraph;
}

/**
 * Builds the card of one game: the close button, the header with cover, title and year, and the ruled area with the quiet note
 * when details are missing and the link to BoardGameGeek last.
 * @param {object} record The card record, or the least a card can show when the data did not arrive.
 * @param {object} copy The visitor-facing strings.
 * @param {object} [options] Reserved for the parts that need the page: the sprite address and the colour table.
 * @returns {HTMLDivElement} The card element.
 */
export function buildCard(record, copy, options = {}) {
  const card = document.createElement('div');
  card.className = 'card';

  const close = document.createElement('button');
  close.type = 'button';
  close.className = 'card-close';
  close.textContent = copy.close;

  const title = document.createElement('h2');
  title.id = 'card-title';
  title.className = 'card-title';
  title.tabIndex = -1;
  title.setAttribute('dir', 'auto');
  title.textContent = titleOrFallback(record.title, copy.untitled);

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
  head.append(buildCover(record), titles);

  const ruled = document.createElement('div');
  ruled.className = 'card-ruled';

  if (record.incomplete === true) {
    const note = document.createElement('p');
    note.className = 'card-note';
    note.textContent = copy.noDetails;
    ruled.append(note);
  }

  const link = buildLink(record, copy);

  if (link !== null) {
    ruled.append(link);
  }

  card.append(close, head, ruled);

  return card;
}
