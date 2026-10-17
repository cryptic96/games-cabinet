# Research questions for the owner

**Outcome (2026-10-15):** the owner accepted everything (Q1 to Q3, and N1 to N3 as settled). Recorded in `07-RESEARCH.md` under "Open Questions" and, for Q1, in the UI contract's assumption A5.

Research for this phase is done (`07-RESEARCH.md`). It left six open questions:
- three change behaviour you will see, so they are yours (Q1 to Q3);
- one is a check the plans will include (N1);
- two are internal code choices I have settled (N2, N3).

Answer by label: "accept all", or name the labels to change.

## Your call

**Q1. Wait for the game details whenever any filter is remembered, search included.**
- **Today's contract.** The UI contract waits for the game details at load only when a remembered players, time or location filter is active. A search-only filter draws at once from the layout.
- **What research found.** An expansion owned for two base games only lights correctly once the game details have arrived. A search-only first draw could therefore show such an expansion dimmed for a moment, then lit, which breaks "never a wrong first frame".
- **Proposal: one rule.** If any filter is remembered, the first draw waits for the game details too. Both are fetched at the same time, so the cost is small. With no remembered filter, nothing changes.

**Q2. A game you re-add on BGG keeps its location and picture.**
- **The problem.** Location and picture are now kept per copy, keyed by BGG's collection entry. When you remove a game on BGG and add it back, BGG most likely gives it a new collection entry, so the old choices would no longer match it.
- **Proposal.** While the old entry's choices are still kept (the 30-day rule), a newly listed copy of the same game that has no choices of its own takes them over.
- **Result.** Removing and re-adding a game, for example to fix its edition, does not lose where it lives.

**Q3. The shorter last section applies only where sections are not balanced.**
- **What changes.** The fix for empty shelves above a sparse last section is applied to the last section only when the layout does not balance section heights, which covers:
  - every phone layout;
  - every small location cabinet.
- **What stays.** Desktop layouts that balance their sections keep their current heights. That leaves the desktop look and its tests untouched.

## Settled, for your information

**N1. Check BGG's real data shapes without recording them.** The repo has never stored a real BGG answer, and must not. The plans therefore:
- build tolerant readers for the owned edition's name and for alternate titles;
- extend the existing BGG access check on the server to report counts only, for example "edition name present for 37 of 48 copies".

**N2. The Filters sheet reuses the card's drag helpers without restructuring the card code.** The card's drag handling stays in place. The sheet reuses its small exported helpers (drag outcome, release speed, resistance). Moving the card code around would risk its tests for no gain.

**N3. Fixed labels are rendered by the server, changing text by the page script.** Fixed control labels (Players, Max. minutes, View) come from the server-side text in both languages. Changing text (counts, announcements) comes from the page script's text table. Strings that exist in both share one test fixture, so they cannot drift apart.
