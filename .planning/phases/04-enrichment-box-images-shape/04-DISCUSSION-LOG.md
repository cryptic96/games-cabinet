# Phase 4: Enrichment, Box Images & Shape - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-10-07
**Phase:** 04-enrichment-box-images-shape
**Areas discussed:** Picking the box image, Fitting art to the box, Spine look from the art, Expansion edge cases

---

## Todo folding

| Option | Description | Selected |
|--------|-------------|----------|
| Box look polish | Written for this phase: small-cover ellipsis, upright-expansion labels, readability minimums, cover share, plinth arch | ✓ |
| Phone cabinet density | Tagged for the filters and locations phase | ✓ |
| Cabinet accessibility notes | Tagged for the detail and accessibility phase | ✓ |
| Selectable finishes | Unscheduled; possibly its own phase | ✓ |

**User's choice:** All four at first.
**Notes:** Claude flagged that three of them belong to other phases and that selectable finishes is a new capability. Follow-up question:

| Option | Description | Selected |
|--------|-------------|----------|
| Polish + density + label fix | Box-look polish and phone density in full, plus the "+N more" accessible-name fix; arrow-key navigation stays with the detail phase; finishes stay unscheduled | ✓ |
| All four in full | Also arrow-key navigation and selectable finishes with a lit-cubbies toggle | |
| All but finishes | Polish, density and both accessibility notes in full | |

**User's choice:** Polish + density + label fix.

---

## Picking the box image

| Option | Description | Selected |
|--------|-------------|----------|
| Lean to the main image | Fewer slanted shots; some flat Dutch covers replaced until overrides | ✓ |
| Lean to your version | Own edition more often; some slanted shots face out | |
| You decide | Claude picks during research | |

**User's choice:** Lean to the main image.

| Option | Description | Selected |
|--------|-------------|----------|
| Generated cover | No slanted shot ever sits in a flat box frame | |
| The 3D shot anyway | Real art beats a generated cover | ✓ |
| Avoid facing it out | Prefer a spine; orientation would depend on image analysis | |

**User's choice:** The 3D shot anyway (when both candidates are 3D shots or the main image is missing).

| Option | Description | Selected |
|--------|-------------|----------|
| Your version's shot | Own edition is the more truthful picture when neither is flat | ✓ |
| The main image | Always fall back to main when the version image is 3D | |
| The cleaner one | Whichever scores closer to flat | |

**User's choice:** Your version's shot.

| Option | Description | Selected |
|--------|-------------|----------|
| Review sheet | Contact sheet of both candidates, verdict and score, sent as images, never committed | ✓ |
| Just the deployed cabinet | Owner points out wrong ones on the site | |
| Diagnostic page | Hidden page listing both candidates; must be removed or owner-only before go-public | |

**User's choice:** Review sheet.

| Option | Description | Selected |
|--------|-------------|----------|
| Live with them | Tune, accept leftovers, fix with overrides later | ✓ |
| Stopgap list | Env-file list forcing specific games now | |
| Zero misses gate | Phase closes only when every game is right | |

**User's choice:** Live with them.

---

## Fitting art to the box

| Option | Description | Selected |
|--------|-------------|----------|
| Never crop | Downscale only; fit whole; fill leftover space with the art's colour | ✓ |
| Trim plain borders only | Trim uniform margins around flat covers | |
| Trim + small crop | Also crop a few percent per side | |

**User's choice:** Never crop.

| Option | Description | Selected |
|--------|-------------|----------|
| Sizes, unless implausible | Real sizes first; a clear mismatch with a flat cover's shape marks them as wrong | ✓ |
| Always the sizes | Sizes win whenever plausible | |
| Always the flat cover | Cover sets the front; sizes only scale and thickness | |

**User's choice:** Sizes, unless implausible.

| Option | Description | Selected |
|--------|-------------|----------|
| From the game's data | Size class from weight, play time, player count; cover sets shape | ✓ |
| One standard size | Common hobby size shaped by the cover | |
| Your collection's median | Median of known sizes, shaped by the cover | |

**User's choice:** From the game's data.

| Option | Description | Selected |
|--------|-------------|----------|
| One-line minimum | True thickness down to one line of title; drop the second line below two lines' room | ✓ |
| Keep two-line minimums | Readability first; thin boxes still look thick | |
| True thickness always | Text may vanish on the thinnest spines | |

**User's choice:** One-line minimum.

---

## Spine look from the art

| Option | Description | Selected |
|--------|-------------|----------|
| One solid colour | Art's main colour, title on it | ✓ |
| Colour + accent bands | Second art colour as thin bands | |
| You decide | Mock both up | |

**User's choice:** One solid colour.

| Option | Description | Selected |
|--------|-------------|----------|
| True to the art | Only lightness nudged for contrast | ✓ |
| Toned for the shelf | Saturation and lightness evened out | |
| Mock both up | Decide from screenshots | |

**User's choice:** True to the art.

| Option | Description | Selected |
|--------|-------------|----------|
| Black or white | Whichever reads at least 4.5 to 1 | ✓ |
| A colour from the art | Contrasting art colour when it reaches 4.5 to 1 | |

**User's choice:** Black or white.

| Option | Description | Selected |
|--------|-------------|----------|
| Decide in review | Screenshots at two or three cover shares; pick becomes the committed default | ✓ |
| Keep about 1 in 4 | Leave the setting as is | |
| More covers | Raise toward 1 in 3 now | |

**User's choice:** At first "I'm not sure what you mean". After an explanation of face-out boxes versus spines: picking from screenshots is fine.
**Notes:** The owner asked whether the cover share was already configurable in appsettings, as decided in the layout phase. Claude checked and confirmed:
- `Layout:CoverSharePercent`: 25, range 0 to 100.
- `Layout:CoverStrategy`: `SizeWeighted`, `Random` or `OversizeOnly`.
- `Layout:ExpansionStackMax`: 6, range 1 to 20.
- `Layout:FewGamesThreshold`: 12, range 0 to 100.
- `Layout:LieFlatBeforeNewSection`: true or false.

All are validated at startup, documented in `docs/cabinet-layout.md` and overridable in the env file. The review only picks the committed default.

---

## Expansion edge cases

| Option | Description | Selected |
|--------|-------------|----------|
| Beside the first owned | Lowest collection id; stable | ✓ |
| Beside the 'main' one | BGG's first-listed base | |

**User's choice:** Beside the first owned.

| Option | Description | Selected |
|--------|-------------|----------|
| Both boxes | Every owned entry is a physical box | ✓ |
| Hide the contained one | Suppress expansions contained in an owned big box | |
| Not sure / check data | Research checks first | |

**User's choice:** Both boxes.

| Option | Description | Selected |
|--------|-------------|----------|
| Follow BGG's type | Base-games call = base game; only expansion-typed items pair | ✓ |
| Pair if base owned | A game that also expands an owned game joins its family | |

**User's choice:** Follow BGG's type.

| Option | Description | Selected |
|--------|-------------|----------|
| New at once, rest weekly | New games enriched in the same sync; others about weekly, spread over runs | ✓ |
| New at once, rest daily | Daily refresh of everything | |
| Everything every sync | All details every hour | |

**User's choice:** New at once, rest weekly.

---

## Claude's Discretion

- Detector signals and thresholds.
- Image variants, cache, caps and the host allowlist.
- Colour extraction and the fill colour.
- Size-class model and the disagreement margin.
- Snapshot shape and schema bump.
- Request budget and refresh spreading.
- Orphan labels with several bases.
- Phone density approach.
- Review and release flow, including the UI design step.

## Deferred Ideas

- Owner image overrides (owner-tools phase).
- Arrow-key navigation / one tab stop (detail phase).
- Showing enriched details (detail phase).
- Straightening 3D shots (v2, CABX-04).
- Selectable finishes (unscheduled).
