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
  selected: '{n} selected',
  selectedOne: '1 selected',
  dismiss: 'Dismiss',
  moveTitle: 'Move {n} games to',
  moveTitleOne: 'Move 1 game to',
  notPlaced: 'Not placed',
  sameAsBase: 'Same as base game',
  keepSelection: 'Keep selection',
  movedGames: 'Moved {n} games to {name}',
  movedOneGame: 'Moved 1 game to {name}',
  gamesNotPlaced: '{n} games are now not placed',
  oneGameNotPlaced: '1 game is now not placed',
  expansionsFollow: '{n} expansions now follow their base game',
  oneExpansionFollows: '1 expansion now follows its base game',
  and: 'and',
  nothingToChange: 'Nothing to change.',
  saved: 'Saved',
  showing: 'Showing {n} of {m} games',
  noMatchQuery: 'No games match "{query}".',
  noMatch: 'No games match.',
  everyPlaced: 'Every game is placed.',
  unplacedLabel: 'Not placed yet ({n})',
  markedChecked: 'Marked as checked',
  looksRight: 'Looks right',
  checked: 'Checked',
  toCheck: '{n} of {m} to check',
  allChecked: "You've checked all {m} games.",
  oneChecked: "You've checked 1 game.",
  nothingNeedsLook: 'Nothing needs a look right now.',
  worthALookLink: 'Worth a look ({n})',
  allGamesLink: 'All games ({m})',
  wontLoad: "Picture won't load",
  wontLoadNote: "The picture can't be loaded right now. The cabinet shows a generated cover until it can.",
  checkLink: 'Check link',
  checking: 'Checking...',
  yours: 'Yours',
  linkFailed: "Couldn't fetch that picture. The link may be wrong or the picture host may be busy. Check the link and try again.",
  removeLink: 'Remove link',
  linkRemoved: 'Link removed',
  tileNoLinkYet: 'None yet',
  tileFetching: 'Fetching...',
  markFetching: 'Fetching your link...',
  linkExpired: 'This preview expired. Check the link again.',
  previewHeading: 'Preview',
  previewSub: 'How this picture will look in the cabinet.',
  boxFront: 'Box front',
  spine: 'Spine',
  boxFrontOf: 'Box front of {title}',
  spineOf: 'Spine of {title}',
  previewDisclaimer: "The site can't check that this picture belongs to this game.",
  useThis: 'Use this',
  discardPreview: 'Discard preview',
  downloadOwnerData: 'Download owner data',
  downloadData: 'Download data',
  preparing: 'Preparing...',
  checking: 'Checking...',
  checkFailed: "Couldn't check that file. Nothing was changed. Try again.",
  fileTooLarge: 'That file holds more than this site accepts, so nothing was changed.',
  downloadFailed: "Couldn't prepare the download. Try again.",
  downloaded: 'Downloaded',
  downloadedWhen: 'Downloaded {when}',
  neverDownloaded: "You haven't downloaded your owner data yet.",
  changesSince: '{n} changes since your last download',
  oneChangeSince: '1 change since your last download',
  changesNotDownloaded: '{n} changes not downloaded',
  oneChangeNotDownloaded: '1 change not downloaded',
  changesOnlyHere: '{n} changes are only on this server.',
  oneChangeOnlyHere: '1 change is only on this server.',
  atTime: '{day} at {time}',
  today: 'today',
  yesterday: 'yesterday',
});

/** The locale every owner time is written in: English words, day before month and a 24-hour clock. */
const TIME_LOCALE = 'en-NL';

/** The length of a day in milliseconds, for telling whole calendar days apart. */
const MS_PER_DAY = 86_400_000;

/**
 * Writes a size in bytes as millions of bytes with at most one decimal, the way the owner reads a picture's size.
 * @param {number} bytes The size in bytes.
 * @returns {string} For example `5.4`, and `1` for a million bytes.
 */
export function megabytes(bytes) {
  return String(Math.round(bytes / 100_000) / 10);
}

/**
 * Shortens a picture address to its host and its file name, as the pictures page shows a stored link: the part in between is
 * replaced by `/.../`. An address that is not a web address gives an empty text.
 * @param {string} address The whole address.
 * @returns {string} For example `cf.example.org/.../pic1.png`, or just the host when the address has no file name.
 */
export function shortAddress(address) {
  let parsed;

  try {
    parsed = new URL(address);
  } catch {
    return '';
  }

  if (parsed.hostname === '') {
    return '';
  }

  const file = decodeURIComponent(parsed.pathname.split('/').filter(Boolean).pop() ?? '');

  return file === '' ? parsed.hostname : `${parsed.hostname}/.../${file}`;
}

/**
 * Splits a message into its lead sentence and the next step, so the page can set the lead in semibold.
 * @param {string} message The whole message.
 * @returns {{ lead: string, rest: string }} The first sentence, and everything after it; the rest is empty for a one-sentence message.
 */
export function leadAndRest(message) {
  const match = /^(.+?[.!?])\s+(.+)$/s.exec(message);

  return match ? { lead: match[1], rest: match[2] } : { lead: message, rest: '' };
}

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

/**
 * The count line of the selection dock.
 * @param {number} count How many games are ticked.
 * @returns {string} `1 selected` or `{n} selected`.
 */
export function selectedText(count) {
  return count === 1 ? OWNER_TEXT.selectedOne : fill(OWNER_TEXT.selected, { n: count });
}

/**
 * The heading of the dialog that asks where the ticked games go.
 * @param {number} count How many games are ticked.
 * @returns {string} `Move 1 game to` or `Move {n} games to`.
 */
export function moveTitleText(count) {
  return count === 1 ? OWNER_TEXT.moveTitleOne : fill(OWNER_TEXT.moveTitle, { n: count });
}

/**
 * The status after games were moved into a location.
 * @param {number} count How many games were moved.
 * @param {string} name The location's name.
 * @returns {string}
 */
export function movedGamesText(count, name) {
  return count === 1
    ? fill(OWNER_TEXT.movedOneGame, { name })
    : fill(OWNER_TEXT.movedGames, { n: count, name });
}

/**
 * The status after games were taken out of their location: the games that are not placed now, and the expansions that follow
 * their base game again. A part that has no games is left out.
 * @param {number} notPlaced How many games are not placed now.
 * @param {number} following How many expansions follow their base game again.
 * @returns {string}
 */
export function clearedGamesText(notPlaced, following) {
  const parts = [];

  if (notPlaced > 0) {
    parts.push(notPlaced === 1 ? OWNER_TEXT.oneGameNotPlaced : fill(OWNER_TEXT.gamesNotPlaced, { n: notPlaced }));
  }

  if (following > 0) {
    parts.push(following === 1 ? OWNER_TEXT.oneExpansionFollows : fill(OWNER_TEXT.expansionsFollow, { n: following }));
  }

  return parts.join(` ${OWNER_TEXT.and} `);
}

/**
 * The count line of the games list.
 * @param {number} shown How many games are shown.
 * @param {number} total How many games there are.
 * @returns {string} `Showing {n} of {m} games`.
 */
export function showingText(shown, total) {
  return fill(OWNER_TEXT.showing, { n: shown, m: total });
}

/**
 * The line for a search that found nothing. The query is part of the text only; the page writes it with `textContent`.
 * @param {string} query What the owner typed.
 * @returns {string} `No games match "{query}".`
 */
export function noMatchText(query) {
  return fill(OWNER_TEXT.noMatchQuery, { query });
}

/**
 * What the page says about the games it shows now: the count, or why there are none.
 * @param {{ shown: number, total: number, query: string, unplacedOnly: boolean }} state What is shown and which rules are on.
 * @returns {string}
 */
export function resultText({ shown, total, query, unplacedOnly }) {
  if (shown === 0 && query !== '') {
    return noMatchText(query);
  }

  if (shown === 0 && unplacedOnly) {
    return OWNER_TEXT.everyPlaced;
  }

  return showingText(shown, total);
}

/**
 * The label of the switch that shows only the games that are not placed.
 * @param {number} count How many games are not placed.
 * @returns {string} `Not placed yet ({n})`.
 */
export function unplacedLabel(count) {
  return fill(OWNER_TEXT.unplacedLabel, { n: count });
}

/**
 * The counter above the pictures worklist: how many games are left to check, or why there is nothing to count.
 * @param {number} toCheck How many candidates the owner has not dealt with.
 * @param {number} candidates How many games are worth a look, including those the owner dealt with.
 * @returns {string} `{n} of {m} to check`, `You've checked all {m} games.` or `Nothing needs a look right now.`
 */
export function counterText(toCheck, candidates) {
  if (candidates === 0) {
    return OWNER_TEXT.nothingNeedsLook;
  }

  if (toCheck > 0) {
    return fill(OWNER_TEXT.toCheck, { n: toCheck, m: candidates });
  }

  return candidates === 1 ? OWNER_TEXT.oneChecked : fill(OWNER_TEXT.allChecked, { m: candidates });
}

/**
 * The text of the link that shows the games worth a look.
 * @param {number} toCheck How many games are left to check.
 * @returns {string} `Worth a look ({n})`.
 */
export function worthALookLinkText(toCheck) {
  return fill(OWNER_TEXT.worthALookLink, { n: toCheck });
}

/**
 * The text of the link that shows every game.
 * @param {number} total How many games there are.
 * @returns {string} `All games ({m})`.
 */
export function allGamesLinkText(total) {
  return fill(OWNER_TEXT.allGamesLink, { m: total });
}

/**
 * Works out where every game is shown from the location each one was given and the base game each expansion follows. A game
 * that has no location of its own is shown where its base game is shown. A base game has to come before its expansions in the list.
 * @param {Array<{ id: string, own: string, baseId: string | null }>} rows The games in list order.
 * @param {string} [removedId] A location to leave out, as if it were deleted; none by default.
 * @returns {Map<string, string>} The location each game is shown in by game id; an empty string means not placed.
 */
export function effectiveLocationIds(rows, removedId = '') {
  const shown = new Map();

  for (const row of rows) {
    const own = removedId !== '' && row.own === removedId ? '' : row.own;
    const inherited = own === '' && row.baseId !== null ? (shown.get(row.baseId) ?? '') : '';

    shown.set(row.id, own === '' ? inherited : own);
  }

  return shown;
}

/**
 * The reminder sentence of the backup banner on a wide screen and on the Settings page.
 * @param {number} count How many changes were made since the last download.
 * @returns {string} `1 change since your last download` or `{n} changes since your last download`.
 */
export function changesBanner(count) {
  return count === 1 ? OWNER_TEXT.oneChangeSince : fill(OWNER_TEXT.changesSince, { n: count });
}

/**
 * The one-line reminder of the backup banner on a phone.
 * @param {number} count How many changes were made since the last download.
 * @returns {string} `1 change not downloaded` or `{n} changes not downloaded`.
 */
export function compactBanner(count) {
  return count === 1 ? OWNER_TEXT.oneChangeNotDownloaded : fill(OWNER_TEXT.changesNotDownloaded, { n: count });
}

/**
 * The reminder sentence when nothing was ever downloaded.
 * @param {number} count How many changes were made.
 * @returns {string} For example `You haven't downloaded your owner data yet. 3 changes are only on this server.`
 */
export function neverDownloaded(count) {
  const rest = count === 1 ? OWNER_TEXT.oneChangeOnlyHere : fill(OWNER_TEXT.changesOnlyHere, { n: count });

  return `${OWNER_TEXT.neverDownloaded} ${rest}`;
}

/**
 * What the banner says for a state of the backup: the full sentence and the one for a phone, or nothing when there is no change.
 * @param {number} count How many changes were made since the last download.
 * @param {boolean} neverDownloadedYet Whether the owner has never downloaded the data.
 * @returns {{ full: string, compact: string }} Both texts; both empty at zero changes.
 */
export function bannerTexts(count, neverDownloadedYet) {
  if (!(count > 0)) {
    return { full: '', compact: '' };
  }

  return {
    full: neverDownloadedYet ? neverDownloaded(count) : changesBanner(count),
    compact: compactBanner(count),
  };
}

/**
 * Finds the calendar day of a moment in a time zone, as a number that counts days, so two moments can be compared by day.
 * @param {Date} moment The moment.
 * @param {string | undefined} timeZone The zone to read the day in; the viewer's own when left out.
 * @returns {number} Days since 1 January 1970 of that day in the zone.
 */
function dayNumber(moment, timeZone) {
  const parts = new Intl.DateTimeFormat('en-CA', { year: 'numeric', month: '2-digit', day: '2-digit', timeZone }).formatToParts(moment);
  const part = (type) => Number(parts.find((each) => each.type === type)?.value);

  return Math.round(Date.UTC(part('year'), part('month') - 1, part('day')) / MS_PER_DAY);
}

/**
 * Writes a moment the way every owner page does, in the viewer's own clock: `today at 21:14`, `yesterday at 21:14` or
 * `9 October at 21:14`, on a 24-hour clock with English words. At the start of a line the first letter is capitalised.
 * @param {string | Date} when The moment, as the text of a `datetime` attribute or a date.
 * @param {Date} [now] The current moment, replaceable in tests.
 * @param {{ capitalise?: boolean, timeZone?: string }} [options] Whether the text starts a line, and the zone to read the clock
 * and the day in; the viewer's own by default.
 * @returns {string} The text, or an empty text when the moment is not a date.
 */
export function whenText(when, now = new Date(), options = {}) {
  const moment = new Date(when);

  if (Number.isNaN(moment.getTime())) {
    return '';
  }

  const { timeZone } = options;
  const time = new Intl.DateTimeFormat(TIME_LOCALE, { hour: '2-digit', minute: '2-digit', hourCycle: 'h23', timeZone }).format(moment);
  const daysAgo = dayNumber(now, timeZone) - dayNumber(moment, timeZone);
  let day;

  if (daysAgo === 0) {
    day = OWNER_TEXT.today;
  } else if (daysAgo === 1) {
    day = OWNER_TEXT.yesterday;
  } else {
    day = new Intl.DateTimeFormat(TIME_LOCALE, { day: 'numeric', month: 'long', timeZone }).format(moment);
  }

  const text = fill(OWNER_TEXT.atTime, { day, time });

  return options.capitalise ? text.charAt(0).toUpperCase() + text.slice(1) : text;
}
