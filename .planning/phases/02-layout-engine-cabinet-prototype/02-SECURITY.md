---
phase: 2
slug: layout-engine-cabinet-prototype
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 2
block_on: high
created: 2026-10-06
---

# Phase 2 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

Audited at HEAD `2d6804d`. The code outside `.planning/` is identical to the `v0.2.0` tag (`git diff v0.2.0 HEAD -- . ':!.planning'` is empty) and CI passed on HEAD. All paths are relative to the repository root.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| browser -> public listener | Anonymous `sample` and `profile` query values and `If-None-Match` reach the page model and `/cabinet/layout` | Untrusted short strings; only allowlisted names are acted on |
| layout JSON -> DOM | Titles, labels and palette colours become page text and CSS custom properties | Invented game titles today, real BGG strings later (untrusted text) |
| operator env file -> app configuration | `Layout__*` and `Prototype__Enabled` overrides read at startup | Operator-controlled settings, validated at startup |
| collection data -> engine | Expansion graphs, counts, depths and box sizes from data the app will not control once the real collection arrives | Untrusted numbers and parent references |
| section design data -> engine | Section designs edited by hand during review rounds | Repository-controlled data, validated before every build |
| npm registry -> executor scratch | Playwright and its browser downloaded for local screenshots only | Third-party package, outside the repository |
| owner -> review decision | The owner's "approve" authorises merge and tag | Human approval |
| milestone branch -> main | Changes reach the release branch only through a checked pull request | Code |
| owner approval -> published release | Deploy-environment approval is the only human gate between a tag and the server | Release artefact |
| GitHub and Sigstore -> container | The container pulls public artefacts and verifies them before installing | Signed release zip, checksum, attestation bundle |

---

## Threat Register

Plan IDs collide: T-02-26 to T-02-29 were used in both 02-08 and 02-09 with different meanings, so they are disambiguated by plan below. T-02-SC appeared in 02-03 (low), 02-07 (high) and 02-09 (high) for the same scratch install and is consolidated at the highest severity.

| Threat ID | Plan | Category | Component | Severity | Disposition | Mitigation (evidence) | Status |
|-----------|------|----------|-----------|----------|-------------|-----------------------|--------|
| T-02-01 | 02-01 | Denial of service | `/cabinet/layout` | medium | mitigate | `Cabinet.Service/Layout/LayoutEndpoint.cs:55-58` checks the prototype switch, the exact ordinal sample allowlist (`Prototype/SampleCatalog.cs:50`) and `SectionDesigns.TryGet` (`Cabinet.Domain/Layout/SectionDesigns.cs:85-87`) before any cache or generation. The only paths into the generator are `SampleCatalog.cs:101` and `LayoutCache.cs:39`. Test: `Cabinet.IntegrationTests/LayoutEndpointTests.cs:85-103` (9 out-of-allowlist cases return 404) | closed |
| T-02-02 | 02-01 | Tampering | `render.js` text output | medium | mitigate | `wwwroot/js/render.js` uses only `createElement`, `textContent` (105, 160), `setAttribute`/`title` (154-155) and `dataset`. No `innerHTML`, `outerHTML`, `insertAdjacent*`, `document.write`, `eval` or `Function` anywhere in `wwwroot/js`. The CSP adds a second layer (`Hosting/ContentSecurityPolicy.cs:12-26`, registered first at `Program.cs:67`) | closed |
| T-02-03 | 02-01 | Information disclosure | layout JSON | low | accept | See the accepted risks log (AR-02-01) | closed (accepted) |
| T-02-04 | 02-01 | Denial of service | engine section growth | medium | mitigate | `Cabinet.Domain/Layout/CabinetLayoutEngine.cs:65-77`: each game opens at most one new section, then the engine throws `InvalidOperationException` (line 73) | closed |
| T-02-05 | 02-01 | Denial of service | ops health | low | mitigate | `Program.cs:70-84`: the `UseHealthChecks` mapping is unchanged since the previous phase. It is pinned to the loopback ops port (`Hosting/OpsEndpoint.cs:11-27`), and no custom health check touches layout code. Test: `HealthEndpointTests.cs:12-24` (ops 200, public 404) | closed |
| T-02-06 | 02-02 | Tampering | `LayoutSettings.FromConfiguration` | medium | mitigate | `Cabinet.Service/Layout/LayoutSettings.cs:23-96` checks every value and throws an error naming the key. It runs eagerly when services are registered (`LayoutEndpoint.cs:26`, from `Program.cs:39` before `Build`). Tests: `LayoutSettingsTests.cs:48-124`. Residual: observation 5 | closed |
| T-02-07 | 02-02 | Denial of service | settings ranges | low | mitigate | Upper bounds of 20 and 100 at `LayoutSettings.cs:32-33`, re-checked by `LayoutOptions.Validate()` (`LayoutOptions.cs:50-63`, engine line 50). Edge tests: `LayoutSettingsTests.cs:63-96` | closed |
| T-02-08 | 02-02 | Information disclosure | committed appsettings | low | mitigate | No secret-shaped key names in `appsettings.json:20-29`. `Cabinet.UnitTests/Configuration/CommittedConfigurationTests.cs:9-31` scans every appsettings file | closed |
| T-02-09 | 02-03 | Tampering | label rendering | medium | mitigate | `render.js:104,159` set `dir="auto"`, and text goes in through `textContent` only. `wwwroot/css/cabinet.css:172-174,340-352` set `unicode-bidi: plaintext` on label and sub-line | closed |
| T-02-10 | 02-03 | Tampering | palette colours as custom properties | low | accept | See the accepted risks log (AR-02-02) | closed (accepted) |
| T-02-11 | 02-03 | Spoofing | shortened titles | low | mitigate | Placements carry both `Title` and `Label` (`CubbyArrangement.cs:334-348`). `render.js:76,153-155` puts the full title in `aria-label` and `title`. Tests: `SpineLabelTests.cs:130-160`, `FamilyLayoutTests.cs:330` | closed |
| T-02-31 | 02-03 | Denial of service | furniture finish on low-end phones | low | mitigate | The finish block (`cabinet.css:436-714`) has no `.placement` selector. Its gradients sit only on section, cubby and decoration elements, with at most 6 background layers per surface. No filter, blur, backdrop-filter, perspective, translateZ or 3D rotate. `content-visibility: auto` at line 50. The 390 px screenshot check is recorded only in the SUMMARY | closed |
| T-02-32 | 02-03 | Information disclosure | review screenshots and scratch scripts | low | mitigate | 02-03-SUMMARY contains no paths. No image or scratch script has ever been tracked since `v0.1.4`, and `git status --porcelain` is clean. Other plans' summaries drift from this (see Unregistered Flags) | closed |
| T-02-12 | 02-04 | Tampering | `IndexModel` sample handling | medium | mitigate | `Pages/Index.cshtml.cs:29-33` sets `SampleName = catalog.Resolve(sample)` (allowlist or default). Razor encodes `data-sample`. Script-tag test: `CabinetPageTests.cs:139-153` | closed |
| T-02-13 | 02-04 | Information disclosure | prototype scaffolding | low | mitigate | One switch, `Prototype:Enabled` (`Prototype/SampleCatalog.cs:77-94`); an invalid value fails startup (`CabinetPageTests.cs:185-191`). When it is off, `Index.cshtml:16,35,48` render no nav, mount or module script, and `LayoutEndpoint.cs:55` returns 404. Test: `CabinetPageTests.cs:155-170` | closed |
| T-02-14 | 02-04 | Denial of service | `/cabinet/layout` | medium | mitigate | The allowlist check comes before the cache (`LayoutEndpoint.cs:55-60`). Cache entries are bounded by the allowlists and built lazily once (`LayoutCache.cs:33`). Samples are generated at most once (`SampleCatalog.cs:60-68`). Conditional requests get 304 (`LayoutEndpoint.cs:65-76`). Tests: `SampleGenerationTests.cs:19-79`, `LayoutEndpointTests.cs:120-150`. No rate limiter yet; acceptable while the route is LAN-only | closed |
| T-02-15 | 02-04 | Tampering | strict-CSP readiness | medium | mitigate | `CabinetPageTests.cs:75-85` (style attribute, inline script body) and `193-204` (script without src, importmap) run against the rendered page. The policy is sent and tested on every response type (`ContentSecurityPolicyTests.cs:16-79`), with no `unsafe-inline`, `unsafe-eval`, wildcard, `data:` or `http` source | closed |
| T-02-16 | 02-04 | Information disclosure | docs | low | mitigate | `docs/cabinet-layout.md:79-85` names only the env file path and key names. The phase's docs and examples use documentation ranges and `example.com` placeholders only. `build/lint/checks/10-repo-rules.sh` passes | closed |
| T-02-17 | 02-05 | Denial of service | very large families | low | mitigate | `StackLayout.cs:24-49` costs O(min(count, ExpansionStackMax)). The column width is fixed (`CubbyArrangement.cs:215,291,310`), heights take one linear pass (270-272), and there is one marker (303-319) | closed |
| T-02-18 | 02-05 | Tampering | expansion titles in the DOM | medium | mitigate | Layer, marker and orphan text comes from `COPY` (`wwwroot/js/copy.js:16-47`), used at `render.js:80,84,105,119`, and is written only via `textContent`, `aria-label` or `title` | closed |
| T-02-19 | 02-05 | Denial of service | cyclic or self-referencing parent refs | low | mitigate | `CabinetLayoutEngine.cs:367-413`: only `ItemKind.Base` items can be parents (371, 388-391); anything else becomes an orphan (395-399). No recursion. Test: `FamilyLayoutTests.cs:409` | closed |
| T-02-20 | 02-06 | Denial of service | oversize or absurd box sizes | medium | mitigate | Sizes are clamped on entry for orphans, bases and expansions (`CabinetLayoutEngine.cs:99,113,141,170-194`). The guarded throw is at 71-75. Tests: `SectionDesignTests.cs:128-230`. The direct guard test was removed as unreachable once `Validate` gained fit rules; the guard stays in code | closed |
| T-02-21 | 02-06 | Denial of service | broken hand-edited design | medium | mitigate | `design.Validate()` runs before every build (`CabinetLayoutEngine.cs:51`; body at `SectionDesign.cs:136-152`). `SectionDesignTests.cs:23-33` validates every shipped design, and line 115 proves that an invalid design throws before any placement | closed |
| T-02-22 | 02-06 | Tampering | silent layout drift | low | mitigate | `LayoutGoldenTests.cs:53,68,81-95,100`: recorded layouts, 400-sample digests, version-bump guard and exact file set. `Golden/layout-version.txt` is 8, matching `CabinetLayoutEngine.LayoutVersion`. Residual: observation 3 | closed |
| T-02-23 | 02-07 | Elevation of privilege | review decision | high | mitigate | The 02-07 review task is a `checkpoint:decision gate="blocking-human"` with an explicit "approve" signal. `.planning/config.json` has auto-advance off. The owner's round-2 "approve" is recorded in 02-07-SUMMARY (SUMMARY claim). On GitHub: PR #7 was merged by the repository-owner account with build-test and lint passing, and the tag ruleset limits `v*` tags to the admin role. Caveats: observation 2 | closed |
| T-02-24 | 02-07 | Information disclosure | screenshots | low | accept | See the accepted risks log (AR-02-03) | closed (accepted) |
| T-02-25 | 02-07 | Tampering | tuning changes | medium | mitigate | The layout version went 6→7 (`a8bc6eb`) and 7→8 (`1f74fd3`) with goldens re-recorded each time, and the guard is enforced by a test. PR #7 and HEAD passed build-test and lint. One deviation: observation 3 | closed |
| T-02-26 (02-08) | 02-08 | Elevation of privilege | release publication | high | mitigate | `.github/workflows/release.yml:168` puts `publish` behind `environment: deploy`. `build/check-github-settings.sh` passed all 13 checks live (the environment needs a reviewer and accepts only `v*.*.*` tags). The v0.2.0 run has one approval record from the repository-owner account. That a human clicked is a SUMMARY claim | closed |
| T-02-27 (02-08) | 02-08 | Tampering | artefact in transit | high | mitigate | Container: `deploy/bin/cabinet-deploy:183-196` checks the checksum, the attestation (pinned signer workflow and tag ref, self-hosted runners denied, `deploy/lib/deploy.sh:49-82`) and that the commit is on main, all before unpacking. CI: `release.yml:195-213` re-verifies before un-drafting. The release is immutable. `build/verify-published-release.sh v0.2.0` was re-run during this audit and all 13 checks passed, including rejection of a tampered copy. Deviation: observation 1 | closed |
| T-02-28 (02-08) | 02-08 | Spoofing | merge and tag identity | medium | mitigate | `v0.2.0` is an annotated tag, and the tagger uses a noreply identity: yes. It points at the PR #7 merge commit on `origin/main`. Every commit from `v0.1.4` to HEAD has noreply author and committer emails. Merge-commit display name: observation 6 | closed |
| T-02-29 (02-08) | 02-08 | Information disclosure | evidence in SUMMARY | medium | mitigate | 02-08-SUMMARY holds no non-documentation addresses, real domains, handles, emails, local paths or denylist matches, and uses `<owner>/<repo>` and `ssh <container>` placeholders. 02-UAT (`2d6804d`) passes the same checks | closed |
| T-02-30 | 02-08 | Information disclosure | prototype on the deployed site | low | accept | See the accepted risks log (AR-02-04) | closed (accepted) |
| T-02-26 (02-09) | 02-09 | Denial of service | families with many thick expansions | low | mitigate | `CabinetLayoutEngine.cs:115` computes the room left; line 147 caps uprights at `Orientation.MaxUprightExpansions` (2, `Orientation.cs:50`) and requires `width <= roomLeft`. One pass (139-159); the rest go to the stack. Tests: `FamilyLayoutTests.cs:506-570` | closed |
| T-02-27 (02-09) | 02-09 | Denial of service | lie-flat fallback | low | mitigate | `CabinetLayoutEngine.cs:67,235-248`: at most one extra integer-only first-fit scan per eligible game. The guarded throw at 71-75 remains | closed |
| T-02-28 (02-09) | 02-09 | Tampering | `Layout:LieFlatBeforeNewSection` value | low | mitigate | `LayoutSettings.cs:37-52` accepts only true or false and otherwise throws an error naming the key (`LayoutSettingsTests.cs:98-131`). The value is in `LayoutOptions.Pack`, so it is part of `Fingerprint` and the ETag (`LayoutCache.cs:40`; `LayoutSettingsTests.cs:133-149`) | closed |
| T-02-29 (02-09) | 02-09 | Tampering | expansion titles on upright spines | medium | mitigate | The upright name comes from `copy.layerName` (`render.js:53-54,79-80`) and the sub-line from `copy.expansionFor` (`render.js:97-105`), written with `textContent` or attributes only | closed |
| T-02-SC | 02-03, 02-07, 02-09 | Tampering | npm install of playwright (scratch only) | high | mitigate | No `package.json`, npm lock file, `node_modules` or playwright reference anywhere in the repository, its full history, the project lock files or the workflows. The research audit marks the package OK with `postinstall` null. The scratch installs outside the repository pin playwright and playwright-core to exactly 1.63.0. Note: observation 7 | closed |

*Status: open · closed · open — below high threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above workflow.security_block_on count toward threats_open*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

These four risks were declared `accept` in the phase's plan threat models. This audit confirmed that each rationale still holds against the current code and configuration. The owner has not re-confirmed them in this run (see Open Items). All four are low severity, below the `high` block threshold, so they do not affect `threats_open` either way.

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-02-01 | T-02-03 | The layout JSON is an undocumented, UI-only endpoint that carries layout fields of invented games. `/cabinet/layout` appears nowhere in README, `docs/` or `deploy/`. There is no CORS code, and `LayoutEndpointTests.cs:117` asserts no `Access-Control-Allow-Origin`. LAN-only exposure is backed by the proxy example's ip allowlist (`deploy/traefik/cabinet.yml.example:23-25,49`) and the default-drop firewall that admits the web port only from the proxy (`deploy/nftables/cabinet.nft.in:15,25`) | Plan threat model (02-01); owner confirmation pending | 2026-10-06 |
| AR-02-02 | T-02-10 | `--bg`/`--fg` come from the fixed constant table `Cabinet.Domain/Layout/SpinePalette.cs:33-47` and are applied with CSSOM `setProperty` (`render.js:149-150`). Carry-forward: art-derived colours must be validated as hex before they reach `--bg`/`--fg` | Plan threat model (02-03); owner confirmation pending | 2026-10-06 |
| AR-02-03 | T-02-24 | Review screenshots show invented data only and live in the session scratch area outside the repository. No image file is tracked now or was ever added | Plan threat model (02-07); owner confirmation pending | 2026-10-06 |
| AR-02-04 | T-02-30 | The prototype route stays LAN and VPN only until the public-exposure work (per the repository's proxy example and firewall; the live proxy configuration cannot be checked from the repository). It shows invented data and is turned off by one setting, and the removal list is recorded in 02-08-SUMMARY. Carry-forward: committed `appsettings.json:26-28` ships `Prototype:Enabled=true`, and the pages have no `noindex` (review IN-05). Both must be handled before public exposure | Plan threat model (02-08); owner confirmation pending | 2026-10-06 |

*Accepted risks do not resurface in future audit runs.*

---

## Unregistered Flags

| Flag | Category | Severity | Evidence | Proposed fix |
|------|----------|----------|----------|--------------|
| Workstation scratch paths in committed planning docs | Information disclosure | low (non-blocking) | `02-05-SUMMARY.md:97`, `02-06-SUMMARY.md:103`, `02-09-SUMMARY.md:112`, `02-09-PLAN.md:148` contain a session temp path. It shows the local user ID and an encoded local repository location, but no login name, hostname, address or denylist match. This drifts from T-02-32's "the SUMMARY lists checks, never paths" and makes 02-07-SUMMARY's "no scratch path appears in the repository" inaccurate if read literally | Replace those paths with a `<scratch>/…` placeholder |

---

## Observations (non-blocking)

1. **T-02-27 (02-08), process deviation.** The planned "draft verified on the workstation before approval" step did not happen: the owner approved the deploy environment while the run was still in progress. The threat stays closed because the publish job re-verifies the attestation before publishing, the container verifies again before installing, the release is immutable, and a re-run against the published bytes passed. For the next release, either verify the draft before approving, or reword the mitigation to rely on the publish job's own check.
2. **T-02-23, scope of the approval.** The owner's checkpoint "approve" covered layout version 7 (`13d6b24`). Four review fixes landed afterwards, including WR-04, which moved 274 of 392 games in the 400 sample and raised the layout version to 8. The owner's re-confirmation of version 8 appears only as a statement in 02-VERIFICATION, with no checkpoint record. Residual risk: this control is procedural only. Nothing technical stops an agent session that holds the owner's GitHub login from merging, pushing a tag or approving the deploy environment, because the project's Claude Code settings have no deny or ask rules for these actions.
3. **T-02-22 / T-02-25, the golden guard can be bypassed.** Commit `725f00c` re-recorded goldens with a changed digest while the layout version stayed at 7 (its parent `a8bc6eb` had just raised it from 6 to 7). Both commits were in the same unreleased review round, and `v0.2.0` is the first release with the endpoint, so nothing shipped with a stale ETag. Cause: `Cabinet.UnitTests/Layout/LayoutGoldenTests.cs:133-171` (`PrepareGoldens`) refuses to re-record only when `layout-version.txt` exists, so deleting that file bypasses the check. Proposed fix: also refuse when goldens exist but the version file is missing, and/or adopt review IN-01 (derive the ETag from a hash of the serialised body).
4. **T-02-02 / T-02-09 / T-02-18 / T-02-29, no CI enforcement.** The ban on markup-string APIs was a plan-time acceptance grep; nothing in `build/lint/checks/*.sh` enforces it. The code is clean today and the CSP backs it up. Proposed fix: a lint check that fails on `innerHTML`, `outerHTML`, `insertAdjacent*` and `document.write` under `Cabinet.Service/wwwroot/js`.
5. **T-02-06, unknown keys are ignored.** Only values are validated; a misspelled key under `Layout:` or `Prototype:` is silently ignored and the default is used. This is within the declared mitigation, but the threat's wording ("a typo cannot silently produce an odd layout") is only partly met. Proposed fix: reject unknown child keys of the `Layout` and `Prototype` sections at startup.
6. **T-02-28 (02-08), merge-commit display name.** The PR #7 merge commit's author display name is the GitHub profile display name, not the configured git name; the email is noreply. It matches no denylist entry, earlier release merges look the same, and the owner accepted it (recorded in 02-08-SUMMARY).
7. **T-02-SC, version prefix in scratch manifests.** Two scratch `package.json` files declare `^1.63.0` (npm's default prefix), while their lock files pin 1.63.0. `npm ci` keeps the pin; a lock-less `npm install` could pick up a newer version. Use `--save-exact` next time.

---

## Open Items (owner decisions)

These are not blocking (`threats_open: 0`), but they need the owner's decision and were not made during this audit:

1. Confirm (or reject) the four plan-time accepted risks AR-02-01 to AR-02-04. If any is rejected, it becomes an open, non-blocking low-severity threat that needs a mitigation.
2. Record the owner's confirmation of layout version 8 after the review fixes, in UAT or here (observation 2).
3. Decide whether to add Claude Code permission rules (ask or deny) for merging pull requests, pushing `v*` tags and approving the deploy environment, so the human gates are enforced technically and not only by process (observation 2).
4. Choose how the next release handles the pre-approval draft check: perform it before approving, or reword the mitigation (observation 1).
5. Before public exposure: turn off `Prototype:Enabled` in committed configuration (or in the server env) and add `noindex` while the prototype is live (AR-02-04).
6. Approve the follow-up changes proposed in the Unregistered Flags and in observations 3, 4 and 5. None was applied during this audit.

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-10-06 | 37 | 37 (33 mitigated, 4 accepted) | 0 | gsd-security-auditor (ASVS L2, block_on high) |

## Security Audit 2026-10-06
| Metric | Count |
|--------|-------|
| Threats found | 37 |
| Closed | 37 |
| Open | 0 |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-10-06 (owner confirmation of the accepted risks pending; see Open Items)
