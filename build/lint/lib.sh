#!/usr/bin/env bash
# Shared helpers for the lint checks. Sourced by every check, never executed.
#
# lint_compose runs one of the pinned tool containers from build/lint/compose.yaml
# with the repository mounted read-only. When the runner exported
# LINT_GIT_COMMON_DIR (a linked worktree whose git directory lives outside the
# repository root), that directory is mounted at the identical absolute path as
# a single quoted argument, so paths containing spaces survive.

lint_compose() {
  local -a compose_args=(docker compose -f build/lint/compose.yaml run --rm --no-deps -T)
  if [ -n "${LINT_GIT_COMMON_DIR:-}" ]; then
    compose_args+=(-v "${LINT_GIT_COMMON_DIR}:${LINT_GIT_COMMON_DIR}:ro")
  fi
  "${compose_args[@]}" "$@"
}
