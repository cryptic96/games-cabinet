---
phase: 06
slug: owner-tools-persistence
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 2
created: 2026-10-13
---

# Phase 06 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| Internet to reverse proxy | Visitors and the owner reach the public and owner hostnames through the reverse proxy; the owner router keeps the home/VPN allow-list permanently | HTTP requests, forwarded client address |
| Reverse proxy to container | Public port 5080 for the cabinet; owner port 5082 admitted by the container firewall from the proxy only | Requests with `X-Forwarded-For` |
| Owner browser to owner pages | Forms and script posts on the owner port only, behind the listener wall and the client-range wall | Owner edits, antiforgery tokens, uploaded restore files |
| Uploaded restore file to owner-data store | An owner-supplied JSON file replaces all owner data after a strict check and a preview | Untrusted JSON (size, depth, shape, names, links) |
| Server to BGG API | Background sync only, with the application token | Bearer token, collection data |
| Server to BGG image host | Token-less image client, allowlisted https host only, size and pixel caps | Picture bytes from owner-pasted links and the sync |
| Owner data to public pages | The overlay publishes locations and picture choices to every visitor's cabinet and card | Location names, chosen pictures |
| Workstation to containers | Maintenance over SSH with dedicated keys pinned to the workstation's address | Shell access (passwordless sudo) |

---

## Threat Register

Built from the 30 plans' threat models (116 IDs; repeated threats across plans share a verdict) and verified at ASVS level 2 against the code at commit 7a4971c. Path abbreviations: OA `Cabinet.Service/Owner/OwnerAccess.cs`, OE `Cabinet.Service/Owner/OwnerEndpoints.cs`, FH `Cabinet.Service/Hosting/ForwardedHeadersSetup.cs`, PG `Cabinet.Service/Program.cs`, ODS `Cabinet.Service/Owner/OwnerDataService.cs`, STO `Cabinet.Repository/Storage/OwnerDataStore.cs`, EXP `Cabinet.Repository/Storage/OwnerDataExport.cs`, CP `Cabinet.Service/Owner/CollectionPublisher.cs`, SR `Cabinet.Service/Sync/SyncRunner.cs`, LOC/PIC/SET the Locations, Pictures and Settings page models, RESP `Cabinet.Service/Owner/OwnerResponses.cs`, LE `Cabinet.Domain/Owner/LocationEdits.cs`, LF `Cabinet.Service/Owner/OwnerLinkFetcher.cs`, LW `Cabinet.Service/Owner/OwnerLinkWorker.cs`, DL `Cabinet.Repository/Images/ImageDownloader.cs`, SHJ `Cabinet.Service/wwwroot/js/owner-shared.js`, WT `Cabinet.IntegrationTests/OwnerWallTests.cs`.

### Threats closed by the review-fix pass

Open at the first audit (7a4971c); closed by the review-fix pass (06-REVIEW-FIX.md) and confirmed by the re-audit at e87b681.

| Threat ID | Category | Component | Severity | Disposition | Gap found, and the fix | Status |
|-----------|----------|-----------|----------|-------------|----------------------|--------|
| T-06-10-01 | Tampering / data loss | 30-day expiry after a held-back or failed sync | high | mitigate | `CollectionPublisher.Publish` always reconciles (CP:48-60); start-up (`SyncStartup.cs`:49-54) and the stored-collection reload (SR:351-366) publish a stale stored collection, so a long outage then a restart can forget a game's owner data before a fresh sync lists it. Fix: reconcile only for a fetched, guard-accepted collection (review WR-03) Fixed: `Publish(snapshot, reconcile)` reconciles only from `SyncRunner.Commit`; start-up and the reload pass `reconcile: false` (CP:52,64-67; `SyncStartup.cs`:53; SR:366); `OwnerExpiryTests.cs`:41,59 | closed |
| T-06-24-02 | Tampering | hostile restore-file content | high | mitigate | `Revision` and `ChangesSinceDownload` are only checked for being negative (EXP:221-222); `long.MaxValue` wraps in `Restored` (ODS:482) and values above 2^53 break script equality, locking the owner tools. Fix: cap both in `WithinLimits`, guard the arithmetic (review WR-02) Fixed: `WithinLimits` caps the revision and change count (EXP:84,87,227-230); `NextRevision` and the counter saturate (ODS:468,502); `OwnerRestoreParserTests.cs`:117 | closed |
| T-06-30-02 | Spoofing | misconfigured known proxy degrading the range wall | high | mitigate | Start-up only refuses a missing known proxy (OA:90-95); the framework's default loopback trust is kept (FH:21-34) so the self-check probe cannot detect a wrong value, and with a wrong value or no usable `X-Forwarded-For` the judged address is the proxy's own, which sits in the home range (OA:113). Fix: record the original peer before forwarded headers, refuse on the owner port unless it is a configured proxy, refuse in Production when no forwarded address was applied, clear the default loopback trust (review WR-01, IN-04) Fixed: `UseConnectionPeer` records the peer before forwarded headers (PG:68-70; OA:117-127); the wall requires a configured proxy peer and, in Production, an applied forwarded address (OA:162-175); default loopback trust cleared (FH:23-24); self-check reworded; `OwnerWallTests.cs`:154,164,176 | closed |
| T-06-06-04 | Information disclosure | owner paths in public HTML, scripts or data | medium | mitigate | Public page and data are clean, but `MapStaticAssets` (PG:96) serves `/js/owner-*.js` and `/css/owner.css` on every port; the leak test skips `owner-*.js` (WT:281). Fix: 404 the owner assets on the public port (review WR-11) Fixed: `MapStaticAssets().MarkOwnerAssets()` marks owner scripts and stylesheet (incl. fingerprinted copies) owner-only (PG:97; OA:242-266); `OwnerWallTests.cs`:292,313,346 | closed |
| T-06-17-04 | Denial of service | re-checks hammering the image host | low | mitigate | When the cache write throws, nothing is recorded (LW:210-222) and `IsDue` ignores `_lastChecked` for a link with no usable copy (LW:177-179), so the link is refetched on every wake. Fix: in-memory backoff respected by `IsDue` (review WR-04) Fixed: in-memory store-failure backoff respected by `IsDue` (LW:182,196-197,216,228); `OwnerLinkWorkerTests.cs`:207 | closed |

### Closed threats

| Threat ID | Category | Component | Severity | Disposition | Mitigation (evidence) | Status |
|-----------|----------|-----------|----------|-------------|-----------------------|--------|
| T-06-01-01 | EoP | owner routes on public port | high | mitigate | OA:136-187; PG:89,94; OE:72-81,121; WT:67,209-255 | closed |
| T-06-01-02 | Spoofing/EoP | missing known proxy | high | mitigate | OA:88-95,232-235; `OwnerAccessTests.cs`:27,35 | closed |
| T-06-01-03 | CSRF | owner forms | medium | mitigate | OE:83-92 (Strict, HttpOnly, Secure in Production, header token) | closed |
| T-06-01-04 | DoS | large owner posts | low | mitigate | OE:30,77-80; OA:177-183; WT:293-308 | closed |
| T-06-01-05 | Spoofing | same-LAN guest editing | low | accept | AR-06-01 | closed |
| T-06-02-01 | EoP | owner port from other hosts | high | mitigate | `deploy/nftables/cabinet.nft.in`:15,26; `render-templates-test.sh`:163-165; `50-firewall.sh`:23-37 (self-check does not yet prove the live rule: review WR-07) | closed |
| T-06-02-02 | EoP | owner tools exposed at go-public | high | mitigate | `deploy/traefik/cabinet.yml.example`:13-16,67-76; `docs/lxc-setup.md`:189-193; owner router keeps the allow-list (06-30-SUMMARY) | closed |
| T-06-02-03 | Spoofing | client X-Forwarded-For through the proxy | high | mitigate | `cabinet.yml.example`:18-22; FH:21-22 (ForwardLimit 1); `render-templates-test.sh`:200-203 | closed |
| T-06-02-04 | Info disclosure | real hostnames or ranges committed | high | mitigate | `cabinet.env.example`:16-18 commented placeholders; tracked tree holds RFC 5737 and example domains only (scanner gaps: review WR-06, IN-06) | closed |
| T-06-02-05 | Info disclosure | self-check or route-check output | medium | mitigate | `build/check-owner-routes.sh`:28-52; its test:106-107; `cabinet-selfcheck`:137-142 | closed |
| T-06-02-06 | DoS | release before ranges are prepared | low | mitigate | `cabinet.env.example`:12-18; OA:72-76,232-235 | closed |
| T-06-03-01 | Tampering | review sheet renaming the snapshot | medium | mitigate | `SnapshotStore.cs`:92-121; `ReviewSheetCommand.cs`:81 | closed |
| T-06-03-02 | DoS | tool crash on path or font errors | low | mitigate | `ReviewSheetCommand.cs`:60-110,255,276 | closed |
| T-06-03-03 | Info disclosure | reason text echoing paths | low | accept | AR-06-02 | closed |
| T-06-04-01 | DoS | hourly requests for undescribed games | medium | mitigate | `EnrichmentPlanner.cs`:20; `EnrichmentSync.cs`:41-43,89 | closed |
| T-06-04-02 | DoS | rollback setting the collection aside | medium | mitigate | `CollectionSnapshot.cs`:81-91; `StoredJson.cs`:18-24 | closed |
| T-06-05-01 | Tampering | crafted picture shape | low | mitigate | `BoxShape.cs`:121-144 | closed |
| T-06-05-02 | Tampering | source order changes card entry | low | mitigate | `SnapshotMapper.cs`:106,119-122 | closed |
| T-06-06-01 | Spoofing | forged X-Forwarded-For | high | mitigate | FH:21-34; PG:58-59,68-69; WT:152-162 (default loopback trust noted under T-06-30-02) | closed |
| T-06-06-02 | EoP | /0, mapped or malformed ranges | high | mitigate | OA:245-280; `OwnerAccessTests.cs`:43-60,156-160 | closed |
| T-06-06-03 | EoP | path or Host tricks | high | mitigate | OA:25-30,136-187; WT:209-255 | closed |
| T-06-06-05 | Info disclosure | configured values in errors | medium | mitigate | OA:82,93-94,207,226,234; FH:29-30; `OwnerAccessTests.cs`:144,156-160 | closed |
| T-06-06-06 | DoS | large owner posts | low | mitigate | WT:293-308 | closed |
| T-06-07-01 | XSS | token inline script | medium | mitigate | `_OwnerLayout.cshtml`:21,63; SHJ:21-23; `ContentSecurityPolicy.cs`:13; `OwnerShellTests.cs`:50 | closed |
| T-06-07-02 | Info disclosure | owner chrome on public page | low | mitigate | `Pages/Owner/_ViewStart.cshtml`:2; `_Layout.cshtml`:16-19 | closed |
| T-06-07-03 | Info disclosure | requests to other hosts | low | mitigate | browser tests assert loopback-only requests, e.g. `OwnerPicturesPageTests.cs`:57 | closed |
| T-06-08-01 | Tampering/loss | damaged or newer owner file | high | mitigate | STO:10-14,52-71,146-210; ODS:207-210,306,352 | closed |
| T-06-08-02 | Tampering | lost update between sync and owner | high | mitigate | CP:15,54-58,66-75; ODS:96,203,212-215 | closed |
| T-06-08-03 | Tampering | owner data in the sync baseline | medium | mitigate | `CollectionStore.cs`:43,85; SR:241-246; `OwnerLocationTests.cs`:68 | closed |
| T-06-08-04 | Info disclosure | location names in logs | medium | mitigate | STO:207; ODS logs revision, counts and exception types only | closed |
| T-06-08-05 | Info disclosure | location names public on the card | low | accept | AR-06-03 | closed |
| T-06-09-01 | CSRF | owner posts | high | mitigate | automatic antiforgery, no opt-outs; OE:83-92; `OwnerLocationTests.cs`:276 | closed |
| T-06-09-02 | XSS | location names | high | mitigate | Razor encoding; LE:92-94,108-111; CSP | closed |
| T-06-09-03 | Open redirect | no-script redirect | medium | mitigate | RESP:94-158; LOC:50-62 | closed |
| T-06-09-04 | Tampering | unknown game or location ids | medium | mitigate | LOC:317-329,446-449; LE:266,275 | closed |
| T-06-09-05 | DoS | many or long names | low | mitigate | `OwnerData.cs`:119,122; LE:87-99; OE:77-80 | closed |
| T-06-10-02 | Tampering/loss | lost edit at sync commit | high | mitigate | CP:54-58; `CollectionPublisherRaceTests.cs`; `OwnerPersistenceTests.cs`:52 | closed |
| T-06-10-03 | Path traversal | version names | medium | mitigate | STO:104-109,129-130 | closed |
| T-06-10-04 | DoS | unbounded versions or notifications | low | mitigate | STO:21,93-96; ODS:85,457; `OwnerChangeNotifier.cs`:18,94-95 | closed |
| T-06-10-05 | Info disclosure | live channel leaking owner data | low | mitigate | `OwnerChangeNotifier.cs`:114-131; `OwnerPersistenceTests.cs`:154 | closed |
| T-06-11-01 | XSS | names in owner pages and dialogs | high | mitigate | Razor, `dir="auto"`; markup-API lint (`10-repo-rules.sh`:340); no markup sinks in page scripts | closed |
| T-06-11-02 | CSRF | location posts | high | mitigate | SHJ:8,74-100,133-135 | closed |
| T-06-11-03 | Tampering | confirm or location id | medium | mitigate | LOC:131-132,217-221,412-426,446-449 | closed |
| T-06-11-04 | Repudiation | unnoticed LAN edits | low | mitigate | ODS:489-520 (change log), 243-285 (Undo); SET:137-150 (export) | closed |
| T-06-11-05 | DoS | many or long names | low | mitigate | as T-06-09-05 | closed |
| T-06-12-01 | CSRF | bulk move | high | mitigate | `owner-placing.js`:331 via `postOwner`; `OwnerBulkMovePostTests.cs`:78 | closed |
| T-06-12-02 | Tampering | unknown or excess game ids | medium | mitigate | LE:257-276; LOC:331-360; 64 KB limit | closed |
| T-06-12-03 | Tampering | accidental mass move | medium | mitigate | `owner-placing.js`:46-60; ODS:243-285 (Undo reliability: review WR-08, WR-10, IN-07) | closed |
| T-06-12-04 | XSS | names and titles in the dock | medium | mitigate | `textContent`; markup-API lint | closed |
| T-06-13-01 | XSS | search query echo | high | mitigate | `owner-pictures.js`:546-551; `owner-games.js`:177 | closed |
| T-06-13-02 | CSRF | assign posts and keepalive | high | mitigate | SHJ:94-100; `owner-games.js`:62-65 | closed |
| T-06-13-03 | Tampering | filter query | low | mitigate | LOC:126 | closed |
| T-06-14-01 | CSRF | Choose and Undo | high | mitigate | PIC:153-165,333-347; `OwnerPictureChoiceTests.cs`:92 | closed |
| T-06-14-02 | XSS | BGG titles on the pictures page | high | mitigate | Razor, `dir="auto"`; CSP | closed |
| T-06-14-03 | Tampering | stale caches | low | mitigate | `CollectionStore.cs`:85 (overlay part of the version) | closed |
| T-06-15-01 | Info disclosure | BGG token to the image host | high | mitigate | `SyncEndpoints.cs`:91-97; `BggTransport.cs`:46-62,72-79; `OwnerLinkTests.cs`:42-44; `OwnerLinkPreviewTests.cs`:62 | closed |
| T-06-15-02 | Tampering/loss | clean-up deleting owner pictures | high | mitigate | ODS:181-187; SR:322-334 | closed |
| T-06-15-03 | DoS | huge or hostile pictures | high | mitigate | DL:133-150,200-229; `ArtProcessor.cs`:102,129-147; LW:134-144 | closed |
| T-06-15-04 | Tampering | worker overwriting a changed link | medium | mitigate | LW:233-251 | closed |
| T-06-15-05 | Info disclosure | link addresses in logs | low | mitigate | LW:148,153,219; LF:69 | closed |
| T-06-16-01 | CSRF | Choose from tiles | high | mitigate | `owner-pictures.js`:326 via `postOwner` | closed |
| T-06-16-02 | XSS | titles on tiles and rows | high | mitigate | Razor, `textContent`, lint, CSP | closed |
| T-06-16-03 | Info disclosure | third-party requests | medium | mitigate | `OwnerPictureTilesTests.cs`:61,104 | closed |
| T-06-17-01 | SSRF | pasted link fetch | high | mitigate | LF:93-113; `ArtUrl.cs`:46-71; DL:131-150; `OwnerLinkFetcherTests.cs`:62-127 | closed |
| T-06-17-02 | DoS | huge pictures | high | mitigate | LF:77,115-116,159-174; DL caps | closed |
| T-06-17-03 | Licence | gallery page links | high | mitigate | LF:79,105-108,128-134; no page client exists | closed |
| T-06-18-01 | XSS | `view` value and search | medium | mitigate | PIC:321-322; `owner-pictures.js`:551 | closed |
| T-06-18-02 | CSRF | Looks right | high | mitigate | `owner-pictures.js`:356; automatic antiforgery | closed |
| T-06-18-03 | Info disclosure | owner marks on public pages | low | mitigate | `CardRecords.cs` carries no mark fields | closed |
| T-06-19-01 | Open redirect | `returnTo` | medium | mitigate | LOC:267-274 | closed |
| T-06-19-02 | XSS | `game` value | medium | mitigate | PIC:133-135,327-331 | closed |
| T-06-19-03 | CSRF | Assign from the pictures page | high | mitigate | automatic antiforgery; `postOwner` | closed |
| T-06-20-01 | SSRF | Check link | high | mitigate | PIC:233-247, then LF | closed |
| T-06-20-02 | Path traversal | preview tokens | high | mitigate | `StagedItems.cs`:38-39,68-91; OE:107-121; `OwnerLinkPreviewTests.cs`:113-115 | closed |
| T-06-20-03 | Info disclosure | preview on the public port | high | mitigate | OE:121; OA:136-153; live route check 404 | closed |
| T-06-20-04 | Content sniffing | served preview bytes | medium | mitigate | OE:116-119 | closed |
| T-06-20-05 | CSRF | Check link and Use this | high | mitigate | `OwnerLinkPreviewTests.cs`:299 | closed |
| T-06-21-01 | DoS | staging memory | medium | mitigate | `StagedItems.cs`:25,31,54-57 | closed |
| T-06-21-02 | XSS | error text echoing the paste | medium | mitigate | LF:13-45 (problem enum); PIC:137-139 (fixed copy) | closed |
| T-06-21-03 | CSRF | Remove link and Undo | high | mitigate | `OwnerLinkPreviewTests.cs`:315 | closed |
| T-06-21-04 | Info disclosure | link addresses in logs | low | mitigate | as T-06-15-05 | closed |
| T-06-22-01 | Info disclosure | download reachable publicly | high | mitigate | SET:137-150 (POST on the owner port); OE:72-81 | closed |
| T-06-22-02 | CSRF | download post | medium | mitigate | `OwnerBackupTests.cs`:226 | closed |
| T-06-22-03 | Info disclosure | change log or banner public | medium | mitigate | `_OwnerLayout.cshtml`:40-43 only | closed |
| T-06-22-04 | Repudiation | backup staleness | low | accept | AR-06-04 | closed |
| T-06-23-01 | Tampering | finish value in a class attribute | medium | mitigate | `SiteFinish.cs`:26-29; `Finishes.cs`:16-17 | closed |
| T-06-23-02 | CSRF | finish posts | medium | mitigate | `FinishPanelTests.cs`:88 | closed |
| T-06-24-01 | DoS | huge or nested uploads | high | mitigate | SET:33-37,302+; EXP:75,81,120-128,161; OA:177-183 (Settings handlers all accept 3 MB: audit note) | closed |
| T-06-24-03 | Tampering/loss | restore by mistake | high | mitigate | SET:158-189,224-257; ODS:413-423; STO:82-97 | closed |
| T-06-24-04 | CSRF | check and restore | high | mitigate | `OwnerRestoreTests.cs`:150,240 | closed |
| T-06-24-05 | SSRF | restored links fetched | medium | mitigate | EXP:287-303 (policy checked, records stripped); worker goes through LF | closed |
| T-06-25-01 | Path traversal | version selection | high | mitigate | SET:194-196 (`int?`); STO:104-109 | closed |
| T-06-25-02 | Tampering/loss | overwriting a damaged file | high | mitigate | ODS:207-210,306,413-423; `OwnerRestoreTests.cs`:113,264,296 | closed |
| T-06-25-03 | DoS | public side while unreadable | medium | mitigate | CP:93; `OwnerRestoreTests.cs`:369 | closed |
| T-06-25-04 | CSRF | check version and restore | high | mitigate | `OwnerRestoreTests.cs`:358 | closed |
| T-06-26-01 | Info disclosure | owner paths or chrome on the public page | high | mitigate | `Pages/Index.cshtml`:7,13,47,119-121,132-134; `Index.cshtml.cs`:130; `_Layout.cshtml`:16-19; WT:257-273; live route check | closed |
| T-06-26-02 | Open redirect | `data-edit-href` | medium | mitigate | `Index.cshtml`:121; `card-view.js`:47,286-311 | closed |
| T-06-26-03 | XSS | accessible names | medium | mitigate | `owner-cabinet.js`:45,55 | closed |
| T-06-27-01 | Info disclosure | owner strip or count on the public page | high | mitigate | `Index.cshtml`:47-59; `Index.cshtml.cs`:130-143 | closed |
| T-06-27-02 | XSS | strip text | low | mitigate | fixed copy plus a number, Razor-encoded | closed |
| T-06-28-01 | Info disclosure | screenshots or docs with personal data | high | mitigate | synthetic review frames only (spot-checked); text scan clean | closed |
| T-06-28-02 | Info disclosure | real hostnames or addresses in docs | high | mitigate | tracked-tree scan clean; `40-secrets.sh` | closed |
| T-06-28-03 | Repudiation | taste decisions without the owner | low | mitigate | 06-28-SUMMARY records the owner's verbatim answer | closed |
| T-06-29-01 | Tampering | release artifact | high | mitigate | checksum, attestation and published-release checks (06-29-SUMMARY) | closed |
| T-06-29-02 | EoP | publishing | high | mitigate | owner's deploy-environment approval (06-29-SUMMARY) | closed |
| T-06-29-03 | EoP | merge and tag | high | mitigate | owner's "approve and tag" (06-29-SUMMARY) | closed |
| T-06-29-04 | Info disclosure | evidence in the SUMMARY | high | mitigate | counts, versions and hashes only | closed |
| T-06-29-05 | EoP | owner tools exposed by the release | high | mitigate | 404 on the public port, nothing on 5082 at release, self-check 24/24 | closed |
| T-06-30-01 | EoP | owner paths via the public router | high | mitigate | application port wall; route check 6/6 and self-check 24/24 (06-30-SUMMARY); outside-network check pending as a verification item | closed |
| T-06-30-03 | Info disclosure | real ranges or hosts in the repository | high | mitigate | `06-OWNER-HOST-STEPS.md` placeholders only; 06-30-SUMMARY counts only | closed |
| T-06-30-04 | Spoofing | LAN guest editing | low | accept | AR-06-01 | closed |
| T-06-30-05 | EoP | maintenance key with sudo on the reverse-proxy host | high | accept | AR-06-05 | closed |

Repeated across plans with a shared verdict: owner routes on the public port (01-01, 06-03, 30-01), body limit (01-04, 06-06), guest editing (01-05, 30-04), real values in the repository (02-04, 28-02, 30-03), lost update (08-02, 10-02), damaged file kept (08-01, 25-02), version traversal (10-03, 25-01), name limits (09-05, 11-05), link logs (15-05, 21-04), hostile pictures (15-03, 17-02), pasted-link SSRF (17-01, 20-01), owner chrome on the public page (07-02, 26-01, 27-01), loopback-only requests (07-03, 16-03), the antiforgery mechanism (15 plans) and the encoding mechanism (5 plans).

*Status: open · closed · open — below high threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above workflow.security_block_on count toward threats_open*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-06-01 | T-06-01-05, T-06-30-04 | Anyone on the home network or the VPN can edit owner data; no login by decision (owner tools are trusted-network only, every change is logged and undoable, and owner data is backed up by download) | owner (phase decision D-03) | 2026-10-13 |
| AR-06-02 | T-06-03-03 | The review-sheet tool, a local command-line tool the owner runs, may name a file path in its error text; the path is the owner's own | owner ("Accept (Recommended)") | 2026-10-13 |
| AR-06-03 | T-06-08-05 | Location names the owner writes are shown to every visitor on the card; that is the feature | owner (phase decision D-16) | 2026-10-13 |
| AR-06-04 | T-06-22-04 | The backup is the owner's own download with a reminder banner, not a scheduled job; a stale backup is possible | owner (phase decision D-23) | 2026-10-13 |
| AR-06-05 | T-06-30-05 | A dedicated maintenance key with passwordless sudo on the reverse-proxy container, pinned to the workstation's address, was added at the owner's request to add the owner router. Kept past go-public by the owner's decision. Mitigation (owner's decision at the final step, 2026-10-26): the keys stay without a passphrase; address pinning (`from=`) to the workstation, verified on 2026-10-20 and again on 2026-10-26 after going public; SSH on both containers reachable only from the admin sources (the reverse-proxy container's port 22 restricted by the owner on 2026-10-26). The owner's words: "I think, since you can only access it from within my local network, i'll accept these access tokens." Status: accepted by the owner without the passphrase mitigation. Residual risk: anything running on the workstation can use the keys at any time; a passphrase can still be added with `ssh-keygen -p`. review point: 2027-01-24 (90 days after going public; the site opened on 2026-10-26) | owner | 2026-10-26 |

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-10-13 | 117 | 112 | 5 (3 blocking: T-06-10-01, T-06-24-02, T-06-30-02; 2 non-blocking: T-06-06-04, T-06-17-04) | gsd-security-auditor (ASVS 2, at 7a4971c); T-06-30-05 registered from an unregistered SUMMARY flag and accepted by the owner |

| 2026-10-13 | 117 | 117 | 0 | gsd-security-auditor re-audit (ASVS 2, at e87b681, after the review-fix pass); no regression in the ten closed threats sharing code with the fixes |

Audit notes: the two level-2 boundaries named for this phase hold (the owner walls key on the local port and the forwarded-headers result, never Host; the image client carries no token, no cookies and follows no redirects, and the token handler attaches only to the BGG API host). The five open threats are each addressed by a code review finding (WR-03, WR-02, WR-01 with IN-04, WR-11, WR-04) being fixed in a parallel pass; the re-audit at e87b681 confirmed all five closed. Re-audit notes (non-blocking): `Publish`'s `reconcile` defaults to true, so a future caller must choose it deliberately; expiry can now run late (only on a changed commit) but never early; the owner wall's own known-proxy check closes T-06-30-02 even if the framework's loopback default were still present.

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-10-13
