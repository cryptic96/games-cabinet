/**
 * Shared helpers for the scratch round scripts: a Chromium launcher under the DejaVu font setup, full-height element
 * shots and an in-browser pixel comparison, so no image library is needed.
 */
import { createRequire } from 'node:module';
import { pathToFileURL } from 'node:url';
import { readFileSync, writeFileSync } from 'node:fs';
export { readFileSync, writeFileSync };

export const S = process.env.ROUND_SCRATCH;
process.env.PLAYWRIGHT_BROWSERS_PATH = S + '/pw/browsers';
process.env.FONTCONFIG_FILE = S + '/dejavu.conf';
const require = createRequire(S + '/pw/package.json');
export const { chromium } = require('playwright');

export const OUT = S + '/round2/out';
export const SHEETS = S + '/round2/sheets';
export const BASE = 'http://127.0.0.1:5391';

export function switcherUrl(file, state) {
  const parts = Object.entries({ settle: '0', bare: '1', ...state }).map(([k, v]) => `${k}=${v}`);
  return pathToFileURL(`${OUT}/${file}`).href + '#' + parts.join('&');
}

export async function open(browser, url, width, height = 900, scale = 1) {
  const context = await browser.newContext({ viewport: { width, height }, deviceScaleFactor: scale });
  const page = await context.newPage();
  const errors = [];
  page.on('console', (m) => {
    if (m.type() === 'error') errors.push(m.text());
  });
  page.on('pageerror', (e) => errors.push(String(e)));
  await page.goto(url, { waitUntil: 'load' });
  await page.evaluate(() => document.fonts.ready);
  page.errors = errors;
  return page;
}

export async function growViewport(page, width) {
  const h = await page.evaluate(() => Math.ceil(document.documentElement.scrollHeight));
  await page.setViewportSize({ width, height: Math.max(h, 600) });
  await page.waitForTimeout(150);
}

export async function diffPngs(browser, aPath, bPath, maskPath) {
  const a = readFileSync(aPath).toString('base64');
  const b = readFileSync(bPath).toString('base64');
  const page = await browser.newPage();
  const result = await page.evaluate(async ([x, y]) => {
    const load = (data) => new Promise((resolve) => {
      const img = new Image();
      img.onload = () => resolve(img);
      img.src = 'data:image/png;base64,' + data;
    });
    const [ia, ib] = await Promise.all([load(x), load(y)]);
    if (ia.width !== ib.width || ia.height !== ib.height) {
      return { same: false, reason: `size ${ia.width}x${ia.height} against ${ib.width}x${ib.height}` };
    }
    const grab = (img) => {
      const c = document.createElement('canvas');
      c.width = img.width;
      c.height = img.height;
      const g = c.getContext('2d');
      g.drawImage(img, 0, 0);
      return g.getImageData(0, 0, c.width, c.height).data;
    };
    const da = grab(ia);
    const db = grab(ib);
    let count = 0;
    let maxDelta = 0;
    let first = null;
    for (let i = 0; i < da.length; i += 4) {
      const d = Math.max(Math.abs(da[i] - db[i]), Math.abs(da[i + 1] - db[i + 1]), Math.abs(da[i + 2] - db[i + 2]), Math.abs(da[i + 3] - db[i + 3]));
      if (d > 0) {
        count++;
        if (d > maxDelta) maxDelta = d;
        if (first === null) first = { x: (i / 4) % ia.width, y: Math.floor(i / 4 / ia.width) };
      }
    }
    const mc = document.createElement('canvas');
    mc.width = ia.width;
    mc.height = ia.height;
    const mg = mc.getContext('2d');
    const md = mg.createImageData(ia.width, ia.height);
    for (let i = 0; i < da.length; i += 4) {
      const d = Math.max(Math.abs(da[i] - db[i]), Math.abs(da[i + 1] - db[i + 1]), Math.abs(da[i + 2] - db[i + 2]));
      md.data[i] = d > 0 ? 255 : 0;
      md.data[i + 1] = d > 0 ? Math.max(0, 255 - d * 3) : 0;
      md.data[i + 2] = 0;
      md.data[i + 3] = 255;
    }
    mg.putImageData(md, 0, 0);
    return { same: count === 0, count, maxDelta, first, w: ia.width, h: ia.height, mask: mc.toDataURL('image/png') };
  }, [a, b]);
  await page.close();
  if (maskPath && result.mask) {
    const { writeFileSync } = await import('node:fs');
    writeFileSync(maskPath, Buffer.from(result.mask.split(',')[1], 'base64'));
  }
  delete result.mask;
  return result;
}
