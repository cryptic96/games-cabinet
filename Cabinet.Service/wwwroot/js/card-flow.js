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
