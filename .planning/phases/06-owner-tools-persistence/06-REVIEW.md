---
phase: 06-owner-tools-persistence
reviewed: 2026-10-13T00:00:00Z
depth: standard
files_reviewed: 99
files_reviewed_list:
  - build/check-owner-routes.sh
  - build/tests/check-owner-routes-test.sh
  - build/tests/owner-css.test.mjs
  - build/tests/owner-format.test.mjs
  - build/tests/owner-link.test.mjs
  - build/tests/owner-restore.test.mjs
  - build/tests/owner-shared.test.mjs
  - build/tests/page-scripts.test.mjs
  - Cabinet.Domain/Cards/CardRecords.cs
  - Cabinet.Domain/Collection/ArtChoice.cs
  - Cabinet.Domain/Collection/ArtVerdictKind.cs
  - Cabinet.Domain/Collection/BoxShape.cs
  - Cabinet.Domain/Collection/CollectionSnapshot.cs
  - Cabinet.Domain/Collection/EnrichmentPlanner.cs
  - Cabinet.Domain/Collection/SnapshotMapper.cs
  - Cabinet.Domain/Owner/Finishes.cs
  - Cabinet.Domain/Owner/LinkHealth.cs
  - Cabinet.Domain/Owner/LinkPreview.cs
  - Cabinet.Domain/Owner/LocationEdits.cs
  - Cabinet.Domain/Owner/OwnerData.cs
  - Cabinet.Domain/Owner/OwnerOverlay.cs
  - Cabinet.Domain/Owner/PictureEdits.cs
  - Cabinet.Domain/Owner/PictureRows.cs
  - Cabinet.Domain/Owner/WorthALook.cs
  - Cabinet.Repository/Images/ArtProcessor.cs
  - Cabinet.Repository/Images/ImageDownloader.cs
  - Cabinet.Repository/Storage/OwnerDataExport.cs
  - Cabinet.Repository/Storage/OwnerDataStore.cs
  - Cabinet.Repository/Storage/SnapshotStore.cs
  - Cabinet.Service/appsettings.Development.json
  - Cabinet.Service/Collection/CollectionStore.cs
  - Cabinet.Service/Hosting/ForwardedHeadersSetup.cs
  - Cabinet.Service/Owner/CollectionPublisher.cs
  - Cabinet.Service/Owner/OwnerAccess.cs
  - Cabinet.Service/Owner/OwnerChangeNotifier.cs
  - Cabinet.Service/Owner/OwnerDataService.cs
  - Cabinet.Service/Owner/OwnerEndpoints.cs
  - Cabinet.Service/Owner/OwnerLinkFetcher.cs
  - Cabinet.Service/Owner/OwnerLinkWorker.cs
  - Cabinet.Service/Owner/OwnerResponses.cs
  - Cabinet.Service/Owner/OwnerStrings.cs
  - Cabinet.Service/Owner/SiteFinish.cs
  - Cabinet.Service/Owner/StagedItems.cs
  - Cabinet.Service/Pages/Index.cshtml.cs
  - Cabinet.Service/Pages/Owner/LinkPreviewModel.cs
  - Cabinet.Service/Pages/Owner/LocationItemModel.cs
  - Cabinet.Service/Pages/Owner/LocationPickerModel.cs
  - Cabinet.Service/Pages/Owner/Locations.cshtml.cs
  - Cabinet.Service/Pages/Owner/OwnerBarItem.cs
  - Cabinet.Service/Pages/Owner/OwnerPage.cs
  - Cabinet.Service/Pages/Owner/Pictures.cshtml.cs
  - Cabinet.Service/Pages/Owner/Settings.cshtml.cs
  - Cabinet.Service/Program.cs
  - Cabinet.Service/Review/ReviewSheetCommand.cs
  - Cabinet.Service/Review/ReviewSheet.cs
  - Cabinet.Service/Review/ReviewSheetModel.cs
  - Cabinet.Service/Sync/ArtRecords.cs
  - Cabinet.Service/Sync/ArtSync.cs
  - Cabinet.Service/Sync/EnrichmentSync.cs
  - Cabinet.Service/Sync/SyncEndpoints.cs
  - Cabinet.Service/Sync/SyncRunner.cs
  - Cabinet.Service/Sync/SyncStartup.cs
  - deploy/bin/cabinet-selfcheck
  - deploy/cabinet.env.example
  - deploy/nftables/cabinet.nft.in
  - deploy/provision.d/50-firewall.sh
  - deploy/tests/render-templates-test.sh
  - deploy/tests/sandboxing-test.sh
  - deploy/traefik/cabinet.yml.example
  - .gitleaks.toml
  - Cabinet.Service/Pages/Index.cshtml
  - Cabinet.Service/Pages/Owner/Locations.cshtml
  - Cabinet.Service/Pages/Owner/Pictures.cshtml
  - Cabinet.Service/Pages/Owner/Settings.cshtml
  - Cabinet.Service/Pages/Owner/_LinkPreview.cshtml
  - Cabinet.Service/Pages/Owner/_LocationItem.cshtml
  - Cabinet.Service/Pages/Owner/_LocationPicker.cshtml
  - Cabinet.Service/Pages/Owner/_ViewStart.cshtml
  - Cabinet.Service/Pages/Shared/_Footer.cshtml
  - Cabinet.Service/Pages/Shared/_Layout.cshtml
  - Cabinet.Service/Pages/Shared/_OwnerBanner.cshtml
  - Cabinet.Service/Pages/Shared/_OwnerLayout.cshtml
  - Cabinet.Service/Pages/Shared/_OwnerRestorePanel.cshtml
  - Cabinet.Service/Pages/Shared/_OwnerUnreadable.cshtml
  - Cabinet.Service/wwwroot/css/owner.css
  - Cabinet.Service/wwwroot/js/cabinet.js
  - Cabinet.Service/wwwroot/js/card-view.js
  - Cabinet.Service/wwwroot/js/copy.js
  - Cabinet.Service/wwwroot/js/detail.js
  - Cabinet.Service/wwwroot/js/owner-cabinet.js
  - Cabinet.Service/wwwroot/js/owner-finish.js
  - Cabinet.Service/wwwroot/js/owner-format.js
  - Cabinet.Service/wwwroot/js/owner-games.js
  - Cabinet.Service/wwwroot/js/owner-link.js
  - Cabinet.Service/wwwroot/js/owner-locations.js
  - Cabinet.Service/wwwroot/js/owner-pictures.js
  - Cabinet.Service/wwwroot/js/owner-placing.js
  - Cabinet.Service/wwwroot/js/owner-settings.js
  - Cabinet.Service/wwwroot/js/owner-shared.js
findings:
  critical: 1
  warning: 12
  info: 14
  total: 27
status: issues_found
---
# Phase 6 code review

The review ran as two standard-depth halves in parallel and is merged here: the server side (C#, scripts, deploy and config; 71 files) and the front end (Razor views, page scripts, CSS; 29 files). Test projects were left out of scope; the full suites run green. Front-end finding IDs continue after the server ones (front-end warnings are WR-08 to WR-12, front-end info IN-07 to IN-14).

## Server side


# Phase 6: Code Review Report (server side)

**Reviewed:** 2026-10-13
**Depth:** standard
**Files Reviewed:** 71
**Status:** issues_found

### Summary

This is the server-side half of a split review. The Razor views, page scripts and CSS were left to the second reviewer.

I traced the owner port and its two walls, owner-data persistence, the restore path, the linked-picture fetcher and worker, the sync/owner write coordination, the deploy scripts and the gitleaks config. I found no blocker.

**What holds up**
- **Walls.** Both walls key off `Connection.LocalPort` and the forwarded-headers result, never the Host header. The path guard, the endpoint marker and the client-range wall are independent and ordered correctly in `Program.cs`. Range parsing refuses `/0`, mapped and shorthand forms.
- **Restore input.** A restored file is size-capped (2 MB), depth-capped (16), shape-checked whole and link-policy-checked. Its fetched records are stripped, so no art file name from an upload can reach the art directory.
- **Atomic writes.** Writes go through temp file plus rename. An unreadable or newer-format file is never overwritten except by an explicit restore, which keeps a copy first.
- **Writer discipline.** Owner writes are serialised by one gate with a revision check and a single undo slot. System writes clear the undo slot.
- **Linked pictures.** The fetcher goes through the token-less `ImageDownloader`: https, default port, no user-info, allowlisted host, every redirect hop re-checked, byte cap enforced on the decompressed stream, and a 15 s overall limit on the injected clock. No address is logged.

**Where the defects are**
- **Wall two** has a fail-open path.
- **Restore** accepts a revision that makes the owner tools unusable.
- **Startup reconcile** treats a stale stored collection as an accepted one, which can forget hand-entered data.
- **Link worker** has a retry loop on one failure mode.
- **Deploy checks** do not prove the firewall wall.
- **Gitleaks** has one allowlist that is wider than its stated purpose.

### Warnings

### WR-01: Client-range wall passes the proxy's own address when no forwarded address was applied

**File:** `Cabinet.Service/Owner/OwnerAccess.cs:109-125` (with `Cabinet.Service/Hosting/ForwardedHeadersSetup.cs:313-331`)

**Issue:** Wall two judges `context.Connection.RemoteIpAddress`. When the peer is a known proxy but sends no usable `X-Forwarded-For`, the forwarded-headers middleware leaves `RemoteIpAddress` as the proxy's address.

The shipped example puts the proxy inside the owner range: `ReverseProxy__KnownProxies__0=192.0.2.10` and `Owner__AllowedRanges__0=192.0.2.0/24`. A home LAN normally holds Traefik inside the allowed LAN range, so the proxy address passes the wall. The start-up check only requires a non-empty `KnownProxies` list. It does not guarantee the proxy cannot pass for the client.

Traefik always appends XFF, so the normal path is safe. The wall is therefore fail-open exactly in the abnormal case it is meant to cover: a header dropped by a middleware change, an `X-Forwarded-For` value that does not parse, or a direct call from the Traefik host.

Separately, `ForwardedHeadersOptions` keeps its default `KnownNetworks` (127.0.0.0/8) and `KnownProxies` (::1). Loopback is therefore also a trusted forwarder. This is what makes the `cabinet-selfcheck` probe work. It also contradicts the setup's doc comment, "believed only when it comes from a configured known proxy", and lets any local process claim an in-range client.

**Fix:** Refuse when the connection peer is a known proxy and the forwarded header was not applied. The middleware leaves the original in `X-Original-For` only when it rewrote the address. For example:

```csharp
var forwarded = context.Request.Headers.ContainsKey("X-Original-For");
var allowed = forwarded && options.Allows(context.Connection.RemoteIpAddress);
if (options.IsOwnerPort(context) && !allowed) { /* 403 */ }
```

Alternatively, reject any range that contains a configured known proxy at start-up. In `ForwardedHeadersSetup.Configure`, call `options.KnownNetworks.Clear()` and `options.KnownProxies.Clear()` before adding the configured proxies, so the stated rule is true.

### WR-02: A restored file's revision is unbounded, so a hand-edited file can lock the owner out

**File:** `Cabinet.Repository/Storage/OwnerDataExport.cs:221-226` and `Cabinet.Service/Owner/OwnerDataService.cs:482`

**Issue:** `WithinLimits` only rejects `Revision < 0` and `ChangesSinceDownload < 0`.

- A file with `"revision": 9223372036854775807` is accepted. `Restored` computes `Math.Max(document.Revision, currentRevision) + 1`, which wraps to `long.MinValue` in unchecked arithmetic.
- A revision above 2^53 is also serialised as a JSON number that JavaScript cannot represent exactly. Every script-driven post then carries a rounded revision, so every write answers out-of-date until another restore.
- `ChangesSinceDownload = int.MaxValue` wraps negative on the next save and hides the backup reminder.

The restore is the one path that takes numbers from an untrusted document and stores them as the writer's own counters.

**Fix:** Bound both fields in `WithinLimits`, for example `data.Revision > int.MaxValue` and `data.ChangesSinceDownload > OwnerData.MaxStoredChanges * 1000` are refused. Use `checked(...)` or saturate in `Restored` and `Save`.

### WR-03: Start-up reconcile treats a stale stored collection as an accepted one and can forget owner decisions

**File:** `Cabinet.Service/Sync/SyncStartup.cs:53`, `Cabinet.Service/Sync/SyncRunner.cs:366`, `Cabinet.Service/Owner/CollectionPublisher.cs:60`

**Issue:** `Publish` always runs `ReconcileAsync`, which stamps games missing from the collection and deletes any whose stamp is 30 days old. It is documented as running "after an accepted collection". Two callers pass something else.

- `SyncStartup` publishes whatever `snapshot.json` holds at boot.
- `SyncRunner.StoredCollectionStillUnreadable` publishes the reloaded file.

If the server was off for a long time, the stored collection lists none of the games the owner re-added on BGG meanwhile. A game stamped absent more than 30 days ago is forgotten at boot, before the first sync can list it again. The loss covers its location, its picture choice and its linked picture. Elapsed wall-clock time, including server downtime, counts toward the 30 days.

The rule needs two consecutive observations with the game missing. Only a freshly fetched collection supplies the second one.

**Fix:** Split the call. `Publish(snapshot, reconcile: false)` for the boot and reload paths, and `reconcile: true` only from `SyncRunner.Commit` after a fetched, guard-accepted collection. The result is that expiry is judged only against a collection BGG just returned.

### WR-04: A failed cache write makes the link worker refetch the picture on every wake

**File:** `Cabinet.Service/Owner/OwnerLinkWorker.cs:519-531` (with `IsDue`, lines 481-500)

**Issue:** When `cache.Write` throws `IOException` or `UnauthorizedAccessException` (disk full, permissions), `FetchOneAsync` returns false. It does not set `_lastChecked`, does not call `LinkHealth.AfterFailure`, and nothing is stored.

The link then has `FailedChecks == 0` and no usable copy, so `IsDue` is true on every pass. A pass runs hourly, after every owner change, and on every `RequestLinkWork`. Each pass re-downloads the picture from the image host and re-processes it. The result is repeated outbound fetches and image decoding for the lifetime of the fault.

**Fix:** Record the attempt before the write: set `_lastChecked[gameId] = attemptedAt` before the write try block, or treat a store failure as a failed check through `MutateSystemAsync`. A failed store should wait its retry interval like any other failed fetch.

### WR-05: "confirm-needed" has no words and is not a known refusal key

**File:** `Cabinet.Service/Pages/Owner/Locations.cshtml.cs:229`, `Cabinet.Service/Owner/OwnerResponses.cs:136-154`, `Cabinet.Service/Owner/OwnerStrings.cs:769-798`

**Issue:** `OnPostDeleteAsync` refuses with `OwnerChange.Refused(data, "confirm-needed")` when games would be left unplaced. This happens on the script path and on the form race where games arrive between the redirect check and the write.

- `OwnerStrings.Message("confirm-needed")` returns null, so the JSON answer carries `message: null`.
- The key is absent from `RefusalKeys`, so `DoneCode` maps it to `refused-save-failed` and the page tells the owner "Couldn't save that. Try again." The real cause is that the owner needs to confirm.

**Fix:** Add `"confirm-needed"` to `RefusalKeys` and give it a message in `OwnerStrings.Message`, for example "This location still holds games. Confirm the delete first."

### WR-06: gitleaks allowlist for `.md`/`.cshtml` also lets a real address with a capitalised domain through

**File:** `.gitleaks.toml:404-411`

**Issue:** The allowlist exists for Razor `text-@Model.Member`. It is applied with `regexTarget = "match"` and the pattern `@[A-Za-z][A-Za-z0-9]*\.[A-Z][A-Za-z0-9]*[a-z][A-Za-z0-9]*$`. The pattern is unanchored at the start, so the local part is irrelevant. Any two-label address whose last label starts with a capital passes in any `.md` or `.cshtml` file, for example `Firstname@Gmail.Com`, `Name@Provider.Nl`, `x@Corp.Eu`.

Pasted real addresses are often capitalised that way, so this is a real hole for a public repository. The other email allowlists check out: the placeholder domains, noreply, reserved TLDs, systemd unit suffixes and the geekdo host are all `$`-anchored on a whole domain or a non-delegated suffix.

**Fix:** Require the shape the allowlist is for, namely a local part that ends in a hyphen or other markup glue:

```toml
regexes = ['''^[a-z-]*-@[A-Z][A-Za-z0-9]*\.[A-Z][A-Za-z0-9]*$''']
```

If that is too tight for the real views, keep the allowlist but restrict `paths` to `^Cabinet\.Service/(Pages|Views)/.*\.cshtml$` and drop `md`.

### WR-07: Deploy self-check never verifies the firewall half of the owner wall

**File:** `deploy/bin/cabinet-selfcheck:132-135` and `deploy/bin/cabinet-selfcheck:159-164`

**Issue:** When port 5082 listens, the check passes with "the owner port 5082 is listening". `check_firewall` still only asserts that the input chain defaults to drop. Nothing confirms that 5082 is admitted only from the proxy address.

If the rendered nftables file drifts or the rule is edited by hand, the self-check stays green while the owner port, bound to `0.0.0.0` per the env example, is open to the whole LAN. The remaining wall then is only the client-range check. The loopback probe also cannot see this, because it never leaves the host.

**Fix:** In `check_firewall`, when `listener_addresses "$sockets" 5082` is non-empty, require a rule `tcp dport 5082 ip saddr <addr> accept` in the ruleset. Fail if it is missing or has no `saddr` restriction. Add the matching case to `sandboxing-test.sh`.

### Info

### IN-01: Owner-data loader checks null elements only one level deep

**File:** `Cabinet.Repository/Storage/OwnerDataStore.cs:200-203`

**Issue:** `HasNullMember` checks the elements of `Locations`, `Games` and `Changes`. A null element inside a stored `ImageRecord.Files` list passes the load. It would then raise `NullReferenceException` in `OwnerLinkWorker.HasUsableCopy` (caught every pass, so link work stops) and in `OwnerDataService.ReferencedArtFiles`. The latter is called from the sync's art clean-up, which catches only IO exceptions, so the run would fail.

This needs a hand-edited or tampered file, since the server writes the file itself. The restore path is unaffected because it strips records.

**Fix:** Treat a null `Files` element as malformed in `HasNullMember`, or make `ReferencedArtFiles` and `HasUsableCopy` skip null entries.

### IN-02: Name check does not refuse invisible format characters

**File:** `Cabinet.Domain/Owner/LocationEdits.cs:109-112`

**Issue:** `IsForbidden` blocks control characters and the bidi embedding and isolate ranges. It does not block LRM/RLM (U+200E/F), ALM (U+061C), zero-width space and joiners (U+200B-D), word joiner (U+2060) or BOM (U+FEFF).

A name can look identical to another and slip past the duplicate check, and a direction mark changes how the card renders for every visitor. This is owner-only input, so the risk is cosmetic.

**Fix:** Refuse `char.GetUnicodeCategory(c) is UnicodeCategory.Format`. This also covers the two bidi ranges already listed.

### IN-03: Restore accepts internally inconsistent picture settings

**File:** `Cabinet.Repository/Storage/OwnerDataExport.cs:275-292`

**Issue:** `PictureIsAcceptable` returns true when `Link` is null, even if `Choice` is `Link`. The restored game then has radio "Your link" selected, no tile behind it, and an automatic pick drawn, because `ForcedPick` returns null. Nothing is stored that can break a write, but the page state is incoherent.

**Fix:** Refuse `Choice == PictureChoice.Link && Link is null` in `PictureIsAcceptable`.

### IN-04: Self-check "forged address" probe depends on a default that is not documented

**File:** `deploy/bin/cabinet-selfcheck:154-172`

**Issue:** The probe sends `X-Forwarded-For` from 127.0.0.1 and expects 403. It works only because `ForwardedHeadersOptions` trusts loopback by default (see WR-01). Nothing in the repo says so. If the default is cleared, as WR-01 recommends, the header is ignored. The request then comes from 127.0.0.1, which is outside the configured ranges, and still answers 403, but it no longer proves the forwarded path.

**Fix:** Keep the check, and reword it to "a request from loopback is refused on the owner port". Say so in the comment, or verify the forwarded path in an integration test that uses a configured known proxy.

### IN-05: Duplicate JSON-accept helper

**File:** `Cabinet.Service/Pages/Owner/Locations.cshtml.cs:228-229`

**Issue:** `LocationsModel.AcceptsJson` duplicates `OwnerResponses.AsksForJson`. The two can drift.

**Fix:** Call `OwnerResponses.AsksForJson(Request)`.

### IN-06: Gitleaks path allowlists skip content anywhere in the tree

**File:** `.gitleaks.toml:436-448`

**Issue:** `(^|/)artifacts/`, `(^|/)TestResults/` and `Cabinet\.[A-Za-z]+/(bin|obj)/` skip every scan of those paths, in any directory, including history. The stated reason is that they are gitignored, but nothing stops a force-added file in a directory with one of those names from reaching the public repository unscanned. The private-ipv4 rule also does not cover 100.64.0.0/10 (common VPN ranges), link-local, or IPv6 unique-local ranges, which the owner's "home network and VPN" setup could use.

**Fix:** Anchor the allowlists to the real locations, for example `^Cabinet\.[A-Za-z]+/(bin|obj)/` and `^artifacts/`. Consider adding a rule for 100.64.0.0/10 and `fd00::/8`.

---

_Reviewed: 2026-10-13_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_


## Front end


# Phase 6: Code Review Report (front end)

**Reviewed:** 2026-10-13
**Depth:** standard
**Files Reviewed:** 29
**Status:** issues_found

### Summary

This is the front-end half of the split review: Razor pages and partials, the owner scripts, the owner stylesheet and the changes to the shared cabinet scripts.

The injection and CSP surface is in good shape.
- No `innerHTML`, `outerHTML`, `insertAdjacentHTML`, `document.write` or `eval` appears anywhere in the reviewed scripts.
- No `style=`, inline `<script>` or event attributes appear in any page.
- All server text goes through Razor encoding or `textContent`.
- Colours are validated against a hex pattern before they reach `style.setProperty`, which is allowed under `default-src 'self'` because it is CSSOM, not an inline attribute.
- Script-built URLs (`/art/...`, `/owner/preview/<hex>`, the Edit link) are validated against strict patterns.
- The pasted picture link is never rendered as an `href` or `src`; it only appears as text and in a `title` attribute.
- Every Razor `<form method="post">` carries an antiforgery field through the form tag helper.
- Every `fetch` POST sends the `RequestVerificationToken` header, matching the `HeaderName` configured in `OwnerEndpoints`.
- The public page renders the owner strip, plate, `data-edit-href` and the owner script only when `IsOwnerAddress` is true, and `_Layout` links `owner.css` only on the owner port.
- All 346 Node tests under `build/tests` pass.

The defects are in the save, undo and settle flows.
- One flow loses the owner's choice while saying `Saved`.
- Every Undo is posted against the run-time revision instead of the revision of the change it belongs to.
- Several handlers are not idempotent under double activation.
- The keepalive flush goes through the single-flight queue and can be delayed past page teardown.
- The owner assets are still fetchable on the public port.

### Critical Issues

### CR-01: Finish picker drops a second choice while a save is in flight and never reverts a failed one

**File:** `Cabinet.Service/wwwroot/js/owner-finish.js:48-73`
**Issue:**
- `save()` returns immediately when `saving` is true. The newest radio is therefore never sent, yet its tile stays checked.
- Arrow keys on a radio group fire a `click` on every step, so two quick key presses (or two quick taps) leave the page showing finish B while the server stored finish A. The row status says `Saved`.
- The failure branch is a no-op, because `checked.checked = true` re-checks the very radio that just failed.
- The failed choice therefore stays selected next to the problem message, while the stored finish is the old one.
- The panel can show one finish and the public cabinet another.

**Fix:** Track the last saved value, loop until the checked value equals it, and put the saved value back on failure.

```js
function initForm(form, region) {
  const fieldset = form.querySelector('fieldset');
  let saving = false;
  let savedValue = fieldset.querySelector('input[type="radio"]:checked')?.value ?? '';

  async function save() {
    if (saving) {
      return;
    }

    saving = true;

    try {
      for (let checked = current(); checked && checked.value !== savedValue; checked = current()) {
        const result = await postOwner(form.getAttribute('action') ?? FINISH_HANDLER, { finish: checked.value });

        if (result.outcome === 'saved' || result.outcome === 'noChange') {
          savedValue = checked.value;
          showRowStatus(region, OWNER_TEXT.saved);
        } else {
          setChecked(fieldset, savedValue);
          showProblem(region, result);

          return;
        }
      }
    } finally {
      saving = false;
    }
  }

  const current = () => fieldset.querySelector('input[type="radio"]:checked');
  ...
}
```

### Warnings

### WR-08: Undo posts the run-time revision, so a queued Undo can revert a different change than the one it was shown for

**File:** `Cabinet.Service/wwwroot/js/owner-shared.js:77` (cause); `owner-locations.js:303`, `owner-placing.js:304`, `owner-pictures.js:292` and `:513`, `owner-link.js:478` and `:535` (callers)
**Issue:**
- `postOwner` fills `revision` from `currentRevision` at the moment the task runs.
- The server refuses an Undo whose revision is not the latest change's revision. That check only protects the owner if the Undo carries the revision of the change it belongs to.
- Every undoer enqueues a task that sends whatever revision is current when the task runs.
- Take this sequence:
  1. Change 1 is saved and its Undo button is visible.
  2. The owner makes change 2 (a picker pick or a move). It is in flight or queued.
  3. The owner presses change 1's Undo before change 2 has returned and removed that button.
- The Undo task runs after change 2, with change 2's revision, and the server reverts change 2.
- The page then applies change 1's `apply()` closure, restoring the picker, name or order from before change 1.
- The server and the page now disagree, and the owner never intended to revert change 2.
- The same hole opens when a status region is replaced while a click is still queued.

**Fix:** Capture the revision the save returned and send it explicitly. `postOwner` already supports `options.revision`.

```js
const saved = getRevision();
showRowStatus(region, OWNER_TEXT.saved, {
  undoLabel: OWNER_TEXT.undo,
  onUndo: hooks.undoer(region, apply, focusAfter, saved),
});
...
const result = await postOwner(HANDLERS.undo, {}, { revision: saved });
```

The server then answers `cant-undo`, which every caller already words correctly.

### WR-09: The keepalive flush on `visibilitychange` goes through the single-flight queue and can start after the page is gone

**File:** `Cabinet.Service/wwwroot/js/owner-shared.js:354-358` with `owner-games.js:54-55`; `owner-pictures.js:417-421` with `:318-319`
**Issue:**
- `flushNow(true)` and `flush(true)` call `commit(..., keepalive)`, and `commit` wraps the post in `hooks.enqueue(...)`.
- The queue runs one request at a time. When any earlier save, undo or move is still awaiting the network, the keepalive request is not issued during the `hidden` event.
- On mobile the page can be frozen or discarded before the earlier request returns, and the last choice is lost.
- The mechanism only works when the queue is idle.

**Fix:** For a keepalive flush, issue the request immediately and outside the queue. Only the revision handling needs care: use the latest known revision, accept that a conflict is possible, and skip the status UI because the page is hidden.

```js
const commit = (value, options = {}) => {
  const task = async () => { ... };

  if (options.keepalive === true) {
    task();
  } else {
    hooks.enqueue(task);
  }
};
```

At minimum, document that the flush can be delayed by an in-flight request.

### WR-10: Double activation produces false errors after a successful save (Undo, Add location, Use this)

**File:** `Cabinet.Service/wwwroot/js/owner-shared.js:231-238`; `owner-locations.js:504-506` with `:598-601`; `owner-link.js:494-503` with `:601-607`
**Issue:**
- Several handlers enqueue work without a "this click was already taken" guard.
- Undo: the button stays enabled while its task is queued or in flight. A double tap enqueues two Undo tasks. The first succeeds and sets `_undo = null` on the server. The second answers `cant-undo`, and `showRowProblem(region, OWNER_TEXT.cantUndo)` replaces the `Undone.` message with an error.
- Add location: `commitAdd` reads `addField.value` when the task runs. A double Enter or double click queues a second task. The first clears the field, so the second posts an empty name and shows `Give the location a name.` after a successful add.
- Use this: `aria-disabled` is set inside the queued task, not at click time. With the queue busy, two clicks queue two tasks. The second posts the same token again, gets `link-expired`, removes the section and focuses the link field with an error.
- `recordLooksRight` already does this correctly by re-checking inside the task.

**Fix:** Mark the control as taken at click time and re-check inside the task.

```js
button.addEventListener('click', () => {
  if (button.getAttribute('aria-disabled') === 'true') {
    return;
  }

  button.setAttribute('aria-disabled', 'true');
  options.onUndo();
});
```

For Add, read the field at submit time and ignore the submit when a task is pending. For Use, set `aria-disabled` before `helpers.enqueue`.

### WR-11: Owner scripts and stylesheet are served on the public port

**File:** `Cabinet.Service/wwwroot/js/owner-*.js` and `Cabinet.Service/wwwroot/css/owner.css`, served by `Cabinet.Service/Program.cs:96` (`MapStaticAssets`); referenced from `Pages/Index.cshtml:134` and `Pages/Shared/_OwnerLayout.cshtml:24,63`
**Issue:**
- The public page never links the owner assets, but `MapStaticAssets` serves all of `wwwroot` on every port.
- The path guard (`UseOwnerPathGuard`) only blocks paths whose first segment is `owner`.
- Anyone on the public port can fetch `/js/owner-locations.js`, `/js/owner-link.js`, `/js/owner-settings.js` and `/css/owner.css`.
- These disclose the full owner surface: handler names (`?handler=BulkMove`, `UseLink`, `RemoveLink`, `Finish`), the `/owner/preview/<token>` route, all owner copy and the existence of a restore flow.
- No secret leaks, since the endpoints stay behind the port, client-range and antiforgery walls.
- It still contradicts the intent that the public port exposes no owner script or owner paths, and it hands an attacker a map.

**Fix:** Move the owner assets under a prefix the existing guard already covers, for example `wwwroot/owner/js/...` and `wwwroot/owner/css/owner.css` served at `/owner/js/...`. Alternatively extend the path guard to refuse `/js/owner-*` and `/css/owner.css` off the owner port, and add a test beside the existing path-guard tests.

### WR-12: Backup banner button name does not contain its visible text on phones

**File:** `Cabinet.Service/Pages/Shared/_OwnerBanner.cshtml:18-21`
**Issue:**
- The button has `aria-label="Download owner data"`. On narrow screens the visible text is the short span, `Download data`.
- `Download data` is not a substring of `Download owner data`. That fails the "label in name" rule (WCAG 2.5.3), so a voice-control user saying "click Download data" does not hit the button.
- The label was added because the short span is `aria-hidden` and the full span is `display: none` on phones, which would leave an empty name.
- `setBusy` also strips and restores this label, which adds fragility.

**Fix:** Drop `aria-hidden` from the short span and drop the `aria-label`. Elements with `display: none` are already excluded from the accessible name, so the visible text becomes the name at every width. Alternatively make the short text a prefix of the full text.

### Info

### IN-07: Bulk move acts on ticked rows that are hidden by the search or the unplaced filter

**File:** `Cabinet.Service/wwwroot/js/owner-placing.js:117-119`; `owner-games.js:195-204`
**Issue:**
- `tickedRows()` counts hidden rows, but select-all only ticks and unticks the rows that are shown.
- Tick A, search for B, tick B, then Move: A moves too.
- The count reads `2 selected`, but the owner cannot see A.
- The move is undoable, so this is a hazard, not data loss.

**Fix:** Either untick rows that a filter hides, or say in the dock count how many selected games are hidden (`2 selected, 1 hidden by the filter`).

### IN-08: A location row added by script keeps the template's stale revision

**File:** `Cabinet.Service/wwwroot/js/owner-locations.js:542-556`
**Issue:**
- `buildItem` calls `setRevision(getRevision(), doc)` before the new item is appended to the document, so the call updates every form except the new one.
- The cloned form's `data-page-revision` input keeps the revision from page load.
- The script intercepts every submit of these forms, so nothing breaks today.
- If the script fails and the native form posts, it is out of date.

**Fix:** Call `setRevision` after `list.append(item)`, or set the cloned input directly.

### IN-09: `shortAddress` can throw, and the preview token is not validated

**File:** `Cabinet.Service/wwwroot/js/owner-format.js:154`; `owner-link.js:78`
**Issue:**
- `decodeURIComponent` throws `URIError` on a malformed percent sequence such as `%E0%A4%A`, which `new URL` accepts. Confirmed with Node.
- The fallback is only reached when the server sends a `fullAddress` without an `address`, but the exception would abort the post-save UI update.
- `checkedPreview` validates `previewUrl` against a 32-hex path but returns `result.token` unchecked. `dataset.token` is safe, but a missing token would be posted as the string `undefined`.

**Fix:** Wrap the decode in a try/catch that falls back to the raw segment. In `checkedPreview`, require `result.token === previewUrl.split('/').pop()`.

### IN-10: Dead code in `owner-format.js`

**File:** `Cabinet.Service/wwwroot/js/owner-format.js`
**Issue:**
- These are never used by any production module: the exports `megabytes` (131) and `deleteDialogBody` (223), and the strings `OWNER_TEXT.up`, `down`, `keepIt`, `deleteLocation`, `noLocations`, `removeLink`, `downloadData`, `downloadOwnerData` and `downloadFailed`.
- `changesBanner`, `compactBanner` and `tinyBanner` are exported but only used inside the module.
- `checking` is defined twice, at lines 80 and 101.
- Only the Node tests reference `megabytes` and `deleteDialogBody`.

**Fix:** Delete the unused entries, remove the second `checking`, and drop `export` from the three banner helpers (or test through `bannerTexts`).

### IN-11: Duplicated logic across the owner scripts

**File:** `owner-locations.js:281`, `owner-pictures.js:248`, `owner-finish.js:19`; `owner-pictures.js:383-403` against `owner-shared.js:333-350`; `owner-pictures.js:58` and `owner-link.js:269`; `owner-locations.js:603-615` and `owner-placing.js:440-450`; `owner-placing.js:27`, `owner-pictures.js:42` and `owner-link.js:30`
**Issue:**
- The following are repeated almost verbatim:
  - `showProblem` (three near-identical copies);
  - the radio settle-and-save logic in `initRow`, which re-implements `saveSettledValue` and its own `KEY_WINDOW_MS` and `SETTLE_MS`;
  - `checkedValue`;
  - the dialog light-dismiss handler;
  - the `HEX_COLOUR` pattern.
- Each copy has to be fixed separately, and WR-08 to WR-10 touch all of them.

**Fix:** Export `showProblem` (outcome to message mapping), `checkedValue` and a `dismissOnBackdrop(dialog)` helper from `owner-shared.js`, and generalise `saveSettledValue` to a group of radios.

### IN-12: Language toggle markup is duplicated in `Index.cshtml`

**File:** `Cabinet.Service/Pages/Index.cshtml:13-29`
**Issue:** The two `<nav class="lang-toggle">` blocks are identical. Only the wrapper differs (`header-aside` on the owner port).

**Fix:** Render the nav once into a local `RenderFragment`, or wrap it conditionally: open the `header-aside` div and render the plate only when `onOwnerPort`, then render the nav once.

### IN-13: Empty header cell in the restore comparison table

**File:** `Cabinet.Service/Pages/Shared/_OwnerRestorePanel.cshtml:30`
**Issue:** The first cell of the header row is an empty `<td>` inside `<thead>`. Screen readers announce an unnamed corner cell, and the row headers have no column title.

**Fix:** `<th scope="col"><span class="visually-hidden">@OwnerStrings.RestoreColumnWhat</span></th>`, or an existing string such as the table caption's subject.

### IN-14: The restore check button re-enables while the browser is navigating to the preview

**File:** `Cabinet.Service/wwwroot/js/owner-settings.js:284-286`
**Issue:**
- `submitRestoreCheck` returns true after `location.assign`, and `clearBusy(button)` runs straight after.
- The button is live again during the navigation, and a second press posts the file again.

**Fix:** Skip `clearBusy` when `submitRestoreCheck` returned true.

---

_Reviewed: 2026-10-13_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
