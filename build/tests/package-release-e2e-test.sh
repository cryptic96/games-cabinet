#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

VERSION="0.0.1"
COMMIT="0123456789abcdef0123456789abcdef01234567"

TMP="$(mktemp -d)"
APP_PID=""

cleanup() {
  if [ -n "$APP_PID" ] && kill -0 "$APP_PID" 2>/dev/null; then
    kill "$APP_PID" 2>/dev/null || true
    wait "$APP_PID" 2>/dev/null || true
  fi
  rm -rf "$TMP"
}
trap cleanup EXIT

fail() {
  echo "FAIL: $*" >&2
  if [ -f "$TMP/app.log" ]; then
    echo "--- application log ---" >&2
    cat "$TMP/app.log" >&2
  fi
  exit 1
}

free_port() {
  python3 -c 'import socket; s = socket.socket(); s.bind(("127.0.0.1", 0)); print(s.getsockname()[1]); s.close()'
}

OUTPUT="$TMP/out"
bash build/package-release.sh --version "$VERSION" --commit "$COMMIT" --output "$OUTPUT" >"$TMP/package.log" 2>&1 \
  || { cat "$TMP/package.log" >&2; fail "package-release.sh did not succeed"; }

ZIP="$OUTPUT/cabinet-$VERSION.zip"
[ -f "$ZIP" ] || fail "missing $ZIP"
[ -f "$ZIP.sha256" ] || fail "missing $ZIP.sha256"

LISTING="$(unzip -Z1 "$ZIP")"
grep -qx 'app/Cabinet.Service.dll' <<<"$LISTING" || fail "zip lacks app/Cabinet.Service.dll"
grep -qx 'release-manifest.json' <<<"$LISTING" || fail "zip lacks release-manifest.json"
grep -qx 'app/libSkiaSharp.so' <<<"$LISTING" || fail "zip lacks app/libSkiaSharp.so"
grep -qx 'app/SkiaSharp.dll' <<<"$LISTING" || fail "zip lacks app/SkiaSharp.dll"
for shipped in \
  deploy/bin/cabinet-deploy deploy/bin/cabinet-selfcheck \
  deploy/lib/common.sh deploy/lib/deploy.sh \
  deploy/systemd/cabinet.service deploy/systemd/cabinet-deploy-poll.service \
  deploy/systemd/cabinet-deploy-poll.timer deploy/provision.sh; do
  grep -qx "$shipped" <<<"$LISTING" || fail "zip lacks $shipped, which the server compares its installed copies against"
done
if grep -q '^deploy/tests/' <<<"$LISTING"; then
  fail "zip ships deploy/tests/, which must stay out of releases"
fi

(cd "$OUTPUT" && sha256sum -c "cabinet-$VERSION.zip.sha256" >/dev/null) || fail "sha256 does not match the zip"

MANIFEST="$(unzip -p "$ZIP" release-manifest.json | jq -c .)"
EXPECTED_MANIFEST="{\"version\":\"$VERSION\",\"commit\":\"$COMMIT\"}"
[ "$MANIFEST" = "$EXPECTED_MANIFEST" ] || fail "manifest was $MANIFEST, expected $EXPECTED_MANIFEST"

mkdir -p "$TMP/releases/$VERSION"
unzip -q "$ZIP" -d "$TMP/releases/$VERSION"
ln -s "$TMP/releases/$VERSION" "$TMP/current"

WEB_PORT="$(free_port)"
OPS_PORT="$(free_port)"
while [ "$OPS_PORT" = "$WEB_PORT" ]; do
  OPS_PORT="$(free_port)"
done

SMOKE_OUTPUT="$(cd "$TMP/current/app" && dotnet "$TMP/current/app/Cabinet.Service.dll" image-smoke)" \
  || fail "image-smoke did not exit 0"
grep -qE '^PASS image-smoke 480x360 webp [0-9]+ bytes$' <<<"$SMOKE_OUTPUT" \
  || fail "image-smoke output was unexpected: $SMOKE_OUTPUT"
echo "$SMOKE_OUTPUT"

(
  cd "$TMP/current/app"
  ASPNETCORE_ENVIRONMENT=Production \
    STATE_DIRECTORY="$TMP/state" \
    Kestrel__Endpoints__Web__Url="http://127.0.0.1:$WEB_PORT" \
    Kestrel__Endpoints__Ops__Url="http://127.0.0.1:$OPS_PORT" \
    exec dotnet "$TMP/current/app/Cabinet.Service.dll"
) >"$TMP/app.log" 2>&1 &
APP_PID=$!

HEALTH=""
for _ in $(seq 1 60); do
  if ! kill -0 "$APP_PID" 2>/dev/null; then
    fail "application exited before becoming healthy"
  fi
  if HEALTH="$(curl -fsS --max-time 2 "http://127.0.0.1:$OPS_PORT/health" 2>/dev/null)"; then
    break
  fi
  HEALTH=""
  sleep 0.5
done
[ -n "$HEALTH" ] || fail "ops health did not answer within 30 seconds"

echo "$HEALTH"

[ "$(jq -r .status <<<"$HEALTH")" = "Healthy" ] || fail "health status is not Healthy: $HEALTH"
[ "$(jq -r .version <<<"$HEALTH")" = "$VERSION" ] || fail "health version is not $VERSION: $HEALTH"
[ "$(jq -r .commit <<<"$HEALTH")" = "$COMMIT" ] || fail "health commit is not $COMMIT: $HEALTH"

PAGE="$(curl -fsS --max-time 5 "http://127.0.0.1:$WEB_PORT/")" || fail "hello page did not return 200"
grep -q "$VERSION" <<<"$PAGE" || fail "hello page does not mention version $VERSION"

STYLESHEET="$({ grep -oE '<link[^>]*rel="stylesheet"[^>]*>' <<<"$PAGE" || true; } | sed -nE 's/.*href="([^"]+)".*/\1/p' | sed -n '1p')"
[ -n "$STYLESHEET" ] || fail "hello page has no stylesheet link"
grep -qE '\?v=|\.[A-Za-z0-9_-]{6,}\.css' <<<"$STYLESHEET" || fail "stylesheet href is not fingerprinted: $STYLESHEET"

STYLESHEET_STATUS="$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 "http://127.0.0.1:$WEB_PORT$STYLESHEET")"
[ "$STYLESHEET_STATUS" = "200" ] || fail "stylesheet $STYLESHEET returned $STYLESHEET_STATUS"

PUBLIC_HEALTH_STATUS="$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 "http://127.0.0.1:$WEB_PORT/health")"
[ "$PUBLIC_HEALTH_STATUS" = "404" ] || fail "/health on the public listener returned $PUBLIC_HEALTH_STATUS, expected 404"

echo "PASS package-release-e2e"
