import { chromium, open, switcherUrl, BASE } from './lib.mjs';

/**
 * Prints one computed property of the same element in the release page and in the switcher. Arguments: a selector, a
 * pseudo-element or "-", a property name.
 */
const [selector, pseudo, prop] = process.argv.slice(2);
const browser = await chromium.launch();
const read = async (page) => page.evaluate(([s, p, name]) => {
  const el = document.querySelector(s);
  return getComputedStyle(el, p === '-' ? null : p).getPropertyValue(name);
}, [selector, pseudo, prop]);
const rel = await open(browser, `${BASE}/?sample=65`, 1440);
await rel.waitForSelector('#cabinet .section');
console.log('RELEASE', await read(rel));
const sw = await open(browser, switcherUrl('switcher.html', {}), 1440);
await sw.waitForSelector('html[data-ready]');
console.log('SWITCHER', await read(sw));
await browser.close();
