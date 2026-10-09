---
phase: 04
slug: enrichment-box-images-shape
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 2
block_on: high
created: 2026-10-09
---

# Phase 04 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| BGG XML API to server | Collection and `thing` answers fetched by the background sync only | Untrusted XML (names, links, statistics); the application token is sent only to the BGG API host |
| BGG image host to server | Box pictures downloaded by the sync, token-less, allowlisted host, capped and paced | Untrusted image bytes and redirects |
| Server to browser | Layout JSON and own-origin `/art` WebP files under a `default-src 'self'` CSP | Public collection data shaped for the cabinet; no BGG addresses |
| Owner workstation to container | Owner-approved SSH commands during review rounds and releases | Counts, versions and statuses only; review artefacts copied to a private scratch folder and deleted |
| GitHub release to container | Pull-based deploy of attested, owner-approved releases | Release archive, checksum, attestation bundle |
| Local review artefacts to public repository | Screenshots, sheets and measurements from the real collection | Never committed; only synthetic screenshots in `ui-refs/` |

---

## Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation | Status |
|-----------|----------|-----------|----------|-------------|------------|--------|
| T-04-01 | Information disclosure | report output | high | mitigate | Every URL, path, title and id seen becomes a forbidden value for `report_leaks`; `http` and `<` stay forbidden; proven by the self-test with invented values | closed |
| T-04-02 | Information disclosure | token to image host | high | mitigate | Separate `HTTPSConnection` per image request with only User-Agent and Accept headers; the Authorization header is built only by the existing API transport for `boardgamegeek.com` | closed |
| T-04-03 | Denial of service | BGG quota and image host | medium | mitigate | 10 API requests and 7 image requests at most, 6 s and 1.5 s gaps, stop on 401/403/429, owner approval of the exact run (Task 2) | closed |
| T-04-04 | Tampering | hostile bodies | medium | mitigate | DOCTYPE and ENTITY bodies are never parsed (existing rule); image bodies are read only to 12 MB plus one byte and parsed only for header fields | closed |
| T-04-05 | Spoofing | redirect to another host | medium | mitigate | No redirect is followed; only the class of the Location host is recorded | closed |
| T-04-06 | Spoofing / Elevation | ImageDownloader (SSRF through an answer URL) | high | mitigate | https only, exact host allowlist, default port, no user info, every redirect re-checked and at most 3, no URL from request input; pinned by Task 2 tests | closed |
| T-04-07 | Information disclosure | token on image requests | high | mitigate | Image client registered without any message handler; test asserts no Authorization header on every image request | closed |
| T-04-08 | Denial of service | decompression bomb or huge download | high | mitigate | Content-Length refusal, streamed byte cap, `MaxResponseContentBufferSize`, pixel cap from `SKCodec.Info` before decode, scaled decode, serial processing | closed |
| T-04-09 | Tampering | path traversal under /art | high | mitigate | `PhysicalFileProvider` confined to the art directory, only `.webp` mapped, `ServeUnknownFileTypes = false`, names validated against the hash pattern on write; traversal test answers 404 | closed |
| T-04-10 | Tampering | MIME sniffing of served files | medium | mitigate | `X-Content-Type-Options: nosniff` and an explicit `image/webp` type on every art response | closed |
| T-04-11 | Denial of service | image step running past the sync limit | medium | mitigate | Per-run download cap, 6-minute extras deadline inside the 10-minute run limit, failures never fail the sync **Fixed after the audit: per-picture failures become failed records and every picture request has its own time limit (`593468e`, `9502203`; `ArtSyncTests`, `ImageDownloaderTests`, `BoxArtTests`). Release v0.5.2.** | closed |
| T-04-12 | Information disclosure | logs | medium | mitigate | Counts and categories only; the repository layer never logs; refusal reasons are single category words | closed |
| T-04-13 | Tampering | cache poisoning or stale art | low | mitigate | Content-hashed immutable names; a changed picture gets a new name; prune only after a successful save and after the grace period | closed |
| T-04-14 | Tampering | stored colour pair reaching CSS | medium | mitigate | `IsValidPair` accepts only lowercase `#rrggbb`, white or black text and a passing two-end check; callers fall back to the palette, so a damaged file can never inject a CSS value or an illegible title | closed |
| T-04-15 | Denial of service | nudge loop | low | mitigate | Bounded 200-step loop with a guaranteed in-gamut end point; grid test runs every colour | closed |
| T-04-16 | Denial of service | ArtAnalysis on large bitmaps | high (raised from medium by the owner, 2026-10-09) | mitigate | All per-pixel work runs on a 96 px wide copy; the flood is iterative with an explicit queue (no recursion); sizes come from the processor's already pixel-capped decode **Fixed after the audit: the analysis copy is never scaled up and is bounded to 96 x 960 px, pictures with one side more than 10 times the other are refused from the header, and any processing exception becomes an undecodable record (`f3f64dc`; `ArtWorkingCopyTests`, `ArtProcessorTests`). Release v0.5.2.** | closed |
| T-04-17 | Tampering | supply chain | low | mitigate | No new package: the two SkiaSharp packages are the versions already locked; lock files committed and restored in locked mode | closed |
| T-04-18 | Elevation of privilege | fake code in a release | low | mitigate | `SyntheticArt` lives in `Cabinet.FakeBgg`, which `ShippedProjectTests` keeps out of every shipped project and the release script never publishes | closed |
| T-04-19 | Information disclosure | outcome file | high | mitigate | Script guard plus the grep check for URLs and image paths, a read for titles and ids, and the owner's sign-off before commit | closed |
| T-04-20 | Information disclosure | scratch report | medium | mitigate | Kept only in the session scratchpad and deleted after commit (Task 3) | closed |
| T-04-21 | Denial of service | BGG and image-host usage | low | mitigate | One run only, with the approved command and the script's own caps and gaps | closed |
| T-04-22 | Tampering / DoS | BggThingParser (hostile XML) | high | mitigate | Same hardened reader as the collection parser (DTD prohibited, no resolver, character cap, no entity characters); tests for DOCTYPE and oversize | closed |
| T-04-23 | Tampering | stored text (designers, mechanics, base titles) | medium | mitigate | `CleanTitle`, 20-entry caps, length cap, text-only rendering (`textContent`, `dir="auto"`) for the base title that reaches the page | closed |
| T-04-24 | Information disclosure | token | high | mitigate | Details client uses `BggAuthHandler`, which attaches the token only to HTTPS requests for `boardgamegeek.com`; test asserts no other host receives it | closed |
| T-04-25 | Denial of service | BGG quota | medium | mitigate | Shared 5-second pacer, at most 20 ids per call, per-run batch cap, one retry, stop the step on failure, extras deadline | closed |
| T-04-26 | Information disclosure | relaying BGG data | medium | mitigate | Layout JSON unchanged except labels; no endpoint lists details; documented | closed |
| T-04-27 | Information disclosure | logs | low | mitigate | Failure category only; no ids, titles or URLs | closed |
| T-04-28 | Tampering | render.js labels and data attributes | medium | mitigate | Titles only through `textContent`; `showBaseLine` read as a strict `=== true`; data attributes set from fixed strings; no style attribute (existing CSP integration test still fails on `style=`) | closed |
| T-04-29 | Denial of service | CSS cost on phones | low | accept | Container queries on cover buttons only and two shadows per repeated item at most; the arch adds three shadows on a handful of plinth elements; checked in the review rounds | closed |
| T-04-30 | Tampering | colour and edge values reaching CSS | medium | mitigate | Server validates with `SpineColour.IsValidPair` and `RgbColour.TryParseHex`; the renderer validates again with a strict `#rrggbb` pattern before `style.setProperty`; invalid values fall back to the palette | closed |
| T-04-31 | Denial of service | two downloads per game | medium | mitigate | Each distinct URL once, existing per-run cap and deadline, own image pacer; a first large sync spreads over several runs | closed |
| T-04-32 | Tampering | settings typo | low | mitigate | `ArtSettings` validated at startup with messages naming the key; inconsistent thresholds refused | closed |
| T-04-33 | Information disclosure | layout JSON | low | mitigate | Only colours, same-origin paths and pixel sizes are added; no URL of BGG or its image host appears (integration assertion) | closed |
| T-04-34 | Tampering | silent rearrangement | low | mitigate | Version bump plus re-recorded goldens; any new stability exception is documented and tested on its own | closed |
| T-04-35 | Tampering | implausible or hostile dimensions | low | mitigate | Existing plausibility bounds; a rebuilt box outside them falls back; the engine still clamps every box to the design limits | closed |
| T-04-36 | Denial of service | layout churn from data drift | low | mitigate | Banded scores with hysteresis and a model version; pose height independent of art; tests pin both | closed |
| T-04-37 | Tampering | settings typo | low | mitigate | Startup validation naming the key | closed |
| T-04-38 | Elevation of privilege | `Images:DevelopmentOrigin` in production | high | mitigate | Honoured only when the environment is Development (the server runs Production); exact origin match only; ignored with a warning elsewhere; tests for Production and Testing | closed |
| T-04-39 | Spoofing | fake reachable from the network | low | mitigate | The fake binds to the loopback interface only (existing behaviour) and is never part of a release (`ShippedProjectTests`) | closed |
| T-04-40 | Information disclosure | sheet pages and console output | high | mitigate | Console prints counts only (tested); pages written only to the given directory; the guide says never commit or publish and delete after delivery; the pre-commit personal-data hook stays active | closed |
| T-04-41 | Elevation of privilege | an operator mode reachable from the web | medium | mitigate | Dispatched from `args` before the web host exists; no route is mapped (acceptance grep); runs only by an operator with server access | closed |
| T-04-42 | Tampering | font file path | low | accept | The font path comes from the operator's own command line or fixed system paths on the server | closed |
| T-04-43 | Denial of service | drawing many pages | low | accept | One page per 8 games, run by hand, on stored data; no network | closed |
| T-04-44 | Tampering | npm supply chain | medium | mitigate | Playwright installed only in the scratchpad (version 1.63.0, cleared in the layout phase), never added to the repository or CI | closed |
| T-04-45 | Information disclosure | screenshots | low | mitigate | Local rounds use the fake with invented titles and in-code pictures; only those are copied into `ui-refs/`; the personal-data hook checks every commit | closed |
| T-04-46 | Information disclosure | third-party requests | medium | mitigate | Check (g) fails the round on any request to a foreign origin | closed |
| T-04-47 | Elevation of privilege | release publication | high | mitigate | Publication only by the owner's deploy-environment approval after Claude reports the draft verified (Task 2, blocking) | closed |
| T-04-48 | Tampering | artefact in transit | high | mitigate | Draft verified on the workstation; the container verifies the attestation again before installing | closed |
| T-04-49 | Information disclosure | evidence | high | mitigate | Only counts, sizes, versions and statuses are read and recorded; `jq` queries return lengths and status counts only | closed |
| T-04-50 | Denial of service | first production art run | low | mitigate | Per-run download cap, image pacer, extras deadline, one extra sync request only | closed |
| T-04-51 | Spoofing | merge and tag identity | medium | mitigate | Identity check on `origin/main` before tagging; noreply tagger | closed |
| T-04-52 | Information disclosure | sheets and screenshots | high | mitigate | Private scratch folder only, sent as images, deleted after delivery; the verify step checks nothing outside `.planning/` changed; SUMMARY uses row numbers and values only | closed |
| T-04-53 | Information disclosure | env file with the token | high | mitigate | Review values go into a separate systemd drop-in; the env file is never opened or edited | closed |
| T-04-54 | Denial of service | restarts calling BGG | low | accept | Each restart runs one start-up sync of two collection calls; the round needs only a handful of restarts | closed |
| T-04-55 | Elevation of privilege | release publication | high | mitigate | Owner approval after the verified-draft message (Task 2, blocking) | closed |
| T-04-56 | Tampering | artefact in transit | high | mitigate | Workstation verification of the draft; attestation verified again on the container | closed |
| T-04-57 | Information disclosure | evidence and audit | high | mitigate | Counts, statuses and PASS lines only; the deployed address stays in a workstation environment variable | closed |
| T-04-58 | Information disclosure | third-party requests from the page | medium | mitigate | Browser audit fails on any request outside the page's origin | closed |
| T-04-59 | Denial of service | `ArtAnalysis` frame and ring checks | medium | mitigate | Every new scan runs on the 96-pixel working copy and is linear in its pixels; floods stay iterative queues; the existing 4000 x 3000 picture test and the decode pixel cap stay in force | closed |
| T-04-60 | Tampering | `Art:UnsureLandscapeMarginPercent` | low | mitigate | Whole number 1 to 100 checked at startup; a bad value stops the app naming the key (tests for 0, 101, text, decimals, empty) | closed |
| T-04-61 | Information disclosure | fixtures, tests, docs, SUMMARY | high | mitigate | New fixtures are drawn in code; numeric features from the owner's real pictures (optional aid) only steer the diagnosis and are never written to any file; `build/lint.sh` (repo rules and secrets) passes | closed |
| T-04-62 | Tampering | a crafted picture steering the verdict | low | accept | A verdict only decides which of the owner's own pictures is drawn and how its box is turned; no security decision depends on it | closed |
| T-04-63 | Denial of service | re-download after the analysis version change | low | accept | Re-measuring runs inside the existing per-run cap, image pacer and extras deadline; the deploy plan bounds the manual syncs | closed |
| T-04-64 | Elevation of privilege | release publication | high | mitigate | Publication only by the owner's deploy-environment approval after Claude reports the draft verified (Task 4, blocking) | closed |
| T-04-65 | Tampering | artefact in transit | high | mitigate | Checksum, attestation and manifest verified on the workstation before approval; the container verifies the attestation again before installing; `verify-published-release.sh` after publication | closed |
| T-04-66 | Information disclosure | evidence and count programs | high | mitigate | The jq programs run on the container and print numbers only; the snapshot is never copied off the server; the SUMMARY holds counts, sizes, versions and statuses | closed |
| T-04-67 | Denial of service | BGG API and image host | medium | mitigate | At most three manual syncs, each after the 10-minute shared window and after the previous sync finished; per-run cap of 80 downloads, the image pacer and the six-minute deadline stay in force | closed |
| T-04-68 | Spoofing | merge and tag identity | medium | mitigate | Identity check on `origin/main` before tagging; noreply tagger | closed |
| T-04-69 | Information disclosure | pull request text | medium | mitigate | Plain title and body with no planning references and no personal data (Task 1 acceptance) PR #12 title and body checked by the orchestrator (2026-10-09): no planning references, no personal data, only the attribution link. | closed |
| T-04-70 | Information disclosure | fixtures, tests, docs, todo, commit messages, SUMMARY | high | mitigate | Fixtures drawn in code with freely chosen colours; constants are code values, never measured values; the stored-feature file is only compared in the terminal; per-row results only in the completion message; the SUMMARY carries the one fixed sentence about real pictures; the todo names row numbers only; `build/lint.sh` secrets and repo-rules checks pass | closed |
| T-04-71 | Information disclosure | title list next to the pictures | medium | mitigate | The harness reads only `rows-files.json`, `stored-features-v2.json` and `art/`; it never opens the title list and prints no title Orchestrator attestation (2026-10-09): the local evidence given to the harness held only hash-named picture files, a row-to-file map and numbers; no titles were ever available to it. No title reached any committed file (denylist scan 0). | closed |
| T-04-72 | Denial of service | new backdrop checks (corners, saturation, convex hull, components) | medium | mitigate | All work on the 96-pixel copy, linear in its pixels; the hull uses at most two points per row; floods and component labelling stay iterative queues; the existing large-picture test and the decode pixel cap stay in force | closed |
| T-04-73 | Tampering | a crafted transparent picture forcing a 3D verdict | low | accept | A verdict only decides which of the owner's own pictures is drawn; no security decision depends on it | closed |
| T-04-74 | Denial of service | re-download after the analysis version change | low | accept | Re-measuring runs inside the per-run cap, image pacer and extras deadline; plan 23 bounds the manual syncs | closed |
| T-04-75 | Tampering | family link names in the details | medium | mitigate | Parsed with the existing hardened reader (no DTD, size cap), names cleaned like every other link name, at most 40 per game, ids parsed as whole numbers; names are only compared and never drawn as markup | closed |
| T-04-76 | Denial of service | series grouping and placement | low | mitigate | Union over at most 40 links per game and one title key, linear in the collection; block placement tries each cubby once per series; the existing 400-item tests stay | closed |
| T-04-77 | Tampering | `Layout:GroupSeries` | low | mitigate | Switch check at startup naming the key; tests for text and empty values | closed |
| T-04-78 | Denial of service | one-off details refresh on the server | low | accept | The existing per-run call limit, 20 games per call and the five-second pacing; for the owner's collection four calls once | closed |
| T-04-79 | Tampering | `Layout:CoverFromExpansions` | low | mitigate | Range check 0 to 20 at startup naming the key; tests for out-of-range, text, decimals and empty values | closed |
| T-04-80 | Denial of service | split family placement | low | mitigate | One extra arrangement try per cubby for a family; the engine stays a pure function covered by the 400-item tests | closed |
| T-04-81 | Tampering | arrangement drawn differs from the one accepted | low | mitigate | Ordering inputs are fixed when a member is placed; the engine already throws when an accepted cubby cannot be arranged, and the invariant tests run on every sample | closed |
| T-04-82 | Information disclosure | realistic size mix, screenshots, SUMMARY | high | mitigate | Coarse bands only, no individual box, no value from the extracts in any file; screenshots of the fake only; the personal-data hook and `build/lint.sh` run on every commit | closed |
| T-04-83 | Tampering | npm supply chain of the review tool | medium | mitigate | Playwright 1.63.0 stays in the scratchpad (already installed), never in the repository or CI | closed |
| T-04-84 | Information disclosure | third-party requests in the local round | low | mitigate | Check (g) fails the round on any request to a foreign origin | closed |
| T-04-85 | Elevation of privilege | release publication | high | mitigate | Publication only by the owner's deploy-environment approval after Claude reports the draft verified (Task 4, blocking) | closed |
| T-04-86 | Tampering | artefact in transit | high | mitigate | Checksum, attestation and manifest verified on the workstation before approval; the container verifies the attestation again before installing; `verify-published-release.sh` after publication | closed |
| T-04-87 | Information disclosure | evidence, count programs and family names | high | mitigate | The jq programs run on the container and print numbers; family names are reduced to a fixed list of category words with everything else counted as `other`; the layout leaves the container only through a count program; the SUMMARY holds counts, sizes, versions and statuses | closed |
| T-04-88 | Denial of service | BGG API and image host | medium | mitigate | At most three manual syncs after the 10-minute window and the previous sync; the details refresh is four calls in one run at the five-second pacing; per-run cap of 80 downloads, the image pacer and the six-minute deadline stay in force | closed |
| T-04-89 | Spoofing | merge and tag identity | medium | mitigate | Identity check on `origin/main` before tagging; noreply tagger | closed |
| T-04-90 | Information disclosure | pull request text | medium | mitigate | Plain title and body with no planning references and no personal data (Task 1 acceptance) PR #13 title and body checked by the orchestrator (2026-10-09): no planning references, no personal data, only the attribution link. | closed |

*Status: open · closed · open — below high threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above workflow.security_block_on count toward threats_open*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-04-01 | T-04-29 | Container queries on cover buttons only and two shadows per repeated item at most; the arch adds three shadows on a handful of plinth elements; checked in the review rounds | owner (plan threat register) | 2026-10-09 |
| AR-04-02 | T-04-42 | The font path comes from the operator's own command line or fixed system paths on the server | owner (plan threat register) | 2026-10-09 |
| AR-04-03 | T-04-43 | One page per 8 games, run by hand, on stored data; no network | owner (plan threat register) | 2026-10-09 |
| AR-04-04 | T-04-54 | Each restart runs one start-up sync of two collection calls; the round needs only a handful of restarts | owner (plan threat register) | 2026-10-09 |
| AR-04-05 | T-04-62 | A verdict only decides which of the owner's own pictures is drawn and how its box is turned; no security decision depends on it | owner (plan threat register) | 2026-10-09 |
| AR-04-06 | T-04-63 | Re-measuring runs inside the existing per-run cap, image pacer and extras deadline; the deploy plan bounds the manual syncs | owner (plan threat register) | 2026-10-09 |
| AR-04-07 | T-04-73 | A verdict only decides which of the owner's own pictures is drawn; no security decision depends on it | owner (plan threat register) | 2026-10-09 |
| AR-04-08 | T-04-74 | Re-measuring runs inside the per-run cap, image pacer and extras deadline; plan 23 bounds the manual syncs | owner (plan threat register) | 2026-10-09 |
| AR-04-09 | T-04-78 | The existing per-run call limit, 20 games per call and the five-second pacing; for the owner's collection four calls once | owner (plan threat register) | 2026-10-09 |

*Accepted risks do not resurface in future audit runs.*

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-10-09 | 90 | 85 | 5 (all medium, non-blocking at block_on high) | gsd-security-auditor x2 (ASVS L2), plans 04-01 to 04-11 and 04-12 to 04-23 |
| 2026-10-09 | 90 | 88 | 2 | orchestrator: T-04-69 and T-04-90 closed by checking the PR #12 and #13 bodies; T-04-71 closed by attestation |
| 2026-10-09 | 90 | 90 | 0 | owner raised T-04-16 to high; T-04-11 and T-04-16 fixed test-first (`f3f64dc`, `593468e`, `9502203`, merged `47a9d4c`), full gate green (2051 tests offline) |

### Notes from the audit (advisory, no status change)

- A PNG within the pixel cap is still decoded at full size before analysis (about 144 MB at the default 36 MP). This is bounded by the pixel cap; see services IN-01 in `04-REVIEW.md`.
- A cancelled run (service stop or run limit) can still lose up to nine pending picture records; with the per-request limit a stalled host no longer reaches the run limit.
- Owner-gated operations (releases, review rounds, syncs) were verified against the release workflow, the deploy scripts and the plan summaries; the denylist scan of the full git history found 0 matches, and every image ever committed is synthetic.

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-10-09 (T-04-11 and T-04-16 fixes ship in release v0.5.2)
