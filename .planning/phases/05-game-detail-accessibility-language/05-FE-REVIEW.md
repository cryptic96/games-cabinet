# Phase 5 discussion: senior front-end review of the pull-out and card decisions

Working document from the discussion. It is folded into `05-CONTEXT.md` once you have reviewed it.
Reply with the labels (for example "drop R3" or "change I-B to …") for anything you want different.

## 1. Decisions you made, and the review's verdict

| # | Your decision | Verdict | Why |
|---|---------------|---------|-----|
| P1 | **Pull-out:** the box lifts toward you out of its cubby, a spine turns to show its cover, and the cover grows into the card's cover spot | Keep | The turn from spine to cover is the signature moment that makes a visitor tap a second box. |
| P2 | **Desktop:** a card centred over a dimmed cabinet (native dialog: Escape closes, clicking outside closes, focus stays inside) | Keep | Clearest focus. The cover has room to land, and the dialog gives keyboard and screen-reader behaviour for free. |
| P3 | **Phone:** a bottom sheet that rises from the bottom; close by dragging down, tapping the dimmed strip, or ✕ | Keep, simplified (see S1) | Familiar and thumb-friendly. |
| P4 | **Back button closes the card**: one history step, the address does not change | Keep | Phone users press Back by reflex. Leaving the site instead would be a bad surprise. |
| P5 | **A sync while a card is open waits:** the card is left alone, and the quiet redraw runs after it closes | Keep | Nothing jumps under the reader's eyes, and the box is guaranteed to still be there to slide back into. |
| P6 | **No previous/next on the card:** close it and tap the next box | Keep | One box out at a time, like a real shelf. Keyboard users browse with Esc, →, Enter, which is quick because focus returns to the box. |
| P7 | **Speed:** about 350 ms from tap to readable card | Keep as a starting value (see R1) | It holds three movements (lift, turn, grow), so it is tight, but it may well be right. |
| P8 | **Reduced motion:** no lift, turn or morph; the card fades in over about 150 ms and the box stays in place with a highlight | Keep | Fades are generally comfortable for motion-sensitive visitors and feel less abrupt than a hard cut. |
| P9 | **Card look:** two or three mocked-up directions in the UI design step, and you pick one | Keep (see I-C) | The same approach that worked for the cabinet finish. |

None of your decisions needs reversing.

## 2. Refinements to how the decisions are built

These do not change what you chose. They make sure it works well.

| # | Refinement | What you would notice |
|---|-----------|-----------------------|
| R1 | 350 ms is a **tunable value**, judged in the review build. **Closing is a bit faster**, about 250 ms. | A pull-out that never feels like waiting, and a close that gets out of the way. |
| R2 | The **turn is a flip trick**: browsers animate flat snapshots, so the spine snapshot flips over into the cover. It runs on that one box only. | Smooth even on a cheap phone, because only one element animates. |
| R3 | **Older browsers** without the morph (Safari before 18, Firefox before 144) get a **plain fade-open**, not a second hand-built animation. | Those few visitors see a simple fade. Everyone else sees the full pull-out. |
| R4 | **The cabinet behind the card is dimmed, not blurred.** | Blurring hundreds of boxes during the animation stutters on cheap phones. A plain dim does not. |
| R5 | **The bottom sheet is dragged only by its handle or header**, and the page behind does not scroll while it is open. | Scrolling the card's content never closes it by accident. |
| R6 | **The card's data is already loaded when you tap.** The game details come with the cabinet's data (small for about 65 games, cached per sync) instead of being fetched on tap. | The card is never empty and never shows a spinner halfway through the pull-out. |
| R7 | The Back button (P4) is **coordinated with the browser's own behaviour**: Chrome on Android already closes a dialog on Back, and closing with ✕ must not leave a dead Back step. This is for research to solve, not a choice for you. | Back always does exactly one sensible thing. |

## 3. Ideas the review thinks are better than leaving it to chance

| # | Idea | What you would notice |
|---|------|-----------------------|
| S1 | **The phone sheet has one height**: it fits its content up to about 90% of the screen and scrolls inside. No half-open snap positions. | Simple and predictable. Snap points add gesture complexity for little gain on a card this size. |
| I-A | **The gap stays visible while the box is out.** Behind the dim, the cubby shows the empty slot, and on close the box slides back into it. | The box really looks like it came *out of* the shelf, which is a free realism cue. |
| I-B | **The cover spot on the card takes the box's real proportions** (square, wide or tall, from the box-size work in the previous phase). | The morph never stretches the art, and a square box stays square on the card. |
| I-C | **The card mock-ups are judged against named real things**, for example **the back of a game box** (a stats strip with players, time and age, like the icons printed on real boxes) and **a paper insert or index card**. Each direction gets a short list of the visual cues that make that thing recognisable. | You compare directions against something real, not against adjectives. |

## 4. Things already right in the code

- The hover lift happens only on devices that can hover (no stuck lifted boxes on phones), and it is switched off for reduced motion. It only needs clicking to do something now (the folded UI-polish item).
