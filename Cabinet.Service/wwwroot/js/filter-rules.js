/**
 * The rules that decide which games stand in the light while the visitor narrows the cabinet down. Everything here is pure: it works
 * on card records and plain values, touches no page and keeps no state, so the same rules run in the browser and under the plain Node
 * test runner. Titles are only ever compared in a folded form; what a box or the list shows is never altered.
 */
import { orderEntries } from './games-list.js';

/** The state of no filter at all: nothing typed and no group chosen. */
export const EMPTY_FILTERS = Object.freeze({ q: '', players: null, time: null, location: null });

/** Matches one combining mark, which folding removes after the text is decomposed. */
const COMBINING_MARK = /\p{M}/gu;

/** Matches a run of any whitespace. */
const WHITESPACE_RUN = /\s+/g;

/**
 * Folds a text for comparison: decomposes it, drops the combining marks, lower-cases it and collapses every run of whitespace to one
 * space. The result is only used to match; it is never shown.
 * @param {unknown} text The text to fold; anything that is not text folds to the empty string.
 * @returns {string} The folded text, with no leading or trailing space.
 */
export function fold(text) {
  if (typeof text !== 'string') {
    return '';
  }

  return text.normalize('NFD').replace(COMBINING_MARK, '').toLowerCase().replace(WHITESPACE_RUN, ' ').trim();
}

/**
 * Tells whether a filter state narrows anything down: a search with at least one visible character, or a chosen group value.
 * @param {{ q: string, players: unknown, time: unknown, location: unknown }} state The filter state.
 * @returns {boolean}
 */
export function isActive(state) {
  return fold(state.q) !== '' || state.players !== null || state.time !== null || state.location !== null;
}

/**
 * Tells whether a link names an entry of the collection, as opposed to a game the owner does not have.
 * @param {{ entryId?: unknown } | null | undefined} link One expansion or base link of a card record.
 * @param {Map<string, object>} byEntry The records of the collection by entry id.
 * @returns {boolean}
 */
function isOwnedLink(link, byEntry) {
  return link !== null && link !== undefined && link.entryId !== null && link.entryId !== undefined && byEntry.has(String(link.entryId));
}

/**
 * Adds an entry to a family, once, keeping both directions of the lookup in step.
 * @param {Map<string, string[]>} familiesOf The families each entry belongs to.
 * @param {Map<string, string[]>} members The entries each family holds.
 * @param {string} family The family key.
 * @param {string} entry The entry key.
 */
function join(familiesOf, members, family, entry) {
  const families = familiesOf.get(entry) ?? [];
  const entries = members.get(family) ?? [];

  if (!families.includes(family)) {
    families.push(family);
  }

  if (!entries.includes(entry)) {
    entries.push(entry);
  }

  familiesOf.set(entry, families);
  members.set(family, entries);
}

/**
 * Builds what the matcher needs from the card records, or from records made of drawn boxes alone. A base game and every owned
 * expansion it names form one family, keyed by the base game's entry. An expansion that names owned base games belongs to the family
 * of each of them. An expansion with no owned base game is a family of one. A record that carries no links but a family id joins the
 * family of every base record whose game id is that id.
 * @param {object[]} records The records of the collection.
 * @returns {{ records: object[], byEntry: Map<string, object>, familiesOf: Map<string, string[]>, members: Map<string, string[]>, haystacks: Map<string, string[]> }}
 * The records, each record by entry id, the families of every entry, the entries of every family and the folded titles of every family.
 */
export function buildModel(records) {
  const byEntry = new Map(records.map((record) => [String(record.entryId), record]));
  const familiesOf = new Map();
  const members = new Map();

  for (const record of records) {
    const entry = String(record.entryId);

    if (record.isExpansion !== true) {
      join(familiesOf, members, entry, entry);

      for (const link of Array.isArray(record.expansions) ? record.expansions : []) {
        if (isOwnedLink(link, byEntry)) {
          join(familiesOf, members, entry, String(link.entryId));
        }
      }
    }
  }

  for (const record of records) {
    const entry = String(record.entryId);

    if (record.isExpansion === true) {
      for (const link of Array.isArray(record.bases) ? record.bases : []) {
        if (isOwnedLink(link, byEntry)) {
          join(familiesOf, members, String(link.entryId), entry);
        }
      }
    }

    const hasLinks = (Array.isArray(record.expansions) && record.expansions.length > 0) || (Array.isArray(record.bases) && record.bases.length > 0);

    if (!hasLinks && record.familyId !== undefined && record.familyId !== null) {
      for (const base of records) {
        if (base.isExpansion !== true && base.gameId === record.familyId) {
          join(familiesOf, members, String(base.entryId), entry);
        }
      }
    }
  }

  for (const record of records) {
    const entry = String(record.entryId);

    if (!familiesOf.has(entry)) {
      join(familiesOf, members, entry, entry);
    }
  }

  const haystacks = new Map(
    [...members].map(([family, entries]) => [family, entries.map((entry) => fold(byEntry.get(entry)?.title))]),
  );

  return { records, byEntry, familiesOf, members, haystacks };
}

/**
 * Tells whether a family passes the search: every family does while nothing is typed, otherwise one whose base title or any owned
 * expansion title contains the folded text.
 * @param {string} needle The folded search text.
 * @param {string[]} haystack The folded titles of the family.
 * @returns {boolean}
 */
function searchPasses(needle, haystack) {
  return needle === '' || haystack.some((title) => title.includes(needle));
}

/**
 * Decides which entries stand in the light. The search is judged per family, so every box of a family whose titles contain the typed
 * text is lit together; an expansion owned for several base games is lit when any family it belongs to is lit. With no filter active
 * every entry is lit.
 * @param {{ q: string, players: unknown, time: unknown, location: unknown }} state The filter state.
 * @param {ReturnType<typeof buildModel>} model The model of the collection.
 * @returns {{ active: boolean, lit: Set<string> }} Whether anything is filtered and the entry ids, as text, of every lit entry.
 */
export function evaluate(state, model) {
  const active = isActive(state);
  const lit = new Set();

  if (!active) {
    return { active, lit: new Set(model.byEntry.keys()) };
  }

  const needle = fold(state.q);
  const passing = new Set();

  for (const [family, haystack] of model.haystacks) {
    if (searchPasses(needle, haystack)) {
      passing.add(family);
    }
  }

  for (const entry of model.byEntry.keys()) {
    const families = model.familiesOf.get(entry) ?? [entry];

    if (families.some((family) => passing.has(family))) {
      lit.add(entry);
    }
  }

  return { active, lit };
}

/**
 * Lists the top-level entries of the screen-reader list for the collection: base games, standalone games and expansions shown on
 * their own, each copy separately, with an expansion beside its base game folded into that base game's entry.
 * @param {ReturnType<typeof buildModel>} model The model of the collection.
 * @param {{ language: string }} options The page language, for the order of the entries.
 * @returns {{ entryId: string, boxes: string[] }[]} Each entry's own id and the ids of every entry nested under it, all as text.
 */
export function listEntries(model, { language }) {
  return orderEntries(model.records, language).map((node) => ({
    entryId: String(node.entryId),
    boxes: [String(node.entryId), ...node.children.map((child) => String(child.entryId))],
  }));
}

/**
 * Counts the entries of the list that are lit: an entry counts when at least one of its own boxes is lit.
 * @param {{ entryId: string, boxes: string[] }[]} entries The top-level entries.
 * @param {Set<string>} lit The ids of the lit entries, as text.
 * @returns {{ lit: number, total: number }} How many entries are lit and how many there are.
 */
export function countLit(entries, lit) {
  return { lit: entries.filter((entry) => entry.boxes.some((box) => lit.has(box))).length, total: entries.length };
}
