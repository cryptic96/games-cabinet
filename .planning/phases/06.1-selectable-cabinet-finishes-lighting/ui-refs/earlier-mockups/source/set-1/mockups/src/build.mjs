/**
 * Builds one self-contained HTML file per finish from the shared sources and the 65-item layout served by
 * the running app. The engine has no phone section yet, so a narrow 640 mm section is derived here by
 * moving whole desktop cubbies (with their boxes, unchanged) into narrow rows.
 */
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const src = dirname(fileURLToPath(import.meta.url));
const out = join(src, '..');
const read = (name) => readFileSync(join(src, name), 'utf8');

const desktop = JSON.parse(read('layout-65.json'));

/** Narrow design: rows of [height, [widths]] in millimetres, 640 mm interior, 20 mm boards. */
const PHONE_ROWS = [
  [360, [400, 220]],
  [360, [640]],
  [300, [260, 360]],
  [300, [200, 420]],
  [400, [460, 160]],
  [400, [420, 200]],
];

/** Which desktop cubby fills each phone cubby, as [desktop section, desktop cubby] or null for empty. */
const PHONE_FILL = [
  [[0, 0], [0, 1], [0, 2], [0, 3], [0, 4], [0, 5], [0, 6], [0, 7], null, [0, 9], null],
  [[0, 14], [1, 0], null, null, [0, 10], null, null, [0, 8], null, null, null],
];

/**
 * Builds one narrow section.
 * @param {number} index The section index.
 * @param {Array} fill The source cubby for each phone cubby.
 * @returns {object}
 */
function phoneSection(index, fill) {
  const frame = 20;
  const cubbies = [];
  let y = 0;
  for (const [height, widths] of PHONE_ROWS) {
    let x = 0;
    for (const width of widths) {
      const source = fill[cubbies.length];
      const placements = source ? desktop.sections[source[0]].cubbies[source[1]].placements : [];
      for (const p of placements) {
        if (p.xMm + p.widthMm > width || p.yMm + p.heightMm > height) {
          throw new Error(`cubby ${cubbies.length} of phone section ${index} is too small for its boxes`);
        }
      }
      cubbies.push({ index: cubbies.length, xMm: x, yMm: y, widthMm: width, heightMm: height, placements });
      x += width + frame;
    }
    y += height + frame;
  }
  return { index, widthMm: 640, heightMm: y - frame, frameMm: frame, cubbies };
}

const phone = { layoutVersion: desktop.layoutVersion, profile: 'phone', sections: PHONE_FILL.map((fill, i) => phoneSection(i, fill)) };

const FINISHES = [
  ['planned', 'Planned finish (the design contract as written)'],
  ['birch', 'Option A, modern birch plywood'],
  ['classic', 'Option B, classic cabinet in the current colours'],
  ['walnut', 'Option C, walnut display cabinet with lit cubbies'],
];

const shared = read('structure.css') + '\n' + read('boxes.css');
const renderer = read('render.js');

for (const [key, label] of FINISHES) {
  const html = `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Games Cabinet mockup: ${label}</title>
<style>
${shared}
${read(`finish-${key}.css`)}
</style>
</head>
<body>
<header class="page-header">
<h1>Games Cabinet</h1>
<p>Invented collection of 65 items.</p>
<p class="mockup-tag">Mockup: ${label}</p>
</header>
<main><div id="cabinet" class="cabinet"></div></main>
<script>window.CABINET_LAYOUT = ${JSON.stringify(desktop)}; window.CABINET_LAYOUT_PHONE = ${JSON.stringify(phone)};</script>
<script>
${renderer}
</script>
</body>
</html>
`;
  writeFileSync(join(out, `option-${key}.html`), html);
  console.log(`wrote option-${key}.html`);
}
