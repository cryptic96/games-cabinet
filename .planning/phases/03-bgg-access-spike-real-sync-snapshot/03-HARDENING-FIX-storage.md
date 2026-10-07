# Hardening fix: stored values the stores accept but should not

**Source:** 03-SECURITY.md, observation 9 (odd but valid stored values).
**Commit:** 79eb3e6
**Status:** fixed (one deliberate deviation, see "Empty title")

## What changed

- **Enums as names only.** A new internal strict enum converter (`Cabinet.Repository/Storage/StrictEnumConverter.cs`) accepts only the camel-case names the app writes. An integer, a number in a string, a different letter case, a comma list or an unknown name throws, which both stores already treat as malformed content (file set aside as `.bad`, start empty / initial state).
- **Required fields and nulls.** Shared options in `StoredJson.cs` turn on nullable-annotation checks (a JSON null for a non-nullable field is malformed) and mark `schemaVersion`, `capturedAtUtc`, `items` on the snapshot and `collectionId`, `gameId`, `title`, `kind` on each item as required. Optional fields (year, dimensions, location) stay optional, so the existing "missing optional fields take defaults" behaviour is unchanged.
- **Cooldown clamp.** `SyncStateStore` now takes a `TimeProvider` and the configured manual cooldown. A loaded `cooldownEndsUtc` later than now plus that cooldown is replaced by now plus the cooldown, with one log line and no values. Wired in `Cabinet.Service/Sync/SyncEndpoints.cs` from `SyncOptions.ManualCooldown`.
- **Held-back fingerprint.** A held-back record whose fingerprint is not exactly 64 lowercase hex characters (the shape `ShrinkGuard.Fingerprint` produces) is dropped, with one log line. The rest of the state is kept; the file is not set aside.
- **Unreadable files untouched.** All checks run only on content that was read. A locked file with odd content stays in place, is reported as unreadable, and is judged (and set aside) only once it can be read; both stores have a test for this.
- **Written JSON unchanged.** Round-trip tests with every field set confirm files written by the current code load unchanged with no log line. The one existing test fingerprint that was not well-formed (`abc123` in the state store test, a 16-character value in the status-line integration test) now uses a real fingerprint.

## Empty title (deviation from the request)

The request said a null or empty title makes the snapshot malformed. Only null (and a missing title) is rejected. The BGG parser deliberately keeps a blank title as an empty string (pinned by an existing parser test, and the domain record documents that a title may be blank), so the app can legitimately write `"title":""`. Rejecting it would make a file the current code wrote get set aside and the collection rebuilt, and would break the "existing files load" requirement. A test pins that a blank title round-trips. If the owner wants empty titles refused, the parser would first have to skip or substitute such items, which is in the other fixer's area.

## Behaviour notes

- Enum name matching is now case-sensitive and exact; STJ's default would also have accepted other letter cases.
- `SyncStateStore` constructor signature changed: `(directory, time, manualCooldown, logger)`. Callers updated: `SyncEndpoints.cs` and `Cabinet.IntegrationTests/SyncStatusLineTests.cs`.
- `SyncState.cs` and `CollectionSnapshot.cs` were not touched.

## Tests

`Cabinet.UnitTests/Snapshot/SyncStateStoreTests.cs` (fake clock, temporary directory): integer, numeric-string, wrong-case, list and unknown enum values; cooldown clamped, kept when within bounds (past, now, edge), checked against the clock at load time; malformed, wrongly cased, wrong-length and non-hex fingerprints dropped, valid kept; full round trip; locked file with odd content left in place.
`Cabinet.UnitTests/Snapshot/SnapshotStoreTests.cs`: integer, numeric-string, wrong-case, list, unknown and null `kind`; null, missing and wrongly typed title / ids / kind; missing `capturedAtUtc`; blank title round trips; full round trip; locked file with odd content left in place.

## Verification

`dotnet test --solution Cabinet.slnx`: 977 passed, 0 failed (run in the isolated worktree checkout). `build/lint.sh repo-rules`: pass.
