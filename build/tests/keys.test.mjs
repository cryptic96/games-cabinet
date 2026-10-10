/**
 * Checks the spatial rule of the cabinet's arrow keys with the plain Node test runner: which box each key moves to, given the
 * rectangles of the boxes on screen. The script is read from disk and imported through a data URL.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const source = readFileSync(new URL('../../Cabinet.Service/wwwroot/js/keys.js', import.meta.url), 'utf8');
const { nextBox } = await import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));

/**
 * Builds the rectangle of one box.
 * @param {number} left The left edge.
 * @param {number} top The top edge.
 * @param {number} width The width.
 * @param {number} height The height.
 * @param {string} shelf The shelf the box stands on.
 * @returns {{ left: number, top: number, right: number, bottom: number, shelf: string }}
 */
const box = (left, top, width, height, shelf) => ({ left, top, right: left + width, bottom: top + height, shelf });

const ROW = [box(0, 100, 40, 200, '0:0'), box(40, 100, 40, 200, '0:0'), box(80, 100, 40, 200, '0:0')];

test('right walks a shelf box by box and stays on the last one', () => {
  assert.equal(nextBox(ROW, 0, 'ArrowRight'), 1);
  assert.equal(nextBox(ROW, 1, 'ArrowRight'), 2);
  assert.equal(nextBox(ROW, 2, 'ArrowRight'), 2);
});

test('left mirrors right and stays on the first box', () => {
  assert.equal(nextBox(ROW, 2, 'ArrowLeft'), 1);
  assert.equal(nextBox(ROW, 1, 'ArrowLeft'), 0);
  assert.equal(nextBox(ROW, 0, 'ArrowLeft'), 0);
});

test('left and right never leave the shelf, even when a box on another shelf is nearer', () => {
  const rects = [box(0, 100, 40, 200, '0:0'), box(44, 400, 40, 200, '0:1'), box(200, 100, 40, 200, '0:0')];

  assert.equal(nextBox(rects, 0, 'ArrowRight'), 2);
  assert.equal(nextBox(rects, 2, 'ArrowLeft'), 0);
  assert.equal(nextBox(rects, 1, 'ArrowRight'), 1);
});

test('from a spine right enters a flat pile at the box nearest its centre, and up and down walk the pile one box at a time', () => {
  const rects = [
    box(0, 100, 40, 200, '0:0'),
    box(40, 100, 100, 60, '0:0'),
    box(40, 160, 100, 60, '0:0'),
    box(40, 220, 100, 80, '0:0'),
    box(140, 100, 40, 200, '0:0'),
  ];

  assert.equal(nextBox(rects, 0, 'ArrowRight'), 2);
  assert.equal(nextBox(rects, 2, 'ArrowDown'), 3);
  assert.equal(nextBox(rects, 2, 'ArrowUp'), 1);
  assert.equal(nextBox(rects, 1, 'ArrowRight'), 4);
  assert.equal(nextBox(rects, 3, 'ArrowRight'), 4);
  assert.equal(nextBox(rects, 4, 'ArrowLeft'), 2);
});

test('down from the bottom of a pile goes on to the nearest box of the shelf below, and up comes back', () => {
  const rects = [
    box(0, 100, 100, 100, '0:0'),
    box(0, 200, 100, 100, '0:0'),
    box(0, 340, 40, 200, '0:1'),
    box(300, 340, 40, 200, '0:1'),
  ];

  assert.equal(nextBox(rects, 0, 'ArrowDown'), 1);
  assert.equal(nextBox(rects, 1, 'ArrowDown'), 2);
  assert.equal(nextBox(rects, 2, 'ArrowUp'), 1);
  assert.equal(nextBox(rects, 3, 'ArrowUp'), 1);
});

test('down prefers the box under the current one over a nearer box far to the side', () => {
  const rects = [box(0, 0, 40, 100, '0:0'), box(400, 105, 40, 100, '0:1'), box(10, 160, 40, 100, '0:1')];

  assert.equal(nextBox(rects, 0, 'ArrowDown'), 2);
});

test('down from the last shelf of one section goes to the first shelf of the section below', () => {
  const rects = [box(0, 100, 40, 200, '0:0'), box(0, 340, 40, 200, '0:1'), box(0, 600, 40, 200, '1:0')];

  assert.equal(nextBox(rects, 1, 'ArrowDown'), 2);
  assert.equal(nextBox(rects, 2, 'ArrowUp'), 1);
});

test('up from the first shelf and down from the last shelf stay where they are', () => {
  const rects = [box(0, 100, 40, 200, '0:0'), box(0, 340, 40, 200, '0:1')];

  assert.equal(nextBox(rects, 0, 'ArrowUp'), 0);
  assert.equal(nextBox(rects, 1, 'ArrowDown'), 1);
});

test('a tie goes to the earlier box', () => {
  const rects = [box(100, 0, 40, 100, '0:0'), box(0, 200, 40, 100, '0:1'), box(200, 200, 40, 100, '0:1')];

  assert.equal(nextBox(rects, 0, 'ArrowDown'), 1);
});

test('edges that touch within two pixels still count as one below the other', () => {
  const rects = [box(0, 0, 100, 100, '0:0'), box(0, 99, 100, 100, '0:0')];

  assert.equal(nextBox(rects, 0, 'ArrowDown'), 1);
  assert.equal(nextBox(rects, 1, 'ArrowUp'), 0);
});

test('Home gives the first box and End the last, from anywhere', () => {
  assert.equal(nextBox(ROW, 1, 'Home'), 0);
  assert.equal(nextBox(ROW, 1, 'End'), 2);
  assert.equal(nextBox(ROW, 0, 'Home'), 0);
  assert.equal(nextBox(ROW, 2, 'End'), 2);
});

test('with no boxes every key gives -1', () => {
  for (const key of ['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End']) {
    assert.equal(nextBox([], -1, key), -1);
    assert.equal(nextBox([], 0, key), -1);
  }
});

test('with one box every key gives that box', () => {
  const rects = [box(0, 0, 40, 100, '0:0')];

  for (const key of ['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End']) {
    assert.equal(nextBox(rects, 0, key), 0);
  }
});

test('a box with no area is still reachable', () => {
  const rects = [box(0, 0, 40, 100, '0:0'), box(40, 0, 0, 0, '0:0'), box(60, 0, 40, 100, '0:0')];

  assert.equal(nextBox(rects, 0, 'ArrowRight'), 1);
  assert.equal(nextBox(rects, 1, 'ArrowRight'), 2);
});
