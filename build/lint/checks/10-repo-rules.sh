#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$REPO_ROOT"

# Each pattern below is written so its own source line cannot match the thing
# it detects (grouped alternations keep a shared suffix outside every branch;
# the directory patterns bracket a single letter to break contiguity).
REQUIREMENT_KEY_PATTERN='\b(A11Y|CABX?|DET|EXP|FILT|I18N|IMG|LOC|NIGHT|OPSX?|OWN|SEC|SHARE|SYNC)-[0-9]{2}\b'
DECISION_ID_PATTERN='\bD-[0-9]{2}\b'
PHASE_WORD_PATTERN='\bphase[-_ ]?([0-9]+|one|two|three|four|five|six|seven|eight|nine|ten)\b'
PLAN_WAVE_PATTERN='\b(plan|wave|milestone)[-_ ]?[0-9]+\b'
REVIEW_FINDING_PATTERN='\b(CR|WR|IN|BL)-[0-9]{2}\b'
PLAN_NUMBER_PATTERN='(^|[^0-9A-Za-z:-])[0-9]{2}-[0-9]{2}([^0-9A-Za-z:-]|$)'
PLANNING_FILE_PATTERN='\b(PROJECT|REQUIREMENTS|ROADMAP|STATE|RESEARCH|CONTEXT|PLAN|SPEC)\.md\b'
PLANNING_DIR_PATTERN='\.plannin[g]/'
CLAUDE_DIR_PATTERN='\.claud[e]/'
EXCLUDE_PATH_PATTERN="^${PLANNING_DIR_PATTERN}|^${CLAUDE_DIR_PATTERN}"
VENDORED_JS_PATTERN='^Cabinet\.Service/wwwroot/lib/'

# A // that is not part of a /// doc comment and does not follow a colon
# (URLs such as https://) is a line comment, wherever it sits on the line.
CS_LINE_COMMENT_PATTERN='(^|[^/:])//([^/]|$)'

# A /* that opens a block comment follows whitespace, a statement or a bracket, or starts the
# line; one inside a string or an address (http://*:80, image/*) follows another character.
CS_BLOCK_COMMENT_PATTERN='(^|[[:space:];{}()])/\*'

# In JavaScript every // not following a colon is a line comment (doc blocks
# are the only allowed comment form), and a block comment must open with /**.
JS_LINE_COMMENT_PATTERN='(^|[^:])//'
JS_PLAIN_BLOCK_COMMENT_PATTERN='/\*([^*]|$)'

PAGE_SCRIPT_PATTERN='^Cabinet\.Service/wwwroot/js/'

# Page scripts build the DOM from elements and text nodes only. The APIs that parse markup or
# style text from a string are refused so a visitor-controlled value can never become markup.
JS_MARKUP_API_PATTERN='\b(inner|outer)HTM[L]\b|insertAdjacentHTM[L]|document\.writ[e]|\.cssTex[t]'

RUNS_ON_ALLOWED='ubuntu-26.04'

LICENSE_FIRST_LINE='MIT License'
LICENSE_COPYRIGHT_LINE='Copyright (c) 2026 cryptic96'

assert_clean_planning_references() {
  local -a files=("$@")
  [ "${#files[@]}" -eq 0 ] && return 0
  local pattern
  local violations=0
  for pattern in "$REQUIREMENT_KEY_PATTERN" "$DECISION_ID_PATTERN" "$PHASE_WORD_PATTERN" "$PLAN_WAVE_PATTERN"; do
    if grep -niE "$pattern" "${files[@]}" 2>/dev/null; then
      violations=1
    fi
  done
  for pattern in "$REVIEW_FINDING_PATTERN" "$PLAN_NUMBER_PATTERN" "$PLANNING_FILE_PATTERN" "$PLANNING_DIR_PATTERN"; do
    if grep -nE "$pattern" "${files[@]}" 2>/dev/null; then
      violations=1
    fi
  done
  [ "$violations" -eq 0 ]
}

assert_clean_cs_comments() {
  local -a files=("$@")
  [ "${#files[@]}" -eq 0 ] && return 0
  local violations=0
  if grep -nE "$CS_LINE_COMMENT_PATTERN" "${files[@]}" 2>/dev/null; then
    violations=1
  fi
  if grep -nE "$CS_BLOCK_COMMENT_PATTERN" "${files[@]}" 2>/dev/null; then
    violations=1
  fi
  [ "$violations" -eq 0 ]
}

# Drops vendored third-party scripts (paths under Cabinet.Service/wwwroot/lib/) from a list of
# file names read on standard input. Only the JavaScript comment rule uses it: the planning
# reference check still reads every tracked file, vendored ones included.
filter_vendored_js() {
  grep -vE "$VENDORED_JS_PATTERN" || true
}

assert_clean_js_comments() {
  local -a files=("$@")
  [ "${#files[@]}" -eq 0 ] && return 0
  local violations=0
  if grep -nE "$JS_LINE_COMMENT_PATTERN" "${files[@]}" 2>/dev/null; then
    violations=1
  fi
  if grep -nE "$JS_PLAIN_BLOCK_COMMENT_PATTERN" "${files[@]}" 2>/dev/null; then
    violations=1
  fi
  [ "$violations" -eq 0 ]
}

filter_page_scripts() {
  grep -E "$PAGE_SCRIPT_PATTERN" || true
}

assert_no_markup_apis() {
  local -a files=("$@")
  [ "${#files[@]}" -eq 0 ] && return 0
  if grep -nE "$JS_MARKUP_API_PATTERN" "${files[@]}" 2>/dev/null; then
    return 1
  fi
  return 0
}

assert_clean_runs_on() {
  local dir="$1"
  shopt -s nullglob
  local files=("$dir"/*.yml "$dir"/*.yaml)
  shopt -u nullglob
  [ "${#files[@]}" -eq 0 ] && return 0
  local bad=0
  local f line value
  for f in "${files[@]}"; do
    while IFS= read -r line; do
      value="$(printf '%s' "$line" | sed -E 's/^[[:space:]]*runs-on:[[:space:]]*//; s/[[:space:]]*$//; s/^"//; s/"$//')"
      if [ "$value" != "$RUNS_ON_ALLOWED" ]; then
        echo "$f: disallowed runs-on value: $value" >&2
        bad=1
      fi
    done < <(grep -hE '^[[:space:]]*runs-on:' "$f" || true)
  done
  return "$bad"
}

assert_valid_license() {
  local file="$1"
  if [ ! -f "$file" ]; then
    echo "$file: licence file is missing" >&2
    return 1
  fi
  local first_line
  first_line="$(head -n 1 "$file")"
  if [ "$first_line" != "$LICENSE_FIRST_LINE" ]; then
    echo "$file: first line must be exactly: $LICENSE_FIRST_LINE" >&2
    return 1
  fi
  if ! grep -qxF -- "$LICENSE_COPYRIGHT_LINE" "$file"; then
    echo "$file: missing the line: $LICENSE_COPYRIGHT_LINE" >&2
    return 1
  fi
  return 0
}

self_test() {
  local tmp
  tmp="$(mktemp -d)"
  local bad_file="$tmp/bad.txt"
  local good_file="$tmp/good.txt"
  local failed=0

  printf 'Just plain prose with no planning references.\n' >"$good_file"
  if ! assert_clean_planning_references "$good_file"; then
    echo "self-test failed: a clean file was flagged as containing a planning reference" >&2
    failed=1
  fi

  local prefix
  for prefix in SEC OPS OPSX CAB CABX A11Y I18N SHARE NIGHT; do
    printf '%s%s\n' "$prefix" "-01 example requirement key" >"$bad_file"
    if assert_clean_planning_references "$bad_file" >/dev/null 2>&1; then
      echo "self-test failed: a synthetic requirement key with prefix ${prefix} was not detected" >&2
      failed=1
    fi
  done

  printf '%s%s\n' "sec" "-01 lowercase requirement key" >"$bad_file"
  if assert_clean_planning_references "$bad_file" >/dev/null 2>&1; then
    echo "self-test failed: a lowercase requirement key was not detected" >&2
    failed=1
  fi

  printf '%s%s\n' "D" "-08 example decision id" >"$bad_file"
  if assert_clean_planning_references "$bad_file" >/dev/null 2>&1; then
    echo "self-test failed: a synthetic decision id was not detected" >&2
    failed=1
  fi

  printf '%s %s\n' "Phase" "3 rollout" >"$bad_file"
  if assert_clean_planning_references "$bad_file" >/dev/null 2>&1; then
    echo "self-test failed: a synthetic phase reference was not detected" >&2
    failed=1
  fi

  printf 'See %s%s for details\n' "ROADMAP" ".md" >"$bad_file"
  if assert_clean_planning_references "$bad_file" >/dev/null 2>&1; then
    echo "self-test failed: a synthetic planning filename was not detected" >&2
    failed=1
  fi

  printf 'Look under %s%s%s\n' "." "planning" "/" >"$bad_file"
  if assert_clean_planning_references "$bad_file" >/dev/null 2>&1; then
    echo "self-test failed: a synthetic planning directory reference was not detected" >&2
    failed=1
  fi

  local finding_prefix
  for finding_prefix in CR WR IN BL; do
    printf '%s%s\n' "$finding_prefix" "-09 example review finding" >"$bad_file"
    if assert_clean_planning_references "$bad_file" >/dev/null 2>&1; then
      echo "self-test failed: a synthetic review finding id with prefix ${finding_prefix} was not detected" >&2
      failed=1
    fi
  done

  printf 'Covered by %s%s\n' "03" "-15" >"$bad_file"
  if assert_clean_planning_references "$bad_file" >/dev/null 2>&1; then
    echo "self-test failed: a synthetic plan number was not detected" >&2
    failed=1
  fi

  local phase_text
  for phase_text in "PHAS""E 3" "phas""e-3" "Phas""e two" "phas""e_ten"; do
    printf 'Done in %s of the rollout\n' "$phase_text" >"$bad_file"
    if assert_clean_planning_references "$bad_file" >/dev/null 2>&1; then
      echo "self-test failed: the phase reference '${phase_text}' was not detected" >&2
      failed=1
    fi
  done

  local grouping
  for grouping in "Pla""n 2" "wav""e 4" "Wav""e-1" "Mileston""e 3" "MILESTON""E_2"; do
    printf 'Shipped with %s today\n' "$grouping" >"$bad_file"
    if assert_clean_planning_references "$bad_file" >/dev/null 2>&1; then
      echo "self-test failed: the grouping reference '${grouping}' was not detected" >&2
      failed=1
    fi
  done

  local innocent
  for innocent in 'Released on 2026-10-07 at noon' 'Branch names look like milestone/v1-example' 'The built-in-10 items plug-in' 'A plan for the next wave of games' 'In phases of the moon' 'Between 10:00-18:00 daily' 'Uses 16-bit and 4-8 players'; do
    printf '%s\n' "$innocent" >"$good_file"
    if ! assert_clean_planning_references "$good_file" >/dev/null 2>&1; then
      echo "self-test failed: innocent text was flagged as a planning reference: ${innocent}" >&2
      failed=1
    fi
  done

  local bad_cs="$tmp/Bad.cs"
  local good_cs="$tmp/Good.cs"
  printf 'namespace Example;\n%s explanation\npublic class Foo { }\n' "// inline" >"$bad_cs"
  printf 'namespace Example;\n/// <summary>Doc comment.</summary>\npublic class Foo { }\n' >"$good_cs"

  if assert_clean_cs_comments "$bad_cs" >/dev/null 2>&1; then
    echo "self-test failed: a // line comment was not detected" >&2
    failed=1
  fi

  if ! assert_clean_cs_comments "$good_cs"; then
    echo "self-test failed: an /// doc comment was incorrectly flagged" >&2
    failed=1
  fi

  local placement
  for placement in '} %s trailing note' '    Call(a, %s argument note' 'var x = 1; %s value note' '[Fact] %s attribute note'; do
    # shellcheck disable=SC2059
    printf "namespace Example;\n${placement}\n" "//" >"$bad_cs"
    if assert_clean_cs_comments "$bad_cs" >/dev/null 2>&1; then
      echo "self-test failed: a // comment was not detected in: ${placement}" >&2
      failed=1
    fi
  done

  printf 'namespace Example;\nvar url = "https://example.com/path";\n' >"$good_cs"
  if ! assert_clean_cs_comments "$good_cs"; then
    echo "self-test failed: a URL inside a string was incorrectly flagged" >&2
    failed=1
  fi

  printf 'namespace Example;\n%s explanation */\npublic class Foo { }\n' "/*" >"$bad_cs"
  if assert_clean_cs_comments "$bad_cs" >/dev/null 2>&1; then
    echo "self-test failed: a C# block comment at the start of a line was not detected" >&2
    failed=1
  fi

  printf 'namespace Example;\nvar x = 1; %s note */\n' "/*" >"$bad_cs"
  if assert_clean_cs_comments "$bad_cs" >/dev/null 2>&1; then
    echo "self-test failed: a trailing C# block comment was not detected" >&2
    failed=1
  fi

  printf 'namespace Example;\nvar listener = "http:%s*:6080";\nvar accept = "image%s*";\n' "//" "/" >"$good_cs"
  if ! assert_clean_cs_comments "$good_cs"; then
    echo "self-test failed: a wildcard address or media type inside a string was flagged as a block comment" >&2
    failed=1
  fi

  local bad_js="$tmp/bad.js"
  local good_js="$tmp/good.mjs"
  printf '%s own line comment\nexport const a = 1;\n' "//" >"$bad_js"
  if assert_clean_js_comments "$bad_js" >/dev/null 2>&1; then
    echo "self-test failed: a JavaScript // line comment was not detected" >&2
    failed=1
  fi

  printf 'export const a = 1; %s trailing note\n' "//" >"$bad_js"
  if assert_clean_js_comments "$bad_js" >/dev/null 2>&1; then
    echo "self-test failed: a trailing JavaScript // comment was not detected" >&2
    failed=1
  fi

  printf '%s plain block comment */\nexport const a = 1;\n' "/*" >"$bad_js"
  if assert_clean_js_comments "$bad_js" >/dev/null 2>&1; then
    echo "self-test failed: a JavaScript block comment without a doc opener was not detected" >&2
    failed=1
  fi

  printf '%s\n%s\n%s\nexport const a = 1;\n' "/**" " * Doc block." " */" >"$good_js"
  if ! assert_clean_js_comments "$good_js"; then
    echo "self-test failed: a JavaScript doc block was incorrectly flagged" >&2
    failed=1
  fi

  printf 'export const url = "https://example.com/path";\n' >"$good_js"
  if ! assert_clean_js_comments "$good_js"; then
    echo "self-test failed: a URL inside a JavaScript string was incorrectly flagged" >&2
    failed=1
  fi

  local kept
  kept="$(printf '%s\n' 'Cabinet.Service/wwwroot/js/live.js' 'Cabinet.Service/wwwroot/lib/signalr/signalr.min.js' | filter_vendored_js)"
  if [ "$kept" != "Cabinet.Service/wwwroot/js/live.js" ]; then
    echo "self-test failed: the vendored path was not dropped from, or the normal path was not kept in, the JavaScript file list" >&2
    failed=1
  fi

  local normal_js_list
  printf '%s own line comment\nexport const a = 1;\n' "//" >"$tmp/Cabinet.Service-wwwroot-js-own.js"
  normal_js_list="$(printf '%s\n' "$tmp/Cabinet.Service-wwwroot-js-own.js" | filter_vendored_js)"
  if [ -z "$normal_js_list" ] || assert_clean_js_comments "$normal_js_list" >/dev/null 2>&1; then
    echo "self-test failed: a // comment in a normal script path was not detected through the vendored-path filter" >&2
    failed=1
  fi

  local markup_api
  for markup_api in 'el.innerHTML' 'el.outerHTML' 'el.insertAdjacentHTML' 'document.write' 'el.style.cssText'; do
    printf 'const value = %s;\n' "$markup_api" >"$tmp/markup.js"
    if assert_no_markup_apis "$tmp/markup.js" >/dev/null 2>&1; then
      echo "self-test failed: the markup-building API '${markup_api}' was not detected" >&2
      failed=1
    fi
  done

  printf 'const node = document.createElement("div");\nnode.textContent = "plain";\nnode.style.setProperty("--x", "1");\n' >"$tmp/clean.js"
  if ! assert_no_markup_apis "$tmp/clean.js" >/dev/null 2>&1; then
    echo "self-test failed: a script that builds elements and text nodes was incorrectly flagged" >&2
    failed=1
  fi

  local page_scripts
  page_scripts="$(printf '%s\n' 'Cabinet.Service/wwwroot/js/render.js' 'Cabinet.Service/wwwroot/lib/signalr/signalr.min.js' 'Cabinet.Service/other/tool.js' | filter_page_scripts)"
  if [ "$page_scripts" != "Cabinet.Service/wwwroot/js/render.js" ]; then
    echo "self-test failed: the vendored folder was not ignored, or a page script was dropped, in the markup-API file list" >&2
    failed=1
  fi

  mkdir -p "$tmp/workflows"
  printf 'jobs:\n  build:\n    runs-on: %s\n' "$RUNS_ON_ALLOWED" >"$tmp/workflows/good.yml"
  printf 'jobs:\n  build:\n    runs-on: self-hosted\n' >"$tmp/workflows/bad.yml"

  if assert_clean_runs_on "$tmp/workflows" >/dev/null 2>&1; then
    echo "self-test failed: a disallowed runs-on value was not detected" >&2
    failed=1
  fi

  rm -f "$tmp/workflows/bad.yml"
  if ! assert_clean_runs_on "$tmp/workflows"; then
    echo "self-test failed: an allowed runs-on value was incorrectly flagged" >&2
    failed=1
  fi

  local licence="$tmp/LICENSE"
  printf '%s\n\n%s\n\nPermission is hereby granted.\n' "$LICENSE_FIRST_LINE" "$LICENSE_COPYRIGHT_LINE" >"$licence"
  if ! assert_valid_license "$licence"; then
    echo "self-test failed: a valid licence was rejected" >&2
    failed=1
  fi

  if assert_valid_license "$tmp/NO-SUCH-LICENSE" >/dev/null 2>&1; then
    echo "self-test failed: a missing licence file was not detected" >&2
    failed=1
  fi

  printf 'Some Other License\n\n%s\n' "$LICENSE_COPYRIGHT_LINE" >"$licence"
  if assert_valid_license "$licence" >/dev/null 2>&1; then
    echo "self-test failed: a licence with the wrong first line was not detected" >&2
    failed=1
  fi

  printf '%s\n\nCopyright (c) 2026 Somebody Else\n' "$LICENSE_FIRST_LINE" >"$licence"
  if assert_valid_license "$licence" >/dev/null 2>&1; then
    echo "self-test failed: a licence with the wrong copyright holder was not detected" >&2
    failed=1
  fi

  printf '%s\n\n%s and others\n' "$LICENSE_FIRST_LINE" "$LICENSE_COPYRIGHT_LINE" >"$licence"
  if assert_valid_license "$licence" >/dev/null 2>&1; then
    echo "self-test failed: a licence with a padded copyright line was not detected" >&2
    failed=1
  fi

  rm -rf "$tmp"
  return "$failed"
}

if ! self_test; then
  echo "FAIL: 10-repo-rules self-test did not behave as expected" >&2
  exit 1
fi
echo "10-repo-rules self-tests passed"

mapfile -t tracked_files < <(git ls-files | grep -vE "$EXCLUDE_PATH_PATTERN" || true)
mapfile -t cs_files < <(git ls-files '*.cs' | grep -vE "$EXCLUDE_PATH_PATTERN" || true)
mapfile -t js_files < <(git ls-files '*.js' '*.mjs' | grep -vE "$EXCLUDE_PATH_PATTERN" | filter_vendored_js || true)

overall_ok=1

if ! assert_clean_planning_references "${tracked_files[@]}"; then
  overall_ok=0
fi

if ! assert_clean_cs_comments "${cs_files[@]}"; then
  overall_ok=0
fi

if ! assert_clean_js_comments "${js_files[@]}"; then
  overall_ok=0
fi

mapfile -t page_script_files < <(git ls-files '*.js' '*.mjs' | filter_page_scripts || true)
if ! assert_no_markup_apis "${page_script_files[@]}"; then
  overall_ok=0
fi

if [ -d "$REPO_ROOT/.github/workflows" ]; then
  if ! assert_clean_runs_on "$REPO_ROOT/.github/workflows"; then
    overall_ok=0
  fi
fi

if ! assert_valid_license "$REPO_ROOT/LICENSE"; then
  overall_ok=0
fi

[ "$overall_ok" -eq 1 ]
