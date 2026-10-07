---
phase: 03-bgg-access-spike-real-sync-snapshot
audited: 2026-10-07
baseline: 03-UI-SPEC.md (approved), extending 02-UI-SPEC.md
screenshots: captured (Chromium and Firefox; WebKit could not start on the audit machine)
overall: 21/24
---

# Phase 3 — UI Review

**Audited:** 2026-10-07
**Baseline:** `03-UI-SPEC.md` (approved design contract), with `02-UI-SPEC.md` for the locked palette, type scale, spacing scale, focus ring and accent list
**Screenshots:** captured after the last page-script change (front-end review fixes included), in Chromium and Firefox at 320, 390 and 1440 px, against the local fake BGG with invented data, app in Development. They are kept in a session scratch folder (not committed). WebKit could not start on this machine, so Safari rendering and the Safari strict-policy live connection were **not** checked.

States captured: being filled, first sync running, cooldown with own-press note, passive second page after the first sync, passive page after a live redraw to 400 items, failed press, held-back press, stale note (last good sync 5 hours old), keyboard focus on both sync-block buttons, exact-time line open, expansion sub-line, footer credit at 320/390/1440, dev sample switcher with an invented sample.

Measured in both engines (run logs): no horizontal scroll at any width or state; the live redraw kept `scrollY` at a single value (no jump), the loading line never appeared during the redraw, and keyboard focus came back to the same `data-entry-id`; zero CSP violations, zero console errors, zero page errors; the exact-time line read `Last synced 7 October 2026 at 11:16 CEST`.

---

## Pillar Scores

| Pillar | Score | Key Finding |
|--------|-------|-------------|
| 1. Copywriting | 4/4 | Every contract string matches word for word in `copy.js`, `SyncStatusText.cs` and the Razor markup; plain dots only |
| 2. Visuals | 3/4 | Footer version text sits at the top of the row while the logo is centred in its 44px link: visible offset on every page |
| 3. Color | 4/4 | Only the declared tokens; no new hue or accent use; measured contrast 11.3:1 / 6.35:1 / 5.08:1 as contracted |
| 4. Typography | 4/4 | Chrome uses only 14/16/24 px and weights 400/600; `tabular-nums` on the button |
| 5. Spacing | 3/4 | Scale respected, but three contracted layout properties are missing (footer `align-items`, `max-width: 40rem` on the sync row and the exact-time line) |
| 6. Experience Design | 3/4 | State coverage and redraw are solid; own-press notes that are not followed by a cooldown are never cleared, so a stale "Couldn't start..." or "A sync is already running." can sit under an idle button |

**Overall: 21/24**

---

## Top 3 Priority Fixes

1. **Footer items are not vertically centred** (WARNING) — on every page the `Version ...` text sits about 11px higher than the centre of the "Powered by BGG" logo (see `synced-1440`, `synced-footer-credit-1440`, `filling-390`), so the one required credit looks misplaced next to the version. Cause: `footer` (`Cabinet.Service/wwwroot/css/site.css:82-88`) has no `align-items: center`, which the footer contract requires (`03-UI-SPEC.md`, "Footer and BGG credit", Footer container row); the default `stretch` makes the `<p class="version">` 44px tall with its text at the top. Fix: add `align-items: center;` to the `footer` rule.
2. **Own-press notes can outlive their meaning** (WARNING) — `sync.js:185-187` empties `#sync-note` only on a cooldown-to-idle change. (a) `Couldn't start the sync. Check your connection and try again.` (`sync.js:433`, `:437`) starts no cooldown, so it stays on screen until the visitor's next press, even after statuses show the site is reachable again. (b) `A sync is already running.` (`sync.js:392`, `:425`) stays under an idle `Sync now` when the running sync outlasts its window (every accepted sync opens a window, `SyncCoordinator.cs:165`; the window is 10 minutes in `appsettings.json`, so this needs a long first sync with many image downloads, but it is reachable). Fix: also clear the note on a running-to-idle change when no own press is pending (the outcome sentence is written after `renderButton` in `applyStatus`, `sync.js:291-299`, so it is not lost), and clear the offline sentence on the next status that arrives (a successful fetch or a pushed status).
3. **Two contracted width limits are missing** (WARNING, no visible effect today) — the contract says the sync block and both notes are `max-width: 40rem` ("Sync block", paragraph under the table) and the exact-time line wraps within 40rem (UI Considerations, E1 long-text). `.sync` (`site.css:135-142`) and `.sync-exact` (`site.css:194-197`) have no `max-width`; only `.sync-note` and `.sync-stale` do (`site.css:199-204`). With the current short copy nothing changes on screen, but a longer translated line would run the full width on desktop. Fix: add `max-width: 40rem;` to both rules.

---

## Detailed Findings

### Pillar 1: Copywriting (4/4)

All strings were compared with the Copywriting Contract table word for word.

| Contract element | Where | Result |
|------------------|-------|--------|
| `Sync now` | `copy.js:107`, `Index.cshtml:16` | exact |
| `Syncing...` | `copy.js:108` | exact, plain dots |
| `Sync again in {m:ss}` / names `Sync again in {n} minutes`, `1 minute`, `less than a minute` | `copy.js:121-123`, `copy.js:29-37`, `copy.js:130-132` | exact |
| `Synced just now`, minutes, hours, `yesterday`, days | `copy.js:55-69` (`Intl.RelativeTimeFormat('en', { numeric: 'auto' })`), server first paint `SyncStatusText.cs:14-45` | exact; same rule on both sides |
| `Not synced yet` | `SyncStatusText.cs:12` | exact |
| `Last synced {exact}` in `en-NL` | `copy.js:5`, `copy.js:13-20`, `copy.js:86`; screenshot `synced-exact-open-390` | exact (`7 October 2026 at 11:16 CEST`) |
| Four outcome sentences, `A sync is already running.`, `You can sync again in ...`, `Couldn't start the sync. ...` | `copy.js:109-114`, `copy.js:139-141` | exact |
| Stale note, both variants | `copy.js:95`, `copy.js:104`; screenshots `stale-note-header-320`, `held-back-note-header-390` | exact |
| "Being filled" heading and body | `Index.cshtml:55-56` | exact |
| `Expansion`, `{title}, expansion` | `copy.js:144`, `copy.js:160-162`; screenshot `synced-expansion-label-1440` | exact |
| Logo alt `Powered by BGG`, `Version {version} ({shortCommit})` | `_Layout.cshtml:14-15` | exact |
| Dev switcher `Synced`, nav label `Collection to show` | `Index.cshtml` nav; screenshot `sample-switcher-header-320` | exact |

- No ellipsis character anywhere in the page scripts, pages or page models (searched).
- No visitor copy uses "snapshot", "cooldown", "queued", "throttled" or "held back".
- The retired "being built" line is gone from the markup.

No deviation found. One owner question about the held-back state is listed under "Open questions" (it is about two contracted sentences appearing together, not about their wording).

### Pillar 2: Visuals (3/4)

What works (checked in the screenshots):

- Clear hierarchy: the 24px title, then a quiet status row whose outlined button stays secondary to the cabinet (`synced-1440`, `filling-390`).
- The not-pressable look reads as intended: dashed border and muted text for `Syncing...` and the countdown (`syncing-390`, `cooldown-header-390`); words and `aria-disabled` carry the state, not colour alone.
- The stale note's left rule sets it apart calmly with no colour change (`stale-note-header-320`, `held-back-note-header-390`).
- The relative time and the button wrap instead of shrinking; no horizontal scroll at 320 px in any state.
- No layout shift when the button is revealed: `.sync` reserves the 44px row (`site.css:140`) and the button only appears in place.
- "Being filled" sits centred above the bare cabinet with nothing inside the cabinet (`filling-390`, `syncing-390`); the message is gone after the first sync on both pages.
- The orphan expansion boxes show the `Expansion` sub-line on one line (`synced-1440`, `synced-expansion-label-1440`).
- The official reversed logo sits directly on the dark backdrop, legible at 32px tall at 320, 390 and 1440 px (`footer-credit-320`, `synced-footer-credit-390`, `synced-footer-credit-1440`).
- Focus rings show on both sync-block buttons (`synced-focus-time-390`, `synced-focus-button-390`). The left edge of the ring is cut in those two images only because they are element crops of `header`; on the page the 8px phone gutter leaves room for the 4px ring.

Finding:

- **WARNING: footer misalignment.** The version text is top-aligned while the logo is centred in its 44px link, so the two sit at visibly different heights on every page and at every width (`synced-1440` around y 1187 vs the logo centre around y 1198; `synced-footer-credit-1440`; `filling-390`). Root cause and fix in Priority Fix 1.

### Pillar 3: Color (4/4)

- Tokens exactly as contracted in `site.css:1-10`, including the two derived tokens: `--chrome-line` resolves to `#a7977d` and `--chrome-muted` to `#b9aa8e`.
- Measured contrast on `#3b2416`: `--wall-text` 11.34:1 (all chrome text), `--chrome-muted` 6.35:1 (not-pressable button text, above the 4.5:1 text minimum), `--chrome-line` 5.08:1 (button border and stale rule, above the 3:1 UI-component minimum), focus ring accent 7.76:1.
- The accent is used only on the reserved list: focus ring (`site.css:215-219`), current sample link (`site.css:72-75`), retry button (`site.css:122-133`) and the Phase 2 "+N more" marker. The sync button is outlined and transparent (`site.css:167-179`), as contracted.
- No opacity is used for the not-pressable state; no red, warning hue or icon anywhere in the new chrome.
- The logo is not recoloured, filtered or stretched (`height: 32px; width: auto; max-width: 100%`, `site.css:100-104`).

No deviation found.

### Pillar 4: Typography (4/4)

- Page-chrome sizes in `site.css`: 24px (`h1`), 16px (body, `.sync-time`, notes, messages), 14px (button, exact-time line, footer, nav). No other size.
- Weights: 400 and 600 only. The sync button is 400 in every state (`site.css:176`); 600 stays on the title, the "being filled" heading (`site.css:111-113`), the current sample link and the retry button.
- `font-variant-numeric: tabular-nums` on `.sync-button` (`site.css:177`); the countdown digits do not shift between screenshots.
- In-cabinet, the `Expansion` sub-line uses the existing 12px / 400 sub-label style (`cabinet.css:340-357`).
- No web fonts; system stack unchanged.

No deviation found.

### Pillar 5: Spacing (3/4)

Respected:

| Contract | Code |
|----------|------|
| Sync row gap 8px rows / 16px columns, 44px min height, 4px under the title | `site.css:139-141`, `site.css:33` |
| Notes 8px apart, stale rule 4px plus 8px padding | `site.css:202`, `site.css:207-208` |
| Exact-time line 8px above | `site.css:195` |
| "Being filled" 24px above the cabinet, centred, 40rem | `site.css:115-119` |
| Header to cabinet 32px, cabinet to footer 48px, footer gap 16px, inline 16px, bottom 64px | `site.css:29`, `site.css:79`, `site.css:85-86` |
| 44px pressables: relative-time button, sync button, credit link | `site.css:159`, `site.css:168`, `site.css:97` |
| Phone gutter 8px, desktop 24px | `site.css:221-225`, `site.css:19` |

Every value is a multiple of 4 from the declared scale plus the two declared exceptions (44px, 32px).

Findings:

- **WARNING:** `footer` lacks `align-items: center` (`site.css:82-88`), which the footer contract lists. Visible effect and fix under Visuals and Priority Fix 1.
- **WARNING (no visible effect today):** `.sync` and `.sync-exact` have no `max-width: 40rem` (`site.css:135-142`, `site.css:194-197`), although the contract applies it to the sync block and the exact-time line. Priority Fix 3.

### Pillar 6: Experience Design (3/4)

What works:

- All button states with the right precedence (running, then cooldown, then idle): `sync.js:158-191`, `status.js` `buttonState`. Screenshots: `filling-390` (idle, never synced), `syncing-390` (`Syncing...`), `cooldown-header-390`, `stale-note-header-320` (idle).
- `aria-disabled`, never `disabled`; pressing while not pressable sends nothing and explains why (`sync.js:389-399`).
- One live region (`Index.cshtml:19`); the cooldown accessible name changes only when the whole-minute wording does (`sync.js:146-152`, `:174`, `:182`); the countdown ticks only while the tab is visible (`sync.js:197-206`).
- Press flow survives a hung request (15 s abort, `sync.js:403-404`) and a sync that ends before its answer (`sync.js:294-299`, polling `sync.js:326-353`); both are review fixes and the outcome sentences were seen in both engines.
- Quiet redraw: one swap, filling message removed in the same step, focus restored with `preventScroll` (`cabinet.js:134-175`, focus at `cabinet.js:151-169`); hidden tabs wait (`cabinet.js:117-125`); a failed redraw is silent and only repeats a first load that still shows the loading line (`cabinet.js:182-188`). Run logs: scroll held, no loading line, focus kept.
- Stale and held-back notes appear and disappear from the status only (`sync.js:127-130`); the 5-hour case shows the "recent syncs" variant with the button idle (`stale-note-header-320`).
- Loading line is no longer a live region (review fix); connection loss shows no banner.

Findings:

- **WARNING: notes that are never cleared.** Details and fix in Priority Fix 2 (`sync.js:185-187`, `:392`, `:425`, `:433`, `:437`). The contract only defines clearing at cooldown end, so the offline and "already running" cases are a gap in both the contract and the code rather than a straight violation; it still leaves the page saying something untrue.
- **Not verified:** Safari/WebKit (live connection under `default-src 'self'` and rendering). Already recorded as an owner advisory in the phase verification; the page falls back to status polling.

---

## Open Questions for the Owner

1. **Held-back state shows two sentences that overlap.** After a held-back press, the own-press note (`BGG returned far fewer games than before, so the last collection is still showing.`) and the held-back stale note (`Showing the last sync from ... A much smaller collection from BGG is waiting for the next sync to confirm.`) stack on top of each other (`held-back-note-header-390`). Both are in the contract and the wording itself stays as decided; the question is only whether the press outcome should stay as it is, or be replaced by a shorter sentence (or nothing) while the held-back note is visible. Not scored.
2. **Safari check.** Run the page once in Safari (desktop or iPhone) against the deployed site to close the WebKit gap above.

---

## Registry Safety

Not applicable: no `components.json`, no component registry, stack is Razor Pages with vanilla ES modules. The only third-party assets are the official BGG logo and the pinned live-connection client script, both outside this contract's registry gate.

---

## Files Audited

- `Cabinet.Service/Pages/Shared/_Layout.cshtml`
- `Cabinet.Service/Pages/Index.cshtml`, `Index.cshtml.cs`, `SyncStatusText.cs`
- `Cabinet.Service/wwwroot/css/site.css`, `cabinet.css`
- `Cabinet.Service/wwwroot/js/copy.js`, `sync.js`, `status.js`, `cabinet.js`, `render.js`, `live.js`
- `Cabinet.Service/wwwroot/img/powered-by-bgg.svg`
- `Cabinet.Service/Sync/SyncCoordinator.cs`, `Cabinet.Service/appsettings.json` (window length, for the note-clearing finding)
- Screenshot set and run logs for Chromium and Firefox (session scratch folder, not committed)
