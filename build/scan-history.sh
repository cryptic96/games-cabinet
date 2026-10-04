#!/usr/bin/env bash
# Scans the complete git history of a repository for personal data.
#
# Every commit reachable from any ref is checked: file contents, file names,
# commit messages, author and committer identities, absolute local paths and
# secrets (gitleaks). The denylist is read at run time from a private file kept
# outside the repository and is never copied or printed; findings are reported
# as commit id, path, line number and denylist line number only.
#
# Usage: build/scan-history.sh [--repo PATH] [--denylist PATH]
#
# The denylist holds one fixed string per line. Blank lines and lines starting
# with '#' are ignored. Matching is case-insensitive. The default location is
# ${CABINET_DENYLIST_FILE:-${XDG_CONFIG_HOME:-$HOME/.config}/games-cabinet/denylist.txt}.
#
# Exit codes: 0 every check passed, 1 at least one finding, 2 usage error or
# missing denylist.
set -euo pipefail

GITLEAKS_IMAGE='ghcr.io/gitleaks/gitleaks:v8.30.1@sha256:c00b6bd0aeb3071cbcb79009cb16a60dd9e0a7c60e2be9ab65d25e6bc8abbb7f'
NOREPLY_SUFFIX='@users.noreply.github.com'
WEB_FLOW_EMAIL='noreply@github.com'
MAX_DETAIL_LINES=40

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$SCRIPT_DIR/.." && pwd)"
DENYLIST="${CABINET_DENYLIST_FILE:-${XDG_CONFIG_HOME:-$HOME/.config}/games-cabinet/denylist.txt}"

usage() {
  echo "usage: build/scan-history.sh [--repo PATH] [--denylist PATH]" >&2
}

while [ "$#" -gt 0 ]; do
  case "$1" in
    --repo)
      [ "$#" -ge 2 ] || { usage; exit 2; }
      REPO="$2"
      shift 2
      ;;
    --denylist)
      [ "$#" -ge 2 ] || { usage; exit 2; }
      DENYLIST="$2"
      shift 2
      ;;
    -h | --help)
      usage
      exit 0
      ;;
    *)
      usage
      exit 2
      ;;
  esac
done

if [ ! -r "$DENYLIST" ] || [ ! -f "$DENYLIST" ]; then
  echo "error: denylist file is missing or unreadable: $DENYLIST" >&2
  exit 2
fi

if ! git -C "$REPO" rev-parse --git-dir >/dev/null 2>&1; then
  echo "error: not a git repository: $REPO" >&2
  exit 2
fi

umask 077
WORK_DIR="$(mktemp -d)"
PATTERN_FILE="$WORK_DIR/patterns"

trap 'rm -rf "$WORK_DIR"' EXIT

PATTERNS=()
PATTERN_LINES=()
line_number=0
while IFS= read -r raw || [ -n "$raw" ]; do
  line_number=$((line_number + 1))
  raw="${raw%$'\r'}"
  raw="${raw#"${raw%%[![:space:]]*}"}"
  raw="${raw%"${raw##*[![:space:]]}"}"
  case "$raw" in
    '' | '#'*) continue ;;
  esac
  PATTERNS+=("$raw")
  PATTERN_LINES+=("$line_number")
  printf '%s\n' "$raw" >>"$PATTERN_FILE"
done <"$DENYLIST"

if [ "${#PATTERNS[@]}" -eq 0 ]; then
  echo "error: denylist file has no patterns: $DENYLIST" >&2
  exit 2
fi

git_repo() {
  git -C "$REPO" "$@"
}

mapfile -t COMMITS < <(git_repo rev-list --all)

OVERALL=0

matching_lines() {
  local text="$1" i found=""
  for ((i = 0; i < ${#PATTERNS[@]}; i++)); do
    if grep -q -i -F -e "${PATTERNS[i]}" <<<"$text"; then
      found+="${PATTERN_LINES[i]} "
    fi
  done
  printf '%s' "${found% }"
}

report() {
  local name="$1"
  shift
  local findings=("$@")
  local shown=0 total="${#findings[@]}" finding
  if [ "$total" -eq 0 ]; then
    echo "PASS $name"
    return 0
  fi
  echo "FAIL $name ($total findings)"
  for finding in "${findings[@]}"; do
    if [ "$shown" -ge "$MAX_DETAIL_LINES" ]; then
      echo "  ... $((total - shown)) more"
      break
    fi
    echo "  $finding"
    shown=$((shown + 1))
  done
  OVERALL=1
}

check_denylist_trees() {
  local findings=() commit hits names text
  for commit in "${COMMITS[@]}"; do
    hits="$(git_repo grep -I -i -F -l -f "$PATTERN_FILE" "$commit" -- 2>/dev/null || true)"
    if [ -n "$hits" ]; then
      while IFS= read -r hit; do
        [ -n "$hit" ] || continue
        text="$(git_repo grep -I -i -F -h -f "$PATTERN_FILE" "$commit" -- "${hit#*:}" 2>/dev/null || true)"
        findings+=("$hit denylist line(s) $(matching_lines "$text")")
      done <<<"$hits"
    fi
    names="$(git_repo ls-tree -r --name-only "$commit" | grep -i -F -f "$PATTERN_FILE" || true)"
    if [ -n "$names" ]; then
      findings+=("$commit a file name matches denylist line(s) $(matching_lines "$names")")
    fi
  done
  report denylist-trees "${findings[@]}"
}

check_denylist_messages() {
  local findings=() commit text ref index=0 refs
  for commit in "${COMMITS[@]}"; do
    text="$(git_repo log -1 --format=%B "$commit")"
    if grep -q -i -F -f "$PATTERN_FILE" <<<"$text"; then
      findings+=("$commit message matches denylist line(s) $(matching_lines "$text")")
    fi
  done
  refs="$(git_repo for-each-ref --format='%(refname)')"
  while IFS= read -r ref; do
    [ -n "$ref" ] || continue
    index=$((index + 1))
    if grep -q -i -F -f "$PATTERN_FILE" <<<"$ref"; then
      findings+=("ref number $index name matches denylist line(s) $(matching_lines "$ref")")
    fi
  done <<<"$refs"
  while IFS= read -r ref; do
    [ -n "$ref" ] || continue
    text="$(git_repo cat-file -p "$ref")"
    if grep -q -i -F -f "$PATTERN_FILE" <<<"$text"; then
      findings+=("annotated tag object ${ref:0:12} matches denylist line(s) $(matching_lines "$text")")
    fi
  done < <(git_repo for-each-ref --format='%(if:equals=tag)%(objecttype)%(then)%(objectname)%(end)' refs/tags)
  report denylist-messages "${findings[@]}"
}

check_denylist_identities() {
  local findings=() commit an ae cn ce text
  while IFS=$'\t' read -r commit an ae cn ce; do
    text="$an"$'\n'"$ae"$'\n'"$cn"$'\n'"$ce"
    if grep -q -i -F -f "$PATTERN_FILE" <<<"$text"; then
      findings+=("$commit identity matches denylist line(s) $(matching_lines "$text")")
    fi
  done < <(git_repo log --all --format='%H%x09%an%x09%ae%x09%cn%x09%ce')
  report denylist-identities "${findings[@]}"
}

check_noreply_identities() {
  local findings=() commit ae ce
  while IFS=$'\t' read -r commit ae ce; do
    ae="${ae,,}"
    ce="${ce,,}"
    if [[ "$ae" != *"$NOREPLY_SUFFIX" ]]; then
      findings+=("$commit author email is not a GitHub noreply address")
    fi
    if [[ "$ce" != *"$NOREPLY_SUFFIX" && "$ce" != "$WEB_FLOW_EMAIL" ]]; then
      findings+=("$commit committer email is not a GitHub noreply address")
    fi
  done < <(git_repo log --all --format='%H%x09%ae%x09%ce')
  report noreply-identities "${findings[@]}"
}

check_absolute_paths() {
  local findings=() commit hits
  local patterns=(
    -e '/hom[e]/[^/[:space:]]+/'
    -e '/mn[t]/[^/[:space:]]+/'
    -e '/medi[a]/[^/[:space:]]+/'
    -e '/User[s]/[^/[:space:]]+/'
    -e '[A-Za-z]:[\\]User[s][\\]'
  )
  for commit in "${COMMITS[@]}"; do
    hits="$(git_repo grep -I -n -E "${patterns[@]}" "$commit" -- 2>/dev/null | cut -d: -f1-3 || true)"
    if [ -n "$hits" ]; then
      while IFS= read -r hit; do
        [ -n "$hit" ] && findings+=("$hit absolute local path")
      done <<<"$hits"
    fi
  done
  report absolute-paths "${findings[@]}"
}

run_gitleaks() {
  local dir="$1" config_args=() status=0
  if [ -f "$dir/.gitleaks.toml" ]; then
    config_args=(--config /repo/.gitleaks.toml)
  fi
  docker run --rm --network none \
    --user "$(id -u):$(id -g)" -e HOME=/tmp \
    --mount "type=bind,source=$dir,target=/repo,readonly" \
    --entrypoint /usr/bin/gitleaks "$GITLEAKS_IMAGE" \
    git "${config_args[@]}" --redact --no-banner --exit-code 10 \
    --log-opts=--all /repo >"$WORK_DIR/gitleaks.out" 2>&1 || status=$?
  return "$status"
}

check_gitleaks() {
  local findings=() status=0 repo_dir
  if ! command -v docker >/dev/null 2>&1; then
    findings+=("docker is not available, so gitleaks could not run")
    report gitleaks-all-refs "${findings[@]}"
    return
  fi
  repo_dir="$(git_repo rev-parse --absolute-git-dir)"
  if [ "$(git_repo rev-parse --is-bare-repository)" = "false" ]; then
    repo_dir="$(git_repo rev-parse --show-toplevel)"
  fi
  run_gitleaks "$repo_dir" || status=$?
  if [ "$status" -ne 0 ] && [ "$status" -ne 10 ]; then
    local clone_dir="$WORK_DIR/clone"
    git clone --quiet --no-local "$REPO" "$clone_dir"
    status=0
    run_gitleaks "$clone_dir" || status=$?
  fi
  case "$status" in
    0) ;;
    10) findings+=("gitleaks reported leaks over all refs; run it directly for the redacted report") ;;
    *) findings+=("gitleaks could not complete (exit $status)") ;;
  esac
  report gitleaks-all-refs "${findings[@]}"
}

check_denylist_trees
check_denylist_messages
check_denylist_identities
check_noreply_identities
check_absolute_paths
check_gitleaks

exit "$OVERALL"
