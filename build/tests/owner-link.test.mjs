/**
 * Checks, with no browser, that the owner's link script trusts nothing the server's preview answer carries without checking it:
 * a good answer passes, and a picture address outside the site's preview route, a bad colour, a bad size or an unknown fit is
 * refused. The modules are copied to a scratch folder so their relative imports resolve.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { copyFileSync, mkdtempSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

const FOLDER = mkdtempSync(join(tmpdir(), 'cabinet-owner-link-'));

writeFileSync(join(FOLDER, 'package.json'), '{"type":"module"}');

for (const name of ['owner-link.js', 'owner-format.js', 'owner-shared.js']) {
  copyFileSync(new URL('../../Cabinet.Service/wwwroot/js/' + name, import.meta.url), join(FOLDER, name));
}

const { checkedPreview } = await import(pathToFileURL(join(FOLDER, 'owner-link.js')).href);
const { OWNER_TEXT } = await import(pathToFileURL(join(FOLDER, 'owner-format.js')).href);

const TOKEN = (0xbeef).toString(16).repeat(8);

/**
 * Makes an answer the way the server writes one.
 * @param {object} [changes] Members of the preview to change.
 * @param {string} [previewUrl] The address of the preview picture.
 * @returns {object}
 */
function answer(changes = {}, previewUrl = '/owner/preview/' + TOKEN) {
  return {
    outcome: 'ready',
    token: TOKEN,
    previewUrl,
    preview: {
      title: 'Invented Game',
      boxWidthMm: 229,
      boxHeightMm: 305,
      boxDepthMm: 76,
      fit: 'exact',
      edges: null,
      artWidth: 240,
      artHeight: 320,
      shapeSource: 'bgg',
      shapeText: "Box shape: from BGG's size",
      verdict: 'flat',
      verdictText: 'Reads as a flat cover',
      note: null,
      spineWidthMm: 76,
      spineHeightMm: 305,
      spineLabel: 'Invented Game',
      spineBackground: '#336699',
      spineText: '#ffffff',
      ...changes,
    },
  };
}

test('a preview written the way the server writes it is accepted and carries the picture address and the token', () => {
  const preview = checkedPreview(answer());

  assert.equal(preview.previewUrl, '/owner/preview/' + TOKEN);
  assert.equal(preview.token, TOKEN);
  assert.equal(preview.boxWidthMm, 229);
});

test('a picture address that is not the site preview route is refused', () => {
  for (const address of ['https://example.org/owner/linked.png', '/owner/preview/../x', '/art/0123456789abcdef-240.webp', '/owner/preview/ABC', '']) {
    assert.equal(checkedPreview(answer({}, address)), null, address);
  }
});

test('a token that is missing or is not the one in the preview address is refused', () => {
  const withoutToken = answer();
  const otherToken = answer();

  delete withoutToken.token;
  otherToken.token = '0'.repeat(32);

  assert.equal(checkedPreview(withoutToken), null);
  assert.equal(checkedPreview(otherToken), null);
  assert.equal(checkedPreview({ ...answer(), token: undefined }), null);
});

test('a bad colour, size, fit or missing text is refused', () => {
  assert.equal(checkedPreview(answer({ spineBackground: 'red' })), null);
  assert.equal(checkedPreview(answer({ spineText: '#808080' })), null);
  assert.equal(checkedPreview(answer({ boxWidthMm: 0 })), null);
  assert.equal(checkedPreview(answer({ boxHeightMm: 1.5 })), null);
  assert.equal(checkedPreview(answer({ artWidth: 100000 })), null);
  assert.equal(checkedPreview(answer({ fit: 'stretch' })), null);
  assert.equal(checkedPreview(answer({ spineLabel: 5 })), null);
  assert.equal(checkedPreview({ outcome: 'ready', previewUrl: '/owner/preview/' + TOKEN }), null);
  assert.equal(checkedPreview(null), null);
});

test('the words the script shows match the words the server writes for the preview', () => {
  assert.equal(OWNER_TEXT.previewHeading, 'Preview');
  assert.equal(OWNER_TEXT.previewDisclaimer, "The site can't check that this picture belongs to this game.");
  assert.equal(OWNER_TEXT.useThis, 'Use this');
  assert.equal(OWNER_TEXT.discardPreview, 'Discard preview');
  assert.equal(OWNER_TEXT.checking, 'Checking...');
});
