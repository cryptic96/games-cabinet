import { writeFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';
import { chromium, S, OUT } from './lib.mjs';

/**
 * Box-top edge contrast on a real sample page. For every box the boxes are hidden, the back is read 3 px above the top
 * edge, and the box colour (after the shade that lies over the top of the cubby) is compared with it. The same box is
 * also given an edge value with the 1 px keyline row that sits right above it when the lights are on (the back darkened
 * by the keyline alpha): the larger of box against back and the weaker side of the line against back and box. Counts are boxes under 1.3 and under 1.15 of all boxes. Run with the sample page and width as arguments.
 */
const file = process.argv[2] || 'switcher-400.html';
const width = Number(process.argv[3] || 1440);
const sampleName = process.argv[4] || '400';
const lin = (c) => { const s = c / 255; return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4; };
const lum = (rgb) => 0.2126 * lin(rgb[0]) + 0.7152 * lin(rgb[1]) + 0.0722 * lin(rgb[2]);
const ratio = (a, b) => { const x = lum(a); const y = lum(b); return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05); };
const blend = (base, over, alpha) => base.map((c, i) => c * (1 - alpha) + over[i] * alpha);
const BACKS = [['brown', 'wood', 'Warm brown'], ['walnut', 'wood', 'Walnut'], ['oak', 'wood', 'Oak'], ['birch', 'wood', 'Birch'], ['mahogany', 'wood', 'Mahogany'], ['brown', 'painted', 'Painted on brown'], ['birch', 'painted', 'Painted on birch']];
const browser = await chromium.launch();
const results = {};
const q = (arr, p) => arr[Math.floor(p * (arr.length - 1))];

for (const depth of (process.env.DEPTHS || 'shaded').split(',')) {
  for (const [wood, backs, label] of BACKS) {
    const row = {};
    for (const lights of ['off', 'on']) {
      const ctx = await browser.newContext({ viewport: { width, height: 900 }, deviceScaleFactor: 1 });
      const page = await ctx.newPage();
      const hash = Object.entries({ settle: '0', bare: '1', design: 'modern', wood, backs, depth, lights }).map(([k, v]) => k + '=' + v).join('&');
      await page.goto(pathToFileURL(`${OUT}/${file}`).href + '#' + hash, { waitUntil: 'load' });
      await page.waitForSelector('html[data-ready]');
      const h = await page.evaluate(() => document.documentElement.scrollHeight);
      await page.setViewportSize({ width, height: h });
      await page.waitForTimeout(200);
      const info = await page.evaluate(() => {
        const out = [];
        for (const c of document.querySelectorAll('#cabinet .cubby')) {
          const cr = c.getBoundingClientRect();
          const cs = getComputedStyle(c);
          const shadeA = parseFloat(cs.getPropertyValue('--shade-alpha')) || 0;
          const deepRgb = cs.getPropertyValue('--box-shade-rgb').trim().split(/\s+/).map(Number);
          const keyline = parseFloat(getComputedStyle(c.querySelector('.placement') || c).getPropertyValue('--keyline')) || 0;
          for (const p of c.querySelectorAll('.placement')) {
            const r = p.getBoundingClientRect();
            const bg = getComputedStyle(p).backgroundColor.match(/\d+/g).slice(0, 3).map(Number);
            out.push({ cx: r.x + r.width / 2, w: r.width, top: r.y + window.scrollY, ctop: cr.y + window.scrollY, ch: cr.height, bg, shadeA, deepRgb, keyline });
          }
        }
        return out;
      });
      await page.addStyleTag({ content: '.placement{visibility:hidden !important}' });
      await page.waitForTimeout(100);
      const png = await page.screenshot({ fullPage: true });
      const vals = await page.evaluate(async ([data, info]) => {
        const img = await new Promise((r) => { const i = new Image(); i.onload = () => r(i); i.src = 'data:image/png;base64,' + data; });
        const cv = document.createElement('canvas'); cv.width = img.width; cv.height = img.height;
        const g = cv.getContext('2d'); g.drawImage(img, 0, 0);
        return info.map((b) => {
          const y = Math.round(b.top) - 3;
          const x0 = Math.round(b.cx - Math.max(1, b.w * 0.3)); const w = Math.max(2, Math.round(b.w * 0.6));
          const d = g.getImageData(x0, y, w, 2).data; const s = [0, 0, 0]; let n = 0;
          for (let i = 0; i < d.length; i += 4) { s[0] += d[i]; s[1] += d[i + 1]; s[2] += d[i + 2]; n++; }
          return s.map((v) => v / n);
        });
      }, [png.toString('base64'), info]);
      const plain = [];
      const keyed = [];
      info.forEach((b, i) => {
        const t = (b.top - b.ctop) / b.ch;
        const a = t < 0.2 ? b.shadeA * (1 - t / 0.2) : 0;
        const box = blend(b.bg, [20, 10, 4], a);
        plain.push(ratio(box, vals[i]));
        const line = blend(vals[i], b.deepRgb, b.keyline);
        keyed.push(Math.max(ratio(box, vals[i]), Math.min(ratio(line, vals[i]), ratio(line, box))));
      });
      const stat = (arr) => { const s = [...arr].sort((x, y) => x - y); return { n: s.length, under13: s.filter((c) => c < 1.3).length, under115: s.filter((c) => c < 1.15).length, min: +s[0].toFixed(2), p10: +q(s, 0.1).toFixed(2), median: +q(s, 0.5).toFixed(2) }; };
      row[lights] = { withoutKeyline: stat(plain), withKeyline: stat(keyed) };
      await ctx.close();
    }
    results[`${label}/${depth}`] = row;
    console.log(label, depth, JSON.stringify(row));
  }
}
await browser.close();
writeFileSync(`${S}/round2/measurements-boxtops-${sampleName}.json`, JSON.stringify(results, null, 2));
