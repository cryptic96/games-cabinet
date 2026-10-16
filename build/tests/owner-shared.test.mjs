/**
 * Checks the helpers every owner page script shares, with no browser: the post helper against an injected fetch (what it sends and
 * how each kind of answer is mapped), the page revision, the token reader, and the row status with its Undo button against a tiny
 * stand-in for the elements it touches. The script is read from disk and imported through a data URL, with its one import pointed
 * at its own data URL.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

/**
 * Imports a script from the page's script folder through a data URL.
 * @param {string} name The file name inside the script folder.
 * @returns {Promise<object>} The module's exports.
 */
async function loadPageScript(name) {
  const read = (file) => readFileSync(new URL('../../Cabinet.Service/wwwroot/js/' + file, import.meta.url), 'utf8');
  const format = 'data:text/javascript;base64,' + Buffer.from(read('owner-format.js')).toString('base64');
  const source = read(name).replace("'./owner-format.js'", `'${format}'`);

  return import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));
}

const {
  readToken,
  postOwner,
  getRevision,
  setRevision,
  showRowStatus,
  showRowProblem,
  reloadButton,
  showProblem,
  checkedValue,
  setChecked,
  dismissOnBackdrop,
  HEX_COLOUR,
  announce,
  saveSettledValue,
  foldForSearch,
  createFinder,
  sortByCollator,
  setPickerValue,
} = await loadPageScript('owner-shared.js');

/**
 * Makes a fetch that remembers what it was asked and answers with a fixed status and body.
 * @param {number} status The HTTP status.
 * @param {string} body The body text.
 * @returns {{ fetch: Function, calls: object[] }}
 */
function answering(status, body) {
  const calls = [];

  return {
    calls,
    fetch: async (url, init) => {
      calls.push({ url, init });

      return { status, text: async () => body };
    },
  };
}

/**
 * Makes a stand-in for an element with just what the status helpers use.
 * @param {string} tag The tag name.
 * @param {object} doc The document that made it.
 * @returns {object}
 */
function element(tag, doc) {
  const node = {
    tagName: tag,
    ownerDocument: doc,
    children: [],
    dataset: {},
    textContent: '',
    className: '',
    type: '',
    listeners: {},
    append(...nodes) {
      for (const child of nodes) {
        child.parent = node;
        node.children.push(child);
      }
    },
    replaceChildren() {
      node.children = [];
    },
    attributes: {},
    getAttribute(name) {
      return name in node.attributes ? node.attributes[name] : null;
    },
    setAttribute(name, value) {
      node.attributes[name] = value;
    },
    removeAttribute(name) {
      if (name === 'data-problem') {
        delete node.dataset.problem;
      }
    },
    addEventListener(type, handler) {
      node.listeners[type] = handler;
    },
    querySelector(selector) {
      return selector === 'button[data-undo]' ? (node.children.find((child) => 'undo' in child.dataset) ?? null) : null;
    },
    remove() {
      if (node.parent) {
        node.parent.children = node.parent.children.filter((child) => child !== node);
      }
    },
  };

  return node;
}

/**
 * Makes a stand-in for a document that can make elements and holds no notice and no server-written form.
 * @returns {object}
 */
function documentStub() {
  const doc = {
    createElement: (tag) => element(tag, doc),
    querySelector: () => null,
    querySelectorAll: () => [],
    getElementById: () => null,
  };

  return doc;
}

test('a post carries the fields, the revision, the token header and a request for JSON', async () => {
  const { fetch, calls } = answering(200, '{"outcome":"saved","revision":4,"status":"location-added","canUndo":true}');

  await postOwner('?handler=AddLocation', { name: 'Cabinet A' }, { fetch, token: 'token-1', revision: 3 });

  assert.equal(calls.length, 1);
  assert.equal(calls[0].url, '?handler=AddLocation');
  assert.equal(calls[0].init.method, 'POST');
  assert.equal(calls[0].init.headers.RequestVerificationToken, 'token-1');
  assert.equal(calls[0].init.headers.Accept, 'application/json');
  assert.equal(calls[0].init.body.get('name'), 'Cabinet A');
  assert.equal(calls[0].init.body.get('revision'), '3');
});

test('a list field is sent as one field per item', async () => {
  const { fetch, calls } = answering(200, '{"outcome":"saved","revision":0}');

  await postOwner('?handler=BulkMove', { gameIds: [4, 7, 9], target: 'not-placed' }, { fetch, token: 't', revision: 2 });

  assert.deepEqual(calls[0].init.body.getAll('gameIds'), ['4', '7', '9']);
  assert.equal(calls[0].init.body.get('target'), 'not-placed');
});

test('a post without a known revision sends none', async () => {
  const { fetch, calls } = answering(200, '{"outcome":"saved","revision":0}');
  setRevision(null);

  await postOwner('?handler=Undo', {}, { fetch, token: 'token-1' });

  assert.equal(calls[0].init.body.has('revision'), false);
});

test('a 200 gives the server answer and the page revision follows it', async () => {
  const { fetch } = answering(200, '{"outcome":"saved","revision":7,"status":"location-added","canUndo":true,"id":"abc"}');

  const result = await postOwner('?handler=AddLocation', { name: 'A' }, { fetch, token: 't', revision: 6 });

  assert.equal(result.outcome, 'saved');
  assert.equal(result.status, 'location-added');
  assert.equal(result.canUndo, true);
  assert.equal(result.id, 'abc');
  assert.equal(getRevision(), 7);
});

test('a 200 that changed nothing is passed on as it is', async () => {
  const { fetch } = answering(200, '{"outcome":"noChange","revision":7,"status":"unchanged","canUndo":false}');

  const result = await postOwner('?handler=Rename', {}, { fetch, token: 't', revision: 7 });

  assert.equal(result.outcome, 'noChange');
});

test('a 409 is out of date and leaves the page revision alone', async () => {
  const { fetch } = answering(409, '{"outcome":"outOfDate","revision":9,"status":"out-of-date","canUndo":false}');
  setRevision(5);

  const result = await postOwner('?handler=Rename', {}, { fetch, token: 't', revision: 5 });

  assert.equal(result.outcome, 'outOfDate');
  assert.equal(result.status, 'out-of-date');
  assert.equal(getRevision(), 5);
});

test('a 409 for data that cannot be read is unreadable', async () => {
  const { fetch } = answering(409, '{"outcome":"unreadable","revision":0,"status":"unreadable","canUndo":false}');

  assert.equal((await postOwner('?handler=Rename', {}, { fetch, token: 't', revision: 1 })).outcome, 'unreadable');
});

test('a 409 for an undo that is no longer possible keeps its status', async () => {
  const { fetch } = answering(409, '{"outcome":"outOfDate","revision":9,"status":"cant-undo","canUndo":false}');

  const result = await postOwner('?handler=Undo', {}, { fetch, token: 't', revision: 5 });

  assert.equal(result.outcome, 'outOfDate');
  assert.equal(result.status, 'cant-undo');
});

test('a 422 is a refusal with the server words and status', async () => {
  const { fetch } = answering(422, '{"outcome":"refused","revision":5,"status":"name-taken","message":"You already have a location with that name."}');

  const result = await postOwner('?handler=AddLocation', { name: 'x' }, { fetch, token: 't', revision: 5 });

  assert.equal(result.outcome, 'refused');
  assert.equal(result.status, 'name-taken');
  assert.equal(result.message, 'You already have a location with that name.');
});

test('a 400 without a body is an expired page', async () => {
  const { fetch } = answering(400, '');

  assert.equal((await postOwner('?handler=Rename', {}, { fetch, token: 't', revision: 1 })).outcome, 'expired');
});

test('any other answer is a failed save', async () => {
  for (const status of [500, 413, 403, 404]) {
    const { fetch } = answering(status, '');

    assert.equal((await postOwner('?handler=Rename', {}, { fetch, token: 't', revision: 1 })).outcome, 'failed', String(status));
  }

  const { fetch } = answering(200, 'not json');

  assert.equal((await postOwner('?handler=Rename', {}, { fetch, token: 't', revision: 1 })).outcome, 'failed');
});

test('a fetch that rejects is a failed save', async () => {
  const fetch = async () => {
    throw new TypeError('Failed to fetch');
  };

  assert.equal((await postOwner('?handler=Rename', {}, { fetch, token: 't', revision: 1 })).outcome, 'failed');
});

test('the token is read from the layout meta element, or is empty', () => {
  const doc = { querySelector: (selector) => (selector.includes('request-verification-token') ? { getAttribute: () => 'abc' } : null) };

  assert.equal(readToken(doc), 'abc');
  assert.equal(readToken({ querySelector: () => null }), '');
});

test('setting the revision writes it into the forms that carry the page revision', () => {
  const inputs = [{ value: '1' }, { value: '1' }];
  const doc = { querySelectorAll: (selector) => (selector === 'input[name="revision"][data-page-revision]' ? inputs : []) };

  setRevision(12, doc);

  assert.deepEqual(inputs.map((input) => input.value), ['12', '12']);
  assert.equal(getRevision(), 12);
});

test('a row status writes its text as text and offers Undo only when asked', () => {
  const doc = documentStub();
  const region = element('div', doc);
  let undone = 0;

  showRowStatus(region, 'Renamed to "<b>x</b>"', { onUndo: () => undone++, undoLabel: 'Undo' });

  assert.equal(region.children[0].textContent, 'Renamed to "<b>x</b>"');
  assert.equal(region.children[1].textContent, 'Undo');
  region.children[1].listeners.click();
  assert.equal(undone, 1);

  showRowStatus(region, 'Moved', {});

  assert.equal(region.children.length, 1);
});

test('the Undo button hands its action the revision of the change it was shown for, not the latest one', () => {
  const doc = documentStub();
  const region = element('div', doc);
  const received = [];

  setRevision(4, doc);
  showRowStatus(region, 'Moved', { onUndo: (saved) => received.push(saved), undoLabel: 'Undo' });
  setRevision(5, doc);
  region.children[1].listeners.click();

  assert.deepEqual(received, [4]);
});

test('an Undo that was sent with a revision reaches the server with that revision', async () => {
  const { fetch, calls } = answering(409, '{"outcome":"outOfDate","status":"cant-undo"}');

  setRevision(7, documentStub());
  await postOwner('?handler=Undo', {}, { fetch, token: 'token-1', revision: 4 });

  assert.equal(calls[0].init.body.get('revision'), '4');
});

test('pressing Undo twice runs the action once', () => {
  const doc = documentStub();
  const region = element('div', doc);
  let undone = 0;

  showRowStatus(region, 'Moved', { onUndo: () => undone++, undoLabel: 'Undo' });
  region.children[1].listeners.click();
  region.children[1].listeners.click();

  assert.equal(undone, 1);
  assert.equal(region.children[1].attributes['aria-disabled'], 'true');
});

test('a new change takes the Undo button away from the row that had it', () => {
  const doc = documentStub();
  const first = element('div', doc);
  const second = element('div', doc);

  showRowStatus(first, 'Added "A"', { onUndo: () => {}, undoLabel: 'Undo' });
  showRowStatus(second, 'Added "B"', { onUndo: () => {}, undoLabel: 'Undo' });

  assert.equal(first.children.length, 1);
  assert.equal(first.children[0].textContent, 'Added "A"');
  assert.equal(second.children.length, 2);
});

test('a problem does not take Undo away from another row and a reload button reloads', () => {
  const doc = documentStub();
  const row = element('div', doc);
  const other = element('div', doc);
  let reloaded = 0;

  showRowStatus(row, 'Added "A"', { onUndo: () => {}, undoLabel: 'Undo' });
  showRowProblem(other, "Couldn't save that. Nothing changed. Try again.");

  assert.equal(row.children.length, 2);
  assert.equal(other.dataset.problem, 'true');

  reloadButton(other, 'This page is out of date. Reload it and try again.', 'Reload', () => reloaded++);

  assert.equal(other.children[1].textContent, 'Reload');
  other.children[1].listeners.click();
  assert.equal(reloaded, 1);
});

test('a problem is told in the words of its kind and an out-of-date page offers Reload', () => {
  const doc = documentStub();
  const region = element('div', doc);

  showProblem(region, { outcome: 'outOfDate' });
  assert.equal(region.children[0].textContent, 'This page is out of date. Reload it and try again.');
  assert.equal(region.children[1].textContent, 'Reload');

  showProblem(region, { outcome: 'expired' });
  assert.equal(region.children[0].textContent, 'This page has expired. Reload it and try again.');

  showProblem(region, { outcome: 'unreadable' });
  assert.equal(region.children[0].textContent.startsWith("Your owner data couldn't be read"), true);
  assert.equal(region.children.length, 1);

  showProblem(region, { outcome: 'failed' });
  assert.equal(region.children[0].textContent, "Couldn't save that. Nothing changed. Try again.");
});

/**
 * Makes a stand-in for a group of radios.
 * @param {string[]} values The values of the radios.
 * @param {string} checkedValueOfGroup The value that starts checked.
 * @returns {object}
 */
function radioGroup(values, checkedValueOfGroup) {
  const radios = values.map((value) => ({ value, checked: value === checkedValueOfGroup }));

  return {
    radios,
    querySelector: () => radios.find((radio) => radio.checked) ?? null,
    querySelectorAll: () => radios,
  };
}

test('the checked radio of a group is read and another one can be checked', () => {
  const group = radioGroup(['a', 'b', 'c'], 'b');

  assert.equal(checkedValue(group), 'b');

  setChecked(group, 'c');

  assert.equal(checkedValue(group), 'c');
  assert.deepEqual(group.radios.map((radio) => radio.checked), [false, false, true]);
  assert.equal(checkedValue(radioGroup(['a'], 'none')), '');
});

test('a press on the backdrop of a dialog closes it and a press inside does not', () => {
  const handlers = {};
  let closed = 0;
  const dialog = {
    addEventListener: (type, handler) => {
      handlers[type] = handler;
    },
    getBoundingClientRect: () => ({ left: 10, right: 110, top: 20, bottom: 220 }),
    close: () => {
      closed++;
    },
  };

  dismissOnBackdrop(dialog);
  handlers.click({ target: {}, clientX: 0, clientY: 0 });
  handlers.click({ target: dialog, clientX: 50, clientY: 100 });
  assert.equal(closed, 0);

  handlers.click({ target: dialog, clientX: 5, clientY: 100 });
  handlers.click({ target: dialog, clientX: 50, clientY: 230 });
  assert.equal(closed, 2);
});

test('only a six-digit hexadecimal colour passes the colour check', () => {
  assert.equal(HEX_COLOUR.test('#8C5d38'), true);
  assert.equal(HEX_COLOUR.test('#8c5d3'), false);
  assert.equal(HEX_COLOUR.test('red'), false);
  assert.equal(HEX_COLOUR.test('#8c5d38; x'), false);
});

test('the announcer takes the text and does nothing without one', () => {
  const region = { textContent: '' };

  announce('Moved "A" up. Now 1 of 2.', { getElementById: (id) => (id === 'owner-status' ? region : null) });
  announce('Ignored', { getElementById: () => null });

  assert.equal(region.textContent, 'Moved "A" up. Now 1 of 2.');
});

/**
 * Makes a stand-in for a select with just what the settled-value helper listens to.
 * @param {object} [doc] The document it belongs to, or none.
 * @returns {object}
 */
function fakeSelect(doc) {
  const select = {
    value: 'a',
    ownerDocument: doc,
    listeners: {},
    addEventListener(type, handler) {
      (select.listeners[type] ??= []).push(handler);
    },
    fire(type, event = {}) {
      for (const handler of select.listeners[type] ?? []) {
        handler(event);
      }
    },
  };

  return select;
}

test('three keyboard-originated changes save once, with the last value, after the pause', (context) => {
  context.mock.timers.enable({ apis: ['setTimeout'] });

  const select = fakeSelect();
  const saved = [];

  saveSettledValue(select, (value) => saved.push(value), 600);

  for (const [value, at] of [['b', 1000], ['c', 1200], ['d', 1400]]) {
    select.fire('keydown', { key: 'ArrowDown', timeStamp: at });
    select.value = value;
    select.fire('change', { timeStamp: at + 2 });
  }

  assert.deepEqual(saved, []);
  context.mock.timers.tick(599);
  assert.deepEqual(saved, []);
  context.mock.timers.tick(1);
  assert.deepEqual(saved, ['d']);
  context.mock.timers.tick(5000);
  assert.equal(saved.length, 1);
});

test('a pointer or touch choice saves at once', (context) => {
  context.mock.timers.enable({ apis: ['setTimeout'] });

  const select = fakeSelect();
  const saved = [];

  saveSettledValue(select, (value) => saved.push(value), 600);
  select.fire('keydown', { key: 'ArrowDown', timeStamp: 100 });
  select.value = 'b';
  select.fire('change', { timeStamp: 5000 });

  assert.deepEqual(saved, ['b']);
  context.mock.timers.tick(5000);
  assert.equal(saved.length, 1);
});

test('Tab and Enter flush what is waiting, and leaving the control does too, once', (context) => {
  context.mock.timers.enable({ apis: ['setTimeout'] });

  const select = fakeSelect();
  const saved = [];

  saveSettledValue(select, (value) => saved.push(value), 600);

  select.fire('keydown', { key: 'ArrowDown', timeStamp: 100 });
  select.value = 'b';
  select.fire('change', { timeStamp: 101 });
  select.fire('keydown', { key: 'Tab', timeStamp: 300 });
  select.fire('focusout', {});

  assert.deepEqual(saved, ['b']);

  select.fire('keydown', { key: 'ArrowDown', timeStamp: 1000 });
  select.value = 'c';
  select.fire('change', { timeStamp: 1001 });
  select.fire('keydown', { key: 'Enter', timeStamp: 1200 });

  assert.deepEqual(saved, ['b', 'c']);

  select.fire('keydown', { key: 'ArrowDown', timeStamp: 2000 });
  select.value = 'd';
  select.fire('change', { timeStamp: 2001 });
  select.fire('focusout', {});

  assert.deepEqual(saved, ['b', 'c', 'd']);
  context.mock.timers.tick(5000);
  assert.equal(saved.length, 3);
});

test('a page that goes to the background flushes with the keepalive flag', (context) => {
  context.mock.timers.enable({ apis: ['setTimeout'] });

  const doc = { visibilityState: 'visible', listeners: {} };

  doc.addEventListener = (type, handler) => {
    doc.listeners[type] = handler;
  };

  const select = fakeSelect(doc);
  const calls = [];

  saveSettledValue(select, (value, options) => calls.push({ value, options }), 600);
  select.fire('keydown', { key: 'ArrowDown', timeStamp: 100 });
  select.value = 'b';
  select.fire('change', { timeStamp: 101 });
  doc.visibilityState = 'hidden';
  doc.listeners.visibilitychange();

  assert.deepEqual(calls, [{ value: 'b', options: { keepalive: true } }]);
});

test('a control with nothing waiting saves nothing when it loses focus', () => {
  const select = fakeSelect();
  const saved = [];

  saveSettledValue(select, (value) => saved.push(value), 600);
  select.fire('focusout', {});

  assert.deepEqual(saved, []);
});

test('a post can ask for the keepalive flag and the token still goes in the header', async () => {
  const { fetch, calls } = answering(200, '{"outcome":"saved","revision":9}');

  await postOwner('?handler=Assign', { gameId: 1, locationId: 'x' }, { fetch, token: 'tok', revision: 8, keepalive: true });

  assert.equal(calls[0].init.keepalive, true);
  assert.equal(calls[0].init.headers.RequestVerificationToken, 'tok');
});

test('the shared setter keeps the saved value in step with the select', () => {
  const select = { value: '', dataset: {} };

  setPickerValue(select, 'x');

  assert.equal(select.value, 'x');
  assert.equal(select.dataset.saved, 'x');
});

test('search folding ignores case and accents', () => {
  assert.equal(foldForSearch('Café'), foldForSearch('cafe'));
  assert.equal(foldForSearch('CAFÉ Quarter'), 'cafe quarter');
  assert.equal(foldForSearch(''), '');
});

/**
 * Makes a stand-in for a game row with a title, the way the page marks nesting, and the one property the finder sets.
 * @param {string} id The game id.
 * @param {string} title The title.
 * @param {string | null} [baseId] The base game an expansion follows.
 * @returns {object}
 */
function gameRow(id, title, baseId = null) {
  return {
    hidden: false,
    dataset: { gameId: id, entryId: id, baseEntryId: baseId ?? '', nested: baseId === null ? 'false' : 'true' },
    querySelector: (selector) => (selector === '.game-title' ? { textContent: title } : null),
  };
}

/**
 * Makes a stand-in for the search box.
 * @param {string} [value] The text in it.
 * @returns {object}
 */
function fakeInput(value = '') {
  const input = {
    value,
    listeners: {},
    addEventListener(type, handler) {
      (input.listeners[type] ??= []).push(handler);
    },
  };

  return input;
}

test('typing waits for a pause, then hides the rows that do not match and reports what is shown', (context) => {
  context.mock.timers.enable({ apis: ['setTimeout'] });

  const rows = [gameRow('1', 'Café Quarter'), gameRow('2', 'Game 2'), gameRow('3', 'Game 10')];
  const input = fakeInput();
  const reports = [];
  const finder = createFinder(input, () => rows, { debounceMs: 250, onApply: (report) => reports.push(report) });

  finder.apply({ announce: false });
  input.value = 'cafe';
  input.listeners.input[0]();
  context.mock.timers.tick(249);

  assert.equal(reports.length, 1);

  context.mock.timers.tick(1);

  assert.deepEqual(rows.map((row) => row.hidden), [false, true, true]);
  assert.deepEqual(reports.at(-1), { shown: 1, total: 3, query: 'cafe', announce: true });
});

test('a base game stays visible as context when only one of its expansions matches', () => {
  const rows = [gameRow('1', 'Harbour'), gameRow('2', 'Harbour: Lanterns', '1'), gameRow('3', 'Quarry')];
  const input = fakeInput('lantern');
  const finder = createFinder(input, () => rows, {});
  const report = finder.apply({ announce: false });

  assert.deepEqual(rows.map((row) => row.hidden), [false, false, true]);
  assert.equal(rows[0].dataset.context, 'true');
  assert.equal(rows[1].dataset.context, undefined);
  assert.equal(report.shown, 2);
});

test('an extra rule narrows the rows and a base that matches is not marked as context', () => {
  const rows = [gameRow('1', 'Harbour'), gameRow('2', 'Harbour: Lanterns', '1'), gameRow('3', 'Quarry')];
  const input = fakeInput('');
  const finder = createFinder(input, () => rows, { accepts: (row) => row.dataset.entryId !== '3' });
  const report = finder.apply({ announce: false });

  assert.deepEqual(rows.map((row) => row.hidden), [false, false, true]);
  assert.equal(rows[0].dataset.context, undefined);
  assert.equal(report.shown, 2);
});

test('clearing the query shows every row again', () => {
  const rows = [gameRow('1', 'Harbour'), gameRow('2', 'Quarry')];
  const input = fakeInput('zzz');
  const finder = createFinder(input, () => rows, {});

  assert.equal(finder.apply({ announce: false }).shown, 0);

  input.value = '  ';

  assert.equal(finder.apply({ announce: false }).shown, 2);
  assert.deepEqual(rows.map((row) => row.hidden), [false, false]);
});

test('the rows are put in collator order with each base game followed by its expansions', () => {
  const rows = [
    gameRow('1', 'Game 10'),
    gameRow('2', 'Game 2'),
    gameRow('3', 'Game 2: Zebra', '2'),
    gameRow('4', 'Game 2: Apple', '2'),
    gameRow('5', 'Café Quarter'),
    gameRow('6', 'Ápple Orchard'),
    gameRow('7', 'Orphan: Pack'),
  ];
  const container = {
    children: [...rows],
    append(...nodes) {
      container.children = nodes;
    },
  };

  sortByCollator(container);

  assert.deepEqual(
    container.children.map((row) => row.querySelector('.game-title').textContent),
    ['Ápple Orchard', 'Café Quarter', 'Game 2', 'Game 2: Apple', 'Game 2: Zebra', 'Game 10', 'Orphan: Pack'],
  );
});

/**
 * Makes a stand-in for the row of one copy of a game, the way the page marks copies and the nesting of an expansion.
 * @param {string} entry The collection entry of the copy.
 * @param {string} game The game id.
 * @param {string} title The title.
 * @param {string | null} [baseEntry] The collection entry of the first copy of the base game an expansion follows.
 * @returns {object}
 */
function copyRow(entry, game, title, baseEntry = null) {
  return {
    hidden: false,
    dataset: { gameId: game, entryId: entry, baseEntryId: baseEntry ?? '', nested: baseEntry === null ? 'false' : 'true' },
    querySelector: (selector) => (selector === '.game-title' ? { textContent: title } : null),
  };
}

test('an expansion is put under the first copy of its base game and the copies stay in collection order', () => {
  const rows = [
    copyRow('71', '7', 'Quiet Quarry'),
    copyRow('72', '7', 'Quiet Quarry'),
    copyRow('73', '7', 'Quiet Quarry'),
    copyRow('90', '9', 'Quiet Quarry: Lanterns', '71'),
  ];
  const container = {
    children: [...rows],
    append(...nodes) {
      container.children = nodes;
    },
  };

  sortByCollator(container);

  assert.deepEqual(
    container.children.map((row) => row.dataset.entryId),
    ['71', '90', '72', '73'],
    'the expansion follows the first copy it names and the other copies keep their order',
  );
});

test('a copy of a base game stays visible as context when only its expansion matches', () => {
  const rows = [copyRow('71', '7', 'Quiet Quarry'), copyRow('72', '7', 'Quiet Quarry'), copyRow('90', '9', 'Quiet Quarry: Lanterns', '71')];
  const finder = createFinder(fakeInput('lantern'), () => rows, {});

  finder.apply({ announce: false });

  assert.deepEqual(rows.map((row) => row.hidden), [false, true, false]);
  assert.equal(rows[0].dataset.context, 'true');
});
