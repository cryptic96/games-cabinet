# Vendored assets

The site has no Node toolchain, so the one third-party script it needs is committed to the repository as a copy of the published file instead of being installed from a package manager.

## What is vendored

| File | What it is | Used for |
| --- | --- | --- |
| `Cabinet.Service/wwwroot/lib/signalr/signalr.min.js` | Microsoft's official SignalR JavaScript client for browsers, version 10.0.11 | Open pages receive the collection status the moment the server broadcasts it, so every open tab redraws without a reload |

The page loads it as a classic same-origin script just before the page's own module script. No other third-party script is loaded, and the content security policy stays `default-src 'self'` with nothing added for it. If the file is missing or blocked, the page still works: it falls back to checking the status once a minute while the tab is visible.

`Cabinet.Service/wwwroot/lib/signalr/NOTICE.md` records the package name and version, where the file came from, the npm integrity string of the tarball, the file's SHA-256 and the MIT licence text.

## How it is checked

- The notice carries the registry integrity string for the tarball and the SHA-256 of the extracted file.
- A unit test (`VendoredAssetTests`) fails if the file's bytes differ from the pinned SHA-256, or if the notice names a different hash or version.
- `.gitattributes` marks `Cabinet.Service/wwwroot/lib/**` as `-text`, so git never converts line endings in the file and the hash is the same on every checkout.
- The repository lint skips the JavaScript comment rule for files under `Cabinet.Service/wwwroot/lib/` only, because a minified file contains comment-like text that the project's own style rule forbids. The planning-reference and secret checks still read the vendored file, and the lint's self-tests prove that a `//` comment under a normal path is still reported.
- The file is never edited, minified or re-encoded by hand.

## How to update it

Dependabot cannot see a vendored file, so updates are manual. When a new release of the client is wanted:

1. In a scratch directory outside the repository, run `npm pack @microsoft/signalr@<version>`. Nothing is installed into the repository.
2. Compare `npm view @microsoft/signalr@<version> dist.integrity` with the tarball's own `sha512-` digest (base64 of the SHA-512 of the `.tgz`). Stop if they differ.
3. Extract `package/dist/browser/signalr.min.js` from the tarball. Do not copy the source map.
4. Copy the file over `Cabinet.Service/wwwroot/lib/signalr/signalr.min.js` without opening it in an editor.
5. Update `NOTICE.md` (version, integrity string, SHA-256, size) and the pinned hash and version in `Cabinet.UnitTests/Configuration/VendoredAssetTests.cs`.
6. Run `dotnet test --solution Cabinet.slnx` and `build/lint.sh`, then check a page in a real browser with the developer console open: there must be no security-policy message.

## The source map

The last line of the file refers to `signalr.min.js.map`. The map is deliberately not shipped: it is large, only useful for debugging the client itself, and serving it would add a file the site does not need. A browser's developer tools may log a harmless missing-map notice.
