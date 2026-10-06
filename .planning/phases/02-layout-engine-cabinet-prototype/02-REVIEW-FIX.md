---
phase: 02-layout-engine-cabinet-prototype
fixed_at: 2026-10-06T08:53:39Z
review_path: .planning/phases/02-layout-engine-cabinet-prototype/02-REVIEW.md
iteration: 1
findings_in_scope: 4
fixed: 4
skipped: 0
status: all_fixed
---

# Phase 02: Code Review Fix Report

**Fixed at:** 2026-10-06T08:53:39Z
**Source review:** .planning/phases/02-layout-engine-cabinet-prototype/02-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 4 (WR-01 to WR-04; owner chose to fix all four before merging into main)
- Fixed: 4
- Skipped: 0

**Verification (run in the main checkout on `milestone/v1-games-cabinet`, no worktree, after the last fix commit):** `dotnet build Cabinet.slnx` (0 warnings, 0 errors), `dotnet test --solution Cabinet.slnx` (414 passed, 0 failed, 0 skipped: 369 unit, 45 integration; the known intermittent SocketException in `CabinetPageTests` did not appear, so no re-run was needed) and `bash build/lint.sh` (repo-rules, workflows, shell, secrets and script-tests all pass). A search of every changed file outside `.planning/` finds no review IDs or planning references. All four commits use the repository's noreply identity.

WR-03 and WR-04 change engine behaviour and are marked "requires human verification": WR-03 adds a guard that no test can trigger (see below), and WR-04 re-records layouts whose visual effect is worth one look on the page.

## Fixed Issues

### WR-01: The layout endpoint and the page regenerate the whole sample collection on every request

**Files modified:** `Cabinet.Service/Prototype/SampleCatalog.cs`, `Cabinet.Service/Layout/LayoutCache.cs`, `Cabinet.Service/Layout/LayoutEndpoint.cs`, `Cabinet.UnitTests/Prototype/SampleGenerationTests.cs` (new)
**Commit:** 3a7cd00
**Applied fix:** The endpoint validates with `catalog.IsKnown(sample)` and `SectionDesigns.TryGet(profile)` only; the dead `TryGetSample(..., out var items)` call is gone. `SampleCatalog` now generates each allowlisted sample at most once per process (`ItemsOf(name)`, a `ConcurrentDictionary` of `Lazy` entries keyed only by allowlisted names, so it stays bounded), and `ItemCount` reads from it, so the page no longer regenerates the sample per view. `LayoutCache` takes the catalog and asks it for items only inside `Build`, i.e. on a cache miss. The catalog has a second constructor that takes the generating function, which is the counting seam for tests; production uses the invented collections. The endpoint body moved into a public `LayoutEndpoint.Handle` (mapped as a method group, same query binding) so it can be driven directly. Tests: the catalog generates a sample once however often it is asked; an unknown name resolves to the default and is never generated; three page views of the 400 sample generate it once; requests outside the allowlists answer 404 without generating anything; a first request, a repeat, a 304 revalidation and the other profile together generate the sample exactly once.
**Note:** the seam observes generation through the catalog. A future change that calls `SyntheticCollections` directly from the endpoint would bypass the seam and not be caught; the catalog is now the only caller in the service project.

### WR-02: A strict CSP is relied on and tested for, but no Content-Security-Policy header is sent

**Files modified:** `Cabinet.Service/Hosting/ContentSecurityPolicy.cs` (new), `Cabinet.Service/Program.cs`, `Cabinet.IntegrationTests/ContentSecurityPolicyTests.cs` (new), `deploy/traefik/cabinet.yml.example`, `docs/lxc-setup.md`
**Commit:** d7a07e6
**Applied fix:** A middleware registered first in the pipeline sets `Content-Security-Policy: default-src 'self'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'; object-src 'none'` on every response (public listener, and harmlessly the loopback ops listener too). No `unsafe-inline`, no `unsafe-eval`, no other origins. The Traefik example and the setup guide state that the app owns the policy and the proxy deliberately sets none, so two policies are never combined. Integration tests: the exact policy is present once on `/`, `/?sample=400`, both layout profiles, a static module, an unknown-sample 404, an unknown path 404, a 304 revalidation and the prototype-off page; the policy contains no `unsafe-inline`, `unsafe-eval`, `*`, `data:` or `http`.
**Browser check (Playwright 1.63.0, Chromium):** the app was run from the repository build output on `127.0.0.1:6180` (public) and `127.0.0.1:6181` (ops) in Development, and `/?sample=65` was loaded at 1440 x 900 and 390 x 900. Both responses carried the policy; 1440 drew 2 sections and 64 placements, 390 drew 3 sections and 62 placements, every placement had its geometry applied through `style.setProperty`, and there were **zero console errors or warnings, zero page errors and zero `securitypolicyviolation` events**. A screenshot at 390 showed the cabinet drawn normally. Negative control on the same page: injecting an inline `<script>` and a `style` attribute produced `script-src-elem` and `style-src-attr` violations and the script did not run, so the listener and the policy are live. The app was stopped afterwards and both ports were free.

### WR-03: A failed re-arrangement is silently turned into an empty cubby

**Files modified:** `Cabinet.Domain/Layout/CabinetLayoutEngine.cs`, `Cabinet.Service/Layout/LayoutCache.cs`
**Commit:** ab98c71
**Status:** fixed: requires human verification
**Applied fix:** `ToLayoutSection` now throws `InvalidOperationException("Cubby {index} of section {n} of the '{design}' design accepted its games during placement but cannot arrange them.")` instead of substituting `[]`. `Build`'s exception documentation lists the new case, `ToLayoutSection` has a summary explaining the invariant, and the `LayoutCache` summary says a failed build is remembered until restart (the `Lazy` default mode caches the exception).
**Why no new test:** there is no seam that makes the two calls differ. Placement and drawing call the same pure `CubbyArrangement.TryArrange` with the same member set and the same order salt, and the arrangement is documented and tested to be order-independent; `Cabinet.Domain` exposes no internals to the test project. Adding an injection point to the engine only to fail it was judged worse than the guard alone. The existing `LayoutAssertions.AssertValid` (every item placed exactly once) runs over the recorded samples and many seeded random collections on both designs and stays green, and the golden layouts were unchanged by this commit.

### WR-04: A base game that has expansions is scaled by the cover-width limit even when it stands as a spine

**Files modified:** `Cabinet.Domain/Layout/CabinetLayoutEngine.cs`, `Cabinet.UnitTests/Layout/SectionDesignTests.cs`, `Cabinet.UnitTests/Layout/PhoneProfileTests.cs`, `Cabinet.UnitTests/Layout/Golden/*` (13 files), `docs/cabinet-layout.md`
**Commit:** 1f74fd3
**Status:** fixed: requires human verification
**Applied fix:** `ToMember` applies the family width limit only when the base game's pose is `Cover` (`facesOutBesideExpansions = expansions.Count > 0 && pose == BoxPose.Cover`); a spine-posed family base is clamped by the general box limits only. `Clamp`'s parameter was renamed to match and its summary explains why. `LayoutVersion` raised from 7 to 8 and the goldens re-recorded with `CABINET_UPDATE_GOLDENS=1`. The layout guide gains two sentences on the rule. New tests on both designs: a 300 x 400 x 70 mm base standing as a spine is drawn 400 mm tall alone and with three expansions, at the same spine width (they failed before the fix with 360 mm on desktop and 306 mm on phone); the same box facing out with expansions is still scaled to the family width limit.
**Existing test adjusted:** `PhoneProfileTests.The_cabinet_grows_with_the_collection_on_both_profiles_and_the_phone_needs_more_sections` asserted that every phone section, including the last, is drawn with all 14 cubbies. That held only by accident: after the fix the 65 sample's last phone section is used down to its second row only and is drawn short, as the trimming rules allow. The assertion now requires all cubbies on every section but the last, and 1 to 14 on the last.

**Golden diff summary** (old and new engines compared placement by placement over every sample on both designs):

| Sample | Desktop | Phone |
|--------|---------|-------|
| 0, 1, 5, 12, edge | only `layoutVersion` 7 -> 8 | only `layoutVersion` 7 -> 8 |
| 65 | 1 placement: family base 10208 spine 290 -> 350 mm tall, label now unshortened; nothing else moves | family base 10208 spine 247 -> 350 mm, so its family moves from section 0 cubby 13 to section 1 cubby 5 (now shows one more layer, 10498, and the marker sits higher); 21 of 62 games change place, 2 swap between lying flat and facing out (10245 cover -> flat, 11156 flat -> cover); still 3 sections, the last now drawn short (7 cubbies, 1100 mm instead of 14, 2060 mm) |
| 400 (digest) | 5 family-base spines taller (for example 197 -> 336, 281 -> 386 mm); 274 of 392 games change place; an eighth section opens (drawn short, 10 cubbies); 21 games change pose, mostly flat -> standing because the new section gives them room | 9 family-base spines taller (for example 168 -> 306, 239 -> 386 mm); 163 of 387 games change place; still 12 sections; 5 games change pose |

Every change traces back to a taller family-base spine: the direct height changes, then the documented cascade (a family that grows taller may move to a taller cubby and later cubbies shift, which can change whether a later game stands or lies flat). No other placement rule changed.

---

_Fixed: 2026-10-06T08:53:39Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
