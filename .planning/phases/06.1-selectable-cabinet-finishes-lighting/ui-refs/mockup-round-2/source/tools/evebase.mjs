import { pathToFileURL } from 'node:url';
import { chromium, open, growViewport, S, SHEETS } from './lib.mjs';
import { montage } from './montage.mjs';

/**
 * The base of one piece in the evening, before and after the second-round critic fixes: the same crop of the first
 * section's base and the floor in front of it, drawn from the stylesheets as they were and as they are now. Pass the
 * folder that holds the earlier build as the first argument.
 */
const before = process.argv[2];
const OUT_NOW = `${S}/round2/out`;
const LOOKS = [['L4', { design: 'legs', wood: 'oak' }, 'On legs + oak'], ['L5', { design: 'library', wood: 'mahogany' }, 'Library + mahogany'], ['L6', { design: 'plywood-legs', wood: 'birch' }, 'Plywood on legs + birch'], ['L3', { design: 'display', wood: 'walnut' }, 'Display + walnut on stone']];
const browser = await chromium.launch();
const items = [];
let n = 0;
for (const [id, state, label] of LOOKS) {
  for (const [dir, tag] of [[before, 'before the fixes'], [OUT_NOW, 'now']]) {
    const hash = Object.entries({ settle: '0', bare: '1', ...state, lights: 'evenings', evening: '1' }).map(([k, v]) => `${k}=${v}`).join('&');
    const page = await open(browser, pathToFileURL(`${dir}/switcher.html`).href + '#' + hash, 1440, 900, 2);
    await page.waitForSelector('html[data-ready]');
    await growViewport(page, 1440);
    await page.waitForTimeout(150);
    const g = await page.evaluate(() => {
      const s = document.querySelector('#cabinet .section');
      const r = s.getBoundingClientRect();
      const get = (k) => parseFloat(getComputedStyle(s).getPropertyValue(k));
      const u = s.querySelector('.section-body').getBoundingClientRect().width / get('--section-w');
      return { x: r.x + scrollX, y: r.y + scrollY, u, top: get('--top-height'), base: get('--base-height'), floor: get('--floor-margin'), sectionH: get('--section-h') };
    });
    const out = `tmp-evebase-${n++}.png`;
    await page.screenshot({ path: `${SHEETS}/${out}`, clip: { x: g.x, y: g.y + (g.top + g.sectionH - 30) * g.u, width: 420 * g.u, height: (g.base + g.floor + 30) * g.u } });
    await page.context().close();
    items.push({ file: out, label: `${id} ${label}`, sub: `evening, ${tag}` });
  }
}
await montage(browser, { items, cols: 2, out: 'round2-evening-bases.png', title: 'The base in the evening, before and after the fixes', cell: [560, 300], labelH: 56 });
await browser.close();
