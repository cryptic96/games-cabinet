/**
 * Baseline: the default look drawn by the layered stylesheets against the release stylesheet, on the same markup. It
 * shoots the cabinet element of both at the same width with the same fonts and counts differing pixels.
 */
import { mkdirSync, writeFileSync } from 'node:fs';
import { chromium, open, growViewport, diffPngs, switcherUrl, BASE, SHEETS } from './lib.mjs';

mkdirSync(SHEETS, { recursive: true });
const browser = await chromium.launch();
const results = [];

for (const [sample, file, width] of [['65', 'switcher.html', 1440], ['65', 'switcher-phone.html', 390], ['400', 'switcher-400.html', 1440], ['400', 'switcher-400-phone.html', 390]]) {
  const rel = await open(browser, `${BASE}/?sample=${sample}`, width);
  await rel.waitForSelector('#cabinet .section');
  await rel.evaluate(() => document.querySelectorAll('header, .switcher').forEach((e) => e.style.setProperty('display', 'none', 'important')));
  await rel.waitForTimeout(400);
  await growViewport(rel, width);
  const relPath = `${SHEETS}/baseline-release-${sample}-${width}.png`;
  await rel.locator('#cabinet').screenshot({ path: relPath });
  await rel.context().close();

  const sw = await open(browser, switcherUrl(file, {}), width);
  await sw.waitForSelector('html[data-ready]');
  await sw.evaluate(() => document.querySelectorAll('header, .switcher').forEach((e) => e.style.setProperty('display', 'none', 'important')));
  await growViewport(sw, width);
  const swPath = `${SHEETS}/baseline-layers-${sample}-${width}.png`;
  await sw.locator('#cabinet').screenshot({ path: swPath });
  const errors = sw.errors.slice();
  await sw.context().close();

  const diff = await diffPngs(browser, relPath, swPath, SHEETS + `/baseline-mask-${sample}-${width}.png`);
  results.push({ sample, viewport: width, ...diff, errors });
  console.log(JSON.stringify(results[results.length - 1]));
}

await browser.close();
writeFileSync(`${SHEETS}/baseline-results.json`, JSON.stringify(results, null, 2));
