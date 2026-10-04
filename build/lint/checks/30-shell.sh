#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$REPO_ROOT"

# shellcheck source=build/lint/lib.sh
source "$REPO_ROOT/build/lint/lib.sh"

run_shellcheck() {
  local mount_dir="$1"
  shift
  lint_compose -v "$mount_dir:/repo:ro" shellcheck -x -P SCRIPTDIR "$@"
}

is_shell_script() {
  local file="$1"
  [ -f "$file" ] || return 1
  [ ! -L "$file" ] || return 1
  case "$file" in
    *.sh) return 0 ;;
  esac
  local opener first_line
  opener="$(head -c 2 "$file" 2>/dev/null || true)"
  [ "$opener" = '#!' ] || return 1
  first_line="$(head -n 1 "$file" 2>/dev/null || true)"
  case "$first_line" in
    *bash* | *'/sh' | *'/sh '* | *' sh' | *' sh '*) return 0 ;;
  esac
  return 1
}

self_test() {
  local tmp
  tmp="$(mktemp -d)"
  local bad="$tmp/bad.sh"
  local good="$tmp/good.sh"
  local failed=0

  # shellcheck disable=SC2016
  printf '#!/usr/bin/env bash\necho $1\n' >"$bad"
  # shellcheck disable=SC2016
  printf '#!/usr/bin/env bash\nset -euo pipefail\necho "$1"\n' >"$good"

  if run_shellcheck "$tmp" "bad.sh" >/dev/null 2>&1; then
    echo "self-test failed: shellcheck did not flag an unquoted variable" >&2
    failed=1
  fi

  if ! run_shellcheck "$tmp" "good.sh" >/dev/null 2>&1; then
    echo "self-test failed: shellcheck rejected a clean script" >&2
    failed=1
  fi

  printf '#!/usr/bin/env bash\nset -euo pipefail\n' >"$tmp/extensionless"
  if ! is_shell_script "$tmp/extensionless"; then
    echo "self-test failed: an extensionless script with a bash shebang was not recognised" >&2
    failed=1
  fi

  printf 'plain text\n' >"$tmp/notes"
  if is_shell_script "$tmp/notes"; then
    echo "self-test failed: a plain text file was recognised as a shell script" >&2
    failed=1
  fi

  rm -rf "$tmp"
  return "$failed"
}

if ! self_test; then
  echo "FAIL: 30-shell self-test did not behave as expected" >&2
  exit 1
fi
echo "30-shell self-tests passed"

shopt -s nullglob
candidate_files=(
  build/*.sh
  build/lint/*.sh
  build/lint/checks/*.sh
  build/tests/*.sh
  .githooks/*
  .githooks/lib/*
  deploy/bin/*
  deploy/lib/*.sh
  deploy/provision.sh
  deploy/provision.d/*.sh
  deploy/tests/*.sh
)
shopt -u nullglob

existing_files=()
for f in "${candidate_files[@]}"; do
  if is_shell_script "$f"; then
    existing_files+=("$f")
  fi
done

while IFS= read -r f; do
  if is_shell_script "$f"; then
    existing_files+=("$f")
  fi
done < <(git ls-files)

if [ "${#existing_files[@]}" -eq 0 ]; then
  echo "no shell scripts found yet"
  exit 0
fi

mapfile -t unique_files < <(printf '%s\n' "${existing_files[@]}" | sort -u)

with_shebang=()
without_shebang=()
for f in "${unique_files[@]}"; do
  opener="$(head -c 2 "$f" 2>/dev/null || true)"
  if [ "$opener" = '#!' ]; then
    with_shebang+=("$f")
  else
    without_shebang+=("$f")
  fi
done

echo "Running shellcheck over ${#unique_files[@]} file(s)"
status=0
if [ "${#with_shebang[@]}" -gt 0 ]; then
  if ! run_shellcheck "$REPO_ROOT" "${with_shebang[@]}"; then
    status=1
  fi
fi
if [ "${#without_shebang[@]}" -gt 0 ]; then
  if ! run_shellcheck "$REPO_ROOT" -s bash "${without_shebang[@]}"; then
    status=1
  fi
fi
exit "$status"
