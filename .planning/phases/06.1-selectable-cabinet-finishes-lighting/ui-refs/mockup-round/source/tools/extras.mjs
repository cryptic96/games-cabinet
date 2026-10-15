import { readFileSync } from 'node:fs';
import { chromium, SHEETS } from './lib.mjs';
import { open, growViewport, switcherUrl } from './lib.mjs';

async function emptyCubbyShot(browser, state, out) {
  const page = await open(browser, switcherUrl('switcher.html', state), 1440, 900, 2);
  await page.waitForSelector('html[data-ready]');
  await growViewport(page, 1440);
  const r = await page.evaluate(() => {
    const all = Array.from(document.querySelectorAll('#cabinet .cubby')).filter((c) => !c.querySelector('.placement'));
    const c = all[0].getBoundingClientRect();
    return { x: c.x + window.scrollX, y: c.y + window.scrollY, w: c.width, h: c.height };
  });
  await page.screenshot({ path: SHEETS + '/' + out, clip: { x: r.x - 40, y: r.y - 40, width: r.w + 80, height: r.h + 80 } });
  await page.context().close();
}
import { montage } from './montage.mjs';

/**
 * The depth strip and the baseline sheet.
 */
const browser = await chromium.launch();

const states = [
  ['flat', { design: 'classic', wood: 'brown', depth: 'flat' }, 'Flat', 'No shading at all; the planks stay'],
  ['shaded', { design: 'classic', wood: 'brown', depth: 'shaded' }, 'Shaded (today)', 'Shelf shadow, side shade, plank seams'],
  ['deep', { design: 'classic', wood: 'brown', depth: 'deep' }, 'Deep', 'Inner walls, floor, lit top face on each shelf'],
  ['deeplit', { design: 'classic', wood: 'brown', depth: 'deep', lights: 'on' }, 'Deep + Always on', 'The same with the strip lights lit'],
];
const items = [];
for (const [id, state, label, sub] of states) {
  const out = 'tmp-depth-' + id + '.png';
  await emptyCubbyShot(browser, state, out);
  items.push({ file: out, label, sub });
}
await montage(browser, { items, cols: 4, out: 'depth-strip.png', title: 'Depth: Flat, Shaded and Deep on Classic + warm brown, an empty cubby and its shelf', cell: [480, 440], labelH: 50 });

const stat = JSON.parse(readFileSync(`${SHEETS}/baseline-results.json`, 'utf8')).find((r) => r.sample === '65' && r.viewport === 1440);
await montage(browser, {
  items: [
    { file: 'baseline-release-65-1440.png', label: 'Release stylesheet', sub: 'Classic + warm brown, as shipped' },
    { file: 'baseline-layers-65-1440.png', label: 'Layered stylesheets', sub: 'The same markup, four ordered layers' },
    { file: 'baseline-mask-65-1440.png', label: `Pixel difference: ${stat.count} differing pixels`, sub: 'Black means identical; any other colour would mark a difference' },
  ],
  cols: 3,
  out: 'baseline-sheet.png',
  title: 'Baseline: Classic + warm brown, sample 65 at 1440 px wide',
  cell: [560, 440],
  labelH: 50,
});
await browser.close();
