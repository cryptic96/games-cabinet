/**
 * Checks the pure part of the Filters sheet with the plain Node test runner: which lit box the page scrolls to when the sheet closes
 * with its matches out of sight. The script is loaded from a temporary copy of the script folder so its relative imports resolve.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { copyFileSync, mkdtempSync, readdirSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

const FOLDER = mkdtempSync(join(tmpdir(), 'cabinet-filter-sheet-'));

writeFileSync(join(FOLDER, 'package.json'), '{"type":"module"}');

for (const name of readdirSync(new URL('../../Cabinet.Service/wwwroot/js/', import.meta.url)).filter((file) => file.endsWith('.js'))) {
  copyFileSync(new URL('../../Cabinet.Service/wwwroot/js/' + name, import.meta.url), join(FOLDER, name));
}

const { nextLitTarget } = await import(pathToFileURL(join(FOLDER, 'filter-sheet.js')).href);

const boxes = [{ top: 120 }, { top: 480 }, { top: 900 }, { top: 1500 }];

test('the next lit box is the first one in reading order that lies below the bar at the current scroll position', () => {
  assert.equal(nextLitTarget(boxes, 600, 0), boxes[2]);
  assert.equal(nextLitTarget(boxes, 300, 0), boxes[1]);
});

test('the bar counts as covering the top of the viewport, so a box under it is not the next one', () => {
  assert.equal(nextLitTarget(boxes, 400, 0), boxes[1]);
  assert.equal(nextLitTarget(boxes, 400, 112), boxes[2]);
  assert.equal(nextLitTarget(boxes, 400, 200), boxes[2]);
});

test('with no lit box below the current position the first lit box of all is the answer', () => {
  assert.equal(nextLitTarget(boxes, 1600, 0), boxes[0]);
  const only = { top: 40 };

  assert.equal(nextLitTarget([only], 500, 60), only);
});

test('with no lit box at all there is nothing to scroll to', () => {
  assert.equal(nextLitTarget([], 0, 0), null);
});

test('a box whose top is exactly at the edge below the bar is the next one', () => {
  assert.equal(nextLitTarget(boxes, 380, 100), boxes[1]);
});
