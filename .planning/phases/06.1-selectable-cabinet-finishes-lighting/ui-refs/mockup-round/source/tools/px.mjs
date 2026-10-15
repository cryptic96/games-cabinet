import { chromium, readFileSync } from './lib.mjs';

/**
 * Prints the colour of chosen pixels of one or two PNGs. Arguments: a PNG, a second PNG or "-", then x,y pairs.
 */
const [a, b, ...points] = process.argv.slice(2);
const browser = await chromium.launch();
const page = await browser.newPage();
const files = [a].concat(b === '-' ? [] : [b]);
const datas = files.map((f) => readFileSync(f).toString('base64'));
const out = await page.evaluate(async ([ds, pts]) => {
  const results = [];
  for (const d of ds) {
    const img = await new Promise((resolve) => {
      const i = new Image();
      i.onload = () => resolve(i);
      i.src = 'data:image/png;base64,' + d;
    });
    const c = document.createElement('canvas');
    c.width = img.width;
    c.height = img.height;
    const g = c.getContext('2d');
    g.drawImage(img, 0, 0);
    results.push(pts.map((p) => {
      const [x, y] = p.split(',').map(Number);
      return Array.from(g.getImageData(x, y, 1, 1).data).slice(0, 3).join(' ');
    }));
  }
  return results;
}, [datas, points]);
for (let i = 0; i < points.length; i++) {
  console.log(points[i], out.map((r) => r[i]).join('  |  '));
}
await browser.close();
