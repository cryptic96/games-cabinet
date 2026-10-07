# API Coverage — BoardGameGeek XML API2

> Full coverage by default. Opt-outs are explicit, reasoned decisions.
> Scope: the server-side sync's use of `https://boardgamegeek.com/xmlapi2/` with the application's Bearer token. Visitors never call BGG; the only visitor-triggered path is the cooldown-guarded "sync now" (SEC-05). Plans: 03-01/03-02 (access check), 03-07 (tracer), 03-09 (fidelity and secrets), 03-10 (failures).

| capability | decision | reason |
|---|---|---|
| collection: owned base games (`own=1&excludesubtype=boardgameexpansion`) | INTEGRATE | |
| collection: owned expansions (`own=1&subtype=boardgameexpansion`) | INTEGRATE | |
| collection: owned version info (`version=1`, interim box sizes) | INTEGRATE | |
| collection: private info (`showprivate=1`, inventory location) | INTEGRATE | conditional on the signed-off access-check outcome (D-04); off when BGG does not return it to the token |
| collection: queued answers (HTTP 202 polling) | INTEGRATE | |
| collection: error and throttle answers (401, 403, 429, 5xx, errors document) | INTEGRATE | |
| collection: statistics (`stats=1`: player counts, play time, ratings) | OPT-OUT | not needed yet — enrichment phase (SYNC-06) decides whether it saves thing calls; measured by the access check only |
| collection: `modifiedsince` incremental fetch | OPT-OUT | not needed — it does not report deletions; a full owned fetch runs every time (locked decision) |
| collection: comment text (`comment=1`) | OPT-OUT | explicitly out of scope — the public-comment location convention was rejected by the owner |
| collection: other status filters (wishlist, want, trade, rated, played) | OPT-OUT | explicitly out of scope — the cabinet shows owned items only (PROJECT core value) |
| collection: other users' collections | OPT-OUT | explicitly out of scope — visitors can never supply a username (SEC-05, Out of Scope table) |
| thing: game details, links, weight (`thing?id=...&stats=1`) | OPT-OUT | not needed yet — enrichment phase (SYNC-06, SYNC-07); the access check makes one `thing` call only to measure User-Agent behaviour |
| thing: versions (`versions=1`) | OPT-OUT | explicitly out of scope — returns every printing; the owner's version comes from the collection call |
| family | OPT-OUT | not needed — no feature uses game families |
| plays | OPT-OUT | not needed — play logging is not a feature |
| user | OPT-OUT | not needed — the username is configuration, profile data is not shown |
| guild | OPT-OUT | not needed — no guild feature |
| forum list, forum, thread | OPT-OUT | explicitly out of scope — the site shows no BGG discussion content |
| hot items | OPT-OUT | not needed — the cabinet shows only the owner's games |
| search | OPT-OUT | explicitly out of scope — would require visitor input to reach BGG (SEC-05) |
| image CDN downloads (box art) | OPT-OUT | not needed yet — box images arrive in the enrichment phase (SYNC-07, IMG-01) through a separate token-less client |
