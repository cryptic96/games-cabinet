# Shared personal-data denylist helpers for the git hooks. Source this file; it is not run on its own.
#
# The denylist is a private file kept outside the repository, one fixed string
# per line. Blank lines and lines starting with '#' are ignored and matching is
# case-insensitive. The default location is
# ${CABINET_DENYLIST_FILE:-${XDG_CONFIG_HOME:-$HOME/.config}/games-cabinet/denylist.txt}.
#
# Output from these helpers names a location and a denylist line number only,
# never the matched value.

DENYLIST_ABSENT_STATUS=3

# Prints the resolved denylist path.
denylist_path() {
  printf '%s\n' "${CABINET_DENYLIST_FILE:-${XDG_CONFIG_HOME:-$HOME/.config}/games-cabinet/denylist.txt}"
}

# Prints one "<line number><TAB><pattern>" row per pattern. Returns 3 when the
# denylist is missing or unreadable.
denylist_load() {
  local path raw line_number=0
  path="$(denylist_path)"
  if [ ! -f "$path" ] || [ ! -r "$path" ]; then
    return "$DENYLIST_ABSENT_STATUS"
  fi
  while IFS= read -r raw || [ -n "$raw" ]; do
    line_number=$((line_number + 1))
    raw="${raw%$'\r'}"
    raw="${raw#"${raw%%[![:space:]]*}"}"
    raw="${raw%"${raw##*[![:space:]]}"}"
    case "$raw" in
      '' | '#'*) continue ;;
    esac
    printf '%s\t%s\n' "$line_number" "$raw"
  done <"$path"
}

# Loads the patterns into the DENYLIST_NUMBERS and DENYLIST_PATTERNS arrays.
# Returns 3 when the denylist is absent; otherwise 0, including when it holds
# no patterns at all.
denylist_init() {
  local rows row status=0
  DENYLIST_NUMBERS=()
  DENYLIST_PATTERNS=()
  rows="$(denylist_load)" || status=$?
  if [ "$status" -ne 0 ]; then
    return "$status"
  fi
  while IFS= read -r row; do
    [ -n "$row" ] || continue
    DENYLIST_NUMBERS+=("${row%%$'\t'*}")
    DENYLIST_PATTERNS+=("${row#*$'\t'}")
  done <<<"$rows"
  return 0
}

# Prints the standard warning for a missing denylist.
denylist_warn_absent() {
  echo "warning: personal-data denylist not found at $(denylist_path); skipping the check" >&2
}

# Succeeds when the text given as the first argument contains any pattern.
denylist_text_matches() {
  local text="$1" i
  for ((i = 0; i < ${#DENYLIST_PATTERNS[@]}; i++)); do
    if grep -a -q -i -F -e "${DENYLIST_PATTERNS[i]}" <<<"$text"; then
      return 0
    fi
  done
  return 1
}

# Reads text on stdin. For every pattern that occurs, prints
# "<label>:<line>: denylist match #<denylist line>" for each matching line and
# returns 1; returns 0 when nothing matches. The label is the first argument.
denylist_scan() {
  local label="$1" text hits hit i found=0
  text="$(tr -d '\000')"
  for ((i = 0; i < ${#DENYLIST_PATTERNS[@]}; i++)); do
    hits="$(grep -a -n -i -F -e "${DENYLIST_PATTERNS[i]}" <<<"$text" | cut -d: -f1 || true)"
    [ -n "$hits" ] || continue
    while IFS= read -r hit; do
      printf '%s:%s: denylist match #%s\n' "$label" "$hit" "${DENYLIST_NUMBERS[i]}" >&2
      found=1
    done <<<"$hits"
  done
  [ "$found" -eq 0 ]
}

# Returns a label that is safe to print for a path: the path itself, or a
# numbered placeholder when the path matches the denylist. Arguments are the
# path, a noun for the placeholder and the position number.
denylist_safe_label() {
  local path="$1" noun="$2" position="$3"
  if denylist_text_matches "$path"; then
    printf '%s number %s\n' "$noun" "$position"
  else
    printf '%s\n' "$path"
  fi
}

# Converts one file's unified diff on stdin into its added lines, padded with
# empty lines so that line N of the output is line N of the new file. Callers
# must produce the diff with --text so that binary files contribute their
# bytes instead of a one-line "Binary files differ" notice; NUL bytes are
# dropped here so a binary file's text still reaches the matcher.
denylist_added_lines() {
  tr -d '\000' | LC_ALL=C awk '
    /^diff / { inhunk = 0; emitted = 0; next }
    /^@@ / {
      inhunk = 1
      match($0, /\+[0-9]+/)
      next_line = substr($0, RSTART + 1, RLENGTH - 1) + 0
      next
    }
    !inhunk { next }
    /^\+/ {
      while (emitted < next_line - 1) { print ""; emitted++ }
      print substr($0, 2)
      emitted++
      next_line++
    }
  '
}
