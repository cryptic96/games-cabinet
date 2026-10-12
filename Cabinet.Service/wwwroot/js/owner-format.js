/**
 * The English words the owner pages' scripts show, and the small pure helpers that put names and numbers into them. The owner
 * tools are written in English only, so there is one frozen table instead of a language switch. Every value is plain text with
 * plain dots; a name or a number goes in through the helpers below, and the result is always written with `textContent`.
 */

/** The strings the location list shows, with `{name}`, `{n}`, `{k}` and `{direction}` where a value goes. */
export const OWNER_TEXT = Object.freeze({
  undo: 'Undo',
  undone: 'Undone.',
  reload: 'Reload',
  keepIt: 'Keep it',
  deleteLocation: 'Delete location',
  added: 'Added "{name}"',
  renamed: 'Renamed to "{name}"',
  moved: 'Moved "{name}" {direction}. Now {k} of {n}.',
  deleted: 'Deleted "{name}"',
  deletedWithGames: 'Deleted "{name}". {n} games are now not placed.',
  deletedWithOneGame: 'Deleted "{name}". 1 game is now not placed.',
  dialogTitle: 'Delete "{name}"?',
  dialogLead: '{n} games will become not placed.',
  dialogRest: "They aren't moved anywhere else.",
  dialogLeadOne: '1 game will become not placed.',
  dialogRestOne: "It isn't moved anywhere else.",
  noGames: 'No games',
  oneGame: '1 game',
  manyGames: '{n} games',
  up: 'Up',
  down: 'Down',
  moveLabel: 'Move {name} {direction}',
  deleteLabel: 'Delete {name}',
  nameLabel: 'Name of location {position}',
  heading: 'Your locations ({n})',
  headingEmpty: 'Your locations',
  noLocations: 'No locations yet. Add one to start placing games.',
  nameEmpty: 'Give the location a name.',
  nameTooLong: 'Use 60 characters or fewer.',
  nameTaken: 'You already have a location called "{name}".',
  tooManyLocations: 'You can have up to 50 locations.',
  nameCharacters: 'Use letters, numbers and ordinary punctuation only.',
  outOfDate: 'This page is out of date. Reload it and try again.',
  expired: 'This page has expired. Reload it and try again.',
  failed: "Couldn't save that. Nothing changed. Try again.",
  cantUndo: "Can't undo because something changed after this.",
  unreadable: "Your owner data couldn't be read, so nothing can be saved until it is restored.",
});

/**
 * Puts values into a template of the table: every `{key}` is replaced by the value of that key.
 * @param {string} template The text with `{key}` markers.
 * @param {Record<string, string | number>} [values] The values by key.
 * @returns {string}
 */
export function fill(template, values = {}) {
  return template.replace(/\{(\w+)\}/g, (marker, key) => (key in values ? String(values[key]) : marker));
}

/**
 * Says how many games a location holds.
 * @param {number} count The number of games.
 * @returns {string} `No games`, `1 game` or `{n} games`.
 */
export function gameCount(count) {
  if (count === 0) {
    return OWNER_TEXT.noGames;
  }

  return count === 1 ? OWNER_TEXT.oneGame : fill(OWNER_TEXT.manyGames, { n: count });
}

/**
 * The status that follows a move up or down the list.
 * @param {string} name The location's name.
 * @param {'up' | 'down'} direction Which way it moved.
 * @param {number} position Where it is now, counting from one.
 * @param {number} total How many locations there are.
 * @returns {string}
 */
export function movedText(name, direction, position, total) {
  return fill(OWNER_TEXT.moved, { name, direction, k: position, n: total });
}

/**
 * The two sentences of the delete dialog, the first of which is the lead the warning line sets in semibold.
 * @param {number} games How many games would become not placed.
 * @returns {{ lead: string, rest: string }}
 */
export function deleteDialogParts(games) {
  if (games === 1) {
    return { lead: OWNER_TEXT.dialogLeadOne, rest: OWNER_TEXT.dialogRestOne };
  }

  return { lead: fill(OWNER_TEXT.dialogLead, { n: games }), rest: OWNER_TEXT.dialogRest };
}

/**
 * The body of the delete dialog as one text.
 * @param {number} games How many games would become not placed.
 * @returns {string}
 */
export function deleteDialogBody(games) {
  const { lead, rest } = deleteDialogParts(games);

  return lead + ' ' + rest;
}

/**
 * The status after a location was deleted.
 * @param {string} name The deleted location's name.
 * @param {number} games How many games became not placed.
 * @returns {string}
 */
export function deletedText(name, games) {
  if (games === 0) {
    return fill(OWNER_TEXT.deleted, { name });
  }

  return games === 1
    ? fill(OWNER_TEXT.deletedWithOneGame, { name })
    : fill(OWNER_TEXT.deletedWithGames, { name, n: games });
}

/**
 * The words for a refusal the server sent with a status key, or null for a key the list has no words for.
 * @param {string | undefined} status The status key of the answer.
 * @param {string} name The name the owner entered, for the duplicate message.
 * @returns {string | null}
 */
export function refusalText(status, name) {
  switch (status) {
    case 'name-empty':
      return OWNER_TEXT.nameEmpty;
    case 'name-too-long':
      return OWNER_TEXT.nameTooLong;
    case 'name-taken':
      return fill(OWNER_TEXT.nameTaken, { name });
    case 'too-many-locations':
      return OWNER_TEXT.tooManyLocations;
    case 'name-characters':
      return OWNER_TEXT.nameCharacters;
    default:
      return null;
  }
}

/**
 * The heading of the locations panel.
 * @param {number} count How many locations there are.
 * @returns {string}
 */
export function headingText(count) {
  return count === 0 ? OWNER_TEXT.headingEmpty : fill(OWNER_TEXT.heading, { n: count });
}
