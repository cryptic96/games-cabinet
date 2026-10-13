/**
 * Detail crops at device scale factor 2 for each mockup: a top corner, a shelf and divider joint with
 * boxes, the base and floor, and empty cubbies. Regions are fractions of the section boxes, so the same
 * part of the cabinet is cropped in every option.
 *
 * Usage: PW_DIR=<playwright dir> node crops.mjs <out dir> <name=file.html> [...]
 */
import { createRequire } from 'node:module';
import { join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const pwDir = resolve(process.env.PW_DIR);
process.env.PLAYWRIGHT_BROWSERS_PATH ??= join(pwDir, 'browsers');
const { chromium } = createRequire(join(pwDir, 'package.json'))('playwright');

const [outDir, ...targets] = process.argv.slice(2);

/** Region name, section index, then x0, y0, x1, y1 as fractions of that section's box. */
const REGIONS = [
  ['corner', 0, 0, 0, 0.42, 0.24],
  ['joint', 0, 0.28, 0.27, 0.72, 0.5],
  ['base', 0, 0, 0.8, 0.5, 1],
  ['empty', 1, 0.1, 0.33, 0.75, 0.62],
];

const browser = await chromium.launch();
for (const target of targets) {
  const [name, file] = target.split('=');
  const page = await browser.newPage({ viewport: { width: 1440, height: 1200 }, deviceScaleFactor: 2 });
  await page.goto(pathToFileURL(resolve(file)).href);
  await page.waitForSelector('.section');
  await page.waitForTimeout(200);
  const boxes = await page.$$eval('.section', (els) => els.map((el) => {
    const r = el.getBoundingClientRect();
    return { x: r.left + window.scrollX, y: r.top + window.scrollY, w: r.width, h: r.height };
  }));
  for (const [region, index, x0, y0, x1, y1] of REGIONS) {
    const b = boxes[index];
    const clip = { x: b.x + x0 * b.w, y: b.y + y0 * b.h, width: (x1 - x0) * b.w, height: (y1 - y0) * b.h };
    await page.screenshot({ path: join(outDir, `${name}-detail-${region}.png`), clip, fullPage: true });
  }
  await page.close();
  console.log(`cropped ${name}`);
}
await browser.close();
