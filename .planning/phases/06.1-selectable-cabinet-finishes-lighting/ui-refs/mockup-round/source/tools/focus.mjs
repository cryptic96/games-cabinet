import { chromium, open, growViewport, switcherUrl, SHEETS } from './lib.mjs';
import { montage } from './montage.mjs';

/**
 * Focus ring of a box on a wall-side shelf for each wood: the accent ring and, on oak and birch, the ink halo.
 */
const browser = await chromium.launch();
const items = [];
for (const [wood, backs, label] of [['brown', 'wood', 'Warm brown'], ['oak', 'wood', 'Oak (ink halo)'], ['birch', 'wood', 'Birch (ink halo)'], ['birch', 'painted', 'Birch, painted back']]) {
  const page = await open(browser, switcherUrl('switcher.html', { design: 'modern', wood, backs }), 1440, 900, 2);
  await page.waitForSelector('html[data-ready]');
  await growViewport(page, 1440);
  const r = await page.evaluate(() => {
    const el = document.querySelector('#cabinet .placement');
    el.focus({ focusVisible: true });
    const b = el.getBoundingClientRect();
    return { x: b.x + scrollX, y: b.y + scrollY, w: b.width, h: b.height };
  });
  await page.waitForTimeout(150);
  const out = `tmp-focus-${wood}-${backs}.png`;
  await page.screenshot({ path: `${SHEETS}/${out}`, clip: { x: r.x - 40, y: r.y - 30, width: Math.max(r.w, 60) + 120, height: 150 } });
  items.push({ file: out, label });
  await page.context().close();
}
await montage(browser, { items, cols: 4, out: 'focus-rings.png', title: 'Focus ring on a box at the edge of a section', cell: [330, 300], labelH: 34 });
await browser.close();
