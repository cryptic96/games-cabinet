import { chromium, open, growViewport, switcherUrl, SHEETS } from './lib.mjs';
import { shoot } from './shots.mjs';
import { LOOKS } from './quick.mjs';

/**
 * The second-round look sheets: each look at 1440 and 390 on the 65 sample, with crops of a corner, a shelf front, the
 * base and the feet, and an evening version with Evenings only lit and a crop of the top of an empty lit cubby.
 */
async function crops(browser, id, state, { lit = false } = {}) {
  const page = await open(browser, switcherUrl('switcher.html', state), 1440, 900, 2);
  await page.waitForSelector('html[data-ready]');
  await growViewport(page, 1440);
  await page.waitForTimeout(150);
  const g = await page.evaluate(() => {
    const s = document.querySelector('#cabinet .section');
    const r = s.getBoundingClientRect();
    const get = (n) => parseFloat(getComputedStyle(s).getPropertyValue(n));
    const u = s.querySelector('.section-body').getBoundingClientRect().width / get('--section-w');
    const cubbies = Array.from(s.querySelectorAll('.cubby'));
    const first = cubbies[0].getBoundingClientRect();
    const sorted = cubbies.slice().sort((a, b) => a.getBoundingClientRect().top - b.getBoundingClientRect().top);
    const second = (sorted.find((c) => c.getBoundingClientRect().top > first.top + 40) || sorted[0]).getBoundingClientRect();
    const empty = (cubbies.find((c) => !c.querySelector('.placement')) || cubbies[0]).getBoundingClientRect();
    return { x: r.x + scrollX, y: r.y + scrollY, u, top: get('--top-height'), base: get('--base-height'), floor: get('--floor-margin'), sectionH: get('--section-h'), second: { x: second.x + scrollX, y: second.y + scrollY }, empty: { x: empty.x + scrollX, y: empty.y + scrollY, w: empty.width, h: empty.height } };
  });
  const { x, y, u, top, base, floor, sectionH } = g;
  const clip = (cx, cy, cw, ch) => ({ x: x + cx * u, y: y + cy * u, width: cw * u, height: ch * u });
  const boxes = lit
    ? { litedge: { x: g.empty.x - 30, y: g.empty.y - 20, width: g.empty.w + 60, height: g.empty.h * 0.32 + 30 } }
    : {
        corner: clip(0, 0, 220, 170),
        shelf: { x: g.second.x - 12 * u, y: g.second.y - 44 * u, width: 260 * u, height: 76 * u },
        base: clip(0, top + sectionH - 30, 300, base + floor + 30),
        feet: clip(0, top + sectionH, 360, base + floor),
      };
  if (lit) await page.addStyleTag({ content: '.placement { visibility: hidden; }' });
  for (const [name, box] of Object.entries(boxes)) await page.screenshot({ path: `${SHEETS}/look-${id}-${name}.png`, clip: box });
  await page.context().close();
}

const only = process.argv.slice(2);
const browser = await chromium.launch();
for (const [id, state, label] of LOOKS) {
  if (only.length > 0 && !only.includes(id)) continue;
  const tag = `look-${id}`;
  await shoot(browser, { ...state, out: `${tag}-desktop.jpg`, full: '1', width: 1440 });
  await shoot(browser, { ...state, file: 'switcher-phone.html', out: `${tag}-phone.jpg`, full: '1', width: 390 });
  await crops(browser, id, state);
  const evening = { ...state, lights: 'evenings', evening: '1' };
  await shoot(browser, { ...evening, out: `${tag}-evening-desktop.jpg`, full: '1', width: 1440 });
  await shoot(browser, { ...evening, file: 'switcher-phone.html', out: `${tag}-evening-phone.jpg`, full: '1', width: 390 });
  await crops(browser, id, evening, { lit: true });
  console.log('done', id, label);
}
await browser.close();
