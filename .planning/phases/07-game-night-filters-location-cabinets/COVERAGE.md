# API Coverage — BoardGameGeek XML API2

> Full coverage by default. Opt-outs are explicit, reasoned decisions.
> Scope: this phase reads two more fields from the BGG calls the server-side sync already makes (alternate names on the details call; the owned version's name on the collection call with `version=1`). No new call, endpoint or parameter is added, visitors never trigger a BGG request, and the token still goes only to the API host. Plans: 07-04 (edition name and alternate names kept by the sync), 07-05 (the counts-only access check), 07-19 (alternate names in the visitor search, served from the snapshot). Rows below restate the surface decided in the enrichment phase, updated where this phase changes a decision.

| capability | decision | reason |
|---|---|---|
| collection: owned base games (`own=1&excludesubtype=boardgameexpansion`) | INTEGRATE | |
| collection: owned expansions (`own=1&subtype=boardgameexpansion`) | INTEGRATE | |
| collection: owned version info (`version=1`: version image and box dimensions) | INTEGRATE | |
| collection: owned version name (`version/item/name`, read tolerantly) | INTEGRATE | |
| collection: item-level `image` | INTEGRATE | |
| collection: item-level and version `thumbnail` | OPT-OUT | not needed — pictures are downscaled locally from the full image |
| collection: private info (`showprivate=1`) | INTEGRATE | conditional on the sync phase's signed-off outcome; unchanged here |
| collection: queued answers (HTTP 202 polling) | INTEGRATE | |
| collection: error and throttle answers (401, 403, 429, 5xx, errors document) | INTEGRATE | |
| collection: statistics (`stats=1`) | OPT-OUT | not needed — the details call with `stats=1` supplies players, play time, ratings and weight |
| collection: `modifiedsince` incremental fetch | OPT-OUT | not needed — it does not report deletions; a full owned fetch runs every time |
| collection: comment text | OPT-OUT | explicitly out of scope — the public-comment location convention was rejected by the owner |
| collection: other status filters (wishlist, want, trade, rated, played) | OPT-OUT | explicitly out of scope — owned items only |
| collection: other users' collections | OPT-OUT | explicitly out of scope — visitors can never supply a username |
| thing: details with `stats=1` (players, play time, age, ratings, weight, image) | INTEGRATE | |
| thing: alternate names (`name[@type="alternate"]`) | INTEGRATE | capped at 30 per game and 100 characters each |
| thing: designer and mechanic links | INTEGRATE | |
| thing: inbound expansion links (which base games an expansion expands) | INTEGRATE | |
| thing: family links (`boardgamefamily`) | INTEGRATE | |
| thing: error, throttle and queued answers | INTEGRATE | |
| thing: other link types (outbound expansion, compilation, ...) | OPT-OUT | not needed — pairing reads only inbound expansion links and no feature shows the others |
| thing: description | OPT-OUT | not needed — the detail card shows no long description |
| thing: polls (suggested players, age, language dependence) | OPT-OUT | explicitly out of scope — best-at-N is a deferred later-version extra |
| thing: rank list | OPT-OUT | not needed — the Bayesian average is stored; no feature shows ranks |
| thing: `versions=1` | OPT-OUT | explicitly out of scope — returns every printing; the owned version and its name come from the collection call |
| thing: comments, rating comments, videos, marketplace | OPT-OUT | not needed — no feature uses them |
| family (the `family` endpoint) | OPT-OUT | not needed — family names arrive with the details call |
| plays | OPT-OUT | not needed — play logging is not a feature |
| user | OPT-OUT | not needed — the username is configuration |
| guild | OPT-OUT | not needed — no guild feature |
| forum list, forum, thread | OPT-OUT | explicitly out of scope — no BGG discussion content |
| hot items | OPT-OUT | not needed — the cabinet shows only the owner's games |
| search | OPT-OUT | explicitly out of scope — the visitor search runs in the browser over the synced snapshot; visitor input never reaches BGG |
| image host: download of the version picture and the main picture | INTEGRATE | |
| image host: sized variants by editing the path | OPT-OUT | not needed — the paths are signed; originals are downscaled locally |
| image host: gallery pictures given by link | INTEGRATE | unchanged from the owner-tools phase (owner picture links, token-less, allow-listed host) |
