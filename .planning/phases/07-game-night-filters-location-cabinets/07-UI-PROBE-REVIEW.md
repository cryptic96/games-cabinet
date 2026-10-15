# State-coverage check of the filters design contract

**Outcome (2026-10-15):** the owner accepted everything (K and P1 to P18). The resolutions are written into the "UI Considerations" section of `07-UI-SPEC.md`.

This is the last step of the UI design workflow. A probe lists, for each surface the contract describes, the states it must account for: empty, loading, error, partial data, overflow, long text, and zero, one or many items. Each listed state then needs one of four outcomes:
- a concrete answer;
- a held-out check (a test or the device round);
- a reasoned dismissal;
- a deferral.

Answer by label: **"accept all"**, or name the labels to change. **K** = the kinds question, **P** = one proposed resolution.

## K. Which kinds each surface is

The probe picks the states from each surface's kind (form, list, navigation, control, static text). Its automatic guess works from words in the description. It reads "rows" and "shelf" as list cues, so it would ask list questions of the "Show on shelf" button and of the empty-shelf fix: 69 states in all. The contract assigned the kinds by hand, which gives 54.

| Surface | Kinds (as the contract assigned them) |
|---------|---------------------------------------|
| E1 Full filter bar | form, control, navigation |
| E2 Slim bar and Filters sheet | form, control |
| E3 The filtered cabinet | list |
| E4 Count, Clear and the live region | static text, control |
| E5 Location cabinets and headings | list, static text |
| E6 View toggle and view switch | navigation, control |
| E7 Screen-reader games list | list |
| E8 Card "Show on shelf" button | control |
| E9 Owner rows per copy | form, list |
| E10 Sparse last section | static text |

**Proposed:** keep the contract's kinds. None is missing; the extra guesses are word matches.

## The 18 states the contract did not answer yet

36 of the 54 already have an answer in the contract. These are the rest.

| Label | Surface | State | Proposed outcome |
|-------|---------|-------|------------------|
| P1 | E2 Slim bar and sheet | empty | **Answer:** with no filter the slim bar shows only the search field and "Filters"; the sheet opens with every group on the quiet "Any" and its button reads "Show 48 games" |
| P2 | E2 Slim bar and sheet | partial | **Answer:** with no locations the sheet has no view toggle and no location control and opens on players; if the game details failed to load, players, time and location are disabled with the existing note and search still works |
| P3 | E4 Count and Clear | error | **Answer:** the count has no request of its own; it is computed while drawing. If the game details failed, it counts only the search, since the other filters are off |
| P4 | E4 Count and Clear | overflow | **Answer:** the full bar keeps a reserved space for it in row 2 (at least 39 px spare in the widest case); the slim bar's short count keeps one row from 360 px, and below that it wraps as a fallback |
| P5 | E6 View switch | error | **Answer:** a failed "By location" load keeps "All together" on screen, sets the toggle back, announces the error text and does not overwrite the remembered view; the next switch tries again |
| P6 | E6 View switch | overflow | **Answer:** two fixed short labels in both languages (measured 210 to 234 px); the toggle never wraps or cuts text |
| P7 | E7 Screen-reader list | loading | **Answer:** the list is built from data the cabinet has already loaded and is rebuilt in the same pass as a new view, so it never shows a stale grouping |
| P8 | E7 Screen-reader list | error | **Answer:** if the game details failed, entries keep the facts the layout carries and only search filtering applies; a failed "By location" load leaves the plain A to Z list |
| P9 | E7 Screen-reader list | populated | **Held-out check:** the filtered and grouped list reads correctly with a real screen reader. It is checked in this phase's device round on the deployed release |
| P10 | E7 Screen-reader list | overflow | **Answer:** the list is visually hidden; when focus is inside, the visible panel keeps its existing scroll and 44 px rows, and a long location name wraps |
| P11 | E8 "Show on shelf" | loading | **Answer:** if the "By location" data is not loaded yet, the card closes and the current view stays until it arrives (no spinner); then the switch, scroll and outline run |
| P12 | E9 Owner rows | loading | **Answer:** the owner pages are drawn on the server with every copy in the response; no loading state is added |
| P13 | E9 Owner rows | error | **Answer:** saving a copy's location or picture uses the existing status line and Undo; a failed save shows the existing error status, unchanged |
| P14 | E9 Owner rows | populated | **Held-out check:** a game owned in several versions shows one row per copy, labelled by edition or "Copy 1 of 4", on both owner pages. Checked by the owner-fixture browser test and in the screenshot round |
| P15 | E9 Owner rows | partial | **Answer:** a copy without an edition name is labelled "Copy k of n"; a copy with no location shows "Not placed yet" as today; an expansion following a base whose copies are in different places shows "Follows the first copy of {base title}." |
| P16 | E9 Owner rows | overflow | **Answer:** rows are one column on phones; the copy tag wraps under the title; the copies of one game stay together in stable order |
| P17 | E10 Sparse last section | overflow | **Answer:** it never overflows; its height is the layout engine's value for its filled rows (at least two), and the reserved shape follows it |
| P18 | E10 Sparse last section | long text | **Dismissed:** the section has no text of its own; box labels follow the existing label rules, unchanged |

## Rows updated for the accepted review

These existing answers are rewritten to match the review you accepted. They are not new decisions:
- **Full bar overflow and long text (E1):** two designed rows at a stable 112 px.
- **Slim bar long text (E2):** the short count and the icon Clear keep one row.
- **Zero, one or many matches (E3):** "0 of 48" in the slim bar.
- **Count with no filter (E4):** its space is kept but nothing shows.
- **Location cabinets (E5):** a cabinet with no match shows the quiet "0 of 14 games" heading.
- **"Show on shelf" (E8):** it scrolls the box itself into view.
