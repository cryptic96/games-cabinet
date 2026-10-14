import { chromium, open, growViewport, switcherUrl, SHEETS } from './lib.mjs';

/**
 * Draws one look of the switcher and saves a screenshot. Arguments are key=value pairs: out, file (default
 * switcher.html), width (default 1440), scale (default 1), full (1 for the whole page), sel (a selector to crop to) and
 * any look key (design, wood, backs, lights, depth, evening, dim, join).
 */
export async function shoot(browser, opt) {
  const { file = 'switcher.html', width = 1440, scale = 1, full = '0', sel = null, out, height = 900, ...state } = opt;
  const page = await open(browser, switcherUrl(file, state), Number(width), Number(height), Number(scale));
  await page.waitForSelector('html[data-ready]');
  await page.waitForTimeout(120);
  if (full === '1' || sel) await growViewport(page, Number(width));
  const path = out.startsWith('/') ? out : `${SHEETS}/${out}`;
  const type = path.endsWith('.jpg') ? { type: 'jpeg', quality: 82 } : {};
  if (sel) await page.locator(sel).first().screenshot({ path, ...type });
  else await page.screenshot({ path, fullPage: full === '1', ...type });
  const errors = page.errors.slice();
  await page.context().close();
  return { path, errors };
}

if (process.argv[1].endsWith('shots.mjs')) {
  const opt = Object.fromEntries(process.argv.slice(2).map((a) => a.split('=')));
  const browser = await chromium.launch();
  console.log(JSON.stringify(await shoot(browser, opt)));
  await browser.close();
}
