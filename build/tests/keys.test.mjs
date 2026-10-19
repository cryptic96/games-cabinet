/**
 * Checks the spatial rule of the cabinet's arrow keys with the plain Node test runner: which box each key moves to, given the
 * rectangles of the boxes on screen. The script is read from disk and imported through a data URL.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const source = readFileSync(new URL('../../Cabinet.Service/wwwroot/js/keys.js', import.meta.url), 'utf8');
const { nextBox, nearestBox, readingOrder } = await import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));

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

test('down at the bottom of a column carries on at the top of the next section standing beside it, and up comes back', () => {
  const rects = [
    box(0, 100, 40, 200, '0:1'),
    box(0, 340, 40, 200, '0:0'),
    box(300, 140, 40, 100, '1:1'),
    box(300, 260, 40, 100, '1:0'),
  ];

  assert.equal(nextBox(rects, 1, 'ArrowDown'), 2);
  assert.equal(nextBox(rects, 2, 'ArrowUp'), 1);
  assert.equal(nextBox(rects, 0, 'ArrowDown'), 1);
  assert.equal(nextBox(rects, 2, 'ArrowDown'), 3);
});

test('down in the last section and up in the first section stay where they are', () => {
  const rects = [box(0, 100, 40, 200, '0:0'), box(300, 100, 40, 200, '1:0')];

  assert.equal(nextBox(rects, 1, 'ArrowDown'), 1);
  assert.equal(nextBox(rects, 0, 'ArrowUp'), 0);
});

test('the nearest box is the one that overlaps first, then the one with the smallest gap', () => {
  const from = box(100, 100, 40, 100, '0:0');
  const rects = [box(300, 100, 40, 100, '0:0'), box(150, 100, 40, 100, '0:0'), box(110, 120, 20, 20, '0:0')];

  assert.equal(nearestBox(rects, from), 2);
  assert.equal(nearestBox(rects.slice(0, 2), from), 1);
});

test('the nearest box measures a gap in both directions and settles a tie by the centres', () => {
  const from = box(0, 0, 40, 40, '0:0');
  const rects = [box(200, 0, 40, 40, '0:0'), box(60, 60, 40, 40, '0:0'), box(0, 90, 40, 40, '0:0')];

  assert.equal(nearestBox(rects, from), 1);
  assert.equal(nearestBox([box(0, 100, 40, 40, '0:0'), box(0, 100, 40, 40, '0:0')], from), 0);
});

test('with no candidates the nearest box is -1', () => {
  assert.equal(nearestBox([], box(0, 0, 10, 10, '0:0')), -1);
});

test('reading order goes by top, then by left, and treats tops within two pixels as one row', () => {
  const rects = [box(200, 100, 40, 100, '0:0'), box(0, 101, 40, 90, '0:0'), box(100, 300, 40, 100, '0:1'), box(100, 100, 40, 100, '0:0'), box(0, 300, 40, 100, '0:1')];

  assert.deepEqual(readingOrder(rects), [1, 3, 0, 4, 2]);
});

test('reading order gives the same boxes in the same order however the input is shuffled', () => {
  const rects = [box(200, 100, 40, 100, 'a'), box(0, 101, 40, 90, 'b'), box(100, 300, 40, 100, 'c'), box(100, 100, 40, 100, 'd'), box(0, 300, 40, 100, 'e')];
  const expected = readingOrder(rects).map((index) => rects[index].shelf);
  const shuffles = [[4, 2, 0, 3, 1], [1, 0, 3, 2, 4], [3, 4, 1, 0, 2]];

  for (const shuffle of shuffles) {
    const shuffled = shuffle.map((index) => rects[index]);

    assert.deepEqual(readingOrder(shuffled).map((index) => shuffled[index].shelf), expected);
  }
});

test('reading order keeps a shelf in one row by its top, so a short box left of a tall one still comes first', () => {
  const tall = { ...box(100, 100, 40, 100, '0:0'), shelfTop: 90 };
  const below = { ...box(0, 300, 40, 100, '0:1'), shelfTop: 260 };
  const short = { ...box(0, 150, 40, 50, '0:0'), shelfTop: 90 };
  const pile = { ...box(160, 170, 60, 30, '0:0'), shelfTop: 90 };
  const pileTop = { ...box(160, 140, 60, 30, '0:0'), shelfTop: 90 };

  assert.deepEqual(readingOrder([tall, below, short, pile, pileTop]), [2, 0, 4, 3, 1]);
});

test('reading order of nothing is nothing', () => {
  assert.deepEqual(readingOrder([]), []);
});
