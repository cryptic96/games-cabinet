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

stage_binary() {
  local dir="$1" path="$2" value="$3"
  mkdir -p "$dir/$(dirname "$path")"
  printf 'IMG\000\000\001meta %s\000\000tail\n' "$value" >"$dir/$path"
  git -C "$dir" add -- "$path"
}

staged_binary_case() {
  local dir="$WORK_DIR/block-binary"
  new_repo "$dir"
  stage_binary "$dir" images/shot.png "${SYNTH_ONE^^}"
  commit_with_hooks "$dir" "$DENYLIST" "add image"
  status_is_nonzero &&
    stderr_contains 'images/shot.png:' &&
    stderr_contains 'denylist match #1' &&
    stderr_lacks_synthetic
}

staged_clean_binary_case() {
  local dir="$WORK_DIR/allow-binary"
  new_repo "$dir"
  stage_binary "$dir" images/shot.png "an ordinary value"
  commit_with_hooks "$dir" "$DENYLIST" "add image"
  status_is_zero &&
    [ "$(git -C "$dir" rev-list --count HEAD)" -eq 1 ]
}

check "pre-commit refuses a binary file whose bytes match the denylist without echoing it" staged_binary_case
check "pre-commit allows a clean binary file" staged_clean_binary_case
check "pre-commit refuses an added line matching the denylist, case-insensitively, without echoing it" staged_block_case
check "pre-commit allows clean content" staged_clean_case
check "pre-commit refuses a staged file name matching the denylist without echoing it" staged_name_case
check "pre-commit allows removing a denylisted line" removal_case
check "pre-commit warns and allows when the denylist is absent" absent_denylist_case
check "pre-commit ignores a denylist that only has comments and blanks, without a warning" comment_only_case

message_block_case() {
  local dir="$WORK_DIR/msg-block"
  new_repo "$dir"
  stage "$dir" notes/a.txt "an ordinary line"
  commit_with_hooks "$dir" "$DENYLIST" "mention ${SYNTH_TWO^^} here"
  status_is_nonzero &&
    stderr_contains 'commit message:1:' &&
    stderr_contains 'denylist match #5' &&
    stderr_contains 'blocked:' &&
    stderr_lacks_synthetic &&
    ! git -C "$dir" rev-parse --verify --quiet HEAD >/dev/null
}

message_clean_case() {
  local dir="$WORK_DIR/msg-clean"
  new_repo "$dir"
  stage "$dir" notes/a.txt "an ordinary line"
  commit_with_hooks "$dir" "$DENYLIST" "an ordinary message"
  status_is_zero &&
    [ "$(git -C "$dir" rev-list --count HEAD)" -eq 1 ]
}

message_comment_case() {
  local dir="$WORK_DIR/msg-comment"
  new_repo "$dir"
  stage "$dir" notes/a.txt "an ordinary line"
  run_hooked "$dir" "$DENYLIST" git commit --quiet -m "an ordinary message" -m "# note about ${SYNTH_ONE}"
  status_is_zero &&
    [ "$(git -C "$dir" rev-list --count HEAD)" -eq 1 ]
}

message_absent_case() {
  local dir="$WORK_DIR/msg-absent"
  new_repo "$dir"
  stage "$dir" notes/a.txt "an ordinary line"
  commit_with_hooks "$dir" "$MISSING_DENYLIST" "mention ${SYNTH_TWO}"
  status_is_zero &&
    stderr_contains 'warning' &&
    [ "$(git -C "$dir" rev-list --count HEAD)" -eq 1 ]
}

check "commit-msg refuses a message matching the denylist without echoing it" message_block_case
check "commit-msg allows a clean message" message_clean_case
check "commit-msg ignores comment lines" message_comment_case
check "commit-msg warns and allows when the denylist is absent" message_absent_case

REMOTE_DIR="$WORK_DIR/remotes"
mkdir -p "$REMOTE_DIR"

new_repo_with_remote() {
  local dir="$1" remote
  remote="$REMOTE_DIR/$(basename "$1").git"
  new_repo "$dir"
  git init --quiet --bare --initial-branch=main "$remote"
  git -C "$dir" remote add origin "$remote"
  REMOTE_PATH="$remote"
}

remote_has_branch() {
  git -C "$REMOTE_PATH" rev-parse --verify --quiet "refs/heads/$1" >/dev/null
}

push_branch() {
  local dir="$1" denylist="$2" branch="$3"
  run_hooked "$dir" "$denylist" git push --quiet origin "$branch"
}

push_clean_case() {
  local dir="$WORK_DIR/push-clean"
  new_repo_with_remote "$dir"
  stage "$dir" notes/a.txt "an ordinary line"
  commit_with_hooks "$dir" "$DENYLIST" "first"
  push_branch "$dir" "$DENYLIST" main
  status_is_zero && remote_has_branch main || return 1
  stage "$dir" notes/b.txt "another ordinary line"
  commit_with_hooks "$dir" "$DENYLIST" "second"
  push_branch "$dir" "$DENYLIST" main
  status_is_zero &&
    [ "$(git -C "$REMOTE_PATH" rev-list --count main)" -eq 2 ]
}

push_content_case() {
  local dir="$WORK_DIR/push-content"
  new_repo_with_remote "$dir"
  stage "$dir" notes/a.txt "line with ${SYNTH_ONE^^}"
  commit_unchecked "$dir" "innocent message"
  push_branch "$dir" "$DENYLIST" main
  status_is_nonzero &&
    stderr_contains 'notes/a.txt:1:' &&
    stderr_contains 'denylist match #1' &&
    stderr_contains 'blocked: commit' &&
    stderr_lacks_synthetic &&
    ! remote_has_branch main
}

push_incremental_content_case() {
  local dir="$WORK_DIR/push-incremental"
  new_repo_with_remote "$dir"
  stage "$dir" notes/a.txt "an ordinary line"
  commit_with_hooks "$dir" "$DENYLIST" "first"
  push_branch "$dir" "$DENYLIST" main
  status_is_zero || return 1
  stage "$dir" notes/b.txt "line with ${SYNTH_TWO}"
  commit_unchecked "$dir" "second"
  push_branch "$dir" "$DENYLIST" main
  status_is_nonzero &&
    stderr_contains 'notes/b.txt:1:' &&
    [ "$(git -C "$REMOTE_PATH" rev-list --count main)" -eq 1 ]
}

push_message_case() {
  local dir="$WORK_DIR/push-message"
  new_repo_with_remote "$dir"
  stage "$dir" notes/a.txt "an ordinary line"
  commit_unchecked "$dir" "mention ${SYNTH_ONE}"
  push_branch "$dir" "$DENYLIST" main
  status_is_nonzero &&
    stderr_contains 'message:1:' &&
    stderr_contains 'blocked: commit' &&
    stderr_lacks_synthetic &&
    ! remote_has_branch main
}

push_author_email_case() {
  local dir="$WORK_DIR/push-author"
  new_repo_with_remote "$dir"
  stage "$dir" notes/a.txt "an ordinary line"
  GIT_AUTHOR_EMAIL='someone@example.com' commit_unchecked "$dir" "first"
  push_branch "$dir" "$DENYLIST" main
  status_is_nonzero &&
    stderr_contains 'author email is not a GitHub noreply address' &&
    ! remote_has_branch main
}

push_committer_email_case() {
  local dir="$WORK_DIR/push-committer"
  new_repo_with_remote "$dir"
  stage "$dir" notes/a.txt "an ordinary line"
  GIT_COMMITTER_EMAIL='someone@example.com' commit_unchecked "$dir" "first"
  push_branch "$dir" "$DENYLIST" main
  status_is_nonzero &&
    stderr_contains 'committer email is not a GitHub noreply address' &&
    ! remote_has_branch main
}

push_web_flow_committer_case() {
  local dir="$WORK_DIR/push-web-flow"
  new_repo_with_remote "$dir"
  stage "$dir" notes/a.txt "an ordinary line"
  GIT_COMMITTER_EMAIL='noreply@github.com' commit_unchecked "$dir" "first"
  push_branch "$dir" "$DENYLIST" main
  status_is_zero && remote_has_branch main
}

push_merge_case() {
  local dir="$WORK_DIR/push-merge"
  new_repo_with_remote "$dir"
  stage "$dir" notes/a.txt "an ordinary line"
  commit_with_hooks "$dir" "$DENYLIST" "base"
  git -C "$dir" checkout --quiet -b topic
  stage "$dir" notes/topic.txt "line with ${SYNTH_ONE}"
  commit_unchecked "$dir" "topic work"
  push_branch "$dir" "$COMMENT_ONLY_DENYLIST" topic
  status_is_zero || return 1
  git -C "$dir" checkout --quiet main
  stage "$dir" notes/main.txt "main work"
  commit_with_hooks "$dir" "$DENYLIST" "main work"
  git -C "$dir" merge --quiet --no-ff --no-verify -m "merge topic" topic
  push_branch "$dir" "$DENYLIST" main
  status_is_zero && remote_has_branch main
}

push_delete_case() {
  local dir="$WORK_DIR/push-delete"
  new_repo_with_remote "$dir"
  stage "$dir" notes/a.txt "an ordinary line"
  commit_with_hooks "$dir" "$DENYLIST" "first"
  git -C "$dir" checkout --quiet -b topic
  push_branch "$dir" "$DENYLIST" topic
  status_is_zero && remote_has_branch topic || return 1
  git -C "$dir" checkout --quiet main
  stage "$dir" notes/b.txt "line with ${SYNTH_ONE}"
  commit_unchecked "$dir" "unpushed and unchecked"
  run_hooked "$dir" "$DENYLIST" git push --quiet origin --delete topic
  status_is_zero && ! remote_has_branch topic
}

push_absent_case() {
  local dir="$WORK_DIR/push-absent"
  new_repo_with_remote "$dir"
  stage "$dir" notes/a.txt "line with ${SYNTH_ONE}"
  commit_unchecked "$dir" "first"
  push_branch "$dir" "$MISSING_DENYLIST" main
  status_is_zero &&
    stderr_contains 'warning' &&
    remote_has_branch main
}

push_absent_identity_case() {
  local dir="$WORK_DIR/push-absent-identity"
  new_repo_with_remote "$dir"
  stage "$dir" notes/a.txt "an ordinary line"
  GIT_AUTHOR_EMAIL='someone@example.com' commit_unchecked "$dir" "first"
  push_branch "$dir" "$MISSING_DENYLIST" main
  status_is_nonzero &&
    stderr_contains 'warning' &&
    stderr_contains 'author email is not a GitHub noreply address' &&
    ! remote_has_branch main
}

push_binary_case() {
  local dir="$WORK_DIR/push-binary"
  new_repo_with_remote "$dir"
  stage_binary "$dir" images/shot.png "${SYNTH_TWO^^}"
  commit_unchecked "$dir" "innocent message"
  push_branch "$dir" "$DENYLIST" main
  status_is_nonzero &&
    stderr_contains 'images/shot.png:' &&
    stderr_contains 'denylist match #5' &&
    stderr_lacks_synthetic &&
    ! remote_has_branch main
}

remote_has_tag() {
  git -C "$REMOTE_PATH" rev-parse --verify --quiet "refs/tags/$1" >/dev/null
}

push_tag() {
  local dir="$1" denylist="$2" tag="$3"
  run_hooked "$dir" "$denylist" git push --quiet origin "$tag"
}

push_tag_identity_case() {
  local dir="$WORK_DIR/push-tag-identity"
  new_repo_with_remote "$dir"
  stage "$dir" notes/a.txt "an ordinary line"
  commit_with_hooks "$dir" "$DENYLIST" "first"
  GIT_COMMITTER_EMAIL='someone@example.com' git -C "$dir" tag -a -m "release" v1.0.0
  push_tag "$dir" "$DENYLIST" v1.0.0
  status_is_nonzero &&
    stderr_contains 'tagger email is not a GitHub noreply address' &&
    ! remote_has_tag v1.0.0
}

push_tag_identity_absent_denylist_case() {
  local dir="$WORK_DIR/push-tag-identity-absent"
  new_repo_with_remote "$dir"
  stage "$dir" notes/a.txt "an ordinary line"
  commit_with_hooks "$dir" "$DENYLIST" "first"
  GIT_COMMITTER_EMAIL='someone@example.com' git -C "$dir" tag -a -m "release" v1.0.0
  push_tag "$dir" "$MISSING_DENYLIST" v1.0.0
  status_is_nonzero &&
    stderr_contains 'tagger email is not a GitHub noreply address' &&
    ! remote_has_tag v1.0.0
}

push_tag_message_case() {
  local dir="$WORK_DIR/push-tag-message"
  new_repo_with_remote "$dir"
  stage "$dir" notes/a.txt "an ordinary line"
  commit_with_hooks "$dir" "$DENYLIST" "first"
  git -C "$dir" tag -a -m "mention ${SYNTH_ONE}" v1.0.0
  push_tag "$dir" "$DENYLIST" v1.0.0
  status_is_nonzero &&
    stderr_contains 'tag ' &&
    stderr_contains 'denylist match #1' &&
    stderr_lacks_synthetic &&
    ! remote_has_tag v1.0.0
}

push_tag_clean_case() {
  local dir="$WORK_DIR/push-tag-clean"
  new_repo_with_remote "$dir"
  stage "$dir" notes/a.txt "an ordinary line"
  commit_with_hooks "$dir" "$DENYLIST" "first"
  git -C "$dir" tag -a -m "release" v1.0.0
  push_tag "$dir" "$DENYLIST" v1.0.0
  status_is_zero && remote_has_tag v1.0.0
}

push_merge_resolution_case() {
  local dir="$WORK_DIR/push-merge-resolution"
  new_repo_with_remote "$dir"
  stage "$dir" notes/a.txt "base line"
  commit_with_hooks "$dir" "$DENYLIST" "base"
  git -C "$dir" checkout --quiet -b topic
  stage "$dir" notes/a.txt "topic line"
  commit_with_hooks "$dir" "$DENYLIST" "topic work"
  git -C "$dir" checkout --quiet main
  stage "$dir" notes/a.txt "main line"
  commit_with_hooks "$dir" "$DENYLIST" "main work"
  git -C "$dir" merge --quiet --no-verify topic >/dev/null 2>&1 || true
  printf 'resolved with %s\n' "${SYNTH_ONE^^}" >"$dir/notes/a.txt"
  git -C "$dir" add notes/a.txt
  git -C "$dir" commit --quiet --no-verify -m "merge topic"
  push_branch "$dir" "$DENYLIST" main
  status_is_nonzero &&
    stderr_contains 'notes/a.txt:1:' &&
    stderr_contains 'denylist match #1' &&
    stderr_contains 'blocked: commit' &&
    stderr_lacks_synthetic &&
    ! remote_has_branch main
}

push_merge_resolution_clean_case() {
  local dir="$WORK_DIR/push-merge-resolution-clean"
  new_repo_with_remote "$dir"
  stage "$dir" notes/a.txt "base line"
  commit_with_hooks "$dir" "$DENYLIST" "base"
  git -C "$dir" checkout --quiet -b topic
  stage "$dir" notes/a.txt "topic line"
  commit_with_hooks "$dir" "$DENYLIST" "topic work"
  git -C "$dir" checkout --quiet main
  stage "$dir" notes/a.txt "main line"
  commit_with_hooks "$dir" "$DENYLIST" "main work"
  git -C "$dir" merge --quiet --no-verify topic >/dev/null 2>&1 || true
  printf 'resolved line\n' >"$dir/notes/a.txt"
  git -C "$dir" add notes/a.txt
  git -C "$dir" commit --quiet --no-verify -m "merge topic"
  push_branch "$dir" "$DENYLIST" main
  status_is_zero && remote_has_branch main
}

check "pre-push refuses a merge commit whose conflict resolution matches the denylist" push_merge_resolution_case
check "pre-push allows a merge commit with a clean conflict resolution" push_merge_resolution_clean_case
check "pre-push refuses a skipped-hook binary file whose bytes match the denylist" push_binary_case
check "pre-push refuses an annotated tag whose tagger email is not a noreply address" push_tag_identity_case
check "pre-push still checks the tagger email when the denylist is absent" push_tag_identity_absent_denylist_case
check "pre-push refuses an annotated tag whose message matches the denylist" push_tag_message_case
check "pre-push allows an annotated tag with a noreply tagger" push_tag_clean_case
check "pre-push allows clean commits, including an incremental push" push_clean_case
check "pre-push refuses a skipped-hook commit whose added lines match the denylist" push_content_case
check "pre-push scans only the commits not yet on the remote" push_incremental_content_case
check "pre-push refuses a commit whose message matches the denylist" push_message_case
check "pre-push refuses a non-noreply author email" push_author_email_case
check "pre-push refuses a non-noreply committer email" push_committer_email_case
check "pre-push accepts the GitHub web-flow committer address" push_web_flow_committer_case
check "pre-push does not diff-scan merge commits" push_merge_case
check "pre-push allows a branch deletion without scanning" push_delete_case
check "pre-push warns and allows content when the denylist is absent" push_absent_case
check "pre-push still enforces noreply identities when the denylist is absent" push_absent_identity_case

if [ "$FAILURES" -ne 0 ]; then
  echo "$FAILURES check(s) failed" >&2
  exit 1
fi
echo "all githooks checks passed"
