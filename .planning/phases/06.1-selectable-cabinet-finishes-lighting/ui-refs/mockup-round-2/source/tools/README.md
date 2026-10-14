# Second-round tools

These scripts rebuilt the switcher pages and every sheet of the second mock-up round from the first round's sources. They are scratch tooling: nothing here is part of the product, and Playwright was installed in a scratch folder only, never in a project manifest.

Set `ROUND_SCRATCH` to a folder that holds `pw/` (a Playwright install with its Chromium), `dejavu.conf` (a fontconfig file that maps the system UI font to DejaVu Sans) and `round2/` (a copy of the first round's folder with these files over it). The release stylesheet screenshots from the first round (`baseline-release-*.png`) sit in `round2/sheets/`.

- `build-all.mjs` writes the four layer stylesheets, the page chrome and the switcher pages into `out/` (the repository path is its only argument); `build-wood.mjs` writes the wood layer from one table.
- `baseline2.mjs` compares the default look with the saved release screenshots, pixel by pixel.
- `looks2.mjs`, `sheets2.mjs`, `focus2.mjs` draw the look sheets and the second-round comparison sheets.
- `measure2.mjs`, `perf2.mjs`, `invariants.mjs`, `evening-diff.mjs` read box visibility and chrome contrast, paint cost, structure checks and the day-to-evening change.

The layer sources are in `css/`.
