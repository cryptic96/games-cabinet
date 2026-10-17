# Phase 7: Game-Night Filters & Location Cabinets - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-10-15
**Phase:** 07-game-night-filters-location-cabinets
**Areas discussed:** Filter bar placement, Filter rules, Dim look & feedback, Location cabinets

The owner's senior-frontend skill was used as an advisory lens throughout. Its review of all decisions is `07-FE-REVIEW.md`, accepted in full ("accept all", including idea I-C).

---

## Todos folded

| Option | Description | Selected |
|--------|-------------|----------|
| Phone last-section fix | Decide what a sparse last section does; per-location cabinets multiply it | ✓ |
| Per-copy locations | Location per collection entry instead of per game; owner-data schema step | ✓ |
| Deployed checks | Android Back and screen-reader checks in this phase's device round | ✓ |

**User's choice:** all three.

---

## Filter bar placement

| Question | Options | Selected |
|----------|---------|----------|
| Desktop placement | Bar that stays on top / Bar that scrolls away / Behind a Filters button | Bar that stays on top |
| Phone filters | Search + Filters sheet / Chip row under search / One Filters button only | Search + Filters sheet |
| Phone bar on scroll | Back on scroll-up / Always pinned / Scrolls away | Back on scroll-up (pinned while a filter is active) |
| Sheet applies | Live, "Show 12 games" / Apply button | Live, "Show 12 games" |
| Count and Clear | Only while filtering / Count always visible | Only while filtering |

---

## Filter rules

| Question | Options | Selected |
|----------|---------|----------|
| Player picker | Number buttons 1–8+ / Stepper / Dropdown | Number buttons 1–8+ |
| Player-count expansions | Yes, expansions widen it / No, base game only | Yes, expansions widen it |
| Play-time range rule | No, the longest time counts / Yes, the shortest time counts | The longest time counts |
| Play-time picker | Preset buttons / Slider | Preset buttons |
| Search scope | Family lights together / Only the matching box | Family lights together |
| Unknown values | It dims / It stays lit | It dims |
| Location filter | One location at a time / Several at once | One location at a time |

---

## Dim look & feedback

| Question | Options | Selected |
|----------|---------|----------|
| Dim look | In shadow / Faded / Shadow + matches lift | In shadow |
| Dimmed taps and keys | Tap all, keys skip dimmed / Everything stays reachable / Dimmed can't be opened | Tap all, keys skip dimmed |
| Screen-reader list | Shows only matches / All, marked | Shows only matches |
| No matches | Quiet line in the bar / Note over the cabinet | Quiet line in the bar |
| Filter lifetime | Kept in this tab / Reset on every load / Remembered across visits | Kept in this tab |

---

## Location cabinets

| Question | Options | Selected |
|----------|---------|----------|
| View toggle place | Start of the filter bar / Header, beside EN · NL | Start of the filter bar |
| Cabinet name | Heading on the wall / Brass label holder / Decide by sight | Heading on the wall |
| Expansion stored apart | Where it really is / Always with its base | Where it really is |
| Default view and memory | Together, remembered here / Together, kept in tab / Owner sets the default | Together, remembered here |
| Per copy | Location and picture / Location only | Location and picture |
| Empty and Unknown | Hide empty, Unknown last / Show every location | Hide empty, Unknown last |
| Sparse last section | Drop empty shelves / Fill from the top | Drop empty shelves |
| List in By location | Grouped by location / Always one A to Z list | Grouped by location |

**Notes:** the owner left two smaller points to the review document: small location cabinets and the few-games rule (P1), and how location cabinets share a wide screen (P2).

---

## Front-end review

**Labels:** kept decisions K1 to K16, refinements R1 to R11, recommendations P1 to P4, and ideas I-A ("Show on shelf"), I-B ("/" to search) and I-C (search on BGG's alternate titles).

**User's choice:** accept all, including I-C.

## Claude's Discretion

- Data plumbing for filter fields and the "By location" payload.
- The alternate-name matching approach, within the BGG relaying and host-load limits.
- Module structure.
- Exact visual values (set in the UI design step).
- Copy in English and Dutch, with the owner reviewing the Dutch.
- Live-redraw behaviour.
- Synthetic fixtures.
- The review flow, following earlier phases.

## Deferred Ideas

- **The v2 extras** (already out of scope):
  - best-at-N, a weight filter, a random picker, and co-op or designer search;
  - address state, link previews, a stats plaque and a new-arrival marker.
- **A brass label holder** for location cabinets, set aside for the wall heading.
