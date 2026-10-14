import { chromium, open, switcherUrl, BASE } from './lib.mjs';

/**
 * Prints the bounding boxes of the cabinet, the first section and its first cubby in the release page and the switcher.
 */
const browser = await chromium.launch();
const read = async (page) => page.evaluate(() => {
  const r = (s) => {
    const b = document.querySelector(s).getBoundingClientRect();
    return [b.x, b.y + window.scrollY, b.width, b.height].map((n) => Math.round(n * 1000) / 1000).join(' ');
  };
  return ['#cabinet', '.section', '.section-body', '.cubby', '.placement'].map((s) => s + ': ' + r(s)).join('\n');
});
const rel = await open(browser, `${BASE}/?sample=65`, 1440);
await rel.waitForSelector('#cabinet .section');
console.log('RELEASE\n' + (await read(rel)));
const sw = await open(browser, switcherUrl('switcher.html', {}), 1440);
await sw.waitForSelector('html[data-ready]');
console.log('SWITCHER\n' + (await read(sw)));
await browser.close();
