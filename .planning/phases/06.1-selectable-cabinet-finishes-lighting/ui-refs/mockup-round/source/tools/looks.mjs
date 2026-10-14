import { chromium } from './lib.mjs';
import { shoot } from './shots.mjs';
import { lookCrops } from './crops.mjs';
import { LOOKS } from './quick.mjs';

/**
 * The seven look sheets: each look at 1440 and 390 on the 65 sample with crops of a corner, a shelf edge, the base and
 * the legs or plinth, and an evening version with Evenings only lit. Page sheets are JPEG to keep the folder small; the
 * detail crops are PNG at device scale 2.
 */
const only = process.argv.slice(2);
const browser = await chromium.launch();
for (const [id, state, label] of LOOKS) {
  if (only.length > 0 && !only.includes(id)) continue;
  const tag = `look-${id}`;
  await shoot(browser, { ...state, out: `${tag}-desktop.jpg`, full: '1', width: 1440 });
  await shoot(browser, { ...state, file: 'switcher-phone.html', out: `${tag}-phone.jpg`, full: '1', width: 390 });
  await lookCrops(browser, tag, state);
  const evening = { ...state, lights: 'evenings', evening: '1' };
  await shoot(browser, { ...evening, out: `${tag}-evening-desktop.jpg`, full: '1', width: 1440 });
  await shoot(browser, { ...evening, file: 'switcher-phone.html', out: `${tag}-evening-phone.jpg`, full: '1', width: 390 });
  await lookCrops(browser, tag, evening, { names: ['edge'], prefix: 'evening-' });
  console.log('done', id, label);
}
await browser.close();
