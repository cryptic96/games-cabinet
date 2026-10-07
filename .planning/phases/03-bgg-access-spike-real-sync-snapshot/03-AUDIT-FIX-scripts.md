# Audit fix report: scripts, docs, logo pin, lint rule

All four items fixed. Each was verified with `dotnet test --solution Cabinet.slnx` (908 passed, 0 failed) and `build/lint.sh` (all checks pass). BGG was never contacted.

## 1. Access check XML refusal (T-03-04)

**Commit:** 161f2f8
**Files:** `build/bgg-access-check.py`, `build/tests/bgg-access-check-test.sh`

- A body starting with a UTF-16/UTF-32 byte-order mark, or holding any NUL byte (which also covers UTF-16/32 without a mark), is classified "other" before any parsing.
- Every other body also goes through a strict `xml.parsers.expat` parse whose doctype and entity-declaration handlers raise, so a declaration is refused regardless of text form; the existing byte-level DOCTYPE/ENTITY check stays as well.
- Self-test: UTF-16 (with mark, LE, BE) and UTF-32 (with mark, LE, BE) doctype and clean documents, mark alone, NUL byte, plain and mixed-case doctype, strict-parse helper, and that clean UTF-8 (with and without mark) is still read.
- Script test: the same encodings through the real classifier in isolated mode. 31 cases pass.

## 2. Docs drift

**Commit:** 5ce4294
**File:** `docs/bgg-access-check.md`

Documents the matching rules (credentials anywhere; short usernames and short values as whole words), the `hint:` line, and the encoding-independent refusal, plus the new self-test and script test coverage.

## 3. Logo pin

**Commit:** c2a71bf
**Files:** `Cabinet.Service/wwwroot/img/NOTICE.md` (new), `Cabinet.UnitTests/Configuration/BrandAssetTests.cs` (new)

The test pins `powered-by-bgg.svg` by SHA-256 (`b577fd17...bc43`) and checks the notice names the same hash, the official reversed RGB SVG origin from the logo pack linked from the XML API terms of use, and unchanged copying. No URL invented; the SVG is untouched.

## 4. Lint rule

**Commit:** e9fdefe
**File:** `build/lint/checks/10-repo-rules.sh`

Forbids `innerHTML`, `outerHTML`, `insertAdjacentHTML`, `document.write` and `.cssText` in tracked JS under `Cabinet.Service/wwwroot/js/`; `wwwroot/lib/` is not scanned. Self-test covers each API caught, a clean script passing, and the file filter keeping page scripts while dropping the vendored folder. The current tree is clean. A temporary violation added to a page script was caught and then reverted.

## Notes

- The logo hash is line-ending sensitive. `.gitattributes` marks only `wwwroot/lib/**` as `-text`; the SVG currently has no CR bytes, but adding `Cabinet.Service/wwwroot/img/*.svg -text` there would protect it on Windows checkouts. `.gitattributes` was outside this task's file list, so it was left alone.
