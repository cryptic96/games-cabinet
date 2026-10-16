/**
 * Keyboard model of the cabinet: the whole cabinet is one tab stop, and the arrow keys, Home and End move focus between the boxes
 * by where they sit on screen. Enter and Space are left to the native button, which opens the card.
 */

/** How far apart two edges may be, in pixels, and still count as touching. */
const TOLERANCE = 2;

/** The weight of the sideways (or vertical) offset against the distance along the direction of travel. */
const ACROSS_WEIGHT = 2;

/** The keys the cabinet takes over. */
const HANDLED_KEYS = new Set(['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End']);

/**
 * Gives the gap between two ranges on one axis, zero when they overlap.
 * @param {number} startA Where the first range starts.
 * @param {number} endA Where the first range ends.
 * @param {number} startB Where the second range starts.
 * @param {number} endB Where the second range ends.
 * @returns {number}
 */
function gapBetween(startA, endA, startB, endB) {
  return Math.max(0, startB - endA, startA - endB);
}

/**
 * Tells whether a candidate lies in the direction of travel from the current box, and how far along it that is.
 * @param {object} from The rectangle of the current box.
 * @param {object} to The rectangle of the candidate.
 * @param {string} key The arrow key pressed.
 * @returns {number | null} The distance along the direction, or null when the candidate does not lie that way.
 */
function distanceAlong(from, to, key) {
  switch (key) {
    case 'ArrowRight':
      return to.left >= from.right - TOLERANCE ? Math.max(0, to.left - from.right) : null;
    case 'ArrowLeft':
      return to.right <= from.left + TOLERANCE ? Math.max(0, from.left - to.right) : null;
    case 'ArrowDown':
      return to.top >= from.bottom - TOLERANCE ? Math.max(0, to.top - from.bottom) : null;
    default:
      return to.bottom <= from.top + TOLERANCE ? Math.max(0, from.top - to.bottom) : null;
  }
}

/**
 * Describes how well a candidate fits a key: the distance along the direction plus twice the offset across it, and how far the
 * centres are apart across the direction, which settles candidates that score the same.
 * @param {object} from The rectangle of the current box.
 * @param {object} to The rectangle of the candidate.
 * @param {string} key The arrow key pressed.
 * @returns {{ score: number, centre: number } | null} The fit, or null when the candidate does not lie that way.
 */
function fitOf(from, to, key) {
  const along = distanceAlong(from, to, key);

  if (along === null) {
    return null;
  }

  const horizontal = key === 'ArrowLeft' || key === 'ArrowRight';
  const across = horizontal
    ? gapBetween(from.top, from.bottom, to.top, to.bottom)
    : gapBetween(from.left, from.right, to.left, to.right);
  const centre = horizontal
    ? Math.abs((from.top + from.bottom) / 2 - (to.top + to.bottom) / 2)
    : Math.abs((from.left + from.right) / 2 - (to.left + to.right) / 2);

  return { score: along + ACROSS_WEIGHT * across, centre };
}

/**
 * Tells whether the first fit is better than the second; scores within the tolerance count as equal and fall to the centres.
 * @param {{ score: number, centre: number }} fit The fit to test.
 * @param {{ score: number, centre: number }} best The fit to beat.
 * @returns {boolean}
 */
function beats(fit, best) {
  if (Math.abs(fit.score - best.score) > TOLERANCE) {
    return fit.score < best.score;
  }

  return fit.centre < best.centre - TOLERANCE;
}

/**
 * Picks the box a key moves to. Left and right stay on the current shelf and stop at its ends; up and down look at every box
 * above or below, so a pile or a stack is walked one box at a time before the next shelf, and a section below or above is reached
 * the same way; where nothing lies that way, they carry on in the next or previous section. Home and End give the first and the
 * last box.
 * @param {{ left: number, top: number, right: number, bottom: number, shelf: string }[]} rects The boxes' rectangles in document order.
 * @param {number} currentIndex The index of the box that has focus.
 * @param {string} key The key pressed.
 * @returns {number} The index to focus: the current one when nothing lies that way, and -1 when there are no boxes.
 */
export function nextBox(rects, currentIndex, key) {
  const last = rects.length - 1;

  if (last < 0) {
    return -1;
  }

  if (key === 'Home') {
    return 0;
  }

  if (key === 'End') {
    return last;
  }

  if (currentIndex < 0 || currentIndex > last) {
    return 0;
  }

  const from = rects[currentIndex];
  const horizontal = key === 'ArrowLeft' || key === 'ArrowRight';
  let bestIndex = currentIndex;
  let best = null;

  rects.forEach((to, index) => {
    if (index === currentIndex || (horizontal && to.shelf !== from.shelf)) {
      return;
    }

    const fit = fitOf(from, to, key);

    if (fit !== null && (best === null || beats(fit, best))) {
      best = fit;
      bestIndex = index;
    }
  });

  if (best === null && !horizontal) {
    return nextSectionBox(rects, currentIndex, key);
  }

  return bestIndex;
}

/**
 * Picks the rectangle nearest to a starting rectangle with the same measure the arrow keys use: a rectangle that overlaps or touches
 * it is at distance zero, any other is as far as the straight gap between the two, and the centres settle candidates that tie.
 * @param {{ left: number, top: number, right: number, bottom: number }[]} rects The candidate rectangles.
 * @param {{ left: number, top: number, right: number, bottom: number }} from The rectangle to measure from.
 * @returns {number} The index of the nearest rectangle, or -1 when the list is empty.
 */
export function nearestBox(rects, from) {
  const centreX = (from.left + from.right) / 2;
  const centreY = (from.top + from.bottom) / 2;
  let bestIndex = -1;
  let best = null;

  rects.forEach((to, index) => {
    const gap = Math.hypot(gapBetween(from.left, from.right, to.left, to.right), gapBetween(from.top, from.bottom, to.top, to.bottom));
    const centre = Math.hypot(centreX - (to.left + to.right) / 2, centreY - (to.top + to.bottom) / 2);

    if (best === null || gap < best.gap - TOLERANCE || (gap <= best.gap + TOLERANCE && centre < best.centre - TOLERANCE)) {
      best = { gap, centre };
      bestIndex = index;
    }
  });

  return bestIndex;
}

/**
 * Lists the indices of the rectangles in on-screen reading order: by top edge, then by left edge, where tops within the tolerance of
 * the first top of a row count as the same row. The order does not depend on the order of the input.
 * @param {{ left: number, top: number }[]} rects The rectangles to order.
 * @returns {number[]} The indices of the rectangles, first in reading order first.
 */
export function readingOrder(rects) {
  const byTop = rects.map((rect, index) => index).sort((a, b) => rects[a].top - rects[b].top || rects[a].left - rects[b].left || a - b);
  const ordered = [];
  let row = [];
  let rowTop = 0;

  const closeRow = () => {
    row.sort((a, b) => rects[a].left - rects[b].left || rects[a].top - rects[b].top || a - b);
    ordered.push(...row);
    row = [];
  };

  for (const index of byTop) {
    if (row.length > 0 && rects[index].top > rowTop + TOLERANCE) {
      closeRow();
    }

    if (row.length === 0) {
      rowTop = rects[index].top;
    }

    row.push(index);
  }

  closeRow();

  return ordered;
}

/**
 * Gives the section number a box stands in, read from its shelf value, or -1 when it has none.
 * @param {{ shelf: string }} rect A box's rectangle.
 * @returns {number}
 */
function sectionOf(rect) {
  const section = Number.parseInt(String(rect.shelf), 10);

  return Number.isNaN(section) ? -1 : section;
}

/**
 * Picks the box for an up or down key when nothing lies in that direction on screen, which happens at the end of a column when the
 * sections stand side by side: down continues at the top of the next section and up at the bottom of the previous one, so every
 * section can be reached from the keyboard. The box nearest across breaks a tie.
 * @param {{ left: number, top: number, right: number, bottom: number, shelf: string }[]} rects The boxes' rectangles in document order.
 * @param {number} currentIndex The index of the box that has focus.
 * @param {string} key ArrowUp or ArrowDown.
 * @returns {number} The index to focus, or the current one when there is no other section that way.
 */
function nextSectionBox(rects, currentIndex, key) {
  const from = rects[currentIndex];
  const own = sectionOf(from);
  const down = key === 'ArrowDown';
  const sections = rects.map(sectionOf).filter((section) => own >= 0 && section >= 0 && (down ? section > own : section < own));

  if (sections.length === 0) {
    return currentIndex;
  }

  const target = down ? Math.min(...sections) : Math.max(...sections);
  const centre = (from.left + from.right) / 2;
  let bestIndex = currentIndex;
  let best = null;

  rects.forEach((to, index) => {
    if (sectionOf(to) !== target) {
      return;
    }

    const edge = down ? to.top : -to.bottom;
    const offset = Math.abs(centre - (to.left + to.right) / 2);

    if (best === null || edge < best.edge - TOLERANCE || (edge <= best.edge + TOLERANCE && offset < best.offset - TOLERANCE)) {
      best = { edge, offset };
      bestIndex = index;
    }
  });

  return bestIndex;
}

/**
 * Gives the key that tells a box apart from every other: its collection entry and its kind, so a marker and its base box differ.
 * @param {HTMLElement} element A box.
 * @returns {string}
 */
function keyOf(element) {
  return element.dataset.entryId + '|' + element.dataset.kind;
}

/**
 * Reads the rectangles of boxes as they sit on screen right now.
 * @param {HTMLElement[]} boxes The boxes to measure.
 * @returns {{ left: number, top: number, right: number, bottom: number, shelf: string }[]}
 */
function rectsOf(boxes) {
  return boxes.map((candidate) => {
    const rect = candidate.getBoundingClientRect();

    return { left: rect.left, top: rect.top, right: rect.right, bottom: rect.bottom, shelf: candidate.dataset.shelf };
  });
}

/**
 * Makes the cabinet one tab stop with arrow-key movement. One box holds tabindex 0 and every other -1; the one that holds it is
 * the box that last had focus, or the first box when none has. While a filter lights some boxes and shadows the rest, the stop follows
 * the light: until the visitor places it, it sits on the first lit box in reading order, and a stop the visitor placed that falls into
 * shadow moves to the nearest lit box without moving focus. A redraw replaces the boxes, so the page calls refresh afterwards.
 * @param {HTMLElement} mount The element that holds the boxes.
 * @returns {{ refresh: () => void, keyOf: (element: HTMLElement) => string, focusKey: (key: string) => boolean }} The controls.
 */
export function initRoving(mount) {
  let currentKey = null;
  let currentBox = null;
  let placedByVisitor = false;
  let wasFiltering = false;

  /**
   * Gives the one tab stop to a box and takes it from the previous holder.
   * @param {HTMLElement} box The box to hold tabindex 0.
   */
  function hold(box) {
    if (currentBox !== null && currentBox !== box && currentBox.isConnected) {
      currentBox.setAttribute('tabindex', '-1');
    }

    box.setAttribute('tabindex', '0');
    currentBox = box;
    currentKey = keyOf(box);
  }

  /**
   * Sets the tab stop on every box again after the boxes were replaced: the box with the remembered key keeps it, and the first box
   * takes it when that box is gone.
   */
  function refresh() {
    const boxes = [...mount.querySelectorAll('.placement')];

    currentBox = null;

    if (boxes.length === 0) {
      currentKey = null;

      return;
    }

    const holder = boxes.find((box) => keyOf(box) === currentKey) ?? boxes[0];

    for (const box of boxes) {
      box.setAttribute('tabindex', box === holder ? '0' : '-1');
    }

    currentBox = holder;
    currentKey = keyOf(holder);
    followTheLight();
  }

  /**
   * Moves the tab stop to a box in the light while a filter lights some boxes and shadows the rest. With zero matches or with no filter
   * the stop stays where it is, so a keyboard user is never held away from any box.
   */
  function followTheLight() {
    const boxes = [...mount.querySelectorAll('.placement')];
    const lit = boxes.filter((box) => !box.hasAttribute('data-dim'));

    if (lit.length === 0 || lit.length === boxes.length) {
      return;
    }

    if (!placedByVisitor) {
      hold(lit[readingOrder(rectsOf(lit))[0]]);

      return;
    }

    const holder = boxes.find((box) => keyOf(box) === currentKey);

    if (holder !== undefined && holder.hasAttribute('data-dim')) {
      hold(lit[nearestBox(rectsOf(lit), rectsOf([holder])[0])]);
    }
  }

  /**
   * Focuses the box with the given key, when it is on the page.
   * @param {string} key A key from keyOf.
   * @returns {boolean} Whether a box took focus.
   */
  function focusKey(key) {
    const box = [...mount.querySelectorAll('.placement')].find((candidate) => keyOf(candidate) === key);

    if (box === undefined) {
      return false;
    }

    box.focus();

    return true;
  }

  mount.addEventListener('focusin', (event) => {
    const box = event.target.closest?.('.placement') ?? null;

    if (box !== null && mount.contains(box)) {
      placedByVisitor = true;
      hold(box);
    }
  });

  document.addEventListener('cabinet:filtered', (event) => {
    const filtering = event.detail?.active === true;
    const focused = document.activeElement?.closest?.('.placement') ?? null;

    if (filtering && !wasFiltering) {
      placedByVisitor = false;
    }

    wasFiltering = filtering;

    if (focused !== null && mount.contains(focused)) {
      placedByVisitor = true;
    }

    followTheLight();
  });

  mount.addEventListener('keydown', (event) => {
    if (!HANDLED_KEYS.has(event.key) || event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) {
      return;
    }

    const box = event.target.closest?.('.placement') ?? null;

    if (box === null || !mount.contains(box)) {
      return;
    }

    event.preventDefault();

    const boxes = [...mount.querySelectorAll('.placement')];
    const lit = boxes.filter((candidate) => !candidate.hasAttribute('data-dim'));
    const filtering = lit.length > 0 && lit.length < boxes.length;

    if (filtering && (event.key === 'Home' || event.key === 'End')) {
      const order = readingOrder(rectsOf(lit));
      const edge = lit[event.key === 'Home' ? order[0] : order[order.length - 1]];

      if (edge !== box) {
        edge.focus();
      }

      return;
    }

    const candidates = filtering ? boxes.filter((candidate) => candidate === box || !candidate.hasAttribute('data-dim')) : boxes;
    const target = nextBox(rectsOf(candidates), candidates.indexOf(box), event.key);

    if (target >= 0 && candidates[target] !== box) {
      candidates[target].focus();
    }
  });

  return { refresh, keyOf, focusKey };
}
