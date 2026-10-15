# Mock-up round tools

These scripts built the switcher pages and every sheet of the round. They are kept so a second round can be rebuilt. They are scratch tooling: nothing here is part of the product, and Playwright was installed in a scratch folder only, never in a project manifest.

Set `ROUND_SCRATCH` to a folder that holds `pw/` (a Playwright install with its Chromium) and `round/` (a copy of this folder). Run the service locally on free ports with the development settings, then:

- `capture.mjs` saves the markup the real renderer produced for the 65 and 400 samples at both widths and adds the new hook elements.
- `build-all.mjs` writes the four layer stylesheets, the page chrome and the four switcher pages into `out/`.
- `baseline.mjs` compares the default look drawn by the layers with the release, pixel by pixel.
- `looks.mjs`, `grid.mjs`, `extras.mjs`, `decisions.mjs`, `focus.mjs` draw the sheets under the DejaVu font setup.
- `measure.mjs` and `invariants.mjs` read the box visibility, contrast, cost and structure numbers.

The layer sources are in `css/`; `build-wood.mjs` writes the wood layer from one table.
