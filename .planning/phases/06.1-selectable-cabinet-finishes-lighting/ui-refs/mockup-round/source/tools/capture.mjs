import { createRequire } from 'node:module';
import { writeFileSync } from 'node:fs';
const S = process.env.ROUND_SCRATCH;
process.env.PLAYWRIGHT_BROWSERS_PATH = S + '/pw/browsers';
const require = createRequire(S + '/pw/package.json');
const { chromium } = require('playwright');

const base = 'http://127.0.0.1:5391';
const browser = await chromium.launch();

for (const sample of ['65', '400']) {
  for (const [name, width] of [['desktop', 1440], ['phone', 390]]) {
    const page = await browser.newPage({ viewport: { width, height: 900 } });
    await page.goto(`${base}/?sample=${sample}`, { waitUntil: 'networkidle' });
    await page.waitForSelector('#cabinet .section');
    const out = await page.evaluate(() => {
      for (const section of document.querySelectorAll('#cabinet .section')) {
        const light = document.createElement('div');
        light.className = 'section-light';
        light.setAttribute('aria-hidden', 'true');
        for (const cubby of section.querySelectorAll('.cubby')) {
          const pool = document.createElement('div');
          pool.className = 'light-pool';
          for (const p of ['--x', '--y', '--w', '--h']) {
            pool.style.setProperty(p, cubby.style.getPropertyValue(p));
          }
          light.append(pool);
        }
        section.append(light);
        const floor = document.createElement('div');
        floor.className = 'section-floor';
        floor.setAttribute('aria-hidden', 'true');
        section.prepend(floor);
        for (const cubby of section.querySelectorAll('.cubby')) {
          const deco = document.createElement('div');
          deco.className = 'cubby-deco';
          deco.setAttribute('aria-hidden', 'true');
          cubby.prepend(deco);
        }
      }
      const clean = (el) => {
        for (const a of el.querySelectorAll('a')) a.setAttribute('href', '#');
        return el.outerHTML;
      };
      return {
        cabinet: document.querySelector('#cabinet').outerHTML,
        sections: document.querySelectorAll('#cabinet .section').length,
        cubbies: document.querySelectorAll('#cabinet .cubby').length,
        header: clean(document.querySelector('header')),
        footer: clean(document.querySelector('footer')),
      };
    });
    writeFileSync(`${S}/round/markup/cabinet-${sample}-${name}.html`, out.cabinet);
    if (sample === '65') {
      writeFileSync(`${S}/round/markup/header-${name}.html`, out.header);
      writeFileSync(`${S}/round/markup/footer.html`, out.footer);
    }
    console.log(sample, name, 'sections', out.sections, 'cubbies', out.cubbies, 'bytes', out.cabinet.length);
    await page.close();
  }
}
await browser.close();
