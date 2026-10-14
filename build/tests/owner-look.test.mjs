/**
 * Checks the look panel script of the owner's settings page with no browser: the summary lines, the trial against the current
 * look, the buttons and the status lines against the copy contract, the focus after discarding, publish and Undo against an
 * injected post, the sample that cannot load, and that the wording and the names match the server's. The script is read from disk
 * and imported through a data URL with its imports pointed at their own data URLs; the elements are a small stand-in.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

/**
 * Reads a file of the service project.
 * @param {string} path The path below the service folder.
 * @returns {string}
 */
function readService(path) {
  return readFileSync(new URL('../../Cabinet.Service/' + path, import.meta.url), 'utf8');
}

/**
 * Turns a source into a data URL a module can be imported from.
 * @param {string} source The module source.
 * @returns {string}
 */
function dataUrl(source) {
  return 'data:text/javascript;base64,' + Buffer.from(source).toString('base64');
}

const format = dataUrl(readService('wwwroot/js/owner-format.js'));
const shared = dataUrl(readService('wwwroot/js/owner-shared.js').replace("'./owner-format.js'", `'${format}'`));
const copy = dataUrl(readService('wwwroot/js/copy.js'));
const render = dataUrl(readService('wwwroot/js/render.js'));
const lookSource = readService('wwwroot/js/owner-look.js')
  .replace("'./owner-format.js'", `'${format}'`)
  .replace("'./owner-shared.js'", `'${shared}'`)
  .replace("'./copy.js'", `'${copy}'`)
  .replace("'./render.js'", `'${render}'`);

const { initLookPanel, readLook, writeLook, sameLook, applyLookAttributes, loadSampleLayout } = await import(dataUrl(lookSource));
const { OWNER_TEXT, LOOK_NAMES, lookSummary } = await import(format);

const DEFAULT_LOOK = { design: 'classic', wood: 'brown', backs: 'wood', lights: 'off', depth: 'shaded' };
const ELEVEN = { design: 'library', wood: 'mahogany', backs: 'painted', lights: 'evenings', depth: 'deep' };

/**
 * Makes a stand-in for an element with what the panel script and the shared status helpers use.
 * @param {object} doc The document that made it.
 * @param {string} tag The tag name.
 * @param {Record<string, string>} [attributes] The attributes it starts with.
 * @returns {object}
 */
function element(doc, tag, attributes = {}) {
  const node = {
    tagName: tag,
    ownerDocument: doc,
    children: [],
    dataset: {},
    attributes: { ...attributes },
    listeners: {},
    textContent: '',
    hidden: false,
    className: '',
    type: '',
    inputs: [],
    append(...nodes) {
      node.children.push(...nodes);
    },
    replaceChildren(...nodes) {
      node.children = nodes;
    },
    getAttribute: (name) => (name in node.attributes ? node.attributes[name] : null),
    setAttribute(name, value) {
      node.attributes[name] = String(value);
    },
    removeAttribute(name) {
      delete node.attributes[name];

      if (name === 'data-problem') {
        delete node.dataset.problem;
      }
    },
    addEventListener(type, handler) {
      node.listeners[type] = handler;
    },
    querySelector: (selector) => (selector === 'button[data-undo]' ? (node.children.find((child) => 'undo' in child.dataset) ?? null) : null),
    querySelectorAll(selector) {
      const match = /^input\[name="(\w+)"\]$/.exec(selector);

      return match ? node.inputs.filter((input) => input.name === match[1]) : [];
    },
    focus() {
      doc.activeElement = node;
    },
    remove() {},
  };

  return node;
}

/**
 * Reads all the text an element shows, its own and its children's.
 * @param {object} node The element.
 * @returns {string}
 */
function textOf(node) {
  return node.textContent + node.children.map(textOf).join('');
}

/**
 * Builds a stand-in page with the look panel's parts and one radio per choice.
 * @param {Record<string, string>} [look] The look the page was built with.
 * @returns {object} The page and its parts.
 */
function panelPage(look = DEFAULT_LOOK) {
  const ids = {};
  const doc = {
    activeElement: null,
    createElement: (tag) => element(doc, tag),
    getElementById: (id) => ids[id] ?? null,
    querySelector: () => null,
    querySelectorAll: () => [],
  };
  const make = (id, tag = 'div', attributes = {}) => {
    ids[id] = element(doc, tag, attributes);

    return ids[id];
  };
  const preview = make('look-preview', 'figure', {
    'data-design': look.design,
    'data-wood': look.wood,
    'data-backs': look.backs,
    'data-lights': look.lights,
    'data-depth': look.depth,
  });
  const form = make('look-form', 'form');
  const radios = (name, values) =>
    values.map((value) => {
      const input = element(doc, 'input');

      input.name = name;
      input.value = value;
      input.checked = look[name] === value;
      form.inputs.push(input);

      return input;
    });
  const parts = {
    design: radios('design', Object.keys(LOOK_NAMES.design)),
    wood: radios('wood', Object.keys(LOOK_NAMES.wood)),
    lights: radios('lights', Object.keys(LOOK_NAMES.lights)),
    depth: radios('depth', Object.keys(LOOK_NAMES.depth)),
  };
  const backs = element(doc, 'input');

  backs.name = 'backs';
  backs.value = 'painted';
  backs.checked = look.backs === 'painted';
  form.inputs.push(backs);
  preview.dataset.sample = '/js/owner-look-sample.json';
  make('owner-status');

  return {
    doc,
    preview,
    form,
    backs,
    radios: parts,
    mount: make('look-preview-mount'),
    current: make('look-current', 'p'),
    trying: make('look-trying', 'p'),
    publish: make('look-publish', 'button', { 'aria-disabled': 'true' }),
    discard: make('look-discard', 'button'),
    status: make('finish-status', 'p'),
  };
}

/**
 * Makes the dependencies the panel takes: a post that answers from a script, a sample that loads or not, and a renderer that notes.
 * @param {object[]} answers The answers the post gives, in order.
 * @param {object | null} [sample] The sample layout, or null for one that cannot load.
 * @returns {{ deps: object, posts: object[], drawn: object[] }}
 */
function dependencies(answers, sample = { sections: [{}] }) {
  const posts = [];
  const drawn = [];

  return {
    posts,
    drawn,
    deps: {
      post: async (url, fields, options) => {
        posts.push({ url, fields, options });

        return answers.shift() ?? { outcome: 'failed' };
      },
      load: async () => sample,
      render: (mount, layout) => drawn.push({ mount, layout }),
    },
  };
}

/**
 * Chooses a value in a group of radios, as a tap does, and lets the form hear the change.
 * @param {object} page The page.
 * @param {string} name The group.
 * @param {string} value The value to choose.
 */
function choose(page, name, value) {
  for (const input of page.radios[name]) {
    input.checked = input.value === value;
  }

  page.form.listeners.change();
}

test('the summary of a look is the control labels joined with plain middle dots', () => {
  assert.equal(lookSummary(ELEVEN), 'Library · Mahogany · Painted backs · Lights: Evenings only · Depth: Deep');
  assert.equal(lookSummary(DEFAULT_LOOK), 'Classic · Warm brown · Wood backs · Lights: Off · Depth: Shaded');
});

test('the look read from the controls and the look written to them agree, with the switch giving painted or wood', () => {
  const page = panelPage(ELEVEN);

  assert.deepEqual(readLook(page.form), ELEVEN);
  writeLook(page.form, DEFAULT_LOOK);
  assert.deepEqual(readLook(page.form), DEFAULT_LOOK);
  assert.equal(page.backs.checked, false);
  assert.equal(sameLook(DEFAULT_LOOK, ELEVEN), false);
  assert.equal(sameLook(ELEVEN, { ...ELEVEN }), true);
});

test('the attributes are written on the wrapper only', () => {
  const wrapper = element({}, 'figure');

  applyLookAttributes(wrapper, ELEVEN);

  assert.deepEqual(wrapper.attributes, { 'data-design': 'library', 'data-wood': 'mahogany', 'data-backs': 'painted', 'data-lights': 'evenings', 'data-depth': 'deep' });
});

test('while the trial equals the current look both buttons are disabled and the Trying line is hidden', async () => {
  const page = panelPage();
  const { deps } = dependencies([]);

  const panel = initLookPanel(page.doc, deps);
  await panel.ready;

  assert.equal(page.publish.getAttribute('aria-disabled'), 'true');
  assert.equal(page.discard.getAttribute('aria-disabled'), 'true');
  assert.equal(page.trying.hidden, true);
  assert.equal(page.discard.hidden, false);
  assert.equal(page.current.textContent, 'Visitors see: Classic · Warm brown · Wood backs · Lights: Off · Depth: Shaded');
});

test('a change starts a trial: the wrapper, the lines, the buttons and the status follow and nothing is posted', async () => {
  const page = panelPage();
  const { deps, posts } = dependencies([]);

  const panel = initLookPanel(page.doc, deps);
  await panel.ready;
  choose(page, 'wood', 'oak');

  assert.equal(page.preview.getAttribute('data-wood'), 'oak');
  assert.equal(page.trying.hidden, false);
  assert.equal(page.trying.textContent, 'Trying: Classic · Oak · Wood backs · Lights: Off · Depth: Shaded');
  assert.equal(page.current.textContent, 'Visitors see: Classic · Warm brown · Wood backs · Lights: Off · Depth: Shaded');
  assert.equal(page.publish.getAttribute('aria-disabled'), null);
  assert.equal(page.discard.getAttribute('aria-disabled'), null);
  assert.equal(textOf(page.status), 'Trying a new look. Visitors still see the current one.');
  assert.equal(posts.length, 0);
});

test('choosing the current look again ends the trial and clears the status', async () => {
  const page = panelPage();
  const { deps } = dependencies([]);

  await initLookPanel(page.doc, deps).ready;
  choose(page, 'depth', 'deep');
  choose(page, 'depth', 'shaded');

  assert.equal(page.trying.hidden, true);
  assert.equal(textOf(page.status), '');
  assert.equal(page.publish.getAttribute('aria-disabled'), 'true');
});

test('Back to current restores the trial, says so and moves focus to the checked design', async () => {
  const page = panelPage();
  const { deps } = dependencies([]);

  await initLookPanel(page.doc, deps).ready;
  choose(page, 'design', 'library');
  choose(page, 'wood', 'oak');
  page.discard.listeners.click();

  assert.deepEqual(readLook(page.form), DEFAULT_LOOK);
  assert.equal(page.preview.getAttribute('data-design'), 'classic');
  assert.equal(page.preview.getAttribute('data-wood'), 'brown');
  assert.equal(textOf(page.status), 'Back to the current look.');
  assert.equal(page.doc.activeElement, page.radios.design[0]);
  assert.equal(page.trying.hidden, true);
});

test('Back to current does nothing while the trial equals the current look', async () => {
  const page = panelPage();
  const { deps } = dependencies([]);

  await initLookPanel(page.doc, deps).ready;
  page.discard.listeners.click();

  assert.equal(textOf(page.status), '');
  assert.equal(page.doc.activeElement, null);
});

test('Use for everyone posts the five values, shows the saved line with Undo and keeps focus on the button', async () => {
  const page = panelPage();
  const { deps, posts } = dependencies([{ outcome: 'saved', revision: 3, canUndo: true }]);

  await initLookPanel(page.doc, deps).ready;
  choose(page, 'wood', 'oak');
  choose(page, 'lights', 'evenings');
  page.backs.checked = true;
  page.form.listeners.change();
  page.form.listeners.submit({ preventDefault() {} });
  await new Promise((resolve) => setTimeout(resolve, 0));

  assert.equal(posts.length, 1);
  assert.equal(posts[0].url, '?handler=Look');
  assert.deepEqual(posts[0].fields, { design: 'classic', wood: 'oak', backs: 'painted', lights: 'evenings', depth: 'shaded' });
  assert.equal(textOf(page.status), 'Saved for everyone. Open pages follow within a few seconds.Undo');
  assert.equal(page.status.querySelector('button[data-undo]') !== null, true);
  assert.equal(page.current.textContent, 'Visitors see: Classic · Oak · Painted backs · Lights: Evenings only · Depth: Shaded');
  assert.equal(page.publish.getAttribute('aria-disabled'), 'true');
  assert.equal(page.doc.activeElement, page.publish);
});

test('Undo posts with the revision of the publish and puts the earlier look back', async () => {
  const page = panelPage();
  const { deps, posts } = dependencies([{ outcome: 'saved', revision: 3 }, { outcome: 'saved', revision: 4 }]);

  await initLookPanel(page.doc, deps).ready;
  choose(page, 'wood', 'oak');
  page.form.listeners.submit({ preventDefault() {} });
  await new Promise((resolve) => setTimeout(resolve, 0));
  page.status.querySelector('button[data-undo]').listeners.click();
  await new Promise((resolve) => setTimeout(resolve, 0));

  assert.equal(posts[1].url, '?handler=Undo');
  assert.deepEqual(readLook(page.form), DEFAULT_LOOK);
  assert.equal(page.preview.getAttribute('data-wood'), 'brown');
  assert.equal(page.current.textContent, 'Visitors see: Classic · Warm brown · Wood backs · Lights: Off · Depth: Shaded');
  assert.equal(textOf(page.status), 'Put back the earlier look.');
});

test('a failed post keeps the trial and says nothing changed', async () => {
  const page = panelPage();
  const { deps } = dependencies([{ outcome: 'failed' }]);

  await initLookPanel(page.doc, deps).ready;
  choose(page, 'wood', 'oak');
  page.form.listeners.submit({ preventDefault() {} });
  await new Promise((resolve) => setTimeout(resolve, 0));

  assert.equal(textOf(page.status), "Couldn't save that. Nothing changed. Try again.");
  assert.equal(page.status.dataset.problem, 'true');
  assert.equal(page.preview.getAttribute('data-wood'), 'oak');
  assert.equal(page.publish.getAttribute('aria-disabled'), null);
  assert.equal(page.current.textContent.includes('Warm brown'), true);
});

test('an out-of-date page and an expired page show their messages with Reload and keep the trial', async () => {
  for (const [outcome, text] of [
    ['outOfDate', 'This page is out of date. Reload it and try again.'],
    ['expired', 'This page has expired. Reload it and try again.'],
  ]) {
    const page = panelPage();
    const { deps } = dependencies([{ outcome }]);

    await initLookPanel(page.doc, deps).ready;
    choose(page, 'wood', 'oak');
    page.form.listeners.submit({ preventDefault() {} });
    await new Promise((resolve) => setTimeout(resolve, 0));

    assert.equal(textOf(page.status), text + 'Reload');
    assert.equal(page.preview.getAttribute('data-wood'), 'oak');
  }
});

test('a refused look shows the words the server gave', async () => {
  const page = panelPage();
  const { deps } = dependencies([{ outcome: 'refused', message: "That look isn't available." }]);

  await initLookPanel(page.doc, deps).ready;
  choose(page, 'wood', 'oak');
  page.form.listeners.submit({ preventDefault() {} });
  await new Promise((resolve) => setTimeout(resolve, 0));

  assert.equal(textOf(page.status), "That look isn't available.");
});

test('Use for everyone does nothing while the trial equals the current look', async () => {
  const page = panelPage();
  const { deps, posts } = dependencies([]);

  await initLookPanel(page.doc, deps).ready;
  page.form.listeners.submit({ preventDefault() {} });
  await new Promise((resolve) => setTimeout(resolve, 0));

  assert.equal(posts.length, 0);
});

test('the sample is drawn into the mount, and a sample that cannot load shows the preview line and announces it', async () => {
  const ready = panelPage();
  const loaded = dependencies([]);

  await initLookPanel(ready.doc, loaded.deps).ready;

  assert.equal(loaded.drawn.length, 1);
  assert.equal(loaded.drawn[0].mount, ready.mount);
  assert.equal(ready.preview.dataset.state, 'ready');

  const failed = panelPage();
  const none = dependencies([], null);

  await initLookPanel(failed.doc, none.deps).ready;

  assert.equal(none.drawn.length, 0);
  assert.equal(failed.preview.dataset.state, 'failed');
  assert.equal(textOf(failed.mount), "The preview couldn't load. You can still choose a look and press Use for everyone.");
  assert.equal(failed.doc.getElementById('owner-status').textContent, "The preview couldn't load. You can still choose a look and press Use for everyone.");
});

test('a page without the panel is left alone', () => {
  assert.equal(initLookPanel({ getElementById: () => null }), null);
});

test('the sample loader gives null for a bad answer, a network error and a body that is not a layout', async () => {
  const answering = (response) => async () => response;

  assert.equal(await loadSampleLayout('/x', answering({ ok: false })), null);
  assert.equal(await loadSampleLayout('/x', async () => { throw new Error('offline'); }), null);
  assert.equal(await loadSampleLayout('/x', answering({ ok: true, json: async () => ({}) })), null);
  assert.equal(await loadSampleLayout('/x', answering({ ok: true, json: async () => ({ sections: [] }) })), null);
  assert.deepEqual(await loadSampleLayout('/x', answering({ ok: true, json: async () => ({ sections: [{}] }) })), { sections: [{}] });
});

test('every string of the look panel equals the copy contract and uses plain dots', () => {
  assert.equal(OWNER_TEXT.lookTrying, 'Trying a new look. Visitors still see the current one.');
  assert.equal(OWNER_TEXT.lookDiscarded, 'Back to the current look.');
  assert.equal(OWNER_TEXT.lookSaved, 'Saved for everyone. Open pages follow within a few seconds.');
  assert.equal(OWNER_TEXT.lookUndone, 'Put back the earlier look.');
  assert.equal(OWNER_TEXT.failed, "Couldn't save that. Nothing changed. Try again.");
  assert.equal(OWNER_TEXT.previewFailed, "The preview couldn't load. You can still choose a look and press Use for everyone.");
  assert.equal(OWNER_TEXT.visitorsSee, 'Visitors see: {summary}');
  assert.equal(OWNER_TEXT.trying, 'Trying: {summary}');

  for (const key of ['lookTrying', 'lookDiscarded', 'lookSaved', 'lookUndone', 'previewFailed', 'visitorsSee', 'trying']) {
    assert.equal(OWNER_TEXT[key].includes('…'), false, key);
    assert.equal(OWNER_TEXT[key].includes('!'), false, key);
  }
});

test('the server words the same look strings the script does', () => {
  const server = readService('Owner/OwnerStrings.cs');

  for (const text of [OWNER_TEXT.lookSaved, OWNER_TEXT.lookUndone, OWNER_TEXT.failed, 'Visitors see: {summary}']) {
    assert.equal(server.includes(text), true, text);
  }

  assert.equal(server.includes('The preview needs scripts. You can still choose a look and press Use for everyone.'), true);
});

test('the display names match the names the server gives every choice', () => {
  const domain = readService('../Cabinet.Domain/Owner/CabinetLook.cs');
  const pairs = [...domain.matchAll(/^\s*"([\w-]+)" => "([^"]+)",$/gm)].map((match) => [match[1], match[2]]);
  const known = new Map(pairs);

  for (const [field, names] of Object.entries(LOOK_NAMES)) {
    for (const [value, name] of Object.entries(names)) {
      if (field === 'backs' && value === 'wood') {
        assert.equal(domain.includes(`_ => "${name}"`), true, name);
      } else {
        assert.equal(known.get(value), name, value);
      }
    }
  }
});

test('the stand-in section of the preview has the size of the sample section', () => {
  const sample = JSON.parse(readService('wwwroot/js/owner-look-sample.json')).sections[0];
  const stylesheet = readService('wwwroot/css/owner.css');
  const standIn = /\.look-preview \.section\[data-standin\] \{([^}]*)\}/.exec(stylesheet)[1];

  assert.match(standIn, new RegExp(`--section-w: ${sample.widthMm + 2 * sample.frameMm};`));
  assert.match(standIn, new RegExp(`--section-h: ${sample.heightMm + 2 * sample.frameMm};`));
  assert.match(standIn, new RegExp(`--frame: ${sample.frameMm};`));
});

test('the preview is sized by the height limit and the column width through the section ratio', () => {
  const stylesheet = readService('wwwroot/css/owner.css');

  assert.match(stylesheet, /width: min\(100%, calc\(var\(--preview-h\) \* var\(--section-ratio\)\)\);/);
  assert.match(stylesheet, /--preview-h: min\(46dvh, 28rem\);/);
  assert.match(stylesheet, /--preview-h: min\(70dvh, 36rem\);/);
  assert.equal(stylesheet.includes('finish-tile'), false);
});
