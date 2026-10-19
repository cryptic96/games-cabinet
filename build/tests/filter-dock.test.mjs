/**
 * Checks the pure part of the desktop filter dock with the plain Node test runner: which state the dock lands in for a width and a
 * remembered choice, and which thing keeps its place on screen while the pushed panel resizes the cabinet. The script is loaded from
 * a temporary copy of the script folder so its relative imports resolve.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { copyFileSync, mkdtempSync, readdirSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

const FOLDER = mkdtempSync(join(tmpdir(), 'cabinet-filter-dock-'));

writeFileSync(join(FOLDER, 'package.json'), '{"type":"module"}');

for (const name of readdirSync(new URL('../../Cabinet.Service/wwwroot/js/', import.meta.url)).filter((file) => file.endsWith('.js'))) {
  copyFileSync(new URL('../../Cabinet.Service/wwwroot/js/' + name, import.meta.url), join(FOLDER, name));
}

const { dockStateFor, pickAnchor, CLICK_SWALLOW_MS, REMEMBER_OPEN, REMEMBER_FOLDED } = await import(pathToFileURL(join(FOLDER, 'filter-dock.js')).href);

test('below the desktop width there is no dock, whatever is remembered', () => {
  for (const remembered of [REMEMBER_OPEN, REMEMBER_FOLDED, null]) {
    assert.equal(dockStateFor({ wide: false, push: false, remembered }), null);
  }
});

test('at a fly-over width the dock always lands on the rail, even when the panel was left open', () => {
  for (const remembered of [REMEMBER_OPEN, REMEMBER_FOLDED, null, 'anything']) {
    assert.equal(dockStateFor({ wide: true, push: false, remembered }), 'rail');
  }
});

test('at a push width the dock lands open only when the visitor left it open', () => {
  assert.equal(dockStateFor({ wide: true, push: true, remembered: REMEMBER_OPEN }), 'push');
  assert.equal(dockStateFor({ wide: true, push: true, remembered: REMEMBER_FOLDED }), 'rail');
  assert.equal(dockStateFor({ wide: true, push: true, remembered: null }), 'rail');
  assert.equal(dockStateFor({ wide: true, push: true, remembered: 'pushed' }), 'rail', 'an unknown value means folded');
});

const cabinet = { name: 'cabinet', top: -900, bottom: 2400 };
const boxes = [
  { name: 'above', top: -300, bottom: -40 },
  { name: 'straddling', top: -20, bottom: 120 },
  { name: 'second', top: 260, bottom: 420 },
  { name: 'first', top: 40, bottom: 200 },
  { name: 'far', top: 1400, bottom: 1600 },
];

test('the focused box keeps its place when it is in view', () => {
  const focused = { name: 'focused', top: 500, bottom: 640 };

  assert.equal(pickAnchor({ focused, boxes, viewportTop: 0, viewportBottom: 800, cabinet }), focused);
});

test('a focused box partly in view still counts as in view', () => {
  const focused = { name: 'peeking', top: -100, bottom: 10 };

  assert.equal(pickAnchor({ focused, boxes, viewportTop: 0, viewportBottom: 800, cabinet }), focused);
});

test('a focused box out of view gives way to the box nearest below the viewport top', () => {
  const above = { name: 'gone up', top: -600, bottom: -400 };
  const below = { name: 'gone down', top: 900, bottom: 1000 };

  assert.equal(pickAnchor({ focused: above, boxes, viewportTop: 0, viewportBottom: 800, cabinet }).name, 'first');
  assert.equal(pickAnchor({ focused: below, boxes, viewportTop: 0, viewportBottom: 800, cabinet }).name, 'first');
});

test('without focus the anchor is the box whose top edge is nearest at or below the viewport top', () => {
  assert.equal(pickAnchor({ boxes, viewportTop: 0, viewportBottom: 800, cabinet }).name, 'first');
  assert.equal(pickAnchor({ boxes, viewportTop: 40, viewportBottom: 800, cabinet }).name, 'first', 'a top edge on the line counts');
  assert.equal(pickAnchor({ boxes, viewportTop: 41, viewportBottom: 800, cabinet }).name, 'second');
});

test('with no box at or below the viewport top the cabinet keeps its place', () => {
  assert.equal(pickAnchor({ boxes, viewportTop: 2000, viewportBottom: 2800, cabinet }), cabinet);
  assert.equal(pickAnchor({ boxes: [], viewportTop: 0, viewportBottom: 800, cabinet }), cabinet);
  assert.equal(pickAnchor({ boxes: [], viewportTop: 0 }), null, 'nothing at all gives nothing');
});

test('the click of a press outside is waited for only a short while', () => {
  assert.ok(Number.isInteger(CLICK_SWALLOW_MS));
  assert.ok(CLICK_SWALLOW_MS > 0 && CLICK_SWALLOW_MS <= 1000);
});
