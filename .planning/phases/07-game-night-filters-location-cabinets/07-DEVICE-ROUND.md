# Device round on the deployed release (v0.9.0)

Checks that only a real phone and a real screen reader can judge. Run them on the cabinet's address (the live site). Nothing here needs a computer.

Answer per label: `pass`, `fail` with what happened, or `not run` with the reason ("not run: no Android phone" is fine). Do not send screenshots that show your collection, your storage places or the site's address; crop them or describe in words.

How the result lines work: every step ends with a line like `Result A1: pending`. Your answers get written over `pending`.

## Part A. Android Back (an Android phone, Chrome; borrowed is fine)

Back means the system Back, by button and by swipe from the screen edge. Each Back should close exactly one thing.

### A1. Back closes an open card

1. In Chrome, open any other page first (for example a search page), then open the cabinet's address.
2. Tap a box so its card opens.
3. Press the system Back button straight away.

Pass: the card closes and you are still on the cabinet.

Result A1: pending

### A2. Back closes a card after tapping inside it

1. Open a card, then tap inside it (for example on one of its rows).
2. Press the system Back button.

Pass: the card closes and you are still on the cabinet. The tap inside did not add an extra step.

Result A2: pending

### A3. Back gesture closes an open card

1. Open a card.
2. Swipe in from the screen edge (the back gesture).

Pass: the card closes and you are still on the cabinet.

Result A3: pending

### A4. Back gesture after tapping inside the card

1. Open a card, tap inside it, then use the back gesture.

Pass: the card closes and you are still on the cabinet.

Result A4: pending

### A5. With nothing open, Back leaves the page as before

1. Close any card or sheet so nothing is open.
2. Press Back once.

Pass: the browser returns to the page you opened in A1 step 1 (the cabinet is left in one step, not stuck).

Result A5: pending

### A6. Back closes the Filters sheet and nothing else (button)

1. On the phone-width cabinet, tap Filters so the sheet opens.
2. Press the system Back button.

Pass: only the sheet closes. You stay on the cabinet, no card opens or closes, nothing else changes.

Result A6: pending

### A7. Back with a filter active keeps the filter (button)

1. Open the Filters sheet, pick a player count, and leave the sheet open (or close it with Done and reopen it).
2. With the sheet open, press the system Back button.

Pass: the sheet closes, the filter is still on (the bar shows the count such as "12 of 48" and the Filters button shows "Filters - 1"), and you are still on the cabinet.

Result A7: pending

### A8. The same by gesture

1. Open the Filters sheet, with a filter chosen as in A7.
2. Use the back gesture from the screen edge.

Pass: the same as A6 and A7: only the sheet closes, the filter stays, you stay on the cabinet. Then, with nothing open, one more Back leaves the page.

Result A8: pending

## Part S. Screen reader (TalkBack on Android, or VoiceOver on iPhone)

Turn the screen reader on, then use the live cabinet. Swipe right for the next item, double-tap to activate.

### S1. The skip link lands on the games list

1. Load the page and swipe right from the top.
2. The first item should be "Skip to the games list". Activate it.

Pass: it is announced as a skip link, and after activating it the next item you reach is the games list (its first game).

Result S1: pending

### S2. An entry reads its title, facts and location

1. In the games list, swipe through about five entries.

Pass: each entry reads the game's title and its game-night facts (players, play time, weight), and the place where it is stored when it has one.

Result S2: pending

### S3. Filtering gives one count announcement

1. Open Filters (or use the filter bar) and choose a player count, then type a few letters in the search field.
2. After you stop typing, wait a moment.

Pass: you hear the count once, in the full form such as "12 of 48 games", not once per letter and not the short form "12 of 48".

Result S3: pending

### S4. The filtered list holds only the matches

1. With the filter from S3 still on, move through the games list.

Pass: only the matching games are read. Games that do not match are skipped.

Result S4: pending

### S5. By location reads one heading per location with its count

1. Switch the view to By location.
2. Move by headings (TalkBack: reading controls set to Headings; VoiceOver: rotor set to Headings).

Pass: you hear one heading per storage location with the number of games in it, and the entries come after the heading of the location they belong to. The new view is announced when you switch.

Result S5: pending

### S6. Clear restores the full list

1. Activate Clear.

Pass: you hear that filters are cleared and the full game count (for example "Filters cleared, 48 games"), the list holds every game again, and the view you were in did not change.

Result S6: pending

## Part P. The slim bar on a small phone

### P1. The slim bar stays one row at 360 px, in English and Dutch

1. On a phone about 360 px wide (a typical small Android phone; use the browser at normal zoom), open the cabinet in English.
2. Type a letter in the search field, or choose a filter in the sheet, so the bar shows a count and the clear button.
3. Look at the top bar. Then switch the page to Dutch and look again.

Pass: in both languages the bar stays one row of the same height, with the search field, the Filters button, the count (short form such as "12 of 48" / Dutch equivalent) and the clear button all visible and not wrapped or cut off. A screenshot is welcome if it shows no personal data.

Result P1: pending

## Optional extras

Only if you have a minute. These come from points the build review left for a real phone. Skipping them is fine.

### P2. (optional) The slim bar slides away and comes back

1. On the phone cabinet with no filter on and the search field not focused, scroll down.
2. Then scroll up just a little.
3. Turn a filter on (for example a player count) and scroll down again; then tap into the search field and scroll again.
4. Edge case: scroll to just below the very top of the page (between about 60 px of scroll and the bar reaching the top). If the bar is away there, watch how it moves.

Pass: scrolling down slides the bar away; a small scroll up brings it back; with a filter on, or with the search field focused, the bar never slides away. In the edge case an away bar slides up out of its place without looking broken (say if it looks odd).

Result P2: pending

### P3. (optional) Switching views with the Filters sheet open

1. On the phone, open the Filters sheet.
2. Switch between All together and By location in the sheet.

Pass: the view changes. The cross-fade may paint over the sheet for about a quarter of a second. Say whether that looks wrong (a flash over the sheet) or is fine.

Result P3: pending

## How to answer

List results by label, for example "A1-A8 pass, S1-S6 pass, P1 pass", or describe issues in your own words ("A7 fail: the filter was cleared"). Say "not run: no Android phone" (or similar) for anything you could not do. Add P2 and P3 notes if you tried them.
