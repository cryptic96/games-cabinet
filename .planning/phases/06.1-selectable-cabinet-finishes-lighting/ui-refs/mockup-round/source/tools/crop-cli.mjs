import { chromium, readFileSync, writeFileSync } from './lib.mjs';

/**
 * Crops a region of a PNG and scales it up without smoothing. Arguments: input, output, x, y, width, height, zoom.
 */
const [inPath, outPath, x, y, w, h, z] = process.argv.slice(2);
const browser = await chromium.launch();
const data = readFileSync(inPath).toString('base64');
const page = await browser.newPage();
const url = await page.evaluate(async ([d, cx, cy, cw, ch, zoom]) => {
  const img = await new Promise((resolve) => {
    const i = new Image();
    i.onload = () => resolve(i);
    i.src = 'data:image/png;base64,' + d;
  });
  const c = document.createElement('canvas');
  c.width = cw * zoom;
  c.height = ch * zoom;
  const g = c.getContext('2d');
  g.imageSmoothingEnabled = false;
  g.drawImage(img, cx, cy, cw, ch, 0, 0, cw * zoom, ch * zoom);
  return c.toDataURL('image/png');
}, [data, +x, +y, +w, +h, +z || 1]);
writeFileSync(outPath, Buffer.from(url.split(',')[1], 'base64'));
await browser.close();
