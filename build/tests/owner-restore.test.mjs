/**
 * Checks, with no browser, how the restore panel's script treats the server's answer to the check of a chosen file: a good answer
 * takes the page to the preview under the token it carries, a refusal shows its words in the alert line, an answer that is not what
 * the server writes, a network error and a file over the request limit all show a fixed line, and nothing the answer carries is
 * ever written as markup. The modules are copied to a scratch folder so their relative imports resolve.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { copyFileSync, mkdtempSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

const FOLDER = mkdtempSync(join(tmpdir(), 'cabinet-owner-restore-'));

writeFileSync(join(FOLDER, 'package.json'), '{"type":"module"}');

for (const name of ['owner-settings.js', 'owner-format.js', 'owner-shared.js']) {
  copyFileSync(new URL('../../Cabinet.Service/wwwroot/js/' + name, import.meta.url), join(FOLDER, name));
}

const { submitRestoreCheck } = await import(pathToFileURL(join(FOLDER, 'owner-settings.js')).href);
const { OWNER_TEXT } = await import(pathToFileURL(join(FOLDER, 'owner-format.js')).href);

const TOKEN = (0xcafe).toString(16).repeat(8);

/**
 * Makes the few parts of a page the script touches: the alert line, the antiforgery meta element and a base address.
 * @returns {{ doc: object, line: { textContent: string, hidden: boolean } }}
 */
function makePage() {
  const line = { textContent: '', hidden: true };
  const doc = {
    baseURI: 'http://127.0.0.1/owner/settings',
    getElementById: (id) => (id === 'restore-problem' ? line : null),
    querySelector: (selector) => (selector.includes('request-verification-token') ? { getAttribute: () => 'page-token' } : null),
  };

  return { doc, line };
}

/**
 * Makes a fetch that remembers its request and answers with a status and a body, or fails like a network error.
 * @param {number} status The status.
 * @param {string} body The body text.
 * @param {boolean} [fails] Whether the request fails instead.
 * @returns {{ send: Function, calls: Array<{ address: string, init: object }> }}
 */
function makeSend(status, body, fails = false) {
  const calls = [];

  return {
    calls,
    send: async (address, init) => {
      calls.push({ address, init });

      if (fails) {
        throw new Error('network');
      }

      return { status, ok: status >= 200 && status < 300, json: async () => JSON.parse(body) };
    },
  };
}

const FORM = { action: 'http://127.0.0.1/owner/settings?handler=CheckFile' };

test('a good answer takes the page to the preview under the token and shows no problem', async () => {
  const { doc, line } = makePage();
  const { send, calls } = makeSend(200, JSON.stringify({ outcome: 'checked', token: TOKEN }));
  let went = null;

  const result = await submitRestoreCheck(FORM, doc, { send, go: (address) => { went = address; }, body: 'file' });

  assert.equal(result, true);
  assert.equal(went, `/owner/settings?restore=${TOKEN}#restore-preview-heading`);
  assert.equal(line.hidden, true);
  assert.equal(calls[0].init.method, 'POST');
  assert.equal(calls[0].init.headers.Accept, 'application/json');
  assert.equal(calls[0].init.headers.RequestVerificationToken, 'page-token');
});

test('a refusal shows the server words in the alert line and goes nowhere', async () => {
  const { doc, line } = makePage();
  const words = 'That file is damaged or incomplete, so nothing was changed.';
  const { send } = makeSend(422, JSON.stringify({ outcome: 'refused', message: words }));
  let went = null;

  const result = await submitRestoreCheck(FORM, doc, { send, go: (address) => { went = address; }, body: 'file' });

  assert.equal(result, false);
  assert.equal(went, null);
  assert.equal(line.textContent, words);
  assert.equal(line.hidden, false);
});

test('a token that is not the shape the server makes is never followed', async () => {
  const { doc, line } = makePage();
  const { send } = makeSend(200, JSON.stringify({ outcome: 'checked', token: '../../evil' }));
  let went = null;

  const result = await submitRestoreCheck(FORM, doc, { send, go: (address) => { went = address; }, body: 'file' });

  assert.equal(result, false);
  assert.equal(went, null);
  assert.equal(line.textContent, OWNER_TEXT.checkFailed);
});

test('a file over the request limit and a network error each show their own fixed line', async () => {
  const large = makePage();
  const broken = makePage();

  await submitRestoreCheck(FORM, large.doc, { send: makeSend(413, '{}').send, body: 'file' });
  await submitRestoreCheck(FORM, broken.doc, { send: makeSend(0, '{}', true).send, body: 'file' });

  assert.equal(large.line.textContent, OWNER_TEXT.fileTooLarge);
  assert.equal(broken.line.textContent, OWNER_TEXT.checkFailed);
  assert.equal(broken.line.hidden, false);
});

test('an answer that is not JSON shows the fixed failure line', async () => {
  const { doc, line } = makePage();
  const send = async () => ({ status: 500, ok: false, json: async () => { throw new SyntaxError('not json'); } });

  const result = await submitRestoreCheck(FORM, doc, { send, body: 'file' });

  assert.equal(result, false);
  assert.equal(line.textContent, OWNER_TEXT.checkFailed);
});
