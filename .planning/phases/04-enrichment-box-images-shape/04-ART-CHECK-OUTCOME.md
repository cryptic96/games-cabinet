## Run

- Date: 2026-10-07
- Release version: latest release tag is 0.3.1 (the deployed version was not read from the health endpoint in this run)
- Duration: 25 seconds as reported by the check
- Request count: 3 of the 10 allowed API requests (calls J, K, L) and 7 of the 7 allowed image requests (M1 to M6, N)
- Exit code: 0 (the check completed; the built-in guard passed, nothing withheld)
- Run once, as the app user, from the owner's SSH alias, after the owner's approval. No other run happened.
- Status: draft, awaiting the owner's sign-off.

## Decision table

| Question | Measured | Consequence for the build |
| --- | --- | --- |
| (a) Where the owned version's image is read from | Collection with the selected version requested: the version element carried an image on 42 of 50 base items (84%) and 12 of 15 expansions (80%), and a thumbnail on the same 42 and 12. A version element was present on 50 of 50 and 15 of 15. The item-level image was present on 50 of 50 and 15 of 15. | Keep the version image (`version/item/image`) as candidate A: it is present on most items. It is absent on 8 base games and 3 expansions, which need the main image from the details call (or the item-level image, see b). |
| (b) Item-level image under the owned-version request versus the version image | Where a version image exists, the item-level image equals it on 42 of 42 base items and 12 of 12 expansions. The 8 base items and 3 expansions without a version image still carry an item-level image. The run did not compare those 11 against the details call's main image, so what they are is inferred, not measured. | The item-level image may stand in for the main image until details arrive: it is the version image when one exists, otherwise some other image that is always present. Treat it as a placeholder, and replace it with the details call's main image when that arrives. |
| (c) Details call without a type returns expansions | Yes. One call with 4 ids (2 base games, 2 expansions) returned 4 items, 0 missing, types: base game 2, expansion 2. | The details call needs no type parameter and no second call for expansions. One call per batch of ids covers both. |
| (d) Statistics and link types on the details answer | Both types: image 2 of 2, thumbnail 2 of 2, minimum age above zero 2 of 2, average present 2 of 2, bayesian average above zero 2 of 2, average weight above zero 2 of 2 (per type, 2 items each). Link counts by type: artist 5, category 13, compilation 3, designer 3, expansion inbound 3, expansion outbound 2, family 18, implementation 3, mechanic 15, publisher 19. Expansion items carry 1 to 2 inbound expansion links each; base items carry at most 1 outbound expansion link. | The details call supplies rating, weight, minimum age, designers, mechanics, categories and the expansion-to-base link (inbound on the expansion). All are present on this sample of 4. |
| (e) Image host classes and the allowlist | Image addresses: 92 on the base call and 27 on the expansion call, all in the single known image host class (0 other hosts, 0 other subdomains of the same family). All 119 are written with a scheme (0 without) and all use the signed-original path form. | `Images:AllowedHosts` keeps its default of the one documented image host. No second host is needed. |
| (f) Request without a User-Agent | Answered 200, same status as the identical request with one (call N against call M1). | The image host does not need a User-Agent. The honest User-Agent still stays on every API call, and a fixed one may be sent on image calls too for politeness. |
| (g) Largest bytes and pixels, against the 12 MB and 36 megapixel caps | 6 downloads, all 200. Largest size 217594 bytes (about 0.2 MB, under 2% of the cap). Largest area 1000 x 1818 (about 1.8 megapixels, about 5% of the cap). Other sizes: 680 x 680, 500 x 500, 500 x 331, 969 x 1268, 800 x 670. No cap flag on any download; content-length was sent on all 6. | Both caps hold with a wide margin on this sample; keep 12 MB and 36 megapixels. |
| (h) Formats seen | 6 downloads: PNG 2 (33%), JPEG 4 (67%). Content types matched the decoded format on all 6. Across the address lists, 47 of 92 base-call addresses and 4 of 27 expansion-call addresses asked for a PNG rendition. | Decode both formats. The decoder must handle PNG (with possible transparency) as a normal case, not an exception. |
| (i) Redirects and throttle answers at the 1.5-second gap | None. Every API call answered 200 with no queued answer, every image call answered 200 (reported gaps 0 to 2 seconds), and no redirect was followed or reported. No 429, no retry-after header. | Keep the 1000 ms default image gap (no throttle answer appeared). One run of 7 image requests is a small sample. |

## Per-call shape summary

The report as printed by the check (shape only by construction; the guard passed):

```
call J: collection
  status: 200
  seconds: 1
  answers with status 202: 0
  header content-type: text/xml; charset=_UTF-8_
  header server: cloudflare
  body bytes: 105453
  body class: xml:items
  items: 50
  items with a version element: 50
  items whose version has an image: 42
  items with an item-level image: 50
  items where the version image equals the item-level image: 42
  items whose version has a thumbnail: 42
  image address host classes: cdn 92
  image address path forms: signed-original 92
  image addresses written without a scheme: 0
  image addresses asking for a PNG rendition: 47
call K: collection
  status: 200
  seconds: 7
  answers with status 202: 0
  header content-type: text/xml; charset=_UTF-8_
  header server: cloudflare
  body bytes: 32145
  body class: xml:items
  items: 15
  items with a version element: 15
  items whose version has an image: 12
  items with an item-level image: 15
  items where the version image equals the item-level image: 12
  items whose version has a thumbnail: 12
  image address host classes: cdn 27
  image address path forms: signed-original 27
  image addresses written without a scheme: 0
  image addresses asking for a PNG rendition: 4
call L: thing
  status: 200
  seconds: 6
  answers with status 202: 0
  header content-type: text/xml; charset=_UTF-8_
  header server: cloudflare
  header cache-control: s-maxage=0, max-age=3600
  body bytes: 36905
  body class: xml:items
  items: 4
  requested ids: 4, missing from the answer: 0
  item types: boardgame 2, boardgameexpansion 2
  type boardgame: items 2, image 2, thumbnail 2, minage above zero 2, average present 2, bayesaverage above zero 2, averageweight above zero 2
  type boardgameexpansion: items 2, image 2, thumbnail 2, minage above zero 2, average present 2, bayesaverage above zero 2, averageweight above zero 2
  link counts by type: boardgameartist 5, boardgamecategory 13, boardgamecompilation 3, boardgamedesigner 3, boardgameexpansion inbound 3, boardgameexpansion outbound 2, boardgamefamily 18, boardgameimplementation 3, boardgamemechanic 15, boardgamepublisher 19
  expansion items: inbound expansion links per item min 1, max 2
  base items: outbound expansion links per item max 1
call M1: image download
  status: 200
  seconds: 0
  content-type: image/png
  content-length sent: yes
  body bytes: 196024
  format: png
  pixel size: 680 x 680
  flags: none
call M2: image download
  status: 200
  seconds: 2
  content-type: image/png
  content-length sent: yes
  body bytes: 31476
  format: png
  pixel size: 500 x 500
  flags: none
call M3: image download
  status: 200
  seconds: 2
  content-type: image/jpeg
  content-length sent: yes
  body bytes: 35900
  format: jpeg
  pixel size: 500 x 331
  flags: none
call M4: image download
  status: 200
  seconds: 2
  content-type: image/jpeg
  content-length sent: yes
  body bytes: 217594
  format: jpeg
  pixel size: 969 x 1268
  flags: none
call M5: image download
  status: 200
  seconds: 2
  content-type: image/jpeg
  content-length sent: yes
  body bytes: 139391
  format: jpeg
  pixel size: 800 x 670
  flags: none
call M6: image download
  status: 200
  seconds: 2
  content-type: image/jpeg
  content-length sent: yes
  body bytes: 58822
  format: jpeg
  pixel size: 1000 x 1818
  flags: none
call N: image download without User-Agent
  status: 200
  same status as call M1: yes
API requests made: 3 of 10
image requests made: 7 of 7
elapsed seconds: 25
```

## Open items

- Single-sample limits: the details call covered 4 ids (2 base, 2 expansion), the downloads covered 6 images. Absence of a field on other games is not ruled out.
- The 11 items without a version image were not compared against the details call's main image. Whether their item-level image equals it is unmeasured.
- The User-Agent result and the absence of throttle answers each come from one run of a few image requests.
- The deployed release version was not read from the health endpoint; the version in the header is the latest tag.
