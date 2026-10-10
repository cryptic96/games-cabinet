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
