---
created: 2026-10-07T00:00:00.000Z
title: Drop the actionlint runner-label entry once actionlint knows ubuntu-26.04
area: ci
severity: minor
files:
  - .github/actionlint.yaml
  - build/lint/compose.yaml
---

## Problem

The pinned actionlint (1.7.12) does not know GitHub's hosted `ubuntu-26.04` label, so `.github/actionlint.yaml` lists it under `self-hosted-runner` to stop actionlint rejecting it. That entry also means actionlint would not flag a workflow that genuinely targeted a self-hosted runner with that label (the zero-runner settings check and `--deny-self-hosted-runners` still guard the real risk). Upstream: https://github.com/rhysd/actionlint/issues/682.

## Solution

When Dependabot or a manual bump brings an actionlint release that knows `ubuntu-26.04`, update the pinned image digest in `build/lint/compose.yaml`, delete `.github/actionlint.yaml` (or its label entry), and run `build/lint.sh workflows`.
