import { chromium, open, growViewport, switcherUrl } from './lib.mjs';

const [design, wood, lights = 'on', depth = 'deep'] = process.argv.slice(2);
const browser = await chromium.launch();
const page = await open(browser, switcherUrl('switcher.html', { design, wood, lights, depth }), 1440, 900, 1);
await page.waitForSelector('html[data-ready]');
await growViewport(page, 1440);
const box = await page.evaluate(() => { const r = document.querySelector('#cabinet .section').getBoundingClientRect(); return { x: r.x + scrollX, y: r.y + scrollY, w: r.width, h: r.height }; });
const clip = { x: Math.floor(box.x) - 6, y: Math.floor(box.y) - 6, width: Math.ceil(box.w) + 12, height: Math.ceil(box.h) + 12 };
const png = await page.screenshot({ clip });
const p2 = await browser.newPage();
const res = await p2.evaluate(async ([d, c, b]) => {
  const img = await new Promise((resolve) => { const i = new Image(); i.onload = () => resolve(i); i.src = 'data:image/png;base64,' + d; });
  const cv = document.createElement('canvas'); cv.width = img.width; cv.height = img.height;
  const g = cv.getContext('2d'); g.drawImage(img, 0, 0);
  const px = (x, y) => Array.from(g.getImageData(x, y, 1, 1).data).slice(0, 3);
  const left = Math.round(b.x) - c.x; const top = Math.round(b.y) - c.y;
  const right = left + Math.round(b.w) - 1; const bottom = top + Math.round(b.h) - 1;
  const out = [];
  for (let x = left + 1; x < right; x += 3) {
    const a = px(x, bottom); const o = px(x, bottom + 3);
    const dlt = Math.max(...a.map((v, i) => Math.abs(v - o[i])));
    if (dlt > 3) out.push({ x: x - left, dlt, a, o });
  }
  return { out: out.slice(0, 12), count: out.length, width: right - left };
}, [png.toString('base64'), clip, box]);
console.log(JSON.stringify(res));
await browser.close();
