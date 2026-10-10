/**
 * Pure helpers of the detail card that need no page: which box a tap resolves to, what a card shows when its data did not
 * arrive, and an index of the drawn boxes by entry. Nothing here touches the document, so the plain Node test runner can drive it.
 */

/** The box kinds that are an expansion even when the placement does not say so. */
const EXPANSION_KINDS = ['expansionLayer', 'expansionSpine', 'orphanExpansion'];

/**
 * Says which entry a tap on a box opens and whether the box was the marker that counts hidden expansions. The marker shares the
 * entry id of its base game, so the entry to open is the same; the flag tells the caller to look for the base box and not the
 * marker when it needs the box itself.
 * @param {string} kind The kind of the tapped box.
 * @param {string | number} entryId The entry id the box carries.
 * @returns {{ entryId: string | number, atExpansions: boolean }}
 */
export function sourceEntry(kind, entryId) {
  return { entryId, atExpansions: kind === 'moreMarker' };
}

/** The box kinds that stand upright with the spine facing out and so turn about the vertical axis. */
const UPRIGHT_KINDS = ['spine', 'expansionSpine'];

/** The box kinds that lie flat with the spine facing out and so tip about the horizontal axis. */
const LYING_KINDS = ['flatBox', 'expansionLayer', 'orphanExpansion'];

/**
 * Says how a box moves when it is pulled out: an upright spine turns about the vertical axis, a lying box tips about the horizontal
 * axis, and a box that already shows its cover only lifts.
 * @param {string} kind The kind of the box.
 * @returns {'turn-y' | 'turn-x' | 'lift'}
 */
export function pullKind(kind) {
  if (UPRIGHT_KINDS.includes(kind)) {
    return 'turn-y';
  }

  return LYING_KINDS.includes(kind) ? 'turn-x' : 'lift';
}

/**
 * Tells whether at least half of a box's area lies inside the viewport, which is the least a box needs to be flown from or to.
 * @param {{ left: number, top: number, width: number, height: number }} rect The box's rectangle in viewport coordinates.
 * @param {{ width: number, height: number }} viewport The size of the viewport.
 * @returns {boolean}
 */
export function isMostlyOnScreen(rect, viewport) {
  const area = rect.width * rect.height;

  if (!(area > 0)) {
    return false;
  }

  const visibleWidth = Math.min(rect.left + rect.width, viewport.width) - Math.max(rect.left, 0);
  const visibleHeight = Math.min(rect.top + rect.height, viewport.height) - Math.max(rect.top, 0);

  if (visibleWidth <= 0 || visibleHeight <= 0) {
    return false;
  }

  return visibleWidth * visibleHeight * 2 >= area;
}

/**
 * Decides how a card opens or closes. A visitor who prefers reduced motion always gets the short fade with a highlighted box and
 * never a view transition; otherwise the pull-out is used unless the browser lacks view transitions, the box is less than half on
 * screen, or the card shows another game than the box it came from, in which case a plain fade takes its place.
 * @param {object} situation What is known at the moment of the tap.
 * @param {boolean} situation.reducedMotion Whether the visitor prefers reduced motion.
 * @param {boolean} situation.hasViewTransition Whether the browser can run a view transition.
 * @param {boolean} situation.boxOnScreen Whether the box to fly is at least half on screen.
 * @param {boolean} [situation.swapped] Whether the card shows another game than its source box.
 * @returns {'reduced' | 'view-transition' | 'fade'}
 */
export function choosePath({ reducedMotion, hasViewTransition, boxOnScreen, swapped = false }) {
  if (reducedMotion) {
    return 'reduced';
  }

  return hasViewTransition && boxOnScreen && !swapped ? 'view-transition' : 'fade';
}

/**
 * Builds the least a card can show from one drawn box: identity, title, kind, colours and picture. It is what the card shows when
 * the card data did not arrive or failed, so the card still opens with its cover, title and link.
 * @param {object} placement One placement from the layout.
 * @returns {object} A card record marked incomplete.
 */
export function recordFromPlacement(placement) {
  const art = placement.art !== null && typeof placement.art === 'object' ? placement.art : undefined;
  const ratio = art !== undefined && art.height > 0 ? art.width / art.height : 1;

  return {
    entryId: placement.entryId,
    gameId: placement.gameId,
    title: placement.title,
    isExpansion: placement.isExpansion === true || EXPANSION_KINDS.includes(placement.kind),
    ratio,
    cover: art,
    colour: placement.colour ?? undefined,
    toneIndex: placement.toneIndex,
    patternIndex: placement.patternIndex,
    incomplete: true,
    designers: [],
    mechanics: [],
    expansions: [],
    bases: [],
  };
}

/**
 * Indexes the boxes of a layout by entry id. The marker that counts hidden expansions is left out because it shares the id of
 * its base game; when an entry is drawn more than once the first box wins.
 * @param {object} layout The layout JSON.
 * @returns {Map<string, object>} The placements, keyed by the entry id as a string.
 */
export function indexPlacements(layout) {
  const index = new Map();

  for (const section of layout.sections ?? []) {
    for (const cubby of section.cubbies ?? []) {
      for (const placement of cubby.placements ?? []) {
        const key = String(placement.entryId);

        if (placement.kind !== 'moreMarker' && !index.has(key)) {
          index.set(key, placement);
        }
      }
    }
  }

  return index;
}

/** How long a re-open waits for the pop of the previous close before it goes on, in milliseconds. */
const REOPEN_WAIT_MS = 150;

/**
 * The one history step a card takes. Opening pushes a step with no address, so Back closes the card; closing in any other way
 * removes that step again with one Back, and the pop that causes is swallowed, so no dead step is left behind and a second Back
 * leaves the page as it would have before.
 * @param {object} parts What the step needs from the page.
 * @param {{ pushState: Function, back: Function, replaceState: Function }} parts.history The browser history.
 * @param {(callback: Function, delay: number) => unknown} parts.setTimer Starts a timer.
 * @param {(timer: unknown) => void} parts.clearTimer Stops a timer.
 * @returns {{ opened: Function, closedHere: Function, popped: Function, beforeOpen: Function, clearStale: Function }} The controls.
 */
export function createHistoryStep({ history, setTimer, clearTimer }) {
  let hasStep = false;
  let ignorePop = false;
  let waiting = [];

  /**
   * Releases every re-open that is waiting for the pending pop.
   */
  function releaseWaiting() {
    const released = waiting;

    waiting = [];

    for (const release of released) {
      release();
    }
  }

  return {
    /**
     * Records that a card opened and pushes its step, unless one already stands.
     */
    opened() {
      if (hasStep) {
        return;
      }

      history.pushState({ card: true }, '');
      hasStep = true;
    },

    /**
     * Records a close that did not come from history and removes the step with one Back, whose pop will be ignored.
     */
    closedHere() {
      if (!hasStep) {
        return;
      }

      hasStep = false;
      ignorePop = true;
      history.back();
    },

    /**
     * Decides what a pop means: the one a close here caused is ignored, any other closes the card and ends the step.
     * @returns {'ignore' | 'close'}
     */
    popped() {
      if (ignorePop) {
        ignorePop = false;
        releaseWaiting();

        return 'ignore';
      }

      hasStep = false;

      return 'close';
    },

    /**
     * Gives a promise that is settled when a new card may push its step: at once when no pop is pending, otherwise on that pop or
     * after a short wait, whichever comes first, so the stack never holds two steps. When the wait runs out the pop is given up
     * on, so a late one closes the card instead of leaving the page.
     * @returns {Promise<void>}
     */
    beforeOpen() {
      if (!ignorePop) {
        return Promise.resolve();
      }

      return new Promise((resolve) => {
        const release = () => {
          clearTimer(timer);
          resolve();
        };
        const timer = setTimer(() => {
          waiting = waiting.filter((entry) => entry !== release);
          ignorePop = false;
          resolve();
        }, REOPEN_WAIT_MS);

        waiting.push(release);
      });
    },

    /**
     * Removes a card step left in the history by a reload or a restore, which no card stands behind.
     * @param {object | null} state The history state found on load.
     */
    clearStale(state) {
      if (state !== null && state !== undefined && state.card === true) {
        history.replaceState(null, '');
      }
    },
  };
}

/**
 * Tracks whether a card is open and lets anyone ask to be told when it has closed and its box is back in its slot. While a card is
 * open every caller gets the one promise of that open period, and closing settles it; a card opened again gets a new promise.
 * @returns {{ opened: Function, closed: Function, whenClosed: Function, isOpen: Function }} The controls.
 */
export function createCloseGate() {
  let open = false;
  let promise = null;
  let release = null;

  return {
    /**
     * Records that a card is opening or open. Calling it again during the same open period changes nothing.
     */
    opened() {
      open = true;
    },

    /**
     * Records that the card is gone and its box is back, and settles everyone who is waiting.
     */
    closed() {
      open = false;

      if (release !== null) {
        release();
      }

      promise = null;
      release = null;
    },

    /**
     * Gives a promise that is settled once the card is closed: already settled when none is open, the same one for every caller
     * during one open period otherwise.
     * @returns {Promise<void>}
     */
    whenClosed() {
      if (!open) {
        return Promise.resolve();
      }

      promise ??= new Promise((resolve) => {
        release = resolve;
      });

      return promise;
    },

    /**
     * Tells whether a card is open or opening.
     * @returns {boolean}
     */
    isOpen() {
      return open;
    },
  };
}

/** The share of the sheet's height a drag must cover to close it. */
export const DRAG_CLOSE_SHARE = 0.25;

/** The downward speed, in pixels per millisecond, that closes the sheet whatever the distance. */
export const DRAG_CLOSE_VELOCITY = 0.6;

/** How much of an upward drag the sheet follows: one part in this many. */
const UPWARD_RESISTANCE = 4;

/**
 * Decides what a released drag of the sheet does: a drag that went far enough down, or that was fast enough at the end, closes the
 * card; anything else lets the sheet spring back. A drag upward never closes it.
 * @param {{ dy: number, height: number, velocity: number }} drag How far the sheet was moved down, how tall it is, and how fast it
 * was moving down at release, in pixels per millisecond.
 * @returns {'close' | 'spring'}
 */
export function dragOutcome({ dy, height, velocity }) {
  if (!(dy >= 0)) {
    return 'spring';
  }

  const farEnough = height > 0 && dy >= DRAG_CLOSE_SHARE * height;

  return farEnough || velocity >= DRAG_CLOSE_VELOCITY ? 'close' : 'spring';
}

/**
 * Gives the offset the sheet follows for a finger offset: downward movement is followed one to one and upward movement only a
 * quarter of the way, so pulling the sheet up feels stiff.
 * @param {number} dy The finger's vertical movement since the press, downward positive.
 * @returns {number}
 */
export function resist(dy) {
  return dy >= 0 ? dy : dy / UPWARD_RESISTANCE;
}
