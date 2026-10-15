import { chromium } from './lib.mjs';
import { montage } from './montage.mjs';

const [out, cols, ...files] = process.argv.slice(2);
const browser = await chromium.launch();
await montage(browser, { items: files.map((f) => ({ file: f, label: f })), cols: Number(cols), out, labelH: 30, titleH: 10, pad: 10, gap: 10 });
await browser.close();
