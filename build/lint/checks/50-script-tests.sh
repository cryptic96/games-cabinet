#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$REPO_ROOT"

run_test_scripts() {
  local -a scripts=("$@")
  local status=0 ran=0
  local test_script base
  for test_script in "${scripts[@]}"; do
    base="$(basename "$test_script")"
    if [[ "$base" == *-network-test.sh ]] && [ "${CABINET_LINT_NETWORK:-0}" != "1" ]; then
      echo "skipping $test_script (network test; set CABINET_LINT_NETWORK=1 to run)"
      continue
    fi
    if [[ "$base" == *-e2e-test.sh ]] && [ "${CABINET_E2E:-0}" != "1" ]; then
      echo "skipping $test_script (end-to-end test; set CABINET_E2E=1 to run)"
      continue
    fi
    echo "==> $test_script"
    ran=$((ran + 1))
    if ! bash "$test_script"; then
      echo "FAIL: $test_script" >&2
      status=1
    fi
  done
  if [ "$ran" -eq 0 ]; then
    echo "no script tests were run (all skipped)"
  fi
  return "$status"
}

self_test() {
  local tmp
  tmp="$(mktemp -d)"
  local failed=0

  printf '#!/usr/bin/env bash\nexit 0\n' >"$tmp/good-test.sh"
  printf '#!/usr/bin/env bash\nexit 1\n' >"$tmp/bad-test.sh"
  printf '#!/usr/bin/env bash\nexit 1\n' >"$tmp/bad-network-test.sh"
  printf '#!/usr/bin/env bash\nexit 1\n' >"$tmp/bad-e2e-test.sh"
  chmod +x "$tmp"/*.sh

  if ! run_test_scripts "$tmp/good-test.sh" >/dev/null; then
    echo "self-test failed: a passing test script was reported as failing" >&2
    failed=1
  fi

  if run_test_scripts "$tmp/bad-test.sh" >/dev/null 2>&1; then
    echo "self-test failed: a failing test script was not detected" >&2
    failed=1
  fi

  if ! CABINET_LINT_NETWORK=0 run_test_scripts "$tmp/bad-network-test.sh" >/dev/null 2>&1; then
    echo "self-test failed: a network test was not skipped by default" >&2
    failed=1
  fi

  if CABINET_LINT_NETWORK=1 run_test_scripts "$tmp/bad-network-test.sh" >/dev/null 2>&1; then
    echo "self-test failed: a network test did not run despite CABINET_LINT_NETWORK=1" >&2
    failed=1
  fi

  if ! CABINET_E2E=0 run_test_scripts "$tmp/bad-e2e-test.sh" >/dev/null 2>&1; then
    echo "self-test failed: an end-to-end test was not skipped by default" >&2
    failed=1
  fi

  if CABINET_E2E=1 run_test_scripts "$tmp/bad-e2e-test.sh" >/dev/null 2>&1; then
    echo "self-test failed: an end-to-end test did not run despite CABINET_E2E=1" >&2
    failed=1
  fi

  rm -rf "$tmp"
  return "$failed"
}

if ! self_test; then
  echo "FAIL: 50-script-tests self-test did not behave as expected" >&2
  exit 1
fi
echo "50-script-tests self-tests passed"

shopt -s nullglob
candidates=(deploy/tests/*-test.sh build/tests/*-test.sh)
shopt -u nullglob

if [ "${#candidates[@]}" -eq 0 ]; then
  echo "no script tests found yet"
  exit 0
fi

run_test_scripts "${candidates[@]}"
