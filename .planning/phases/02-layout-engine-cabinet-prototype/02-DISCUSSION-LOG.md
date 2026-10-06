# Phase 2: Layout Engine & Cabinet Prototype - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-10-04
**Phase:** 02-layout-engine-cabinet-prototype
**Areas discussed:** Cabinet shape & growth, Shelf mix, Stability rules, Expansion families, Review loop

---

## Cabinet shape & growth

### How should the cabinet's structure come about?

| Option | Description | Selected |
|--------|-------------|----------|
| Fixed furniture design | A hand-designed section with irregular cubbies, repeated as the collection grows; furniture never changes when games are added | ✓ |
| Cubbies built around games | Dividers placed between groups of games; structure shifts as the collection changes | |
| Mix of both | Fixed frame and shelf heights, content-driven vertical dividers | |

**User's choice:** Fixed furniture design
**Notes:** The owner first asked whether this could be changed later. Answer given: yes. The layout is recomputed after every sync, nothing stored depends on it, the renderer draws whatever placements it gets, and the section is kept as an editable definition. The only cost after go-live is one intentional rearrangement. The question was then asked again and fixed furniture was chosen.

### When the first section is full, how should the cabinet grow on desktop?

| Option | Description | Selected |
|--------|-------------|----------|
| Side by side, then rows | Sections fill the screen width, then a new row starts below; vertical scroll only | ✓ |
| One cabinet, taller | Single fixed-width cabinet gaining shelves at the bottom | |
| Side by side, scroll sideways | One long row of sections, horizontal scrolling | |

**User's choice:** Side by side, then rows

### What should the phone cabinet look like?

| Option | Description | Selected |
|--------|-------------|----------|
| Narrow section design | Separate narrow section in the same style, stacked vertically; a game may sit elsewhere than on desktop | ✓ |
| Desktop section, shrunk | Same section scaled to phone width; spines get tiny | |
| Desktop section, swipe sideways | Full-size sections swiped horizontally | |

**User's choice:** Narrow section design

### What goes in empty cubbies?

| Option | Description | Selected |
|--------|-------------|----------|
| Bare wood | Empty cubbies are just shaded wood | ✓ |
| A few simple decorations | CSS-drawn plant, dice, bookend in some empty cubbies | |
| Bare, plus a small note | "Room for more games" in the minimum cabinet only | |

**User's choice:** Bare wood

---

## Shelf mix

### What share of boxes should show their cover?

| Option | Description | Selected |
|--------|-------------|----------|
| About 1 in 4 | Mostly spines with a cover here and there, like a real shelf | ✓ |
| About half | More art, less shelf-like | |
| As many as fit nicely | Covers wherever a cubby has room | |

**User's choice:** About 1 in 4, and asked to make it configurable.
**Notes:** Follow-up on what "configurable" means:

| Option | Description | Selected |
|--------|-------------|----------|
| Server setting | Value in app configuration (appsettings / env file), default 1 in 4, changed by config edit and restart | ✓ |
| Setting + review knob | Server setting plus a preview-only URL parameter for review rounds | |
| Visitor control | Public control; would be a new visitor feature for a later phase | |

### Which games get to face out?

| Option | Description | Selected |
|--------|-------------|----------|
| Big boxes favoured | Larger boxes much more likely, with a stable random pick | ✓ (as default) |
| Stable random pick | Any game, by BGG id, regardless of size | |
| Only boxes that don't fit as spines | Covers only where a box can't stand as a spine | |

**User's choice:** "Can we make this a setting as well? defaulting to your recommended?"
**Notes:** Recorded as a server setting with three strategies (size-weighted default, random, oversize-only; oversize-only ignores the share setting).

### Should some boxes lie flat in stacks?

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, small and flat boxes | Small or thin boxes sometimes lie flat in short stacks | ✓ |
| Only when needed | Lie flat only when a box can't stand in its cubby | |
| No stacks | Every box stands as a spine or faces out | |

**User's choice:** Yes, small and flat boxes

### What should face-out boxes look like in this prototype?

| Option | Description | Selected |
|--------|-------------|----------|
| Generated cover | Coloured box front, title in large type, simple pattern; stays as the fallback for missing images | ✓ |
| Plain coloured block | Flat colour with the title | |
| Synthetic artwork images | Generated abstract images served like real covers | |

**User's choice:** Generated cover

---

## Stability rules

### When a new game is added, how much of the existing cabinet may change?

| Option | Description | Selected |
|--------|-------------|----------|
| Nothing moves | Existing boxes keep their exact spot; matches the roadmap wording | |
| Only its own cubby | The receiving cubby may rearrange; everything else stays put | ✓ |
| Only its own shelf | The whole receiving shelf row may rearrange | |

**User's choice:** Only its own cubby
**Notes:** This refines roadmap success criterion 3 ("adding a game does not move any existing box").

### Where should a newly added game go?

| Option | Description | Selected |
|--------|-------------|----------|
| First cubby with room | First cubby (top-left onward) with space; gaps get used | ✓ |
| Always at the end | Last partly filled cubby or a new one; recent additions together | |

**User's choice:** First cubby with room

### What happens when a game is removed?

| Option | Description | Selected |
|--------|-------------|----------|
| Rearrange from scratch | Pure function of the current collection; a removal can shift later boxes | ✓ |
| Leave a gap | Gap stays until a new game fills it; needs stored layout state | |

**User's choice:** Rearrange from scratch

### Few games → normal mix transition

| Option | Description | Selected |
|--------|-------------|----------|
| One-time switch | All face out below the threshold; crossing it rearranges once | ✓ |
| Gradual taper | Cover share drops slowly, small changes keep happening | |
| No few-games rule | Always the normal mix, even with 5 games | |

**User's choice:** One-time switch

---

## Expansion families

### What should a base game's expansions look like?

| Option | Description | Selected |
|--------|-------------|----------|
| Flat stack beside it | Expansions lie flat next to the base game, names horizontal; stack grows upward | ✓ |
| Thin upright spines | Narrower upright spines beside the base; each one needs more width | |
| Lying on top of the base | Stacked on the base box; needs headroom | |

**User's choice:** Flat stack beside it

### When the base faces out, where does the stack go?

| Option | Description | Selected |
|--------|-------------|----------|
| Beside the cover | Families always read as base game + stack | ✓ |
| Always a spine | Games with expansions never face out | |

**User's choice:** Beside the cover
**Notes:** The owner asked to go back to this question and have it explained better. It was asked again with ASCII sketches of both options, and the same answer was confirmed.

### When do expansions collapse into "+N more"?

| Option | Description | Selected |
|--------|-------------|----------|
| When the stack is full | As many as fit under the shelf above, up to a configurable max; new expansion with no room raises N | ✓ |
| Fixed small cap | Always at most 3 visible, then "+N more" | |

**User's choice:** When the stack is full (asked with sketches)

### Where do orphan expansions go?

| Option | Description | Selected |
|--------|-------------|----------|
| Like any other game | First cubby with room, labelled "Expansion for <base game>" | ✓ |
| Grouped in one spot | All orphans in a "loose expansions" stack in the last cubby | |

**User's choice:** Like any other game

---

## Review loop

| Option | Description | Selected |
|--------|-------------|----------|
| Screenshots, then deploy | Local iteration with screenshots at desktop and phone widths per sample; a tagged release for the final check on the server | ✓ |
| Every round deployed | Each round is a tagged, approved release | |
| Local dev server | Owner runs the app locally; the server only sees the final version | |

**User's choice:** Screenshots, then deploy

---

## Claude's Discretion

- Section dimensions, shelf heights, divider positions and irregularity (desktop and phone)
- Few-games threshold and minimum cabinet size
- Base-game stack height and what counts as "small or flat"
- Within-cubby arrangement rule (within D-09/D-12)
- Spine text fitting and minimum sizes, placeholder spine palette with guaranteed contrast
- Synthetic collection design (invented titles only)
- How the owner picks a sample on the deployed prototype (must not survive as a public feature)
- Whether the prototype replaces the hello page; keep the version visible
- Layout transport (JSON + client renderer vs server-rendered markup), number of profiles
- Names and shape of the layout settings

## Deferred Ideas

None. A visitor control for the cover mix and decorations in empty cubbies were offered and not chosen.
