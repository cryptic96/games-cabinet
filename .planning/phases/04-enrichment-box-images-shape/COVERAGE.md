# API Coverage — BoardGameGeek XML API2 and its image host

> Full coverage by default. Opt-outs are explicit, reasoned decisions.
> Scope: the server-side sync's use of `https://boardgamegeek.com/xmlapi2/` with the application's Bearer token, and token-less downloads from BGG's image host. Visitors never call BGG or its image host. Plans: 04-01/04-05 (art check), 04-02 (box art tracer), 04-06 (details and pairing), 04-08 (two candidates, choice and colours), 04-11 (local fake only).

| capability | decision | reason |
|---|---|---|
| collection: owned base games (`own=1&excludesubtype=boardgameexpansion`) | INTEGRATE | |
| collection: owned expansions (`own=1&subtype=boardgameexpansion`) | INTEGRATE | |
| collection: owned version info (`version=1`: version image and box dimensions) | INTEGRATE | |
| collection: item-level `image` | INTEGRATE | |
| collection: item-level and version `thumbnail` | OPT-OUT | not needed — pictures are downscaled locally from the full image to 480 and 240 px |
| collection: private info (`showprivate=1`) | INTEGRATE | conditional on the sync phase's signed-off outcome; unchanged here |
| collection: queued answers (HTTP 202 polling) | INTEGRATE | |
| collection: error and throttle answers (401, 403, 429, 5xx, errors document) | INTEGRATE | |
| collection: statistics (`stats=1`) | OPT-OUT | not needed — the details call with `stats=1` supplies players, play time, ratings and weight in one place |
| collection: `modifiedsince` incremental fetch | OPT-OUT | not needed — it does not report deletions; a full owned fetch runs every time |
| collection: comment text | OPT-OUT | explicitly out of scope — the public-comment location convention was rejected by the owner |
| collection: other status filters (wishlist, want, trade, rated, played) | OPT-OUT | explicitly out of scope — owned items only |
| collection: other users' collections | OPT-OUT | explicitly out of scope — visitors can never supply a username |
| thing: details with `stats=1` (players, play time, age, ratings, weight, image) | INTEGRATE | |
| thing: designer and mechanic links | INTEGRATE | |
| thing: inbound expansion links (which base games an expansion expands) | INTEGRATE | |
| thing: error, throttle and queued answers | INTEGRATE | |
| thing: other link types (outbound expansion, compilation, family, ...) | OPT-OUT | not needed — pairing reads only inbound expansion links (big-box editions hide nothing) and no feature shows the others |
| thing: description | OPT-OUT | not needed — the detail card needs no long description |
| thing: polls (suggested players, age, language dependence) | OPT-OUT | not needed yet — "best at N players" is a later-version feature |
| thing: rank list | OPT-OUT | not needed yet — the Bayesian average is stored; the detail phase decides what to show |
| thing: `versions=1` | OPT-OUT | explicitly out of scope — returns every printing; the owned version comes from the collection call |
| thing: comments, rating comments, videos, marketplace | OPT-OUT | not needed — no feature uses them |
| family | OPT-OUT | not needed — no feature uses game families |
| plays | OPT-OUT | not needed — play logging is not a feature |
| user | OPT-OUT | not needed — the username is configuration |
| guild | OPT-OUT | not needed — no guild feature |
| forum list, forum, thread | OPT-OUT | explicitly out of scope — no BGG discussion content |
| hot items | OPT-OUT | not needed — the cabinet shows only the owner's games |
| search | OPT-OUT | explicitly out of scope — would let visitor input reach BGG |
| image host: download of the version picture and the main picture | INTEGRATE | |
| image host: sized variants by editing the path | OPT-OUT | not needed — the paths are signed; originals are downscaled locally |
| image host: gallery pictures given by link | OPT-OUT | not needed yet — owner image overrides belong to the owner-tools phase |
