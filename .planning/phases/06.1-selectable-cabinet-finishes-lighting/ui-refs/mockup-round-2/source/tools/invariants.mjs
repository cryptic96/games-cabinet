import { writeFileSync } from 'node:fs';
import { chromium, open, growViewport, switcherUrl, S } from './lib.mjs';

/**
 * Checks the structural rules on the real layers: section boxes never change with wood, backs, depth or lights; nothing
 * paints outside a section box (the one-pixel ring at the box edge matches the wall just outside it); no horizontal
 * overflow at 1440 and 390; no console error.
 */
const DESIGNS = ['classic', 'modern', 'display', 'legs', 'library', 'plywood', 'plywood-legs'];
const WOODS = ['brown', 'walnut', 'oak', 'birch', 'mahogany'];
const browser = await chromium.launch();
const report = { geometry: {}, containment: [], overflow: [], errors: [] };

for (const design of DESIGNS) {
  const page = await open(browser, switcherUrl('switcher.html', { design }), 1440, 900, 1);
  await page.waitForSelector('html[data-ready]');
  const rects = () => page.evaluate(() => Array.from(document.querySelectorAll('#cabinet .section')).map((s) => { const r = s.getBoundingClientRect(); return [r.x, r.y, r.width, r.height].map((n) => Math.round(n * 100) / 100).join(','); }).join('|'));
  const first = await rects();
  let combos = 0;
  let broken = 0;
  for (const wood of WOODS) for (const backs of ['wood', 'painted']) for (const depth of ['flat', 'shaded', 'deep']) for (const lights of ['off', 'on', 'evenings']) for (const deepvar of ['merged', 'mitre']) {
    await page.evaluate(([w, b, d, l, v]) => {
      document.documentElement.setAttribute('data-deepvar', v);
      const r = document.documentElement;
      r.setAttribute('data-wood', w); r.setAttribute('data-backs', b); r.setAttribute('data-depth', d); r.setAttribute('data-lights', l);
      if (l !== 'off') r.setAttribute('data-lit', ''); else r.removeAttribute('data-lit');
    }, [wood, backs, depth, lights, deepvar]);
    combos++;
    if ((await rects()) !== first) broken++;
  }
  report.geometry[design] = { combos, broken };
  await page.context().close();
}

async function ringCheck(design, wood) {
  const page = await open(browser, switcherUrl('switcher.html', { design, wood, lights: 'on', depth: 'deep' }), 1440, 900, 1);
  await page.waitForSelector('html[data-ready]');
  await growViewport(page, 1440);
  const box = await page.evaluate(() => { const r = document.querySelector('#cabinet .section').getBoundingClientRect(); return { x: r.x + scrollX, y: r.y + scrollY, w: r.width, h: r.height }; });
  const m = 6;
  const clip = { x: Math.floor(box.x) - m, y: Math.floor(box.y) - m, width: Math.ceil(box.w) + 2 * m, height: Math.ceil(box.h) + 2 * m };
  const png = await page.screenshot({ clip });
  const p2 = await browser.newPage();
  const res = await p2.evaluate(async ([d, c, b, mm]) => {
    const img = await new Promise((resolve) => { const i = new Image(); i.onload = () => resolve(i); i.src = 'data:image/png;base64,' + d; });
    const cv = document.createElement('canvas'); cv.width = img.width; cv.height = img.height;
    const g = cv.getContext('2d'); g.drawImage(img, 0, 0);
    const px = (x, y) => Array.from(g.getImageData(x, y, 1, 1).data).slice(0, 3);
    const left = Math.round(b.x) - c.x; const top = Math.round(b.y) - c.y;
    const right = left + Math.round(b.w) - 1; const bottom = top + Math.round(b.h) - 1;
    let worst = 0; let where = '';
    const cmp = (inside, outside, label) => { const dlt = Math.max(...inside.map((v, i) => Math.abs(v - outside[i]))); if (dlt > worst) { worst = dlt; where = label; } };
    for (let x = left + 1; x < right; x += 3) { cmp(px(x, bottom), px(x, bottom + 3), 'bottom'); }
    for (let y = top + 1; y < bottom; y += 3) { cmp(px(left, y), px(left - 3, y), 'left'); cmp(px(right, y), px(right + 3, y), 'right'); }
    return { worst, where };
  }, [png.toString('base64'), clip, box, m]);
  await p2.close();
  await page.context().close();
  return res;
}
for (const design of DESIGNS) for (const wood of WOODS) {
  const r = await ringCheck(design, wood);
  report.containment.push({ design, wood, ...r });
}

for (const [file, width] of [['switcher.html', 1440], ['switcher-phone.html', 390], ['switcher-400.html', 1440], ['switcher-400-phone.html', 390]]) {
  for (const design of DESIGNS) {
    const page = await open(browser, switcherUrl(file, { design, wood: 'oak', depth: 'deep', lights: 'on' }), width, 900, 1);
    await page.waitForSelector('html[data-ready]');
    const o = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth);
    if (o) report.overflow.push(`${file} ${design}`);
    report.errors.push(...page.errors);
    await page.context().close();
  }
}
await browser.close();
writeFileSync(`${S}/round2/invariants.json`, JSON.stringify(report, null, 2));
console.log('geometry', JSON.stringify(report.geometry));
console.log('containment worst per pair above 3:', JSON.stringify(report.containment.filter((c) => c.worst > 3)));
console.log('containment max', Math.max(...report.containment.map((c) => c.worst)));
console.log('overflow', report.overflow, 'errors', report.errors);
