import { writeFileSync } from 'node:fs';
import { chromium, open, switcherUrl, S } from './lib.mjs';

/**
 * Kick tokens against the wood front and the wall, read from the computed colours of the wood layer: luminance of the
 * recess and recess-low tokens, the contrast of the kick against the wall and the lightness step against the wood.
 */
const lin = (c) => { const s = c / 255; return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4; };
const lum = (rgb) => 0.2126 * lin(rgb[0]) + 0.7152 * lin(rgb[1]) + 0.0722 * lin(rgb[2]);
const ratio = (a, b) => { const x = lum(a); const y = lum(b); return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05); };
const browser = await chromium.launch();
const out = {};
for (const wood of ['brown', 'walnut', 'oak', 'birch', 'mahogany']) {
  const page = await open(browser, switcherUrl('switcher.html', { design: 'modern', wood }), 1440);
  await page.waitForSelector('html[data-ready]');
  const v = await page.evaluate(() => {
    const root = document.documentElement;
    const probe = document.createElement('div');
    root.appendChild(probe);
    const cv = document.createElement('canvas'); cv.width = 1; cv.height = 1; const g = cv.getContext('2d', { willReadFrequently: true });
    const read = (name) => { probe.style.backgroundColor = `var(${name})`; g.clearRect(0, 0, 1, 1); g.fillStyle = getComputedStyle(probe).backgroundColor; g.fillRect(0, 0, 1, 1); const d = g.getImageData(0, 0, 1, 1).data; return `rgb(${d[0]}, ${d[1]}, ${d[2]})`; };
    const r = { wood: read('--wood'), recess: read('--recess'), recessLow: read('--recess-low'), wall: read('--room') };
    probe.remove();
    return r;
  });
  const rgb = (t) => { const m = t.match(/[\d.]+/g).map(Number); return t.startsWith('color(') ? m.slice(1, 4).map((x) => Math.round(x * 255)) : m.slice(0, 3); };
  const w = rgb(v.wood); const k = rgb(v.recess); const kl = rgb(v.recessLow); const wall = rgb(v.wall);
  out[wood] = { wood: v.wood, recess: v.recess, recessLow: v.recessLow, wallLuminance: +lum(wall).toFixed(3), recessLuminance: +lum(k).toFixed(4), recessLowLuminance: +lum(kl).toFixed(4), kickAgainstWall: +ratio(k, wall).toFixed(2), woodOverKick: +ratio(w, k).toFixed(2), woodLuminance: +lum(w).toFixed(3) };
  console.log(wood, JSON.stringify(out[wood]));
  await page.context().close();
}
await browser.close();
writeFileSync(`${S}/round2/measurements-kicks.json`, JSON.stringify(out, null, 2));
