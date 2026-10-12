# Phase 5: Game Detail, Accessibility & Language - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md. This log preserves the alternatives that were considered.

**Date:** 2026-10-09
**Phase:** 05-game-detail-accessibility-language
**Areas discussed:** Pull-out & card shape, Card content & gaps, Keyboard & screen reader, Language switch
**Todos folded:** cabinet accessibility notes, the previous phase's UI-review polish, family stack cap across cubbies

---

## Pull-out & card shape

### How a box comes out of the shelf
| Option | Description | Selected |
|--------|-------------|----------|
| Lift, turn, become card | The box slides out, a spine turns to its cover, and the cover morphs into the card (View Transitions, with a fallback) | ✓ |
| Lift out, card beside | The box stays raised on the shelf, and the card fades in over the cabinet | |
| Quick pop | A short lift of about 150 ms, then the card | |

### Desktop card position
| Option | Description | Selected |
|--------|-------------|----------|
| Centred over dim cabinet | Native modal dialog in the middle of the screen | ✓ |
| Side panel | Slides in from the right, and the cabinet stays live | |
| Next to the box | A popover anchored beside the pulled-out box | |

### Phone card form
| Option | Description | Selected |
|--------|-------------|----------|
| Bottom sheet | Rises from the bottom; drag down, tap the backdrop or press ✕ | ✓ |
| Full screen | The whole screen, with a back button | |
| Same centred card | The desktop card, narrower | |

### Back button
| Option | Description | Selected |
|--------|-------------|----------|
| Yes, Back closes the card | One history step, the address unchanged | ✓ |
| No, Back leaves the page | Closes only with ✕, Escape, outside tap or drag | |

### A sync while a card is open
| Option | Description | Selected |
|--------|-------------|----------|
| Card stays, cabinet waits | The redraw runs after close | ✓ |
| Redraw behind the card | Immediate redraw behind the backdrop | |
| Close card and redraw | Close with a note | |

### Stepping to the next game from the card
| Option | Description | Selected |
|--------|-------------|----------|
| No, close and tap again | One box out at a time | ✓ |
| Prev / next arrows on the card | Step through in cabinet order | |
| Swipe on phone only | A hidden gesture | |

### Speed
| Option | Description | Selected |
|--------|-------------|----------|
| Snappy, about 350 ms | Close plays in reverse, slightly faster | ✓ |
| Showy, about 700 ms | Slower and more theatrical | |
| Decide in the screenshot review | A tuning value | |

### Reduced motion
| Option | Description | Selected |
|--------|-------------|----------|
| Short cross-fade | The card fades in over about 150 ms; no lift, turn or morph | ✓ |
| Instant, no transition | A single-frame open and close | |

### Card look
| Option | Description | Selected |
|--------|-------------|----------|
| Mock up directions, then pick | Two or three directions in the UI design step | ✓ |
| Back of a game box | A stats strip like a box back | |
| Clean neutral panel | Plain panel in the page colours | |

**Notes:** Mid-area, the owner asked to use the senior-frontend skill as an advisory lens (not to create mockups) and to have it review every decision of the session. The review was written to `05-FE-REVIEW.md` at the owner's request, so it could be read properly instead of in a question title. Verdict: all nine decisions kept. Seven build refinements were proposed (R1 to R7: tunable timing with a faster close, the turn as a snapshot flip, a plain fade fallback, dim without blur, sheet dragged only by its handle or header, card data preloaded, Back coordinated with Chrome's close-watcher behaviour), plus a single-height sheet (S1), the gap staying visible (I-A), the cover spot in the box's real proportions (I-B), and mock-ups judged against named real references (I-C). **Owner: "accept all".**

---

## Card content & gaps

### Rating
| Option | Description | Selected |
|--------|-------------|----------|
| Average rating, e.g. 7.8 | BGG's headline number, "7.8 / 10" | ✓ |
| Geek rating (ranked) | Bayesian, pulls toward 5.5 | |
| Both | "7.8 (geek 7.2)" | |

### Weight
| Option | Description | Selected |
|--------|-------------|----------|
| Word plus number | "Medium-light · 2.4 / 5", BGG bands | ✓ |
| Number only | "2.4 / 5" | |
| Word only | "Medium-light" | |

### Location row while all locations are empty
| Option | Description | Selected |
|--------|-------------|----------|
| Hide when unknown | Designed and tested with invented locations locally | ✓ |
| Always show, "Not recorded yet" | Same layout on every card | |

### Owned expansions on a base game's card
| Option | Description | Selected |
|--------|-------------|----------|
| Yes, swaps the card in place | Cross-fade to the expansion's card, with a link back to its base | ✓ |
| Names only | A plain list | |

### "+N more" marker
| Option | Description | Selected |
|--------|-------------|----------|
| Base game's card, at its expansions | One mechanism, with the hidden expansions tappable | ✓ |
| A small list of just the hidden ones | A second kind of pop-up | |

### Family stack cap (folded todo)
| Option | Description | Selected |
|--------|-------------|----------|
| Keep, document as intended | Per-column cap, matches the approved look, no layout change | ✓ |
| One cap across both columns | Layout version bump, re-recorded goldens | |

### Missing details
| Option | Description | Selected |
|--------|-------------|----------|
| Show what's known, quiet note | Missing rows left out, BGG 0 counts as missing, one note when everything is missing | ✓ |
| Leave rows out, no note | A shorter card with no explanation | |
| Show "unknown" per row | Always every row | |

### Card hierarchy
| Option | Description | Selected |
|--------|-------------|----------|
| Game-night strip first | Players, time, weight and age under the title; rating lower | ✓ |
| Rating up top | Rating large next to the title | |

### Mechanics
| Option | Description | Selected |
|--------|-------------|----------|
| All, as a quiet wrapped list | Nothing hidden | ✓ |
| First few, then "+N more" | About five, with a tap for the rest | |
| All, as tag chips | Pills | |

**Notes:** Front-end notes added at the summary: the strip's icons are the site's own and never emoji, with text labels; the expansions heading speaks from the visitor's side ("Owned expansions", not "Expansions you own").

---

## Keyboard & screen reader

### Arrow keys inside the cabinet
| Option | Description | Selected |
|--------|-------------|----------|
| Spatial | Left/right on the shelf, up/down to the nearest box above or below, Home/End | ✓ |
| Reading order | Every box in cabinet order | |

### Who sees the games list
| Option | Description | Selected |
|--------|-------------|----------|
| Screen readers only, with a skip link | Visually hidden, with a skip link on focus | ✓ |
| A visible list toggle for everyone | A Cabinet/List switch in the header (adds scope) | |

### List order
| Option | Description | Selected |
|--------|-------------|----------|
| A–Z, expansions nested | Base games alphabetical, expansions under their base | ✓ |
| Cabinet order | The same order as the shelves | |

### List entry announcement
| Option | Description | Selected |
|--------|-------------|----------|
| Title plus game-night facts | "Title (2019), 2–4 players, 60–90 minutes, medium-light" | ✓ |
| Title and year only | "Title (2019)" | |

---

## Language switch

### Position and look
| Option | Description | Selected |
|--------|-------------|----------|
| Header, top right, "EN · NL" | A text toggle next to the title, no flags | ✓ |
| Footer, by the version | Out of the way, a long scroll on big cabinets | |
| In the status line | Appended to the sync line | |

### Remembering the choice
| Option | Description | Selected |
|--------|-------------|----------|
| Small preference cookie | Server renders the right language from the first byte | ✓ |
| Browser storage (localStorage) | No cookie, but the wrong language flashes first | |
| Language in the address (/nl/) | Separate addresses per language | |

### Dutch tone
| Option | Description | Selected |
|--------|-------------|----------|
| Informal "je" | Friendly, suits a site for friends | ✓ |
| Formal "u" | Polite and stiff | |
| Avoid addressing at all | Neutral, sometimes clumsy | |

### BGG words on the Dutch page
| Option | Description | Selected |
|--------|-------------|----------|
| Stay exactly as BGG gives them | Marked as English; the site's own labels translated | ✓ |
| Translate mechanics with a fixed list | A hand-made Dutch name per mechanic | |

**Notes:** At wrap-up the owner accepted the defaults: formats follow the language (Dutch "2,4" and "9 oktober 2026 om 14:32"; English keeps `en-NL`), and Claude drafts the Dutch for the owner to review in a side-by-side table during the screenshot round.

---

## Claude's Discretion

- View Transition choreography and easing, bottom sheet drag mechanics, the Back button and close-watcher mechanism, how backdrop clicks close the dialog
- How card data is delivered (layout JSON or a sibling endpoint, only the fields shown)
- Play time and player count formatting, the BGG link's wording and new tab
- The spatial navigation algorithm, the screen-reader list markup and avoiding duplication, live-region wording
- Language plumbing: resolver, shared translations for Razor and JS, cache variation, cookie attributes, `<html lang>`
- How the folded polish items are fixed, and the review flow (UI design step with the senior-frontend skill, local screenshots in both languages and reduced motion, release, deployed check)

## Deferred Ideas

- A visible list view for every visitor (Cabinet/List toggle)
- Shareable links to a game's card (v2 sharing extras)
- Stepping between games on the card (previous/next or swipe)
