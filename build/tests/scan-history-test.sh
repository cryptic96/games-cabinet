#!/usr/bin/env bash
# Exercises build/scan-history.sh against throwaway repositories.
#
# Every denylist value used here is invented and assembled from fragments, so
# this file never contains a real personal-data string. The scanner's output
# must name locations and denylist line numbers only, never a matched value.
# The scanner also runs gitleaks in a container; these checks read only the
# lines for the individual checks, so a missing container runtime does not
# change their result.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SCANNER="$REPO_ROOT/build/scan-history.sh"

unset GIT_DIR GIT_WORK_TREE GIT_INDEX_FILE GIT_PREFIX
export GIT_CONFIG_GLOBAL=/dev/null
export GIT_CONFIG_SYSTEM=/dev/null

WORK_DIR="$(mktemp -d)"
cleanup() {
  rm -rf "$WORK_DIR"
}
trap cleanup EXIT

SYNTH_ONE="$(printf '%s%s' 'zqx' 'orbit')"
SYNTH_TWO="$(printf '%s-%s' 'plover' 'mesa')"
NOREPLY_EMAIL='1+tester@users.noreply.github.com'

DENYLIST="$WORK_DIR/denylist.txt"
printf '%s\n' '# synthetic values' "$SYNTH_ONE" "$SYNTH_TWO" >"$DENYLIST"

FAILURES=0
OUT_FILE="$WORK_DIR/output"
LAST_STATUS=0

pass() {
  echo "PASS: $1"
}

fail() {
  echo "FAIL: $1" >&2
  FAILURES=$((FAILURES + 1))
}

check() {
  local name="$1"
  shift
  if "$@"; then
    pass "$name"
  else
    fail "$name"
  fi
}

new_repo() {
  local dir="$1"
  mkdir -p "$dir"
  git init --quiet --initial-branch=main "$dir"
  git -C "$dir" config user.email "$NOREPLY_EMAIL"
  git -C "$dir" config user.name "Test User"
  git -C "$dir" config commit.gpgsign false
  git -C "$dir" config core.hooksPath /dev/null
}

commit_all() {
  local dir="$1" message="$2"
  git -C "$dir" add -A
  git -C "$dir" commit --quiet -m "$message"
}

run_scan() {
  local dir="$1"
  set +e
  bash "$SCANNER" --repo "$dir" --denylist "$DENYLIST" >"$OUT_FILE" 2>&1
  LAST_STATUS=$?
  set -e
}

output_has() {
  grep -q -F -e "$1" "$OUT_FILE"
}

output_lacks_synthetic() {
  local lowered value
  lowered="$(tr '[:upper:]' '[:lower:]' <"$OUT_FILE")"
  for value in "$SYNTH_ONE" "$SYNTH_TWO"; do
    if [[ "$lowered" == *"${value,,}"* ]]; then
      return 1
    fi
  done
  return 0
}

write_binary() {
  local path="$1" value="$2"
  mkdir -p "$(dirname "$path")"
  printf 'IMG\000\000\001meta %s\000\000tail\n' "$value" >"$path"
}

DIRTY_REPO="$WORK_DIR/dirty"
new_repo "$DIRTY_REPO"
printf 'an ordinary line\n' >"$DIRTY_REPO/notes.txt"
write_binary "$DIRTY_REPO/images/shot.png" "$SYNTH_ONE"
commit_all "$DIRTY_REPO" "add files"
GIT_COMMITTER_EMAIL='someone@example.com' git -C "$DIRTY_REPO" tag -a -m "release" v1.0.0
run_scan "$DIRTY_REPO"

binary_case() {
  [ "$LAST_STATUS" -eq 1 ] &&
    output_has 'FAIL denylist-trees' &&
    output_has 'images/shot.png denylist line(s) 2' &&
    output_lacks_synthetic
}

tagger_case() {
  output_has 'FAIL noreply-identities' &&
    output_has 'annotated tag object' &&
    output_has 'tagger email is not a GitHub noreply address'
}

check "the scanner finds a denylisted value inside a binary file without echoing it" binary_case
check "the scanner flags an annotated tag whose tagger email is not a noreply address" tagger_case

CLEAN_REPO="$WORK_DIR/clean"
new_repo "$CLEAN_REPO"
printf 'an ordinary line\n' >"$CLEAN_REPO/notes.txt"
commit_all "$CLEAN_REPO" "add files"
git -C "$CLEAN_REPO" tag -a -m "release" v1.0.0
run_scan "$CLEAN_REPO"

clean_case() {
  output_has 'PASS denylist-trees' &&
    output_has 'PASS denylist-messages' &&
    output_has 'PASS denylist-identities' &&
    output_has 'PASS noreply-identities' &&
    output_has 'PASS absolute-paths'
}

check "the scanner passes a clean repository with a noreply-tagged release" clean_case

if [ "$FAILURES" -ne 0 ]; then
  echo "$FAILURES check(s) failed" >&2
  exit 1
fi
echo "all scan-history checks passed"
