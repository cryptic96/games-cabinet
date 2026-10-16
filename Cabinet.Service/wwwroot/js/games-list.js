/**
 * The games list behind the cabinet, for screen readers and keyboard users: every game once, A to Z, each owned expansion nested
 * under its base game, each entry named with the facts that matter on game night and opening the same card as the box. The
 * ordering and naming are pure functions of the card data and the page language; the DOM part only turns them into buttons.
 * Titles are never translated or changed: only the words around them follow the page language.
 */
import { playersText, playTimeText, weightBand } from './format.js';

/** The id the first entry carries, so the skip link has a target even without a script. */
const START_ID = 'games-list-start';

/** The keys the list handles itself; Enter, Space and Tab keep their native meaning. */
const LIST_KEYS = Object.freeze(['ArrowDown', 'ArrowUp', 'Home', 'End']);

/**
 * Tells whether a value is a usable amount: a finite number above zero.
 * @param {unknown} value The value from the card data.
 * @returns {boolean}
 */
function isKnown(value) {
  return typeof value === 'number' && Number.isFinite(value) && value > 0;
}

/**
 * Tells whether a title is missing or blank.
 * @param {unknown} title The title as the card data gives it.
 * @returns {boolean}
 */
function isBlank(title) {
  return typeof title !== 'string' || title.trim() === '';
}

/**
 * Orders two entry ids, which are numbers or numbers written as text.
 * @param {string | number} left The first entry id.
 * @param {string | number} right The second entry id.
 * @returns {number}
 */
function compareIds(left, right) {
  const difference = Number(left) - Number(right);

  return Number.isNaN(difference) ? String(left).localeCompare(String(right)) : difference;
}

/**
 * Builds the comparison that fixes the order of the list: the title by the language's collation with numbers read as numbers and
 * accents and case ignored, then the year ascending with a missing year last (a blank title sorts after every named one), then the entry id ascending.
 * @param {string} language The page language.
 * @returns {(left: object, right: object) => number} The comparison over records or placements.
 */
function entryComparison(language) {
  const collator = new Intl.Collator(language, { numeric: true, sensitivity: 'base' });

  return (left, right) => {
    if (isBlank(left.title) !== isBlank(right.title)) {
      return isBlank(left.title) ? 1 : -1;
    }

    const byTitle = isBlank(left.title) ? 0 : collator.compare(left.title, right.title);

    if (byTitle !== 0) {
      return byTitle;
    }

    const leftYear = isKnown(left.year) ? left.year : Number.POSITIVE_INFINITY;
    const rightYear = isKnown(right.year) ? right.year : Number.POSITIVE_INFINITY;

    if (leftYear !== rightYear) {
      return leftYear < rightYear ? -1 : 1;
    }

    return compareIds(left.entryId, right.entryId);
  };
}

/**
 * Gives the title of the first named game an expansion belongs to, for an expansion drawn without a base game of its own.
 * @param {object} record The card record of the expansion.
 * @returns {string | undefined} The title, or undefined when no base game is named.
 */
function firstNamedBase(record) {
  const bases = Array.isArray(record.bases) ? record.bases : [];

  return bases.length > 0 ? bases[0].title : undefined;
}

/**
 * Orders the card records into the list: base games and expansions without an owned base game at the top level, A to Z; each
 * owned expansion under every owned base game it expands, in the order the base game lists them. An expansion of two owned base
 * games appears under both and is never merged. Titles equal under the collation stay separate entries.
 * @param {object[]} records The card records of the collection.
 * @param {string} language The page language, for the collation.
 * @returns {{ entryId: string | number, record: object, baseTitle: string | undefined, children: object[] }[]} The top-level entries.
 */
export function orderEntries(records, language) {
  const byId = new Map(records.map((record) => [String(record.entryId), record]));
  const compare = entryComparison(language);
  const nested = new Set();
  const childrenOf = new Map();

  for (const record of records) {
    if (record.isExpansion === true) {
      continue;
    }

    const seen = new Set();
    const children = [];

    for (const link of Array.isArray(record.expansions) ? record.expansions : []) {
      const key = String(link.entryId);
      const expansion = byId.get(key);

      if (link.entryId === null || link.entryId === undefined || expansion === undefined || seen.has(key)) {
        continue;
      }

      seen.add(key);
      nested.add(key);
      children.push({ entryId: expansion.entryId, record: expansion, baseTitle: record.title, children: [] });
    }

    childrenOf.set(String(record.entryId), children);
  }

  return records
    .filter((record) => !nested.has(String(record.entryId)))
    .sort(compare)
    .map((record) => ({
      entryId: record.entryId,
      record,
      baseTitle: record.isExpansion === true ? firstNamedBase(record) : undefined,
      children: childrenOf.get(String(record.entryId)) ?? [],
    }));
}

/**
 * Writes the accessible name of a list entry: the title, the year in brackets, then only what helps to pick a game for game night,
 * separated by commas: that it is an expansion and for which game, the players, the play time in full minutes and the weight word.
 * A location phrase, when given, closes the name.
 * A fact that is missing is left out with its comma; a missing year drops the brackets; a blank title reads as the untitled text.
 * @param {object} record The card record, or anything with the same fields.
 * @param {object} copy The visitor-facing strings of the page language.
 * @param {string | undefined | null} baseTitle The base game an expansion is named for, when one is known.
 * @param {string | undefined} [locationPhrase] The words that say where the game is stored, already starting with their comma.
 * @returns {string}
 */
export function entryName(record, copy, baseTitle, locationPhrase) {
  const title = isBlank(record.title) ? copy.untitled : record.title;
  const parts = [isKnown(record.year) ? title + ' (' + record.year + ')' : title];

  if (record.isExpansion === true) {
    const hasBase = baseTitle !== undefined && baseTitle !== null;

    parts.push(hasBase ? copy.listExpansionFor(isBlank(baseTitle) ? copy.untitled : baseTitle) : copy.listExpansion);
  }

  const players = playersText(record.minPlayers, record.maxPlayers);

  if (players !== null) {
    parts.push(copy.listPlayers(players, isKnown(record.maxPlayers) ? record.maxPlayers : record.minPlayers));
  }

  const minutes = playTimeText(record.playTime, record.minPlayTime, record.maxPlayTime);

  if (minutes !== null) {
    parts.push(copy.listMinutes(minutes));
  }

  const band = weightBand(record.weight);

  if (band !== null) {
    parts.push(copy.weightWordsLower[band]);
  }

  return parts.join(', ') + (typeof locationPhrase === 'string' ? locationPhrase : '');
}

/**
 * Names one drawn box for the list before the card data has arrived: the title alone, and for an expansion that says which game
 * it belongs to, the same words the box carries.
 * @param {object} placement One placement from the layout.
 * @param {object} copy The visitor-facing strings of the page language.
 * @returns {string}
 */
function placementName(placement, copy) {
  const title = isBlank(placement.title) ? copy.untitled : placement.title;
  const hasBase = placement.baseTitle !== undefined && placement.baseTitle !== null;

  if (hasBase) {
    return copy.layerName(title, isBlank(placement.baseTitle) ? copy.untitled : placement.baseTitle);
  }

  return placement.isExpansion === true ? copy.expansionName(title) : title;
}

/**
 * Builds the list from the drawn boxes alone, for the moment before the card data arrives or when it never does: one entry for
 * every collection entry, A to Z by title, named by title only, with expansions marked where the box says so. The marker that
 * counts hidden expansions shares the entry of its base game and adds nothing.
 * @param {Iterable<object>} placements The placements of the layout, or a map of them by entry id.
 * @param {object} copy The visitor-facing strings of the page language.
 * @returns {{ entryId: string | number, name: string, children: object[] }[]} The entries, none of them nested.
 */
export function titleOnlyEntries(placements, copy) {
  const source = placements instanceof Map ? [...placements.values()] : [...placements];
  const seen = new Set();
  const unique = [];

  for (const placement of source) {
    const key = String(placement.entryId);

    if (placement.kind === 'moreMarker' || seen.has(key)) {
      continue;
    }

    seen.add(key);
    unique.push(placement);
  }

  return unique
    .sort(entryComparison(copy.numberLocale))
    .map((placement) => ({ entryId: placement.entryId, name: placementName(placement, copy), children: [] }));
}

/**
 * Writes the heading of one location group in the list: the location with how many of its games the list shows. While filtering the
 * count is the matching one; the group of games with no location has its own wording.
 * @param {{ id: string | null, name: string | null, shown: number, total: number }} group The group and its two counts.
 * @param {object} copy The visitor-facing strings of the page language.
 * @param {boolean} filtering Whether a filter is in use.
 * @returns {string}
 */
export function groupHeading(group, copy, filtering) {
  if (group.id === null) {
    return copy.listUnknownGroupHeading(filtering ? group.shown : group.total);
  }

  return copy.listGroupHeading(group.name ?? '', group.shown, group.total, filtering);
}

/**
 * Gives the words that close a list entry's name: where the game is stored, or that the place is unknown. Nothing is said while the
 * owner has no locations at all.
 * @param {object} record The card record.
 * @param {object} copy The visitor-facing strings of the page language.
 * @param {boolean} hasLocations Whether the owner has locations.
 * @returns {string | undefined}
 */
function locationPhrase(record, copy, hasLocations) {
  if (!hasLocations) {
    return undefined;
  }

  return typeof record.location === 'string' && record.location.trim() !== '' ? copy.listStoredIn(record.location) : copy.listUnknownEntry();
}

/**
 * Gives the name a list node shows: the name it already carries, or the one made from its record.
 * @param {object} node One entry of the ordered list.
 * @param {object} copy The visitor-facing strings of the page language.
 * @returns {string}
 */
function nameOf(node, copy) {
  return node.name ?? entryName(node.record, copy, node.baseTitle);
}

/**
 * Describes the shape of a list in one line, so a redraw with the same entries in the same order only has to change the text.
 * @param {object[]} nodes The entries of the list.
 * @returns {string}
 */
function shapeOf(nodes) {
  return nodes.map((node) => String(node.entryId) + '(' + node.children.map((child) => String(child.entryId)).join(',') + ')').join('|');
}

/**
 * Tells whether an entry of the filter event stands in the light: always while nothing is filtered, otherwise when one of its boxes is lit.
 * @param {{ boxes: string[] }} entry The entry from the event.
 * @param {{ active: boolean, lit: Set<string> }} detail The event detail.
 * @returns {boolean}
 */
function isLit(entry, detail) {
  return !detail.active || entry.boxes.some((box) => detail.lit.has(box));
}

/**
 * Builds the list in the page and wires its keys. The entries are buttons that are not tab stops: the skip link or a screen
 * reader's own cursor enters the list, the arrow keys, Home and End move inside it and Tab leaves it for the cabinet. After every
 * filter pass the list shows exactly the entries the count counts: all of them in one A to Z list, or in the by-location view one
 * group under its own heading for every location with a match.
 * @param {object} parts What the list needs from the page.
 * @param {HTMLElement} parts.section The list section: its heading and its list.
 * @param {object} parts.copy The visitor-facing strings of the page language.
 * @param {string} parts.language The page language.
 * @param {(entryId: string, from: { opener: HTMLElement }) => unknown} parts.openCard Opens the card of an entry.
 * @returns {{ showTitles: Function, showCards: Function, focusStart: Function }} The controls.
 */
export function initGamesList({ section, copy, language, openCard }) {
  const heading = section.querySelector('.games-list-heading');
  const list = section.querySelector('.games-list-items');
  const owner = section.ownerDocument ?? document;
  const groupsHost = document.createElement('div');
  const plainHeading = heading.textContent;
  let shape = '';
  let flat = [];
  let kind = 'none';
  let recordList = [];
  let recordsById = new Map();
  let placementsById = new Map();
  let detail = null;

  groupsHost.className = 'games-list-groups';
  groupsHost.hidden = true;
  section.append(groupsHost);

  /**
   * Makes one entry button.
   * @returns {HTMLButtonElement}
   */
  function createButton() {
    const button = document.createElement('button');

    button.setAttribute('type', 'button');
    button.setAttribute('tabindex', '-1');
    button.setAttribute('aria-haspopup', 'dialog');
    button.addEventListener('click', () => openCard(button.dataset.entryId, { opener: button }));

    return button;
  }

  /**
   * Gives the buttons of the list in document order, nested ones included.
   * @returns {HTMLElement[]}
   */
  function buttons() {
    return Array.from(section.querySelectorAll('button'));
  }

  /**
   * Draws a plan. Buttons are reused by entry id, so a card that was opened from the list still finds its entry afterwards, and
   * keyboard focus that was inside the list stays on the same entry.
   * @param {{ heading: string, grouped: boolean, groups: { key: string, title: string | null, nodes: object[] }[] }} plan What to draw.
   */
  function render(plan) {
    heading.textContent = plan.heading;

    const next = plan.groups.flatMap((group) => group.nodes.flatMap((node) => [node, ...node.children]));
    const nextShape = (plan.grouped ? 'g:' : 'l:') + plan.groups.map((group) => group.key + '[' + shapeOf(group.nodes) + ']').join('|');

    list.hidden = plan.grouped;
    groupsHost.hidden = !plan.grouped;

    if (nextShape === shape && next.length === flat.length) {
      const current = buttons();
      const titles = Array.from(groupsHost.querySelectorAll('h3'));

      next.forEach((node, index) => {
        current[index].textContent = nameOf(node, copy);
      });
      plan.groups.filter((group) => group.title !== null).forEach((group, index) => {
        titles[index].textContent = group.title;
      });

      return;
    }

    const existing = buttons();
    const held = existing.find((button) => button === document.activeElement);
    const heldId = held?.dataset.entryId;
    const pool = new Map();

    for (const button of existing) {
      const key = button.dataset.entryId;

      pool.set(key, [...(pool.get(key) ?? []), button]);
    }

    const take = (entryId) => {
      const free = pool.get(String(entryId)) ?? [];
      const button = free.shift() ?? createButton();

      button.dataset.entryId = String(entryId);

      return button;
    };

    /**
     * Makes the items of one list of entries, expansions nested under their base game.
     * @param {object[]} nodes The entries.
     * @returns {HTMLElement[]}
     */
    const itemsOf = (nodes) => nodes.map((node) => {
      const item = document.createElement('li');
      const button = take(node.entryId);

      button.textContent = nameOf(node, copy);
      item.append(button);

      if (node.children.length > 0) {
        const nestedList = document.createElement('ul');

        nestedList.setAttribute('role', 'list');
        nestedList.append(...node.children.map((child) => {
          const childItem = document.createElement('li');
          const childButton = take(child.entryId);

          childButton.textContent = nameOf(child, copy);
          childItem.append(childButton);

          return childItem;
        }));
        item.append(nestedList);
      }

      return item;
    });

    if (plan.grouped) {
      list.replaceChildren();
      groupsHost.replaceChildren(...plan.groups.flatMap((group) => {
        const title = document.createElement('h3');
        const groupList = document.createElement('ul');

        title.className = 'games-list-heading';
        title.setAttribute('dir', 'auto');
        title.textContent = group.title;
        groupList.setAttribute('role', 'list');
        groupList.append(...itemsOf(group.nodes));

        return [title, groupList];
      }));
    } else {
      groupsHost.replaceChildren();
      list.replaceChildren(...itemsOf(plan.groups[0]?.nodes ?? []));
    }

    shape = nextShape;
    flat = next;

    const all = buttons();

    all.forEach((button, index) => {
      if (index === 0) {
        button.setAttribute('id', START_ID);
      } else {
        button.removeAttribute('id');
      }
    });

    if (heldId !== undefined) {
      const same = all.includes(held) ? held : all.find((button) => button.dataset.entryId === heldId);

      same?.focus({ preventScroll: true });
    }
  }

  /**
   * Makes the node of one entry of the filter event, named from the card data or from the drawn box, or nothing when the entry is
   * not known to the list.
   * @param {{ entryId: string, boxes: string[] }} entry The entry from the event.
   * @returns {object | null}
   */
  function nodeOfEntry(entry) {
    if (kind === 'titles') {
      const placement = placementsById.get(String(entry.entryId));

      return placement === undefined ? null : { entryId: entry.entryId, name: placementName(placement, copy), children: [] };
    }

    const own = recordsById.get(String(entry.entryId));

    if (own === undefined) {
      return null;
    }

    const places = detail.hasLocations === true;
    const children = entry.boxes.slice(1)
      .map((box) => recordsById.get(String(box)))
      .filter((child) => child !== undefined)
      .map((child) => ({ entryId: child.entryId, name: entryName(child, copy, own.title, locationPhrase(child, copy, places)), children: [] }));

    return {
      entryId: own.entryId,
      name: entryName(own, copy, own.isExpansion === true ? firstNamedBase(own) : undefined, locationPhrase(own, copy, places)),
      children,
    };
  }

  /**
   * Plans the list from the latest filter event: only the lit entries, under the count the bar shows, in one list or in one group
   * for every location with a match.
   * @returns {{ heading: string, grouped: boolean, groups: { key: string, title: string | null, nodes: object[] }[] }}
   */
  function planFromEvent() {
    const filtering = detail.active === true;
    const text = !filtering ? plainHeading : detail.count.lit === 0 ? copy.noMatch : copy.filterCount(detail.count.lit, detail.count.total);
    const nodesOf = (entries) => entries.filter((entry) => isLit(entry, detail)).map(nodeOfEntry).filter((node) => node !== null);
    const byLocation = detail.view === 'location' && detail.entries !== null && !Array.isArray(detail.entries) && Array.isArray(detail.entries.groups);

    if (!byLocation) {
      return { heading: text, grouped: false, groups: [{ key: '', title: null, nodes: nodesOf(Array.isArray(detail.entries) ? detail.entries : []) }] };
    }

    const groups = detail.entries.groups
      .map((group) => ({ group, nodes: nodesOf(group.entries) }))
      .filter(({ group, nodes }) => group.entries.length > 0 && (!filtering || nodes.length > 0))
      .map(({ group, nodes }) => ({
        key: String(group.id),
        title: groupHeading({ id: group.id, name: group.name, shown: nodes.length, total: group.entries.length }, copy, filtering),
        nodes,
      }));

    return { heading: text, grouped: groups.length > 0, groups };
  }

  /**
   * Plans the list before any filter event has arrived: every entry once, as the card data or the drawn boxes give them.
   * @returns {{ heading: string, grouped: boolean, groups: { key: string, title: string | null, nodes: object[] }[] }}
   */
  function planPlain() {
    const nodes = kind === 'titles' ? titleOnlyEntries(placementsById, copy) : orderEntries(recordList, language);

    return { heading: plainHeading, grouped: false, groups: [{ key: '', title: null, nodes }] };
  }

  /**
   * Draws the list from what is held: the latest filter event when there is one, otherwise the plain list.
   */
  function refresh() {
    if (kind === 'none') {
      return;
    }

    render(detail === null ? planPlain() : planFromEvent());
  }

  owner.addEventListener('cabinet:filtered', (event) => {
    detail = event.detail ?? null;
    refresh();
  });

  section.addEventListener('keydown', (event) => {
    if (event.altKey || event.ctrlKey || event.metaKey || event.shiftKey || !LIST_KEYS.includes(event.key)) {
      return;
    }

    const all = buttons();
    const from = all.indexOf(event.target.closest('button'));

    if (all.length === 0 || (from === -1 && (event.key === 'ArrowDown' || event.key === 'ArrowUp'))) {
      return;
    }

    const target = {
      ArrowDown: Math.min(from + 1, all.length - 1),
      ArrowUp: Math.max(from - 1, 0),
      Home: 0,
      End: all.length - 1,
    }[event.key];

    event.preventDefault();
    all[target].focus();
  });

  return {
    /**
     * Shows every game by title only, from the boxes the cabinet drew.
     * @param {Iterable<object> | Map<string, object>} placements The placements of the layout.
     */
    showTitles(placements) {
      const source = placements instanceof Map ? [...placements.values()] : [...placements];

      kind = 'titles';
      placementsById = new Map();

      for (const placement of source) {
        if (placement.kind !== 'moreMarker' && !placementsById.has(String(placement.entryId))) {
          placementsById.set(String(placement.entryId), placement);
        }
      }

      refresh();
    },

    /**
     * Shows the full list from the card data: A to Z, expansions nested, each entry named with its facts.
     * @param {object[]} records The card records of the collection.
     */
    showCards(records) {
      kind = 'cards';
      recordList = records;
      recordsById = new Map(records.map((record) => [String(record.entryId), record]));
      refresh();
    },

    /**
     * Moves focus to the first entry, or to the heading when there are none, which is where the skip link leads.
     */
    focusStart() {
      const first = buttons()[0];

      (first ?? heading).focus();
    },
  };
}
