---
gsd_state_version: 1.0
milestone: v1.0
milestone_name: milestone
current_phase: 06.1
current_phase_name: Selectable Cabinet Finishes & Lighting
status: planning
stopped_at: Phase 6 UI-SPEC approved
last_updated: "2026-10-13T14:13:30.046Z"
last_activity: 2026-10-13
last_activity_desc: Phase 06 complete, transitioned to Phase 06.1
progress:
  total_phases: 7
  completed_phases: 6
  total_plans: 108
  completed_plans: 108
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-10-06)

**Core value:** Anyone with the link sees an up-to-date, good-looking cabinet of exactly the games the owner owns on BGG, with no manual data entry in the app.
**Current focus:** Phase 06 — owner-tools-persistence

## Current Position

Phase: 06.1 — Selectable Cabinet Finishes & Lighting
Plan: Not started
Status: Ready to plan
Last activity: 2026-10-13 — Phase 06 complete, transitioned to Phase 06.1

Progress: [██████████] 100%

## Performance Metrics

**Velocity:**

- Total plans completed: 108
- Average duration: - min
- Total execution time: 0.0 hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 01 | 15 | - | - |
| 2 | 9 | - | - |
| 03 | 15 | - | - |
| 04 | 23 | - | - |
| 05 | 16 | - | - |
| 06 | 30 | - | - |

**Recent Trend:**

- Last 5 plans: -
- Trend: -

*Updated after each plan completion*
**Per-Plan Metrics:**

| Plan | Duration | Tasks | Files |
|------|----------|-------|-------|
| Phase 01 P01 | 25min | 3 tasks | 1 files |
| Phase 05 P05-15 | two sessions | 4 tasks | 14 files |
| Phase 05 P16 | several sessions | 5 tasks | 0 files |

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.
Recent decisions affecting current work:

- [Roadmap]: Vertical MVP, 8 phases. Deploy plumbing first (least reversible), layout prototype on synthetic data while BGG token approval is pending, then real sync, enrichment, interaction, owner tools, filters, and a public hardening gate.
- [Roadmap]: Owner tools (Phase 6) come before filters and location cabinets (Phase 7), so the location toggle and filter are built against real location data.
- [Roadmap]: Mobile reflow (CAB-07) and expansion layout (EXP-01..03) are mapped to the layout phase because they are layout-engine outputs the owner must review early.
- [Roadmap]: SEC-05 (token and username server-side only) is mapped to the sync phase where it is built; SEC-06 (resource caps) stays in hardening so caps are sized from real load.
- [Phase ?]: Phase 01-01: history rewritten with replace-text only; backup mirror kept under XDG state until go-live scan passes
- [Phase 2]: Cabinet look approved and shipped as v0.2.0 (classic wooden finish, piles widest-first, big boxes may lie flat, thick expansions upright, last section trimmed, layout version 8); owner walked the deployed build on desktop and phone.
- [Phase 2]: Layout is a pure versioned function guarded by golden files; later visual changes are a version bump plus a release, with no stored state to migrate.
- [Phase 2]: Remaining taste calls deferred as todos to Phases 4, 5 and 7; selectable finishes stay an unscheduled todo.
- [Phase 2]: Front-end work in GSD agents uses the owner's personal senior-frontend skill via agent_skills (planner, executor, UI agents).
- [Phase 05]: [05-15] Dutch site name stays Spellenkast (owner review round 1)
- [Phase 05]: [05-15] Card has no punched hole (owner review round 1)
- [Phase 05]: [05-15] Swap rows are grids packed to the start so one-line titles sit on their first ruling on a phone
- [Phase 05]: [05-15] Uneven desktop sections accepted for now; todo recorded for a later layout pass

### Pending Todos

- [Phase 3] Add dev-only fake BGG host for the sync phase (minor, tooling): `.planning/todos/pending/2026-10-05-add-dev-only-fake-bgg-host-for-the-sync-phase.md`
- [Phase 4] Box look polish for the box images phase (cosmetic, ui): `.planning/todos/pending/2026-10-06-box-look-polish-for-the-box-images-phase.md`
- [Phase 5] Cabinet accessibility notes for the detail phase (minor, ui): `.planning/todos/pending/2026-10-06-cabinet-accessibility-notes-for-the-detail-phase.md`
- [Phase 7] Phone cabinet density for the filters and locations phase (cosmetic, ui): `.planning/todos/pending/2026-10-06-phone-cabinet-density-for-the-filters-phase.md`
- [Phase 8] Turn off prototype mode and add noindex before go-public (major, security): `.planning/todos/pending/2026-10-06-turn-off-prototype-mode-and-add-noindex-before-go-public.md`
- [Unscheduled] Selectable cabinet finishes and lit-cubbies toggle (minor, ui), needs discussion, possibly its own phase: `.planning/todos/pending/2026-10-06-selectable-cabinet-finishes-and-lit-cubbies-toggle.md`

### Blockers/Concerns

- [Resolved 2026-10-06]: The BGG application (non-commercial) is approved and the owner has the API token, so Phase 3 is unblocked. The token goes only into the server env file (and `dotnet user-secrets` locally), never into the repository, logs or chat. Phase 3 also adds the linked "Powered by BGG" logo to every public page (SYNC-08), taken from BGG's official usage page.
- [Resolved 2026-10-06]: Plan 03-04 finished with the official reversed "Powered by BGG" SVG from the owner's download, served from the site with no plate; the exact BGG source page URL is still to be confirmed by the owner for the provenance record.
- [Phase 3]: Location spike outcome decides whether LOC-03 and LOC-04 are built in Phase 6. BGG throttle numbers are unpublished; treat all rates as unconfirmed.
- [Phase 4]: Coverage and units of BGG version dimensions are unconfirmed; image resizing under BGG terms is ambiguous (downscale only, never crop, keep the credit).
- [Phase 8]: DNS, TLS and router port-forward are owner actions; the router stays LAN-only until the go-public checklist passes.
- [Phase 8]: Before go-public, revoke or downgrade the development admin account (passwordless sudo) on the server and drop the LAN/VPN allow-list from the route (accepted risk AR-08 expires then).

### Quick Tasks Completed

| # | Description | Date | Commit | Directory |
|---|-------------|------|--------|-----------|
| 261004-vqo | Make out-of-date provisioning visible on the server (poll warning + selfcheck per-file check) | 2026-10-04 | fa25c8f | [261004-vqo-make-out-of-date-provisioning-visible-on](./quick/261004-vqo-make-out-of-date-provisioning-visible-on/) |
| 2 | Move CI and release workflows to ubuntu-26.04 (actionlint label config added) | 2026-10-06 | 7d802a1 | — |
| 3 | Fix flaky port race in integration tests (retry host start on fresh ports) | 2026-10-06 | 3376015 | — |

### Roadmap Evolution

- Phase 06.1 inserted after Phase 6: Selectable cabinet finishes and lighting, split out of the Phase 6 discussion (owner chose its own phase); runs after Phase 6, before Phase 7

## Deferred Items

Items acknowledged and carried forward from previous milestone close:

| Category | Item | Status | Deferred At |
|----------|------|--------|-------------|
| *(none)* | | | |

## Session Continuity

Last session: 2026-10-12T12:41:37.217Z
Stopped at: Phase 6 UI-SPEC approved
Resume file: .planning/phases/06-owner-tools-persistence/06-UI-SPEC.md
