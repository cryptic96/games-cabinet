# Walking Skeleton — Games Cabinet

**Phase:** 1
**Generated:** 2026-10-04

## Capability Proven End-to-End

A visitor on the home network opens the Games Cabinet hello page and sees the running version, served by a release that travelled semver tag -> attested draft release -> owner approval in the protected `deploy` environment -> the container's pull timer finding it, verifying its attestation without any GitHub credential, installing it and health-checking it -> and a deliberately broken follow-up that rolls back automatically and is skipped until a fixed release ships.

## Architectural Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Framework | .NET 10 / ASP.NET Core, Razor Pages shell with static assets via `MapStaticAssets`; vanilla ES modules and modern CSS later; no SPA framework, no Node toolchain | The owner's stack across projects; the smallest payload and supply chain for one public page on a low-power host |
| Solution layout | `Cabinet.slnx` with `Cabinet.Domain` (pure models and logic), `Cabinet.Repository` (everything touching the outside: images now, BGG client and snapshot store later), `Cabinet.Service` (host, pages, endpoints, hosted services), `Cabinet.UnitTests`, `Cabinet.IntegrationTests` | Mirrors ing-dashboard; keeps the future layout engine pure and unit-testable |
| Data layer | No database. Persistence touchpoints in this phase are the installer's root-owned state files (`/var/lib/cabinet-deploy/state/previous` and `.../rejected`, written atomically and read on every poll) and the app's systemd `StateDirectory=/var/lib/cabinet`, reserved for the JSON snapshot and image cache later. "One real DB read/write" from the skeleton template is replaced by the rejected-version write and read, proven on the real container during the rollback rehearsal | The collection is a few hundred read-mostly records with one writer; a database would add schema, migrations and a deploy step for nothing |
| Health contract | Loopback-only ops listener (`127.0.0.1:5081`) serving `/health` as JSON `{status, version, commit}`; never depends on BGG, a snapshot or any state; public listener `0.0.0.0:5080` | The installer accepts a release only when health reports the expected version; a BGG outage or empty disk can never roll back a good release |
| Auth | No visitor authentication. Admin access is SSH key-only from admin ranges; publication is gated by the owner as required reviewer of the `deploy` environment; tags are admin-only | Public read-only site; the only privileged actions are deploys, which are human-approved |
| Deployment target | Unprivileged Ubuntu 24.04 container with nesting (1 core, 1 GB, 8 GB to start) on a shared low-power host; framework-dependent `linux-x64` publish on the distribution's ASP.NET Core runtime; root installer `cabinet-deploy` driven by a 10-minute jittered systemd timer pulling unauthenticated public releases; `releases/<version>` plus an atomic `current` symlink; LAN-only Traefik route until the hardening phase | Pull-based, attested, no self-hosted runner and no GitHub credential on the server (the ing-dashboard security model) |
| Release pipeline | GitHub-hosted runners only; SHA-pinned actions; `permissions: {}`; strict-semver tags on `main`; build provenance attestation; draft release; environment-gated publish that re-verifies checksum and attestation; immutable releases | Every artefact on the server is traceable to a reviewed commit on `main` and a human approval |
| Guardrails | Required checks `build-test` and `lint`; protected `main` (PR only, merge or squash, no force-push); self-testing lint suite (planning references, comment rules, licence, workflows, shell, secrets over all refs); owner denylist hooks outside the repository | A public repository must never leak personal data or planning internals |
| Image library | SkiaSharp 4.153.1 with the Linux NoDependencies native assets, proven by the `image-smoke` subcommand inside the production sandbox | Builds and publishes in Release without a licence key; WebP encode verified |
| Directory layout | `build/` (lint, packaging, release and settings scripts with tests), `deploy/` (installer, libraries, provisioning modules, units, templates, tests), `docs/` (operator guides), `.githooks/`, `.github/` | Same shape as ing-dashboard so its operational knowledge carries over |

## Stack Touched in Phase 1

- [x] Project scaffold (framework, build, lint, test runner): solution, `Directory.Build.props`, `global.json`, MTP test runner, lint suite
- [x] Routing — at least one real route: `/` hello page on the public listener, `/health` on the ops listener
- [x] Persistence — no database by design; the real write and read are the installer's rejected-version state file (and `previous`), exercised for real by the rollback rehearsal on the container
- [ ] UI — at least one interactive element wired to the API: not applicable in this phase. The hello page is read-only; the only interaction in the walking skeleton is the owner's approval of the `deploy` environment, which drives publication. The first visitor interaction arrives with the cabinet prototype.
- [x] Deployment — running on the real container through the full release path, with rollback proven

## Out of Scope (Deferred to Later Slices)

- Any BGG call, the token, the location spike (real-sync phase)
- The cabinet layout, spines, box art and image pipeline beyond the smoke test
- Snapshot persistence in the state directory, background sync, "sync now"
- Public exposure, public DNS, rate limits, CSP and security headers on the app, resource caps (hardening phase)
- Outbound (egress) firewall allow-list for the container
- Deploy and sync failure email
- Moving the containers to a newer Ubuntu LTS

## Subsequent Slice Plan

Each later phase adds one vertical slice on top of this skeleton without altering its architectural decisions:

- Phase 2: a deterministic wooden-cabinet layout on synthetic collections, approved visually on desktop and phone
- Phase 3: the owner's real BGG collection appears through a polite hourly sync and a guarded "sync now", resilient to BGG outages
- Phase 4: real box art, art-coloured spines and true box proportions, served from the site
- Phase 5: pull-out animation, detail card, keyboard and screen-reader access, English and Dutch labels
- Phase 6: home/VPN-only owner tools for image overrides (and locations if needed), persisted and backed up
- Phase 7: dim-to-filter search and per-location cabinets
- Phase 8: public HTTPS exposure with rate limits, headers, resource caps and a go-public checklist
