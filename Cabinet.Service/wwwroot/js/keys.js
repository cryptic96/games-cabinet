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
 * the same way. Home and End give the first and the last box.
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
 * Makes the cabinet one tab stop with arrow-key movement. One box holds tabindex 0 and every other -1; the one that holds it is
 * the box that last had focus, or the first box when none has. A redraw replaces the boxes, so the page calls refresh afterwards.
 * @param {HTMLElement} mount The element that holds the boxes.
 * @returns {{ refresh: () => void, keyOf: (element: HTMLElement) => string, focusKey: (key: string) => boolean }} The controls.
 */
export function initRoving(mount) {
  let currentKey = null;
  let currentBox = null;

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
      hold(box);
    }
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
    const rects = boxes.map((candidate) => {
      const rect = candidate.getBoundingClientRect();

      return { left: rect.left, top: rect.top, right: rect.right, bottom: rect.bottom, shelf: candidate.dataset.shelf };
    });
    const target = nextBox(rects, boxes.indexOf(box), event.key);

    if (target >= 0 && boxes[target] !== box) {
      boxes[target].focus();
    }
  });

  return { refresh, keyOf, focusKey };
}
