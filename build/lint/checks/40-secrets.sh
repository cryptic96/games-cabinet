#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$REPO_ROOT"

# shellcheck source=build/lint/lib.sh
source "$REPO_ROOT/build/lint/lib.sh"

GITLEAKS_CONFIG="$REPO_ROOT/.gitleaks.toml"

random_lower_letters() {
  local count="$1" result="" i idx
  local alphabet="abcdefghijklmnopqrstuvwxyz"
  for ((i = 0; i < count; i++)); do
    idx=$((RANDOM % 26))
    result+="${alphabet:idx:1}"
  done
  printf '%s' "$result"
}

fake_github_token() {
  printf 'ghp_%s' "$(LC_ALL=C tr -dc 'A-Za-z0-9' </dev/urandom | head -c 36)"
}

fake_private_ipv4() {
  printf '192.168.%d.%d' "$((RANDOM % 256))" "$((1 + RANDOM % 254))"
}

fake_non_example_email() {
  printf '%s@%s.nl' "$(random_lower_letters 8)" "$(random_lower_letters 6)"
}

fake_internal_hostname() {
  local suffix="$1"
  printf '%s.%s' "$(random_lower_letters 7)" "$suffix"
}

gitleaks_git_mode() {
  local dir="$1"
  if lint_compose -v "$dir:/repo:ro" gitleaks detect --source /repo --config /repo/.gitleaks.toml --redact --no-banner --log-opts="--all" >/dev/null 2>&1; then
    return 0
  fi
  return 1
}

gitleaks_dir_mode() {
  local dir="$1"
  if lint_compose -v "$dir:/repo:ro" gitleaks detect --no-git --source /repo --config /repo/.gitleaks.toml --redact --no-banner >/dev/null 2>&1; then
    return 0
  fi
  return 1
}

assert_not_shallow() {
  local dir="$1"
  local is_shallow
  is_shallow="$(git -C "$dir" rev-parse --is-shallow-repository)"
  [ "$is_shallow" = "false" ]
}

make_throwaway_repo() {
  local dir="$1"
  mkdir -p "$dir"
  git -C "$dir" init -q
  git -C "$dir" config user.email "lint-self-test@example.com"
  git -C "$dir" config user.name "lint self-test"
  git -C "$dir" config commit.gpgsign false
  git -C "$dir" config core.hooksPath /dev/null
  cp "$GITLEAKS_CONFIG" "$dir/.gitleaks.toml"
}

commit_notes_repo() {
  local dir="$1"
  git -C "$dir" add notes.txt .gitleaks.toml
  git -C "$dir" commit -q -m "notes"
}

assert_notes_rejected() {
  local description="$1" content="$2"
  local repo
  repo="$(mktemp -d)"
  make_throwaway_repo "$repo"
  printf '%s\n' "$content" >"$repo/notes.txt"
  commit_notes_repo "$repo"
  local ok=0
  if gitleaks_git_mode "$repo"; then
    echo "self-test failed: ${description} was not detected" >&2
    ok=1
  fi
  rm -rf "$repo"
  return "$ok"
}

assert_fixture_rejected() {
  local description="$1" content="$2"
  local repo
  repo="$(mktemp -d)"
  make_throwaway_repo "$repo"
  mkdir -p "$repo/deploy/tests/fixtures"
  printf '%s\n' "$content" >"$repo/deploy/tests/fixtures/data.txt"
  git -C "$repo" add deploy/tests/fixtures/data.txt .gitleaks.toml
  git -C "$repo" commit -q -m "fixture"
  local ok=0
  if gitleaks_git_mode "$repo"; then
    echo "self-test failed: ${description} in a test fixture was not detected" >&2
    ok=1
  fi
  rm -rf "$repo"
  return "$ok"
}

assert_notes_accepted() {
  local description="$1" content="$2"
  local repo
  repo="$(mktemp -d)"
  make_throwaway_repo "$repo"
  printf '%s\n' "$content" >"$repo/notes.txt"
  commit_notes_repo "$repo"
  local ok=0
  if ! gitleaks_git_mode "$repo"; then
    echo "self-test failed: ${description} was rejected in git mode" >&2
    ok=1
  fi
  if ! gitleaks_dir_mode "$repo"; then
    echo "self-test failed: ${description} was rejected in dir mode" >&2
    ok=1
  fi
  rm -rf "$repo"
  return "$ok"
}

self_test() {
  local failed=0

  assert_notes_accepted "loopback, documentation, example and noreply content" \
    "$(printf 'loopback address: 127.0.0.1\ndocumentation address: 192.0.2.10\ncontact: someone@example.com\nnoreply: someone@users.noreply.github.com\nweb flow: noreply@github.com\n')" || failed=1

  local json_name
  json_name="$(printf 'settings file: appsettings%sjson' '.local.')"
  assert_notes_accepted "a local settings file name" "$json_name" || failed=1

  local token_repo
  token_repo="$(mktemp -d)"
  make_throwaway_repo "$token_repo"
  fake_github_token >"$token_repo/secret.txt"
  git -C "$token_repo" add secret.txt .gitleaks.toml
  git -C "$token_repo" commit -q -m "add token"
  git -C "$token_repo" rm -q secret.txt
  git -C "$token_repo" commit -q -m "remove token"

  if gitleaks_git_mode "$token_repo"; then
    echo "self-test failed: a token added then deleted in history was not detected" >&2
    failed=1
  fi

  local side_repo
  side_repo="$(mktemp -d)"
  make_throwaway_repo "$side_repo"
  printf 'clean\n' >"$side_repo/notes.txt"
  commit_notes_repo "$side_repo"
  git -C "$side_repo" checkout -q -b side-branch
  fake_github_token >"$side_repo/secret.txt"
  git -C "$side_repo" add secret.txt
  git -C "$side_repo" commit -q -m "add token on a side branch"
  git -C "$side_repo" checkout -q -
  if gitleaks_git_mode "$side_repo"; then
    echo "self-test failed: a token that exists only on a non-checked-out branch was not detected" >&2
    failed=1
  fi
  rm -rf "$side_repo"

  local shallow_dir
  shallow_dir="$(mktemp -d)"
  if git clone -q --depth 1 "file://$token_repo" "$shallow_dir" 2>/dev/null; then
    if assert_not_shallow "$shallow_dir"; then
      echo "self-test failed: a shallow clone was not detected as shallow" >&2
      failed=1
    fi
  else
    echo "self-test failed: could not create a shallow clone to test against" >&2
    failed=1
  fi
  rm -rf "$shallow_dir" "$token_repo"

  assert_notes_rejected "a generated 192.168.x.y address" "server: $(fake_private_ipv4)" || failed=1
  assert_notes_rejected "a generated non-example-domain email" "contact: $(fake_non_example_email)" || failed=1

  assert_fixture_rejected "a generated 192.168.x.y address" "server: $(fake_private_ipv4)" || failed=1
  assert_fixture_rejected "a generated non-example-domain email" "contact: $(fake_non_example_email)" || failed=1
  assert_fixture_rejected "a generated internal hostname" "host: $(fake_internal_hostname lan)" || failed=1

  local suffix
  for suffix in lan internal home.arpa local; do
    assert_notes_rejected "a generated internal hostname under .${suffix} (end of line)" \
      "host: $(fake_internal_hostname "$suffix")" || failed=1
  done
  assert_notes_rejected "a generated internal hostname followed by a port" \
    "url: $(fake_internal_hostname lan):8080" || failed=1
  assert_notes_rejected "a generated internal hostname followed by a slash" \
    "url: https://$(fake_internal_hostname local)/path" || failed=1

  return "$failed"
}

if ! self_test; then
  echo "FAIL: 40-secrets self-test did not behave as expected" >&2
  exit 1
fi
echo "40-secrets self-tests passed"

if ! assert_not_shallow "$REPO_ROOT"; then
  echo "refusing to scan a shallow clone; fetch full history first (git fetch --unshallow)" >&2
  exit 1
fi

status=0

echo "Running gitleaks over the full git history (every ref)"
if ! lint_compose gitleaks detect --source /repo --config .gitleaks.toml --redact --no-banner --log-opts="--all"; then
  status=1
fi

echo "Running gitleaks over the working tree"
if ! lint_compose gitleaks detect --no-git --source /repo --config .gitleaks.toml --redact --no-banner; then
  status=1
fi

exit "$status"
