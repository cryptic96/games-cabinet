---
created: 2026-10-08T00:00:00.000Z
updated: 2026-10-08T00:00:00.000Z
title: Sharpen tests loosened during phase 4 (code review)
area: tests
severity: minor
files:
  - Cabinet.UnitTests/Layout/PhoneProfileTests.cs
  - Cabinet.IntegrationTests/LayoutEndpointTests.cs
  - Cabinet.UnitTests/Layout/ShelfMixTests.cs
  - Cabinet.UnitTests/Layout/SectionDesignTests.cs
  - Cabinet.IntegrationTests/RealNetworkGuardTests.cs
  - Cabinet.IntegrationTests/LocalArtTests.cs
  - Cabinet.IntegrationTests/ArtChoiceTests.cs
---

## Problem

Found in the phase 4 code review (tests WR-01 to WR-05, plus info items). The owner chose to log them.

- Phone-versus-desktop section checks were loosened from "more" to "at least as many", and one 65-game check from "fewer" to "no more".
- A family check now allows two cubbies without asserting that the second one is the neighbour.
- `RealNetworkGuardTests` creates the program's clients by string name, so a renamed or extra client would not make it fail; one theory case leaks its factory when an earlier assertion fails.
- Some tests depend on the real clock (a 90 s wait running in parallel with the suite, a 250 ms lower bound against a 300 ms delay, a fixed real delay).
- The "older analysis is measured again" test has no control case.
- Info: a `fit` assertion that accepts every value, the `+N more` marker no longer exercised through the served layout, and settings pins that claim more than they test.

## Solution

Restore strict bounds where the design allows it, or document why equality is now expected. Assert neighbouring cubbies. Enumerate the program's registered clients instead of naming them. Move timing-sensitive tests to fake time. Add the missing control.
