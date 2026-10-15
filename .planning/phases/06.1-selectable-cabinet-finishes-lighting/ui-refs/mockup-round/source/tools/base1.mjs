import { mkdirSync } from 'node:fs';
import { chromium, open, growViewport, diffPngs, switcherUrl, BASE, SHEETS } from './lib.mjs';

/**
 * Quick baseline for one case (sample 65, desktop width), used while bisecting a difference.
 */
mkdirSync(SHEETS, { recursive: true });
const browser = await chromium.launch();
const hide = () => document.querySelectorAll('header, .switcher').forEach((e) => e.style.setProperty('display', 'none', 'important'));
const rel = await open(browser, `${BASE}/?sample=65`, 1440);
await rel.waitForSelector('#cabinet .section');
await rel.evaluate(hide);
await rel.waitForTimeout(300);
await growViewport(rel, 1440);
await rel.locator('#cabinet').screenshot({ path: `${SHEETS}/b1-release.png` });
const sw = await open(browser, switcherUrl('switcher.html', {}), 1440);
await sw.waitForSelector('html[data-ready]');
await sw.evaluate(hide);
await growViewport(sw, 1440);
await sw.locator('#cabinet').screenshot({ path: `${SHEETS}/b1-layers.png` });
console.log(JSON.stringify(await diffPngs(browser, `${SHEETS}/b1-release.png`, `${SHEETS}/b1-layers.png`, `${SHEETS}/b1-mask.png`)));
await browser.close();
