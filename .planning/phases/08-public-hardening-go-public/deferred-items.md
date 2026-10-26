# Deferred items

Out-of-scope discoveries logged during execution. Not fixed by the plan that found them.

## Browser WebSockets are refused behind the proxy (found in plan 08-13)

- **Status: fixed in commit 153a43f.** `LiveOriginGuard` now treats `wss` as `https` and `ws` as `http` for the default port
  and requires the Origin scheme to be secure exactly when the request is. Covered by unit tests on the guard and by
  integration tests that send negotiate and upgrade requests with `X-Forwarded-Proto: wss`/`ws`. Not yet re-measured on
  the real route; that needs a release.

- **Symptom:** through the real route every browser WebSocket handshake to the live channel answers 403, so the page falls
  back to Server-Sent Events. Negotiate and the stream requests work, so the site looks fine.
- **Cause (reproduced straight against the app from the proxy container):** Traefik sends `X-Forwarded-Proto: wss` on a
  WebSocket upgrade. With the forwarded-headers setup the request scheme becomes `wss`, `Request.IsHttps` is false, and
  `LiveOriginGuard.IsAllowed` expects port 80 for the Host while the browser's Origin carries 443. The same request with
  `X-Forwarded-Proto: https` gets 101; a request with no Origin header gets 101.
- **Where:** `Cabinet.Service/Limits/LiveOriginGuard.cs` (the expected port) and the forwarded-headers setup in
  `Cabinet.Service/Hosting/ForwardedHeadersSetup.cs`. Unchanged on the milestone head.
- **Suggested fix:** treat the scheme `wss` (and `ws`) as `https` (`http`) when deriving the expected port, with a test
  that sends an upgrade with `X-Forwarded-Proto: wss` and a matching Origin, or normalise the scheme in the forwarded-headers
  setup. Decide whether Server-Sent Events only is acceptable instead; the per-connection memory is the same on both.
- **Impact:** none on correctness today (SSE works); the transport the real visitors use is SSE, which holds one long
  request in flight per page at the proxy.
