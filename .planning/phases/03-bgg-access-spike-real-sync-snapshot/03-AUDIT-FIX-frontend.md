# Phase 3: Audit Fix Report (front end)

**Fixed at:** 2026-10-07
**Source:** 03-UI-REVIEW.md (priority fixes and owner question 1), 03-SECURITY.md (T-03-59 client side, observation 21)
**Verification ran in:** the isolated worktree of this fixer (not the main checkout). `node --test build/tests/page-scripts.test.mjs` 75 passed; `dotnet test --solution Cabinet.slnx` 907 passed, 0 failed; `build/lint.sh repo-rules` passed.

**Summary:** 6 of 6 items fixed.

## Fixed

### 1. Footer centre line
**Files:** `Cabinet.Service/wwwroot/css/site.css`
**Commit:** 569b3de
Added `align-items: center` to the `footer` rule. Only the cross axis changes, so the 32px logo floor, the wrapping and the left alignment are untouched. At 1440 the version text and logo share one row and a centre line; at 390 and 320 the items wrap onto their own lines, each left aligned and centred within its own line height.

### 2. Width limits
**Files:** `Cabinet.Service/wwwroot/css/site.css`
**Commit:** 5c76ca1
`max-width: 40rem` on `.sync` and `.sync-exact`.

### 3 and 4. Notes that never clear; stacked held-back sentences
**Files:** `Cabinet.Service/wwwroot/js/sync.js`, `Cabinet.Service/wwwroot/css/site.css`, `build/tests/page-scripts.test.mjs`
**Commit:** d95d9da (one commit, because both changes share the note helpers)
- The offline sentence clears on the next status that reaches `applyStatus`, from any source.
- "A sync is already running." clears when the button leaves the running state (to the window or to idle) with no own press waiting; an own press waiting still gets its outcome sentence. Clearing is the only thing the page does to the note; only the press writes it. The existing clear at the end of the window is kept.
- The visitor's own held-back sentence is covered while the held-back older-sync note is showing: `#sync-note` keeps the text (it stays the only live region and is still announced) and gets `data-covered`, which a CSS rule turns into a visually hidden element. The wording is unchanged, and the sentence shows again if the older-sync note goes away.
- Tests: connection sentence clears and only then; a refused press that returned a status; a status inside the window leaves the come-back sentence alone; already-running clears with and without a window; own pending press is not cleared; cover on and off; no cover without an older-sync note. Four of these fail on the old code (checked).
- The test page fake gained a `dataset` on the note and a minimal time button so a page with a last sync can be built.

### 5. Reconnect jitter (client side)
**Files:** `Cabinet.Service/wwwroot/js/status.js`, `Cabinet.Service/wwwroot/js/live.js`, `build/tests/page-scripts.test.mjs`
**Commit:** c759104
- `reconnectDelayMs(previousRetryCount, random = Math.random)`: steps of 2 s, 10 s, 30 s and 60 s vary by up to 20 percent either way; the first retry is 250 ms plus a random 0 to 999 ms, so it is never zero and pages that lost the channel together do not return together. The random answer is clamped into [0, 1) (non-numbers count as the middle), so a delay is never negative or out of range.
- `startLive` takes an optional `random`, used by both the automatic-reconnect schedule and the page's own back-off loop.
- Tests are deterministic through the injected source (middle, lowest, highest, bad values, and a Math.random bounds check). The fake connection gained `refuse()`: a close with no reconnecting or reconnected events, which is what a refused page now looks like; the "closed again at once" test uses it and its expected start times moved to the jittered schedule.
- Needs the back-end change (refusal without auto-reconnect) to be effective end to end. `docs/bgg-sync.md` still describes the schedule as "at once, 2 s, 10 s, 30 s": docs were out of scope for this fixer, so that wording needs a follow-up by whoever owns the docs.

### 6. noindex
**Files:** `Cabinet.Service/Pages/Shared/_Layout.cshtml`, `Cabinet.IntegrationTests/CreditTests.cs`, `.planning/todos/completed/2026-10-06-turn-off-prototype-mode-and-add-noindex-before-go-public.md` (moved with `git mv` from pending, one status note appended)
**Commit:** 9a385fd
`<meta name="robots" content="noindex" />` in the layout head. `Every_razor_page_asks_search_engines_not_to_index_it` reuses the credit test's page enumeration and checks each page has exactly one such tag, in the head.

## Skipped

None.

---

_Fixed: 2026-10-07_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
