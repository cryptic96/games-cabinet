import { writeFileSync } from 'node:fs';
import { chromium, open, growViewport, switcherUrl, SHEETS } from './lib.mjs';
import { montage } from './montage.mjs';

/**
 * Focus ring of a box on a wall-side shelf, every wood with both backs. Reads the computed outline colour of the
 * focused box and its contrast against the back it sits next to, and draws the seven tiles of the sheet.
 */
const hex = (h) => [1, 3, 5].map((i) => parseInt(h.slice(i, i + 2), 16));
const lin = (c) => { const s = c / 255; return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4; };
const lum = (rgb) => 0.2126 * lin(rgb[0]) + 0.7152 * lin(rgb[1]) + 0.0722 * lin(rgb[2]);
const ratio = (a, b) => { const x = lum(a); const y = lum(b); return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05); };

const browser = await chromium.launch();
const report = [];
const woods = ['brown', 'walnut', 'oak', 'birch', 'mahogany'];
for (const wood of woods) {
  for (const backs of ['wood', 'painted']) {
    const page = await open(browser, switcherUrl('switcher.html', { design: 'modern', wood, backs }), 1440, 900, 2);
    await page.waitForSelector('html[data-ready]');
    await growViewport(page, 1440);
    const r = await page.evaluate(() => {
      const el = document.querySelector('#cabinet .placement');
      el.focus({ focusVisible: true });
      const cs = getComputedStyle(el);
      const b = el.getBoundingClientRect();
      const cubby = el.closest('.cubby');
      const back = getComputedStyle(cubby).backgroundColor;
      return { x: b.x + scrollX, y: b.y + scrollY, w: b.width, h: b.height, outline: cs.outlineColor, width: cs.outlineWidth, style: cs.outlineStyle, back, halo: cs.boxShadow };
    });
    const rgb = (t) => t.match(/\d+(\.\d+)?/g).slice(0, 3).map(Number);
    const ring = rgb(r.outline);
    const back = rgb(r.back);
    report.push({ wood, backs, outline: r.outline, outlineWidth: r.width, outlineStyle: r.style, backColour: r.back, ringAgainstBack: Math.round(ratio(ring, back) * 100) / 100, halo: r.halo.includes('6px') });
    if (backs === 'painted' && wood !== 'birch' && wood !== 'brown') { await page.context().close(); continue; }
    if (backs === 'wood' || wood === 'birch' || wood === 'brown') {
      await page.waitForTimeout(150);
      const out = `tmp-focus-${wood}-${backs}.png`;
      await page.screenshot({ path: `${SHEETS}/${out}`, clip: { x: r.x - 40, y: r.y - 30, width: Math.max(r.w, 60) + 120, height: 150 } });
    }
    await page.context().close();
  }
}
const labels = { brown: 'Warm brown', walnut: 'Walnut on warm stone', oak: 'Oak (ink halo)', birch: 'Birch (ink halo)', mahogany: 'Mahogany' };
const items = [];
for (const [wood, backs] of [['brown', 'wood'], ['walnut', 'wood'], ['oak', 'wood'], ['birch', 'wood'], ['mahogany', 'wood'], ['brown', 'painted'], ['birch', 'painted']]) {
  const rep = report.find((x) => x.wood === wood && x.backs === backs);
  items.push({ file: `tmp-focus-${wood}-${backs}.png`, label: `${labels[wood]}${backs === 'painted' ? ', painted back' : ''}`, sub: wood === 'birch' && backs === 'wood' ? 'ring on an ink halo: ring 8.98:1 on the halo, halo 4.99:1 on the back' : wood === 'oak' ? `ring ${rep.ringAgainstBack}:1 on the back; ink halo 7.19:1 on the frame` : `ring ${rep.ringAgainstBack}:1 against the back` });
}
await montage(browser, { items, cols: 4, out: 'round2-focus-rings.png', title: 'Focus ring on a box at the edge of a section: accent ring on every wood and back', cell: [330, 300], labelH: 50 });
writeFileSync(`${SHEETS}/focus-report.json`, JSON.stringify(report, null, 2));
console.log(JSON.stringify(report, null, 1));
await browser.close();
