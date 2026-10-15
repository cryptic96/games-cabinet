---
status: awaiting_human_verify
trigger: "Post-merge: LookDepthTests.Every_depth_and_back_leaves_the_boxes_where_they_are_and_paints_inside_them fails for legs, library, display, plywood-legs at 1440 (edge differences up to 168); Deep's lit top face only shows on Classic because the six new designs override .cubby::before background-image without the --top-face-img slot."
created: 2026-10-14T00:00:00Z
updated: 2026-10-14T20:30:00Z
---

## Current Focus

hypothesis: CONFIRMED - the depth theory grows the viewport to the page height for Classic, then switches to a taller design (legs/library/display/plywood-legs), so its full-page screenshot has to capture beyond the viewport. Chromium does that by briefly resizing the page to 1x1 px, which flips the phone media query (max-width: 40rem) on and off, so cabinet.js reloads the cabinet twice (loading line, then redraw). When the capture lands in the loading window the frame shows "Loading the cabinet..." and the footer where the sections should be, so the edge ring reads text pixels.
bug_class: Heisenbug (timing race between the screenshot capture and the page's reload; load-dependent, design-dependent only through page height)
test: probe test logging resize/matchMedia/DOM mutations around 25 full-page shots of a taller-than-viewport desktop page
expecting: resize 1x1 + phone true/false + cabinet-loading insertions per shot; occasional shot showing the loading state
next_action: owner confirms; then archive to resolved/ and add the knowledge-base entry (pattern: full-page screenshot beyond the viewport at desktop width flips the phone query and reloads the cabinet)

reasoning_checkpoint:
  hypothesis: "The depth theory fails because its full-page screenshots of designs taller than Classic capture beyond a viewport grown for Classic; Chromium resizes the page to 1x1 for that capture, the phone query flips, cabinet.js reloads the cabinet, and the capture sometimes lands on the loading line, whose text and the footer sit where the section rings are sampled."
  confirming_evidence:
    - "Identical offender list for all four failing designs; dumped frame shows the loading state while the DOM holds two sections."
    - "Probe logs resize 1x1 + phone true/false + cabinet-loading per full-page shot; 1 of 100 shots caught the loading state."
    - "Only designs whose page exceeds the Classic-grown viewport fail (legs 1227, library 1238, display 1209, plywood-legs 1218 > 1201); 390 never fails because 1 px still matches the phone query."
  falsification_test: "If, with the viewport fitted to the page before every frame, the probe still logs resize 1x1 or the theory still fails under load, the hypothesis is wrong."
  fix_rationale: "The test, not the CSS, is wrong: its frames must be taken with the whole page inside the viewport (the identity test's established pattern), so the capture never resizes the page and never triggers the profile reload. The page's reload on a real profile change is correct behaviour."
  blind_spots: "SaveScreenshotAsync (CABINET_SCREENSHOT_DIR, set in CI) takes FullPage shots of desktop pages that may be taller than the viewport and so can trigger the same reload mid-test; not changed here, reported as a residual risk."
  candidate_causes:
    - "code (test): full-page capture beyond the viewport after a design switch changes page height"
    - "code (CSS): a new design (legs, plinth, float shadow, floor patch) paints outside its box - eliminated, identical offenders across designs and frame shows the loading state"
    - "environment: CPU load from the parallel full suite widens the race window (contributing, not sufficient)"
  and_gate: "yes - needs both the beyond-viewport capture (test code, design-height dependent) and a capture timed inside the reload window (load-dependent). Removing the beyond-viewport capture removes the reload entirely, so it is sufficient."

## Symptoms

expected: every design x width x depth x back keeps rects equal and edge ring within 3/255; full browser suite green twice under DejaVu and once with default fonts.
actual: theory fails for legs, library, display, plywood-legs at 1440, e.g. library flat+wood -> {106, 151, 106, 50, 81, 156, 168, 10}. Isolated class run: only library failed.
errors: Expected (EdgeDifferencesAsync()) to contain only items matching (difference <= 3) because library at 1440 with flat and wood backs, but {106, 151, 106, 50, 81, 156, 168, 10}
reproduction: FONTCONFIG_FILE=.../dejavu.conf dotnet test --project Cabinet.BrowserTests (full or LookDepthTests class)
started: after merging the four parallel plans (commit 43c1bf1); each plan's own branch was green.

## Eliminated

- hypothesis: A design (legs shadows, Display float shadow, Library plinth/crown, floor patch) paints outside its section box.
  evidence: All four failing designs produce the byte-identical offender list {106,151,106,50,81,156,168,10} at the same coordinates (x 707/710 y 195-204, x 65/68 y 279-285); the dumped frame shows the page's loading state (text "Loading the cabinet..." and the footer), not furniture. Isolated class run passes 19/19.
  timestamp: 2026-10-14T19:15:00Z

## Evidence

- timestamp: 2026-10-14T19:05:00Z
  checked: Full browser run under DejaVu with diagnostics in EdgeDifferencesAsync (dump offenders + frame)
  found: 4 failures (legs, library, display, plywood-legs at 1440). Viewport 1201 high (grown for Classic), page scrollHeight 1209-1238 after the design switch; frame 1440x(scrollHeight). The frame image shows "Loading the cabinet..." and the footer at the top; offenders are the loading text and footer text pixels.
  implication: The screenshot captured the cabinet's loading state, while the DOM read right after holds two sections. Only designs taller than Classic (page exceeds the grown viewport) fail; modern and plywood are shorter and fit.

- timestamp: 2026-10-14T19:20:00Z
  checked: Probe test (1440, sample 65, viewport grown for Classic, design library), 25 full-page shots, logging resize, matchMedia change and #cabinet child insertions
  found: Every shot logs "resize 1x1 | added cabinet-loading | phone true | resize 1440x1201 | added cabinet-loading | phone false | added section x2". 1 shot of 100 captured the loading state (pixel inside the cabinet equals the wall).
  implication: A full-page screenshot beyond the viewport makes Chromium resize the page to 1x1 and back; the phone query flips and cabinet.js reloadForNewProfile redraws the cabinet; the capture can land in the loading window. At 390 the query stays true, so no reload and no failure.

- timestamp: 2026-10-14T19:22:00Z
  checked: DefaultLookIdentityTests.RenderAsync
  found: grows the viewport to scrollHeight until settled before its FullPage shot, so it never captures beyond the viewport.
  implication: The established pattern; the depth theory broke it by changing the page height (design switch) after growing.

- timestamp: 2026-10-14T19:45:00Z
  checked: Resize/loading logger in the depth theory, unfixed vs fixed test (isolated run, DejaVu)
  found: Unfixed - legs/library/display at 1440 log a cabinet reload ("loading") on all 6 frames; plywood-legs 1440 failed even in the isolated run; 390 cases resize but never reload. Fixed - only the expected viewport fits (e.g. 1440x1201 then 1440x1238), zero reloads, 14/14 pass.
  implication: Fitting the viewport removes the reload deterministically, not just by luck of timing.

- timestamp: 2026-10-14T19:40:00Z
  checked: New top-face theory before the CSS fix
  found: Classic lift 29-147 (sum of channels) on every board at 1440 and 390; the six other designs exactly 0.0 on every board (12 of 14 cases fail). Node slot check fails on ") .cubby::before: background-image" without the CSS fix and passes with it.
  implication: The gap is real and both new checks have teeth.

## Resolution

root_cause: "Test harness defect plus a design-layer gap. (1) The depth theory grew the viewport to the page height for Classic, then switched to a taller design (legs, library, display, plywood-legs); its full-page screenshots then had to capture beyond the viewport, which makes Chromium resize the page to 1x1 px and back, flipping the phone media query so cabinet.js reloads the cabinet twice per shot; a capture landing in the loading window shows the loading line and footer where the section edge ring is sampled. (2) The six non-classic designs rewrite the shelf board's background list without the --top-face-img slot, so Deep's lit top face only reached Classic."
fix: "LookDepthTests fits the viewport to the page after every look change and before every full-page picture (FitViewportToPageAsync, replacing the one-time GrowAsync), the established identity-test pattern; the ring sampling itself is unchanged. cabinet-design.css: the six designs' .cubby::before starts with var(--top-face-img) with matching size and position entries. New browser theory Deep_lights_the_top_face_of_every_shelf_board_on_every_design (7 designs x 1440/390) and a node check that every design rule writing a cubby or shelf-board background keeps the deep slot first."
verification:
  guardrail_verdict: accepted
  oracle_type: "specified (edge ring within 3/255; top-face lift >= 6 channel-sum on every board; deep slot first in every cubby/board background rule)"
  reproduction_before: "full DejaVu run: 4 failures (legs, library, display, plywood-legs at 1440); isolated: plywood-legs 1440 failed; logger shows a cabinet reload on all 6 frames of the 4 taller designs at 1440"
  reproduction_after: "logger shows zero reloads; theory 14/14"
  revert_check: "unfixed test with the logger: reload on every frame for the 4 taller designs and a failure in an isolated run; top-face theory red on 12/14 (six designs, lift 0.0) and node slot check red without the CSS change"
  build: "dotnet build Cabinet.slnx: 0 warnings, 0 errors"
  unit: "2574/2574"
  integration: "496/496 (first run had 1 unrelated SignalR WebSocket transport failure, 496/496 on rerun)"
  node: "397/397"
  lint: "bash build/lint.sh: all PASS"
  browser: "415/415 twice under DejaVu, 415/415 default fonts, 415/415 DejaVu with CABINET_SCREENSHOT_DIR set (as CI); DefaultLookIdentityTests untouched and green"
  commits: [3b0262c, 79f33a6]
files_changed: [Cabinet.BrowserTests/LookDepthTests.cs, Cabinet.Service/wwwroot/css/cabinet-design.css, build/tests/cabinet-layers.test.mjs]
