/**
 * Draws a cabinet layout as DOM. Geometry and colours reach the page only through custom properties set on element
 * styles and text only through text content; the script makes no layout decisions.
 *
 * Every box takes its background and text colour from the layout's palette table through --bg and --fg. A face-out box
 * is a generated cover: the palette colour, a pattern chosen from the game's own hash and a solid title plate. That
 * generated cover is also what stands in whenever a game has no usable box art.
 *
 * The three decorative elements of every section and the two attributes on every cubby carry no meaning of their own.
 * They exist only so the stylesheet can draw the furniture: the moulded top, the side boards, the plinth, a stable tone
 * for each shelf and the end of each row.
 */

/** The cover pattern names in the order of the pattern index the layout carries. */
const PATTERN_NAMES = ['stripes', 'chevrons', 'dots', 'rings', 'diagonal', 'plain'];

/**
 * Sets one custom property to a whole-number value.
 * @param {HTMLElement} element The element to style.
 * @param {string} name The custom property name.
 * @param {number} value The value in millimetres or as a plain number.
 */
function setNumber(element, name, value) {
  element.style.setProperty(name, String(value));
}

/**
 * Returns the title, or the fallback text when the title is missing or blank.
 * @param {string | undefined} text The title from the layout.
 * @param {string} fallback The text to use instead.
 * @returns {string}
 */
function textOrFallback(text, fallback) {
  return typeof text === 'string' && text.trim() !== '' ? text : fallback;
}

/**
 * Returns the accessible name and tooltip of a placement. A layer names its base game, the marker says how many
 * expansions it stands for, and every other placement is named by its full title.
 * @param {object} placement One placement from the layout.
 * @param {object} copy The visitor-facing strings.
 * @returns {string}
 */
function accessibleName(placement, copy) {
  const title = textOrFallback(placement.title, copy.untitled);
  const baseTitle = textOrFallback(placement.baseTitle, copy.untitled);

  if (placement.kind === 'expansionLayer') {
    return copy.layerName(title, baseTitle);
  }

  if (placement.kind === 'moreMarker') {
    return copy.moreName(placement.moreCount, baseTitle);
  }

  return title;
}

/**
 * Returns the text drawn on a placement. The marker shows its hidden count; the rest show the label the layout
 * prepared, or the fallback when it is blank.
 * @param {object} placement One placement from the layout.
 * @param {object} copy The visitor-facing strings.
 * @returns {string}
 */
function labelText(placement, copy) {
  return placement.kind === 'moreMarker'
    ? copy.moreLabel(placement.moreCount)
    : textOrFallback(placement.label, copy.untitled);
}

/**
 * Builds the button for one placement.
 * @param {object} placement One placement from the layout.
 * @param {object} copy The visitor-facing strings.
 * @param {object[]} palette The colour table of the layout.
 * @returns {HTMLButtonElement}
 */
function buildPlacement(placement, copy, palette) {
  const button = document.createElement('button');
  button.type = 'button';
  button.className = 'placement';
  button.dataset.kind = placement.kind;
  button.dataset.gameId = String(placement.gameId);

  if (placement.familyId !== undefined) {
    button.dataset.familyId = String(placement.familyId);
  }

  setNumber(button, '--x', placement.xMm);
  setNumber(button, '--y', placement.yMm);
  setNumber(button, '--w', placement.widthMm);
  setNumber(button, '--h', placement.heightMm);

  const tone = palette[placement.toneIndex];

  if (tone !== undefined && placement.kind !== 'moreMarker') {
    button.style.setProperty('--bg', tone.background);
    button.style.setProperty('--fg', tone.text);
  }

  const name = accessibleName(placement, copy);
  button.setAttribute('aria-label', name);
  button.title = name;

  const label = document.createElement('span');
  label.className = 'placement-label';
  label.setAttribute('dir', 'auto');
  label.textContent = labelText(placement, copy);

  if (placement.kind === 'cover') {
    button.dataset.pattern = PATTERN_NAMES[placement.patternIndex] ?? 'plain';

    const plate = document.createElement('span');
    plate.className = 'cover-plate';
    plate.append(label);
    button.append(plate);
  } else {
    button.append(label);
  }

  return button;
}

/** The number of distinct shelf tones the stylesheet defines. */
const BOARD_TONE_COUNT = 6;

/**
 * Builds one cubby with its placements.
 * @param {object} cubby One cubby from the layout.
 * @param {object} copy The visitor-facing strings.
 * @param {object[]} palette The colour table of the layout.
 * @param {object} section The section the cubby belongs to.
 * @param {number[]} rowTops The distinct cubby tops of the section in ascending order.
 * @returns {HTMLDivElement}
 */
function buildCubby(cubby, copy, palette, section, rowTops) {
  const element = document.createElement('div');
  element.className = 'cubby';
  element.dataset.board = String((section.index * 2 + rowTops.indexOf(cubby.yMm)) % BOARD_TONE_COUNT);

  if (cubby.xMm + cubby.widthMm === section.widthMm) {
    element.dataset.rowEnd = 'true';
  }

  setNumber(element, '--x', cubby.xMm);
  setNumber(element, '--y', cubby.yMm);
  setNumber(element, '--w', cubby.widthMm);
  setNumber(element, '--h', cubby.heightMm);
  element.append(...cubby.placements.map((placement) => buildPlacement(placement, copy, palette)));

  return element;
}

/**
 * Builds one empty decorative element that screen readers skip.
 * @param {string} className The class the stylesheet draws it by.
 * @returns {HTMLDivElement}
 */
function buildDecoration(className) {
  const element = document.createElement('div');
  element.className = className;
  element.setAttribute('aria-hidden', 'true');

  return element;
}

/**
 * Builds one section with all of its cubbies.
 * @param {object} section One section from the layout.
 * @param {object} copy The visitor-facing strings.
 * @param {object[]} palette The colour table of the layout.
 * @returns {HTMLDivElement}
 */
function buildSection(section, copy, palette) {
  const element = document.createElement('div');
  element.className = 'section';
  setNumber(element, '--section-w', section.widthMm + 2 * section.frameMm);
  setNumber(element, '--section-h', section.heightMm + 2 * section.frameMm);
  setNumber(element, '--frame', section.frameMm);

  const rowTops = [...new Set(section.cubbies.map((cubby) => cubby.yMm))].sort((a, b) => a - b);

  const body = document.createElement('div');
  body.className = 'section-body';
  body.append(...section.cubbies.map((cubby) => buildCubby(cubby, copy, palette, section, rowTops)));
  element.append(
    body,
    buildDecoration('section-top'),
    buildDecoration('section-trim'),
    buildDecoration('section-base'),
  );

  return element;
}

/**
 * Replaces the content of the mount with the drawn cabinet.
 * @param {HTMLElement} mount The element that holds the cabinet.
 * @param {object} layout The layout JSON.
 * @param {object} copy The visitor-facing strings.
 */
export function renderCabinet(mount, layout, copy) {
  mount.replaceChildren(...layout.sections.map((section) => buildSection(section, copy, layout.palette ?? [])));
}
