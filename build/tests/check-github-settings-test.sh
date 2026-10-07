#!/usr/bin/env bash
set -euo pipefail

# Offline test of build/check-github-settings.sh. A stub `gh` answers
# `gh api <path> [--jq <filter>]` from synthetic JSON files written into a
# temporary directory; nothing here ever talks to GitHub.

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SCRIPT="$REPO_ROOT/build/check-github-settings.sh"

WORK_DIR="$(mktemp -d)"
cleanup() {
  rm -rf "$WORK_DIR"
}
trap cleanup EXIT

SLUG="example-owner/example-repo"
STUB_BIN="$WORK_DIR/bin"
mkdir -p "$STUB_BIN"

cat >"$STUB_BIN/gh" <<'STUB'
#!/usr/bin/env bash
set -euo pipefail

if [ "${1:-}" != "api" ]; then
  echo "stub gh: only 'gh api' is supported" >&2
  exit 2
fi
shift

path=""
filter=""
while [ "$#" -gt 0 ]; do
  case "$1" in
    --jq)
      filter="${2:-}"
      shift 2
      ;;
    -X | --method | -f | -F | --input | --field | --raw-field)
      echo "stub gh: refusing a non-read argument '$1'" >&2
      exit 3
      ;;
    -*)
      shift
      ;;
    *)
      path="$1"
      shift
      ;;
  esac
done

path="${path#/}"
path="${path#repos/$STUB_SLUG}"
path="${path#/}"
key="${path//\//__}"
[ -n "$key" ] || key="repo"
file="$STUB_FIXTURES/$key.json"

if [ ! -f "$file" ]; then
  echo "stub gh: HTTP 404 for $path" >&2
  exit 1
fi

if [ -n "$filter" ]; then
  jq -r "$filter" "$file"
else
  cat "$file"
fi
STUB
chmod +x "$STUB_BIN/gh"

write_good_fixtures() {
  local dir="$1"
  rm -rf "$dir"
  mkdir -p "$dir"

  cat >"$dir/rulesets.json" <<'JSON'
[
  { "id": 101, "name": "release tags", "target": "tag" },
  { "id": 102, "name": "main protection", "target": "branch" }
]
JSON

  cat >"$dir/rulesets__101.json" <<'JSON'
{
  "id": 101,
  "name": "release tags",
  "target": "tag",
  "enforcement": "active",
  "conditions": { "ref_name": { "include": ["refs/tags/v*"], "exclude": [] } },
  "rules": [ { "type": "creation" }, { "type": "update" }, { "type": "deletion" } ],
  "bypass_actors": [ { "actor_type": "RepositoryRole", "actor_id": 5, "bypass_mode": "always" } ]
}
JSON

  cat >"$dir/rulesets__102.json" <<'JSON'
{
  "id": 102,
  "name": "main protection",
  "target": "branch",
  "enforcement": "active",
  "bypass_actors": [],
  "conditions": { "ref_name": { "include": ["~DEFAULT_BRANCH"], "exclude": [] } },
  "rules": [
    { "type": "deletion" },
    { "type": "non_fast_forward" },
    {
      "type": "pull_request",
      "parameters": {
        "required_approving_review_count": 0,
        "dismiss_stale_reviews_on_push": true,
        "require_code_owner_review": false,
        "require_last_push_approval": false,
        "required_review_thread_resolution": false,
        "allowed_merge_methods": ["merge", "squash"]
      }
    },
    {
      "type": "required_status_checks",
      "parameters": {
        "strict_required_status_checks_policy": false,
        "required_status_checks": [
          { "context": "build-test", "integration_id": 1 },
          { "context": "lint", "integration_id": 1 }
        ]
      }
    }
  ]
}
JSON

  cat >"$dir/repo.json" <<'JSON'
{
  "allow_merge_commit": true,
  "allow_squash_merge": true,
  "allow_rebase_merge": false,
  "security_and_analysis": {
    "secret_scanning": { "status": "enabled" },
    "secret_scanning_push_protection": { "status": "enabled" },
    "dependabot_security_updates": { "status": "enabled" }
  }
}
JSON

  cat >"$dir/environments__deploy.json" <<'JSON'
{
  "name": "deploy",
  "can_admins_bypass": false,
  "protection_rules": [
    { "id": 1, "type": "required_reviewers", "prevent_self_review": false, "reviewers": [ { "type": "User", "reviewer": { "id": 1 } } ] },
    { "id": 2, "type": "branch_policy" }
  ],
  "deployment_branch_policy": { "protected_branches": false, "custom_branch_policies": true }
}
JSON

  cat >"$dir/environments__deploy__deployment-branch-policies.json" <<'JSON'
{ "total_count": 1, "branch_policies": [ { "id": 1, "name": "v*.*.*", "type": "tag" } ] }
JSON

  echo '{ "approval_policy": "all_external_contributors" }' >"$dir/actions__permissions__fork-pr-contributor-approval.json"
  echo '{ "default_workflow_permissions": "read", "can_approve_pull_request_reviews": false }' >"$dir/actions__permissions__workflow.json"
  echo '{ "enabled": true, "allowed_actions": "all", "sha_pinning_required": true }' >"$dir/actions__permissions.json"
  : >"$dir/vulnerability-alerts.json"
  echo '{ "enabled": true, "enforced_by_owner": false }' >"$dir/immutable-releases.json"
  echo '{ "total_count": 0, "runners": [] }' >"$dir/actions__runners.json"
}

mutate() {
  local dir="$1" file="$2" filter="$3"
  local tmp="$dir/.mutate.tmp"
  jq "$filter" "$dir/$file" >"$tmp"
  mv "$tmp" "$dir/$file"
}

STDOUT_FILE="$WORK_DIR/stdout"

run_checker() {
  local dir="$1"
  local status=0
  PATH="$STUB_BIN:$PATH" STUB_SLUG="$SLUG" STUB_FIXTURES="$dir" CABINET_GITHUB_REPO="$SLUG" \
    "$SCRIPT" >"$STDOUT_FILE" 2>&1 || status=$?
  return "$status"
}

assert_only_failure() {
  local desc="$1" expected_label="$2"
  local fail_count
  fail_count="$(grep -c '^FAIL: ' "$STDOUT_FILE" || true)"
  if [ "$fail_count" -ne 1 ]; then
    echo "FAIL ($desc): expected exactly one FAIL line, got $fail_count" >&2
    cat "$STDOUT_FILE" >&2
    exit 1
  fi
  if ! grep -q "^FAIL: .*$expected_label" "$STDOUT_FILE"; then
    echo "FAIL ($desc): the failing line does not mention '$expected_label'" >&2
    cat "$STDOUT_FILE" >&2
    exit 1
  fi
}

CASE_DIR="$WORK_DIR/fixtures"

echo "=== all controls as intended ==="
write_good_fixtures "$CASE_DIR"
if ! run_checker "$CASE_DIR"; then
  echo "FAIL (all good): expected exit 0" >&2
  cat "$STDOUT_FILE" >&2
  exit 1
fi
pass_count="$(grep -c '^PASS: ' "$STDOUT_FILE" || true)"
if [ "$pass_count" -ne 14 ] || grep -q '^FAIL: ' "$STDOUT_FILE"; then
  echo "FAIL (all good): expected 14 PASS lines and no FAIL line, got $pass_count" >&2
  cat "$STDOUT_FILE" >&2
  exit 1
fi
echo "PASS: all controls as intended"

expect_failure() {
  local desc="$1" expected_label="$2" file="$3" filter="$4"
  echo "=== $desc ==="
  write_good_fixtures "$CASE_DIR"
  if [ -n "$file" ]; then
    mutate "$CASE_DIR" "$file" "$filter"
  fi
  if run_checker "$CASE_DIR"; then
    echo "FAIL ($desc): expected a non-zero exit" >&2
    cat "$STDOUT_FILE" >&2
    exit 1
  fi
  assert_only_failure "$desc" "$expected_label"
  echo "PASS: $desc"
}

expect_failure "main ruleset disabled" "main branch ruleset" \
  rulesets__102.json '.enforcement = "disabled"'

expect_failure "main ruleset without the lint check" "main branch ruleset" \
  rulesets__102.json '(.rules[] | select(.type == "required_status_checks") | .parameters.required_status_checks) |= map(select(.context != "lint"))'

expect_failure "main ruleset with an extra required check" "main branch ruleset" \
  rulesets__102.json '(.rules[] | select(.type == "required_status_checks") | .parameters.required_status_checks) += [{"context": "other", "integration_id": 1}]'

expect_failure "main ruleset requiring one approval" "main branch ruleset" \
  rulesets__102.json '(.rules[] | select(.type == "pull_request") | .parameters.required_approving_review_count) = 1'

expect_failure "main ruleset allowing rebase" "main branch ruleset" \
  rulesets__102.json '(.rules[] | select(.type == "pull_request") | .parameters.allowed_merge_methods) += ["rebase"]'

expect_failure "main ruleset without force-push protection" "main branch ruleset" \
  rulesets__102.json '.rules |= map(select(.type != "non_fast_forward"))'

expect_failure "main ruleset with a bypass actor" "main branch ruleset" \
  rulesets__102.json '.bypass_actors = [{"actor_type": "RepositoryRole", "actor_id": 5, "bypass_mode": "always"}]'

expect_failure "main ruleset missing entirely" "main branch ruleset" \
  rulesets.json 'map(select(.target != "branch"))'

expect_failure "repository allows rebase merges" "merge methods" \
  repo.json '.allow_rebase_merge = true'

expect_failure "repository forbids merge commits" "merge methods" \
  repo.json '.allow_merge_commit = false'

expect_failure "deploy environment without a required reviewer" "deploy environment requires" \
  environments__deploy.json '.protection_rules |= map(select(.type != "required_reviewers"))'

expect_failure "deploy environment without the tag policy" "deployment policy" \
  environments__deploy__deployment-branch-policies.json '.branch_policies = []'

expect_failure "deploy environment lets administrators bypass" "let administrators bypass" \
  environments__deploy.json '.can_admins_bypass = true'

expect_failure "deploy environment does not report the bypass setting" "let administrators bypass" \
  environments__deploy.json 'del(.can_admins_bypass)'

expect_failure "a self-hosted runner is registered" "self-hosted runners" \
  actions__runners.json '.runners = [{"id": 1, "name": "synthetic-runner"}]'

expect_failure "immutable releases are off" "immutable releases" \
  immutable-releases.json '.enabled = false'

echo "=== deploy environment missing ==="
write_good_fixtures "$CASE_DIR"
rm -f "$CASE_DIR/environments__deploy.json" "$CASE_DIR/environments__deploy__deployment-branch-policies.json"
if run_checker "$CASE_DIR"; then
  echo "FAIL (deploy environment missing): expected a non-zero exit" >&2
  exit 1
fi
if [ "$(grep -c '^FAIL: ' "$STDOUT_FILE" || true)" -ne 3 ]; then
  echo "FAIL (deploy environment missing): expected all three environment checks to fail" >&2
  cat "$STDOUT_FILE" >&2
  exit 1
fi
echo "PASS: deploy environment missing"

echo "=== the checker never mutates anything ==="
if grep -nE 'gh api (-X|--method) (POST|PUT|PATCH|DELETE)' "$SCRIPT" >/dev/null; then
  echo "FAIL: the checker contains a mutating gh api call" >&2
  exit 1
fi
echo "PASS: the checker contains no mutating gh api call"

echo "All check-github-settings.sh cases passed."
