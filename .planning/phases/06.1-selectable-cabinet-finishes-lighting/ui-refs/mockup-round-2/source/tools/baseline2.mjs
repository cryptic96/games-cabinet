/**
 * Baseline for the second round: the default look drawn by the layered stylesheets against the release screenshots
 * saved in the first round (same markup, same browser, same font setup), counting differing pixels.
 */
import { writeFileSync } from 'node:fs';
import { chromium, open, growViewport, diffPngs, switcherUrl, SHEETS } from './lib.mjs';

const browser = await chromium.launch();
const results = [];
for (const [sample, file, width] of [['65', 'switcher.html', 1440], ['65', 'switcher-phone.html', 390], ['400', 'switcher-400.html', 1440], ['400', 'switcher-400-phone.html', 390]]) {
  const sw = await open(browser, switcherUrl(file, {}), width);
  await sw.waitForSelector('html[data-ready]');
  await sw.evaluate(() => document.querySelectorAll('header, .switcher').forEach((e) => e.style.setProperty('display', 'none', 'important')));
  await growViewport(sw, width);
  const swPath = `${SHEETS}/baseline-layers-${sample}-${width}.png`;
  await sw.locator('#cabinet').screenshot({ path: swPath });
  await sw.context().close();
  const diff = await diffPngs(browser, `${SHEETS}/baseline-release-${sample}-${width}.png`, swPath, `${SHEETS}/baseline-mask-${sample}-${width}.png`);
  results.push({ sample, viewport: width, ...diff });
  console.log(JSON.stringify(results[results.length - 1]));
}
await browser.close();
writeFileSync(`${SHEETS}/baseline-results.json`, JSON.stringify(results, null, 2));
