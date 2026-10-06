## Run

- Date: 2026-10-06
- Deployed release version (from the health endpoint, version only): 0.2.0
- Duration: 77 seconds as reported by the check (79 seconds wall time including the connection)
- Request count: 13 of the 14 allowed (calls A to I, with one queued answer each on A, C, D and E)
- Exit code: 0 (the check completed; the built-in guard passed)
- Run once, as the app user, from the owner's SSH alias, after the owner's approval. An earlier attempt exited 3 (not configured) before any request was sent and is not counted.
- Signed off by the owner on 2026-10-06.

## Decision table

| Question | Measured | Consequence for the build |
| --- | --- | --- |
| Private inventory location readable with the token alone | No. Call B (same as A plus the private info flag) was accepted with 200 and returned the same 50 items and the same body size as call A (138192 bytes). No private info element on any of the 50 base items, and none on any of the 15 expansions in call E. Attribute name: none observed. Items with a location value: 0 of 50 and 0 of 15. | `Bgg:IncludePrivateInfo` = false. The parser reads no private attribute and the snapshot carries no location from BGG. The owner location tools are built in the owner-tools phase. The owner's password or session cookie is never used. |
| Version dimensions: share, form, magnitudes, factor | Version element present on 50 of 50 base items, but width, length and depth are each non-zero on 35 of 50 (70%); 15 are zero. Form: value attribute on all, text on none. Base medians: width 6.3, length 8.27, depth 2.09. Base ranges: width 3.7 to 12.52, length 4.53 to 17.01, depth 0.79 to 7.56. Expansions (call C): version on 15 of 15, non-zero on 9 of 15 (60%); medians width 11.61, length 11.61, depth 2.36; ranges width 7.56 to 11.62, length 10 to 11.62, depth 1.54 to 3.15. Dimensions only come back when the selected version is requested (calls D and E, which do not ask for it, carry none). | Millimetres-per-unit factor: 25.4 (inches). Not a clean rule match for base games: the depth median (2.09) and the whole expansion set sit inside the inch bands (front sides 10 to 16, depth 1 to 4), but the base front-side medians (6.3 and 8.27) sit below the 10 to 16 band. Inches still hold: read as inches, the ranges come to about 9 to 32 cm wide, 11 to 43 cm long and 2 to 19 cm deep, which are realistic boxes, and the low medians reflect many small-box games. The centimetre and millimetre readings (front medians about 25 to 40 and 250 to 400) are both clearly missed and would give implausibly tiny boxes. The owner reviewed the magnitudes and agreed they look right; no box was measured. Items with a zero triple (30% of base games, 40% of expansions) need the fallback size heuristic. |
| Length versus width | Length is not smaller than width on 34 of the 35 base items that have a non-zero pair (97%) and on 9 of 9 expansions. | Map length to standing height and width to front width. |
| User-Agent missing (call G against call F) | Both answered 200 with the same body size (47761 bytes) and the same headers; the request without a User-Agent was neither refused nor challenged. Tested on the single-game endpoint only. | The honest User-Agent stays mandatory in the client either way (politeness), and is always sent. |
| 202 answers: count and seconds | One 202 before the data on each of A, C, D and E; none on B (served right after A). Every queued answer cleared on its first retry. Wall time per call: 7 seconds for A, 13 seconds for each of C, D and E, including the 6 second spacing between requests. | Keep the 202 wait schedule of 5, 10, 20 and 30 seconds with a cap of 6 polls. One retry sufficed in this run; one run is too little to tighten the schedule. |
| Declared total equals parsed item count | Yes on all five collection answers (50, 50, 15, 65, 15). The single-game answer carries no declared total (not applicable). | Keep it as a hard integrity check on collection calls: a mismatch rejects the fetch and leaves the last good snapshot untouched. |
| Default call D labels expansions as base games | Yes. All 65 items in call D carry the base game subtype, 15 of them also appear in call C as expansions, and the count equals call A plus call C (50 + 15). Call C shares 0 collection ids with call A. | The two-call split is confirmed as required: base games with the exclude filter, expansions with the include filter. |
| Name form and entity artefacts | Name is element text on 50 of 50 base items and 15 of 15 expansions (value attribute on none). No name in any collection answer still contains an entity marker. The name element carries a sort index attribute; an original-name child exists on 21 of 50 base items and 1 of 15 expansions. | Read names as element text with no extra decoding step. |
| Refusal shape (calls H and I) and any 403 or 429 | Both the call without a token and the call with a wrong token answered 401 with a web page content type and an empty body, indistinguishable from each other. No 403, no 429 and no retry-after header was seen anywhere in the run. | Classify 401 as an authentication problem (token missing or wrong), never retried, body not parsed. No observed data for 403 or 429; the client relies on its resilience defaults for those. |

## Per-call shape summary

The report as printed by the check (shape only by construction; the guard passed):

```
call A: collection
  status: 200
  seconds: 7
  answers with status 202: 1
  header content-type: text/xml; charset=_UTF-8_
  header server: cloudflare
  body bytes: 138192
  body class: xml:items
  totalitems: 50
  other root attribute names: pubdate, termsofuse
  parsed items: 50
  totalitems equals parsed items: yes
  item attribute names: collid 50, objectid 50, objecttype 50, subtype 50
  child element image: in 50 items, attribute names: none
  child element name: in 50 items, attribute names: sortindex 50
  child element numplays: in 50 items, attribute names: none
  child element originalname: in 21 items, attribute names: none
  child element stats: in 50 items, attribute names: maxplayers 50, maxplaytime 50, minplayers 50, minplaytime 50, numowned 50, playingtime 50
  child element status: in 50 items, attribute names: fortrade 50, lastmodified 50, own 50, preordered 50, prevowned 50, want 50, wanttobuy 50, wanttoplay 50, wishlist 50
  child element thumbnail: in 50 items, attribute names: none
  child element version: in 50 items, attribute names: none
  child element yearpublished: in 50 items, attribute names: none
  subtype values: boardgame 50
  own status values: 1 50
  repeated objectid values: 1
  repeated collid values: 0
  version element: present in 50 items
  width: value attribute 50, text 0, non-zero 35, zero 15, unparseable 0, min 3.7, median 6.3, max 12.52
  length: value attribute 50, text 0, non-zero 35, zero 15, unparseable 0, min 4.53, median 8.27, max 17.01
  depth: value attribute 50, text 0, non-zero 35, zero 15, unparseable 0, min 0.79, median 2.09, max 7.56
  length not smaller than width: 34 items
  name elements: text 50, value attribute 0
  names still containing an entity marker: 0
  yearpublished: present 50, text 50, value attribute 0
  numplays: present 50, text 50, value attribute 0
  comment: present 0, text 0, value attribute 0
  image: present 50, text 50, value attribute 0
  thumbnail: present 50, text 50, value attribute 0
  items with a private info element: 0
  private info attribute names: none
  private info with a non-empty inventorylocation: 0
  distinct non-empty inventorylocation values: 0
  longest inventorylocation value: 0
  items with a stats element: 50
  stats attribute names: maxplayers 50, maxplaytime 50, minplayers 50, minplaytime 50, numowned 50, playingtime 50
call B: collection
  status: 200
  seconds: 7
  answers with status 202: 0
  header content-type: text/xml; charset=_UTF-8_
  header server: cloudflare
  body bytes: 138192
  body class: xml:items
  totalitems: 50
  other root attribute names: pubdate, termsofuse
  parsed items: 50
  totalitems equals parsed items: yes
  item attribute names: collid 50, objectid 50, objecttype 50, subtype 50
  child element image: in 50 items, attribute names: none
  child element name: in 50 items, attribute names: sortindex 50
  child element numplays: in 50 items, attribute names: none
  child element originalname: in 21 items, attribute names: none
  child element stats: in 50 items, attribute names: maxplayers 50, maxplaytime 50, minplayers 50, minplaytime 50, numowned 50, playingtime 50
  child element status: in 50 items, attribute names: fortrade 50, lastmodified 50, own 50, preordered 50, prevowned 50, want 50, wanttobuy 50, wanttoplay 50, wishlist 50
  child element thumbnail: in 50 items, attribute names: none
  child element version: in 50 items, attribute names: none
  child element yearpublished: in 50 items, attribute names: none
  subtype values: boardgame 50
  own status values: 1 50
  repeated objectid values: 1
  repeated collid values: 0
  version element: present in 50 items
  width: value attribute 50, text 0, non-zero 35, zero 15, unparseable 0, min 3.7, median 6.3, max 12.52
  length: value attribute 50, text 0, non-zero 35, zero 15, unparseable 0, min 4.53, median 8.27, max 17.01
  depth: value attribute 50, text 0, non-zero 35, zero 15, unparseable 0, min 0.79, median 2.09, max 7.56
  length not smaller than width: 34 items
  name elements: text 50, value attribute 0
  names still containing an entity marker: 0
  yearpublished: present 50, text 50, value attribute 0
  numplays: present 50, text 50, value attribute 0
  comment: present 0, text 0, value attribute 0
  image: present 50, text 50, value attribute 0
  thumbnail: present 50, text 50, value attribute 0
  items with a private info element: 0
  private info attribute names: none
  private info with a non-empty inventorylocation: 0
  distinct non-empty inventorylocation values: 0
  longest inventorylocation value: 0
  items with a stats element: 50
  stats attribute names: maxplayers 50, maxplaytime 50, minplayers 50, minplaytime 50, numowned 50, playingtime 50
call C: collection
  status: 200
  seconds: 13
  answers with status 202: 1
  header content-type: text/xml; charset=_UTF-8_
  header server: cloudflare
  body bytes: 40814
  body class: xml:items
  totalitems: 15
  other root attribute names: pubdate, termsofuse
  parsed items: 15
  totalitems equals parsed items: yes
  item attribute names: collid 15, objectid 15, objecttype 15, subtype 15
  child element image: in 15 items, attribute names: none
  child element name: in 15 items, attribute names: sortindex 15
  child element numplays: in 15 items, attribute names: none
  child element originalname: in 1 items, attribute names: none
  child element stats: in 15 items, attribute names: maxplayers 15, maxplaytime 11, minplayers 15, minplaytime 11, numowned 15, playingtime 11
  child element status: in 15 items, attribute names: fortrade 15, lastmodified 15, own 15, preordered 15, prevowned 15, want 15, wanttobuy 15, wanttoplay 15, wishlist 15
  child element thumbnail: in 15 items, attribute names: none
  child element version: in 15 items, attribute names: none
  child element yearpublished: in 15 items, attribute names: none
  subtype values: boardgameexpansion 15
  own status values: 1 15
  repeated objectid values: 0
  repeated collid values: 0
  version element: present in 15 items
  width: value attribute 15, text 0, non-zero 9, zero 6, unparseable 0, min 7.56, median 11.61, max 11.62
  length: value attribute 15, text 0, non-zero 9, zero 6, unparseable 0, min 10, median 11.61, max 11.62
  depth: value attribute 15, text 0, non-zero 9, zero 6, unparseable 0, min 1.54, median 2.36, max 3.15
  length not smaller than width: 9 items
  name elements: text 15, value attribute 0
  names still containing an entity marker: 0
  yearpublished: present 15, text 15, value attribute 0
  numplays: present 15, text 15, value attribute 0
  comment: present 0, text 0, value attribute 0
  image: present 15, text 15, value attribute 0
  thumbnail: present 15, text 15, value attribute 0
  items with a private info element: 0
  private info attribute names: none
  private info with a non-empty inventorylocation: 0
  distinct non-empty inventorylocation values: 0
  longest inventorylocation value: 0
  items with a stats element: 15
  stats attribute names: maxplayers 15, maxplaytime 11, minplayers 15, minplaytime 11, numowned 15, playingtime 11
  collection ids shared with call A: 0
call D: collection
  status: 200
  seconds: 13
  answers with status 202: 1
  header content-type: text/xml; charset=_UTF-8_
  header server: cloudflare
  body bytes: 87045
  body class: xml:items
  totalitems: 65
  other root attribute names: pubdate, termsofuse
  parsed items: 65
  totalitems equals parsed items: yes
  item attribute names: collid 65, objectid 65, objecttype 65, subtype 65
  child element image: in 65 items, attribute names: none
  child element name: in 65 items, attribute names: sortindex 65
  child element numplays: in 65 items, attribute names: none
  child element originalname: in 22 items, attribute names: none
  child element stats: in 65 items, attribute names: maxplayers 65, maxplaytime 61, minplayers 65, minplaytime 61, numowned 65, playingtime 61
  child element status: in 65 items, attribute names: fortrade 65, lastmodified 65, own 65, preordered 65, prevowned 65, want 65, wanttobuy 65, wanttoplay 65, wishlist 65
  child element thumbnail: in 65 items, attribute names: none
  child element yearpublished: in 65 items, attribute names: none
  subtype values: boardgame 65
  own status values: 1 65
  repeated objectid values: 1
  repeated collid values: 0
  version element: present in 0 items
  width: value attribute 0, text 0, non-zero 0, zero 0, unparseable 0, min none, median none, max none
  length: value attribute 0, text 0, non-zero 0, zero 0, unparseable 0, min none, median none, max none
  depth: value attribute 0, text 0, non-zero 0, zero 0, unparseable 0, min none, median none, max none
  length not smaller than width: 0 items
  name elements: text 65, value attribute 0
  names still containing an entity marker: 0
  yearpublished: present 65, text 65, value attribute 0
  numplays: present 65, text 65, value attribute 0
  comment: present 0, text 0, value attribute 0
  image: present 65, text 65, value attribute 0
  thumbnail: present 65, text 65, value attribute 0
  items with a private info element: 0
  private info attribute names: none
  private info with a non-empty inventorylocation: 0
  distinct non-empty inventorylocation values: 0
  longest inventorylocation value: 0
  items with a stats element: 65
  stats attribute names: maxplayers 65, maxplaytime 61, minplayers 65, minplaytime 61, numowned 65, playingtime 61
  item count equals call A plus call C: yes
  items with subtype boardgame that also appear in call C: 15
call E: collection
  status: 200
  seconds: 13
  answers with status 202: 1
  header content-type: text/xml; charset=_UTF-8_
  header server: cloudflare
  body bytes: 11054
  body class: xml:items
  totalitems: 15
  other root attribute names: pubdate, termsofuse
  parsed items: 15
  totalitems equals parsed items: yes
  item attribute names: collid 15, objectid 15, objecttype 15, subtype 15
  child element image: in 15 items, attribute names: none
  child element name: in 15 items, attribute names: sortindex 15
  child element numplays: in 15 items, attribute names: none
  child element originalname: in 1 items, attribute names: none
  child element status: in 15 items, attribute names: fortrade 15, lastmodified 15, own 15, preordered 15, prevowned 15, want 15, wanttobuy 15, wanttoplay 15, wishlist 15
  child element thumbnail: in 15 items, attribute names: none
  child element yearpublished: in 15 items, attribute names: none
  subtype values: boardgameexpansion 15
  own status values: 1 15
  repeated objectid values: 0
  repeated collid values: 0
  version element: present in 0 items
  width: value attribute 0, text 0, non-zero 0, zero 0, unparseable 0, min none, median none, max none
  length: value attribute 0, text 0, non-zero 0, zero 0, unparseable 0, min none, median none, max none
  depth: value attribute 0, text 0, non-zero 0, zero 0, unparseable 0, min none, median none, max none
  length not smaller than width: 0 items
  name elements: text 15, value attribute 0
  names still containing an entity marker: 0
  yearpublished: present 15, text 15, value attribute 0
  numplays: present 15, text 15, value attribute 0
  comment: present 0, text 0, value attribute 0
  image: present 15, text 15, value attribute 0
  thumbnail: present 15, text 15, value attribute 0
  items with a private info element: 0
  private info attribute names: none
  private info with a non-empty inventorylocation: 0
  distinct non-empty inventorylocation values: 0
  longest inventorylocation value: 0
  items with a stats element: 0
  stats attribute names: none
call F: thing
  status: 200
  seconds: 6
  answers with status 202: 0
  header content-type: text/xml; charset=_UTF-8_
  header server: cloudflare
  header cache-control: s-maxage=0, max-age=3600
  body bytes: 47761
  body class: xml:items
  totalitems: missing
  other root attribute names: termsofuse
  parsed items: 1
  totalitems equals parsed items: no
  item attribute names: id 1, type 1
  child element description: in 1 items, attribute names: none
  child element image: in 1 items, attribute names: none
  child element link: in 1 items, attribute names: id 302, type 302, value 302
  child element maxplayers: in 1 items, attribute names: value 1
  child element maxplaytime: in 1 items, attribute names: value 1
  child element minage: in 1 items, attribute names: value 1
  child element minplayers: in 1 items, attribute names: value 1
  child element minplaytime: in 1 items, attribute names: value 1
  child element name: in 1 items, attribute names: sortindex 65, type 65, value 65
  child element playingtime: in 1 items, attribute names: value 1
  child element poll: in 1 items, attribute names: name 3, title 3, totalvotes 3
  child element poll-summary: in 1 items, attribute names: name 1, title 1
  child element thumbnail: in 1 items, attribute names: none
  child element yearpublished: in 1 items, attribute names: value 1
call G: thing
  status: 200
  seconds: 6
  answers with status 202: 0
  header content-type: text/xml; charset=_UTF-8_
  header server: cloudflare
  header cache-control: s-maxage=0, max-age=3600
  body bytes: 47761
  body class: xml:items
  totalitems: missing
  other root attribute names: termsofuse
  parsed items: 1
  totalitems equals parsed items: no
  item attribute names: id 1, type 1
  child element description: in 1 items, attribute names: none
  child element image: in 1 items, attribute names: none
  child element link: in 1 items, attribute names: id 302, type 302, value 302
  child element maxplayers: in 1 items, attribute names: value 1
  child element maxplaytime: in 1 items, attribute names: value 1
  child element minage: in 1 items, attribute names: value 1
  child element minplayers: in 1 items, attribute names: value 1
  child element minplaytime: in 1 items, attribute names: value 1
  child element name: in 1 items, attribute names: sortindex 65, type 65, value 65
  child element playingtime: in 1 items, attribute names: value 1
  child element poll: in 1 items, attribute names: name 3, title 3, totalvotes 3
  child element poll-summary: in 1 items, attribute names: name 1, title 1
  child element thumbnail: in 1 items, attribute names: none
  child element yearpublished: in 1 items, attribute names: value 1
call H: collection
  status: 401
  seconds: 6
  answers with status 202: 0
  header content-type: text/html; charset=UTF-8
  header server: cloudflare
  body bytes: 0
  body class: empty
call I: collection
  status: 401
  seconds: 6
  answers with status 202: 0
  header content-type: text/html; charset=UTF-8
  header server: cloudflare
  body bytes: 0
  body class: empty
requests made: 13 of 14
elapsed seconds: 77
```

## Open items

- Token status: call A itself was accepted (200 with items), so the token is not rejected and the phase is not blocked on the owner. Only call B lacks private info, which means the location is not readable with the token alone.
- Prerequisite not provable from the report: the check cannot show whether an inventory location was set on any game before the run. The report has no private info element at all (not even an empty one) on 65 items, which fits the token not being treated as a logged-in session, but the owner should confirm that locations had been set. If none had, the "not readable" answer is not conclusive and one more run would be needed after setting a few.
- Unit factor note: the owner reviewed the magnitudes and agreed they look right; no box was measured. The rule's inch band (front sides about 10 to 16) is not matched by the base-game front medians (6.3 and 8.27), while the depth median and the expansion medians fit it; the centimetre and millimetre readings would give implausibly tiny boxes, so 25.4 stands.
- Fallback location source not tested: the collection comment field was absent on every item (it was not requested), so the comment-text convention remains untested if a location source other than the owner tools is ever wanted.
- One object id appears twice in the base-game answers (calls A, B and D; two different collection ids, so two entries for one game). The sync must decide whether duplicates collapse to one cabinet entry or stay as two copies; the run does not decide this.
- Stats gaps: in the expansion answers 4 of 15 items lack the playing time and play-time range attributes (and 4 of 65 in the unfiltered answer); player count was present everywhere. Filters must tolerate missing values.
- The content type of collection answers carries an odd charset parameter with underscores around the encoding name. The client must decode from the document itself rather than trust that parameter.
- Single-sample limits: the 202 behaviour, the missing User-Agent result and the 401 shape each come from one run and one request; the User-Agent result covers the single-game endpoint only, not the collection endpoint.
