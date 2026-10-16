/**
 * The rules that decide which games stand in the light while the visitor narrows the cabinet down. Everything here is pure: it works
 * on card records and plain values, touches no page and keeps no state, so the same rules run in the browser and under the plain Node
 * test runner. Titles are only ever compared in a folded form; what a box or the list shows is never altered.
 */
import { orderEntries } from './games-list.js';

/** The value the location select holds for boxes that have no location. */
export const UNKNOWN_LOCATION = 'unknown';

/** The state of no filter at all: nothing typed and no group chosen. */
export const EMPTY_FILTERS = Object.freeze({ q: '', players: null, time: null, location: null });

/** The player counts the chips offer; the last one stands for that many or more. */
export const PLAYER_CHOICES = Object.freeze([1, 2, 3, 4, 5, 6, 7, 8]);

/** The play times the chips offer, in minutes; each one means "at most". */
export const TIME_CHOICES = Object.freeze([30, 45, 60, 90, 120]);

/** The player count that stands for "this many or more". */
const OPEN_ENDED_PLAYERS = 8;

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
 * @param {{ id: string, name: string, games: number }[]} [locations] The owner's locations in the owner's order, as the cards document
 * lists them; empty or missing when the owner has none.
 * @returns {{ records: object[], byEntry: Map<string, object>, familiesOf: Map<string, string[]>, members: Map<string, string[]>, haystacks: Map<string, string[]>, locations: object[] }}
 * The records, each record by entry id, the families of every entry, the entries of every family, the folded titles of every family
 * and the locations.
 */
export function buildModel(records, locations = []) {
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

  return { records, byEntry, familiesOf, members, haystacks, locations: Array.isArray(locations) ? locations : [] };
}

/**
 * Adds the other titles of games to the text the search matches, without touching what the model shows. Every family that holds an
 * entry of a game with other titles gains those titles, folded like the rest, so typing one of them lights the whole family. A game
 * the collection does not hold, and anything that is not a list of texts, is ignored. The model given is not changed.
 * @param {ReturnType<typeof buildModel>} model The model of the collection.
 * @param {Record<string, string[]> | null | undefined} names The other titles of each game, keyed by game id as text.
 * @returns {ReturnType<typeof buildModel>} A model whose haystacks also hold the other titles; the model itself when there are none.
 */
export function withAlternates(model, names) {
  if (names === null || typeof names !== 'object') {
    return model;
  }

  const haystacks = new Map();

  for (const [family, haystack] of model.haystacks) {
    const extra = [];

    for (const entry of model.members.get(family) ?? []) {
      const key = String(model.byEntry.get(entry)?.gameId);
      const other = Object.hasOwn(names, key) ? names[key] : null;

      for (const name of Array.isArray(other) ? other : []) {
        const folded = fold(name);

        if (folded !== '') {
          extra.push(folded);
        }
      }
    }

    haystacks.set(family, extra.length === 0 ? haystack : [...haystack, ...extra]);
  }

  return { ...model, haystacks };
}

/**
 * Tells whether a family passes the search: every family does while nothing is typed, otherwise one whose base title, any owned
 * expansion title or any other title of those games contains the folded text.
 * @param {string} needle The folded search text.
 * @param {string[]} haystack The folded titles of the family.
 * @returns {boolean}
 */
function searchPasses(needle, haystack) {
  return needle === '' || haystack.some((title) => title.includes(needle));
}

/**
 * Gives a number back when it is a usable count or length of time, and null for anything else: a value that is missing, not a
 * number, not finite or not above zero is unknown.
 * @param {unknown} value The value of a card record.
 * @returns {number | null}
 */
function known(value) {
  return typeof value === 'number' && Number.isFinite(value) && value > 0 ? value : null;
}

/**
 * Works out how many players a family can seat: from the smallest known minimum to the largest known maximum over the base game and
 * its owned expansions, so a gap or a touching range between members is joined. Values that are missing or not above zero are
 * ignored. A family with no usable minimum, or no usable maximum, has no range.
 * @param {object[]} members The card records of the family.
 * @returns {{ min: number, max: number } | null} The span, or null when it cannot be judged.
 */
export function familyRange(members) {
  const mins = members.map((card) => known(card?.minPlayers)).filter((value) => value !== null);
  const maxes = members.map((card) => known(card?.maxPlayers)).filter((value) => value !== null);

  return mins.length === 0 || maxes.length === 0 ? null : { min: Math.min(...mins), max: Math.max(...maxes) };
}

/**
 * Tells whether a family can seat a chosen number of players. Nothing chosen passes every family; a family with no range never passes
 * a chosen count. The chosen count and both ends are compared as whole numbers as given; the open-ended count passes when the largest
 * maximum reaches it.
 * @param {number | null} players The chosen count, or null for any.
 * @param {{ min: number, max: number } | null} range The range of the family.
 * @returns {boolean}
 */
function playersPass(players, range) {
  if (players === null) {
    return true;
  }

  if (range === null) {
    return false;
  }

  return players >= OPEN_ENDED_PLAYERS ? range.max >= OPEN_ENDED_PLAYERS : range.min <= players && players <= range.max;
}

/**
 * Tells whether a game is done within a chosen number of minutes. Nothing chosen passes every game; a stated playing time that is
 * missing or not above zero never passes a chosen time. The stated playing time is the long end of a range, so a 45 to 90 minute
 * game is judged as 90 minutes.
 * @param {number | null} minutes The chosen limit, or null for any.
 * @param {object | undefined} base The card record of the base game of the family, or of the expansion that is a family of one.
 * @returns {boolean}
 */
function timePasses(minutes, base) {
  if (minutes === null) {
    return true;
  }

  const stated = known(base?.playTime);

  return stated !== null && stated <= minutes;
}

/**
 * Gives the id of the location a record is stored in, or null when it has none.
 * @param {object | undefined} record A card record.
 * @returns {string | null}
 */
function locationOf(record) {
  const id = record?.locationId;

  return typeof id === 'string' && id !== '' ? id : null;
}

/**
 * Lists what the location select offers: the owner's locations that hold at least one game, in the owner's order, and whether
 * the unknown choice is offered, which is the case only while some game has no location and the owner has locations at all.
 * @param {ReturnType<typeof buildModel>} model The model of the collection.
 * @returns {{ places: { id: string, name: string }[], unknown: boolean }}
 */
export function locationChoices(model) {
  const places = model.locations
    .filter((place) => typeof place?.id === 'string' && place.id !== '' && Number(place.games) > 0)
    .map((place) => ({ id: place.id, name: String(place.name ?? '') }));
  const unknown = model.locations.length > 0 && model.records.some((record) => locationOf(record) === null);

  return { places, unknown };
}

/**
 * Narrows a chosen location to what is offered: an id that is not in the list, or the unknown choice while it is not offered, is
 * Any. Ids are compared as text and never used for anything else.
 * @param {unknown} chosen The location in the filter state.
 * @param {ReturnType<typeof buildModel>} model The model of the collection.
 * @returns {string | null} The location to judge by, or null for Any.
 */
export function effectiveLocation(chosen, model) {
  if (typeof chosen !== 'string' || chosen === '') {
    return null;
  }

  const { places, unknown } = locationChoices(model);

  if (chosen === UNKNOWN_LOCATION) {
    return unknown ? chosen : null;
  }

  return places.some((place) => place.id === chosen) ? chosen : null;
}

/**
 * Tells whether a box is stored where the filter asks: any box when no location is chosen, otherwise a box whose own location is
 * the chosen one, or a box with no location when unknown is chosen.
 * @param {string | null} location The location to judge by, or null for Any.
 * @param {object | undefined} record The card record of the box.
 * @returns {boolean}
 */
function locationPasses(location, record) {
  if (location === null) {
    return true;
  }

  const own = locationOf(record);

  return location === UNKNOWN_LOCATION ? own === null : own === location;
}

/**
 * Decides which entries stand in the light. Search, players and time are judged per family and combine with AND, so every box of a
 * family that passes all of them is lit together; an expansion owned for several base games is lit when any family it belongs to is
 * lit. The players test uses the span of the whole family; the time test uses the base game's own playing time, so an expansion never
 * shortens it. The location is the one rule judged per box: a box is lit only when it is itself stored in the chosen location, so an
 * expansion kept apart from its base can be lit while the base is in shadow. A chosen location that is not offered counts as Any.
 * With no filter active every entry is lit.
 * @param {{ q: string, players: unknown, time: unknown, location: unknown }} chosen The filter state.
 * @param {ReturnType<typeof buildModel>} model The model of the collection.
 * @returns {{ active: boolean, lit: Set<string> }} Whether anything is filtered and the entry ids, as text, of every lit entry.
 */
export function evaluate(chosen, model) {
  const location = effectiveLocation(chosen.location, model);
  const state = { ...chosen, location };
  const active = isActive(state);
  const lit = new Set();

  if (!active) {
    return { active, lit: new Set(model.byEntry.keys()) };
  }

  const needle = fold(state.q);
  const players = typeof state.players === 'number' ? state.players : null;
  const minutes = typeof state.time === 'number' ? state.time : null;
  const passing = new Set();

  for (const [family, haystack] of model.haystacks) {
    const range = players === null ? null : familyRange((model.members.get(family) ?? []).map((entry) => model.byEntry.get(entry)));

    if (searchPasses(needle, haystack) && playersPass(players, range) && timePasses(minutes, model.byEntry.get(family))) {
      passing.add(family);
    }
  }

  for (const entry of model.byEntry.keys()) {
    const families = model.familiesOf.get(entry) ?? [entry];

    if (families.some((family) => passing.has(family)) && locationPasses(location, model.byEntry.get(entry))) {
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
