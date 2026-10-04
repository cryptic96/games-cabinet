#!/usr/bin/env bash
# Verification, activation, health acceptance, rollback and rejected-version
# memory for the deploy installer. Sourced, never executed directly.

if [ -n "${CABINET_DEPLOY_SH_LOADED:-}" ]; then
  return 0
fi
CABINET_DEPLOY_SH_LOADED=1

CABINET_STATUS_QUIET=10
CABINET_INSTALL_ROLLED_BACK=10
CABINET_INSTALL_FAILED=11

# Checks ARTIFACT against CHECKSUM_FILE, a single "HASH  NAME" line. The hash
# is compared with the artifact's own hash, and the file name inside the
# checksum file must be the artifact's own name, so a checksum file that lists
# some other file can never vouch for the artifact. Returns non-zero, after
# logging the reason, on any mismatch or malformed checksum file.
cabinet_verify_checksum() {
  local artifact="$1"
  local checksum_file="$2"

  [ -f "$checksum_file" ] || { cabinet_log "checksum file not found: $checksum_file"; return 1; }

  local line_count expected listed_name actual
  line_count="$(wc -l < "$checksum_file" | tr -d ' ')"
  read -r expected listed_name < "$checksum_file" || true
  listed_name="${listed_name#\*}"
  if [ "$line_count" != "1" ] || [[ ! "$expected" =~ ^[0-9a-f]{64}$ ]] \
      || [ "$listed_name" != "$(basename "$artifact")" ]; then
    cabinet_log "malformed checksum file $checksum_file"
    return 1
  fi

  actual="$(sha256sum -- "$artifact" | awk '{print $1}')"
  if [ "$expected" != "$actual" ]; then
    cabinet_log "checksum mismatch for $(basename "$artifact")"
    return 1
  fi
}

# Verifies a downloaded release artifact against the Sigstore bundle
# published with it. No GitHub credential is read or used and no GitHub API
# call is made: gh runs with GH_TOKEN, GITHUB_TOKEN and GH_ENTERPRISE_TOKEN
# unset and a fresh, empty GH_CONFIG_DIR. gh does fetch Sigstore's public
# trust root, so an unreachable Sigstore instance fails verification rather
# than skipping it. On success, prints the certificate's
# sourceRepositoryDigest; returns non-zero on any failure.
cabinet_verify_attestation() {
  local artifact="$1"
  local bundle="$2"
  local repo="$3"
  local signer_workflow="$4"
  local source_ref="$5"

  local gh_config_dir
  gh_config_dir="$(mktemp -d)"

  local output
  if ! output=$(env -u GH_TOKEN -u GITHUB_TOKEN -u GH_ENTERPRISE_TOKEN \
      GH_CONFIG_DIR="$gh_config_dir" \
      gh attestation verify "$artifact" \
        --bundle "$bundle" \
        --repo "$repo" \
        --signer-workflow "${repo}/${signer_workflow}" \
        --source-ref "$source_ref" \
        --deny-self-hosted-runners \
        --format json 2>&1); then
    cabinet_log "attestation verification failed for $artifact: $output"
    rm -rf "$gh_config_dir"
    return 1
  fi
  rm -rf "$gh_config_dir"

  local digest
  digest="$(printf '%s' "$output" | jq -r '.[0].verificationResult.signature.certificate.sourceRepositoryDigest // empty')"
  if [ -z "$digest" ]; then
    cabinet_log "attestation verification produced no sourceRepositoryDigest for $artifact"
    return 1
  fi
  printf '%s' "$digest"
}

# Confirms an attested commit SHA is identical to or an ancestor of BRANCH on
# REPO, using the unauthenticated GitHub compare API (no Authorization
# header is ever sent). Fails on any other status or an unreachable API.
cabinet_commit_on_branch() {
  local repo="$1"
  local sha="$2"
  local branch="$3"

  local response
  if ! response=$(curl --fail --silent --show-error --max-time 30 \
      "https://api.github.com/repos/${repo}/compare/${branch}...${sha}" 2>&1); then
    cabinet_log "commit reachability check failed for ${repo}@${sha}: $response"
    return 1
  fi

  local status
  status="$(printf '%s' "$response" | jq -r '.status // empty')"
  case "$status" in
    identical|behind)
      return 0
      ;;
    *)
      cabinet_log "commit ${sha} on ${repo} is not on ${branch} (compare status: ${status:-unknown})"
      return 1
      ;;
  esac
}

# Performs one unauthenticated GET of URL, writing the response headers to
# HEADERS_FILE and the body to BODY_FILE, and prints the final HTTP status
# code, or 000 when the request could not be completed at all. This is the
# single seam the offline tests replace for the poll path.
cabinet_http_get() {
  local url="$1"
  local headers_file="$2"
  local body_file="$3"

  local status
  status="$(curl --silent --show-error --max-time 30 --user-agent cabinet-deploy \
      --location --dump-header "$headers_file" --output "$body_file" \
      --write-out '%{http_code}' "$url")" || status=000
  printf '%s' "${status:-000}"
}

# Prints the value of the last header called NAME (lower case) found in
# HEADERS_FILE, or nothing when it is absent.
cabinet_header_value() {
  local headers_file="$1"
  local name="$2"

  awk -v wanted="$name" '
    {
      line = $0
      sub(/\r$/, "", line)
      position = index(line, ":")
      if (position == 0) next
      if (tolower(substr(line, 1, position - 1)) != wanted) next
      value = substr(line, position + 1)
      sub(/^[ \t]+/, "", value)
    }
    END { print value }
  ' "$headers_file"
}

# Reads the newest published release tag for REPO from the unauthenticated
# releases/latest endpoint and prints it. Returns 0 with a tag; returns
# CABINET_STATUS_QUIET when there is simply nothing to act on (no release has
# been published yet, or the shared unauthenticated rate limit is spent), so
# the caller can treat the run as a success; returns 1 for every other
# outcome. The remaining rate limit is logged whenever GitHub reports it.
cabinet_fetch_latest_release() {
  local repo="$1"
  local url="https://api.github.com/repos/${repo}/releases/latest"

  local work headers body
  work="$(mktemp -d)"
  headers="${work}/headers"
  body="${work}/body"
  : > "$headers"
  : > "$body"

  local status remaining retry_after reset
  status="$(cabinet_http_get "$url" "$headers" "$body")"
  remaining="$(cabinet_header_value "$headers" x-ratelimit-remaining)"
  retry_after="$(cabinet_header_value "$headers" retry-after)"
  reset="$(cabinet_header_value "$headers" x-ratelimit-reset)"
  if [ -n "$remaining" ]; then
    cabinet_log "github api rate limit remaining: ${remaining}"
  fi

  local tag result=1
  case "$status" in
    200)
      tag="$(jq -r '.tag_name // empty' "$body" 2>/dev/null || true)"
      if [[ "$tag" =~ $CABINET_STRICT_SEMVER_TAG_REGEX ]]; then
        printf '%s\n' "$tag"
        result=0
      else
        cabinet_log "ERROR: latest release tag '${tag}' is not a strict semver tag"
      fi
      ;;
    404)
      cabinet_log "no published release yet"
      result="$CABINET_STATUS_QUIET"
      ;;
    403|429)
      if [ "$remaining" = "0" ] || [ -n "$retry_after" ]; then
        local reset_text="the next cycle"
        if [[ "$reset" =~ ^[0-9]+$ ]]; then
          reset_text="$(date -u -d "@${reset}" '+%Y-%m-%dT%H:%M:%SZ' 2>/dev/null || printf 'the next cycle')"
        fi
        cabinet_log "rate limited by github (status ${status}); limit resets at ${reset_text}, trying again next cycle"
        result="$CABINET_STATUS_QUIET"
      else
        cabinet_log "ERROR: github returned status ${status} without a rate limit signal"
      fi
      ;;
    000)
      cabinet_log "ERROR: could not reach github to read the latest release"
      ;;
    *)
      cabinet_log "ERROR: reading the latest release failed with status ${status}"
      ;;
  esac

  rm -rf "$work"
  return "$result"
}

# Records VERSION (plain X.Y.Z) as the highest release that failed its health
# check, writing a temporary file in STATE_DIR and renaming it into place. The
# recorded value only ever rises: recording a version that is not newer than
# the one already recorded changes nothing.
cabinet_record_rejected_version() {
  local version="$1"
  local state_dir="$2"

  local recorded
  recorded="$(cabinet_read_rejected_version "$state_dir")"
  if [ -n "$recorded" ] && ! cabinet_semver_gt "$version" "$recorded"; then
    return 0
  fi

  mkdir -p "$state_dir"
  local tmp
  tmp="$(mktemp "${state_dir}/.rejected.XXXXXX")"
  printf '%s\n' "$version" > "$tmp"
  mv -f "$tmp" "${state_dir}/rejected"
}

# Prints the recorded rejected version, or nothing when none is recorded.
cabinet_read_rejected_version() {
  local state_dir="$1"

  if [ -f "${state_dir}/rejected" ]; then
    head -n 1 "${state_dir}/rejected"
  fi
}

# Forgets the recorded rejected version. When INSTALLED_VERSION is given, the
# record is kept if it is newer than that version, because a successful install
# of an older release says nothing about a newer release that failed.
cabinet_clear_rejected_version() {
  local state_dir="$1"
  local installed_version="${2:-}"

  if [ -n "$installed_version" ]; then
    local recorded
    recorded="$(cabinet_read_rejected_version "$state_dir")"
    if [ -n "$recorded" ] && cabinet_semver_gt "$recorded" "$installed_version"; then
      return 0
    fi
  fi
  rm -f "${state_dir}/rejected"
}

# Succeeds when VERSION is not newer than the recorded rejected version, and
# fails when it is newer or when nothing is recorded.
cabinet_is_rejected() {
  local version="$1"
  local state_dir="$2"

  local rejected
  rejected="$(cabinet_read_rejected_version "$state_dir")"
  [ -n "$rejected" ] || return 1
  ! cabinet_semver_gt "$version" "$rejected"
}

# Atomically repoints CURRENT_LINK at RELEASES_DIR/VERSION. Unless
# RECORD_PREVIOUS is "no", records the previously active version (if any) into
# STATE_DIR/previous before swapping; a rollback passes "no" so the release it
# leaves is never remembered as the one to go back to. The new symlink is
# built under a temporary name and moved into place with mv -T so the swap is
# a single atomic rename.
cabinet_activate_release() {
  local version="$1"
  local releases_dir="$2"
  local current_link="$3"
  local state_dir="$4"
  local record_previous="${5:-yes}"

  local target="${releases_dir}/${version}"
  [ -d "$target" ] || cabinet_die "cannot activate ${version}: ${target} does not exist"

  mkdir -p "$state_dir" || cabinet_die "cannot create ${state_dir}"
  if [ "$record_previous" != "no" ] && [ -L "$current_link" ]; then
    local previous_version
    previous_version="$(basename "$(readlink -f "$current_link")")"
    printf '%s\n' "$previous_version" > "${state_dir}/previous"
  fi

  local tmp_link
  tmp_link="$(mktemp -u "${current_link}.XXXXXX")"
  ln -s "$target" "$tmp_link" || cabinet_die "cannot create the temporary link ${tmp_link}"
  mv -T "$tmp_link" "$current_link" || cabinet_die "cannot swap ${current_link} to ${version}"
}

# Removes release directories beyond the configured KEEP count, oldest
# first, never removing the active or previous release even if that leaves
# more than KEEP directories on disk.
cabinet_prune_releases() {
  local releases_dir="$1"
  local current_link="$2"
  local state_dir="$3"
  local keep="$4"

  local active_version="" previous_version=""
  if [ -L "$current_link" ]; then
    active_version="$(basename "$(readlink -f "$current_link")")"
  fi
  if [ -f "${state_dir}/previous" ]; then
    previous_version="$(cat "${state_dir}/previous")"
  fi

  local versions=()
  local entry
  for entry in "${releases_dir}"/*; do
    [ -d "$entry" ] || continue
    versions+=("$(basename "$entry")")
  done
  [ "${#versions[@]}" -gt 0 ] || return 0

  local sorted=()
  mapfile -t sorted < <(printf '%s\n' "${versions[@]}" | sort -V)

  local total="${#sorted[@]}"
  local to_delete_count=$(( total > 10#${keep} ? total - 10#${keep} : 0 ))
  [ "$to_delete_count" -gt 0 ] || return 0

  local deleted=0
  local v
  for v in "${sorted[@]}"; do
    [ "$deleted" -lt "$to_delete_count" ] || break
    if [ "$v" = "$active_version" ] || [ "$v" = "$previous_version" ]; then
      continue
    fi
    rm -rf "${releases_dir:?}/${v}"
    deleted=$((deleted + 1))
  done
}

# Restarts the application. This is the only call into the service manager,
# so tests can redefine it.
cabinet_restart_app() {
  systemctl restart cabinet.service
}

# Waits for the loopback ops endpoint to answer with JSON whose status is
# Healthy and whose version is exactly VERSION. Returns non-zero if the
# deadline is reached first. CABINET_HEALTH_INTERVAL_SECONDS (default 2) sets
# the pause between attempts.
cabinet_wait_for_health() {
  local ops_url="$1"
  local version="$2"
  local timeout_seconds="$3"

  local interval="${CABINET_HEALTH_INTERVAL_SECONDS:-2}"
  local deadline=$(( $(date +%s) + 10#${timeout_seconds} ))
  local body status reported
  while [ "$(date +%s)" -lt "$deadline" ]; do
    if body="$(curl --silent --max-time 5 "${ops_url}/health" 2>/dev/null)"; then
      status="$(printf '%s' "$body" | jq -r '.status // empty' 2>/dev/null || true)"
      reported="$(printf '%s' "$body" | jq -r '.version // empty' 2>/dev/null || true)"
      if [ "$status" = "Healthy" ] && [ "$reported" = "$version" ]; then
        return 0
      fi
    fi
    sleep "$interval"
  done

  return 1
}

# Reactivates the existing releases/VERSION directory (a plain version, no
# leading v), restarts the application and waits for it to report that
# version healthy. A rollback never changes the recorded previous release.
# Returns 0 when the release is healthy and 1 when it is not, logging the
# unhealthy result at error level. When RESTORE_ON_FAILURE is "yes" and the
# release is not healthy, the release it replaced is put back first; the
# automatic rollback after a rejected install leaves it "no" because the
# release it replaced is the one that just failed.
cabinet_rollback_release() {
  local version="$1"
  local releases_dir="$2"
  local current_link="$3"
  local state_dir="$4"
  local ops_url="$5"
  local health_timeout="$6"
  local restore_on_failure="${7:-no}"

  local target="${releases_dir}/${version}"
  [ -d "$target" ] || cabinet_die "cannot roll back to ${version}: ${target} does not exist"

  local replaced=""
  if [ -L "$current_link" ]; then
    replaced="$(basename "$(readlink -f "$current_link")")"
  fi

  cabinet_activate_release "$version" "$releases_dir" "$current_link" "$state_dir" no
  cabinet_restart_app

  if ! cabinet_wait_for_health "$ops_url" "$version" "$health_timeout"; then
    cabinet_log "ERROR: release ${version} was reactivated but did not report healthy within ${health_timeout} seconds"
    if [ "$restore_on_failure" = "yes" ] && [ -n "$replaced" ] && [ "$replaced" != "$version" ] \
        && [ -d "${releases_dir}/${replaced}" ]; then
      cabinet_log "restoring release ${replaced}"
      cabinet_activate_release "$replaced" "$releases_dir" "$current_link" "$state_dir" no
      cabinet_restart_app
    fi
    return 1
  fi
  cabinet_log "release ${version} is active and healthy"
}

# Orchestrates the activation of an already-verified release: unpack, check
# the manifest version, atomically activate, restart, accept only a health
# answer naming the expected version, and otherwise roll back to the active
# release. Returns 0 on success, CABINET_INSTALL_ROLLED_BACK when the release
# was rejected and the previous release is healthy again, and
# CABINET_INSTALL_FAILED when the release was rejected and nothing healthy
# could be restored. Prunes old releases on success.
cabinet_install_verified_release() {
  local tag="$1"
  local version="$2"
  local artifact="$3"
  local active_version="$4"
  local releases_dir="$5"
  local current_link="$6"
  local state_dir="$7"
  local keep_releases="$8"
  local ops_url="$9"
  local health_timeout="${10}"

  local staging_dir="${releases_dir}/.staging-${version}"
  rm -rf "$staging_dir"
  mkdir -p "$staging_dir" || cabinet_die "cannot create ${staging_dir}"

  if ! unzip -q "$artifact" -d "$staging_dir"; then
    rm -rf "$staging_dir"
    cabinet_die "failed to unpack the verified artifact for ${tag}"
  fi

  local manifest_version
  manifest_version="$(jq -r '.version // empty' "${staging_dir}/release-manifest.json" 2>/dev/null || true)"
  if [ "$manifest_version" != "$version" ]; then
    rm -rf "$staging_dir"
    cabinet_die "release-manifest.json version '${manifest_version}' does not match tag ${tag}"
  fi

  local target="${releases_dir}/${version}"
  if [ -e "$target" ]; then
    if [ "$(readlink -f "$target")" = "$(readlink -f "$current_link" 2>/dev/null || true)" ]; then
      rm -rf "$staging_dir"
      cabinet_die "${tag} is already the active release"
    fi
    cabinet_log "replacing ${target}, left by an earlier attempt"
    rm -rf "$target"
  fi
  mv -T "$staging_dir" "$target" || cabinet_die "cannot move the unpacked release into ${target}"

  cabinet_activate_release "$version" "$releases_dir" "$current_link" "$state_dir"
  cabinet_restart_app

  if cabinet_wait_for_health "$ops_url" "$version" "$health_timeout"; then
    cabinet_log "release ${version} is active and healthy"
    cabinet_prune_releases "$releases_dir" "$current_link" "$state_dir" "$keep_releases"
    return 0
  fi

  cabinet_log "ERROR: release ${version} did not report healthy within ${health_timeout} seconds"
  if [ -z "$active_version" ]; then
    cabinet_log "ERROR: there is no earlier release to return to"
    return "$CABINET_INSTALL_FAILED"
  fi

  cabinet_log "rolling back to release ${active_version}"
  if cabinet_rollback_release "$active_version" "$releases_dir" "$current_link" "$state_dir" \
      "$ops_url" "$health_timeout"; then
    return "$CABINET_INSTALL_ROLLED_BACK"
  fi
  cabinet_log "ERROR: rollback to ${active_version} failed; the application is not healthy"
  return "$CABINET_INSTALL_FAILED"
}
