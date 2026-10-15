import { pathToFileURL } from 'node:url';
import { chromium, open, OUT } from './lib.mjs';

/**
 * Opens the switcher from disk, presses one button per group and checks the root attribute each one names, then checks
 * the lit marker for the three light states and the evening toggle. Fails on any console error.
 */
const browser = await chromium.launch();
const page = await open(browser, pathToFileURL(OUT + '/switcher.html').href, 1440);
await page.waitForSelector('html[data-ready]');
const checks = [
  ['design', 'library', 'data-design'],
  ['wood', 'mahogany', 'data-wood'],
  ['backs', 'painted', 'data-backs'],
  ['lights', 'on', 'data-lights'],
  ['depth', 'deep', 'data-depth'],
  ['dim', 'on', 'data-dim'],
  ['join', 'on', 'data-join'],
];
let failed = 0;
for (const [key, value, attr] of checks) {
  await page.click(`.switcher button[data-key="${key}"][data-value="${value}"]`);
  const got = await page.evaluate((a) => document.documentElement.getAttribute(a), attr);
  const ok = got === value;
  if (!ok) failed++;
  console.log(ok ? 'ok  ' : 'FAIL', attr, got);
}
await page.waitForTimeout(2500);
console.log('lit with lights on:', await page.evaluate(() => document.documentElement.hasAttribute('data-lit')));
await page.click('.switcher button[data-key="lights"][data-value="evenings"]');
await page.waitForTimeout(300);
console.log('lit with evenings by day:', await page.evaluate(() => document.documentElement.hasAttribute('data-lit')));
await page.click('.switcher button[data-key="evening"][data-value="1"]');
await page.waitForTimeout(600);
console.log('lit with evenings at night:', await page.evaluate(() => document.documentElement.hasAttribute('data-lit')));
console.log('evening marker:', await page.evaluate(() => document.documentElement.hasAttribute('data-evening')));
console.log('console errors:', JSON.stringify(page.errors));
if (page.errors.length > 0) failed++;
await browser.close();
process.exit(failed === 0 ? 0 : 1);
