#!/usr/bin/env bash
# Exercises the committed git hooks in throwaway repositories.
#
# Every denylist value used here is invented and assembled from fragments, so
# this file never contains a real personal-data string. Hook output must name
# the file, line and denylist line only, never the matched value.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
HOOKS_DIR="$REPO_ROOT/.githooks"

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
SYNTH_COMMENTED="$(printf '%s%s' 'quill' 'fjord')"
NOREPLY_EMAIL='1+tester@users.noreply.github.com'

DENYLIST="$WORK_DIR/denylist.txt"
printf '%s\n' "$SYNTH_ONE" '# comment lines are ignored' "# $SYNTH_COMMENTED" '' "  $SYNTH_TWO  " >"$DENYLIST"
COMMENT_ONLY_DENYLIST="$WORK_DIR/comment-only.txt"
printf '%s\n' '# nothing to match' '' "# $SYNTH_ONE" >"$COMMENT_ONLY_DENYLIST"
MISSING_DENYLIST="$WORK_DIR/does-not-exist.txt"

FAILURES=0
OUT_FILE="$WORK_DIR/stdout"
ERR_FILE="$WORK_DIR/stderr"

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
  git -C "$dir" config core.hooksPath "$HOOKS_DIR"
}

run_hooked() {
  local dir="$1" denylist="$2"
  shift 2
  set +e
  (cd "$dir" && CABINET_DENYLIST_FILE="$denylist" "$@") >"$OUT_FILE" 2>"$ERR_FILE"
  LAST_STATUS=$?
  set -e
}

stderr_lacks_synthetic() {
  local lowered
  lowered="$(tr '[:upper:]' '[:lower:]' <"$ERR_FILE")"
  local value
  for value in "$SYNTH_ONE" "$SYNTH_TWO" "$SYNTH_COMMENTED"; do
    if [[ "$lowered" == *"${value,,}"* ]]; then
      return 1
    fi
  done
  return 0
}

stderr_contains() {
  grep -q -F -e "$1" "$ERR_FILE"
}

stage() {
  local dir="$1" path="$2" content="$3"
  mkdir -p "$dir/$(dirname "$path")"
  printf '%s\n' "$content" >"$dir/$path"
  git -C "$dir" add -- "$path"
}

# Commit with hooks and report in LAST_STATUS.
commit_with_hooks() {
  local dir="$1" denylist="$2" message="$3"
  run_hooked "$dir" "$denylist" git commit --quiet -m "$message"
}

# Commit without running any hook, to create content the push hook must catch.
commit_unchecked() {
  local dir="$1" message="$2"
  git -C "$dir" commit --quiet --no-verify -m "$message"
}

EXPECTED_STATUS_ZERO=0

status_is_zero() {
  [ "$LAST_STATUS" -eq "$EXPECTED_STATUS_ZERO" ]
}

status_is_nonzero() {
  [ "$LAST_STATUS" -ne 0 ]
}

staged_block_case() {
  local dir="$WORK_DIR/block-content"
  new_repo "$dir"
  stage "$dir" notes/a.txt "first line"$'\n'"second ${SYNTH_ONE^^} line"
  commit_with_hooks "$dir" "$DENYLIST" "add notes"
  status_is_nonzero &&
    stderr_contains 'notes/a.txt:2:' &&
    stderr_contains 'denylist match #1' &&
    stderr_contains 'blocked:' &&
    stderr_lacks_synthetic
}

staged_clean_case() {
  local dir="$WORK_DIR/allow-clean"
  new_repo "$dir"
  stage "$dir" notes/a.txt "an ordinary line"
  commit_with_hooks "$dir" "$DENYLIST" "add notes"
  status_is_zero &&
    [ "$(git -C "$dir" rev-list --count HEAD)" -eq 1 ]
}

staged_name_case() {
  local dir="$WORK_DIR/block-name"
  new_repo "$dir"
  stage "$dir" "docs/${SYNTH_TWO}.txt" "harmless content"
  commit_with_hooks "$dir" "$DENYLIST" "add file"
  status_is_nonzero &&
    stderr_contains 'denylist match #5' &&
    stderr_contains 'blocked:' &&
    stderr_lacks_synthetic
}

removal_case() {
  local dir="$WORK_DIR/allow-removal"
  new_repo "$dir"
  stage "$dir" notes/a.txt "keep"
  printf '%s\n' "keep" "scrub ${SYNTH_ONE}" >"$dir/notes/a.txt"
  git -C "$dir" add notes/a.txt
  commit_unchecked "$dir" "seed with unchecked content"
  printf '%s\n' "keep" >"$dir/notes/a.txt"
  git -C "$dir" add notes/a.txt
  commit_with_hooks "$dir" "$DENYLIST" "scrub notes"
  status_is_zero &&
    [ "$(git -C "$dir" rev-list --count HEAD)" -eq 2 ]
}

absent_denylist_case() {
  local dir="$WORK_DIR/warn-absent"
  new_repo "$dir"
  stage "$dir" notes/a.txt "line with ${SYNTH_ONE}"
  commit_with_hooks "$dir" "$MISSING_DENYLIST" "add notes"
  status_is_zero &&
    stderr_contains 'warning' &&
    [ "$(git -C "$dir" rev-list --count HEAD)" -eq 1 ]
}

comment_only_case() {
  local dir="$WORK_DIR/comment-only"
  new_repo "$dir"
  stage "$dir" notes/a.txt "line with ${SYNTH_ONE}"
  commit_with_hooks "$dir" "$COMMENT_ONLY_DENYLIST" "add notes"
  status_is_zero &&
    ! stderr_contains 'warning' &&
    [ "$(git -C "$dir" rev-list --count HEAD)" -eq 1 ]
}

check "pre-commit refuses an added line matching the denylist, case-insensitively, without echoing it" staged_block_case
check "pre-commit allows clean content" staged_clean_case
check "pre-commit refuses a staged file name matching the denylist without echoing it" staged_name_case
check "pre-commit allows removing a denylisted line" removal_case
check "pre-commit warns and allows when the denylist is absent" absent_denylist_case
check "pre-commit ignores a denylist that only has comments and blanks, without a warning" comment_only_case

if [ "$FAILURES" -ne 0 ]; then
  echo "$FAILURES check(s) failed" >&2
  exit 1
fi
echo "all githooks checks passed"
