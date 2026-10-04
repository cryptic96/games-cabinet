#!/usr/bin/env bash
set -euo pipefail

VERSION=""
COMMIT=""
OUTPUT=""

while [ "$#" -gt 0 ]; do
  case "$1" in
    --version)
      VERSION="${2:-}"
      shift 2
      ;;
    --commit)
      COMMIT="${2:-}"
      shift 2
      ;;
    --output)
      OUTPUT="${2:-}"
      shift 2
      ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 1
      ;;
  esac
done

if [ -z "$VERSION" ] || [ -z "$COMMIT" ] || [ -z "$OUTPUT" ]; then
  echo "Usage: package-release.sh --version X.Y.Z --commit <40-hex-sha> --output DIR" >&2
  exit 1
fi

if ! [[ "$VERSION" =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$ ]]; then
  echo "Invalid --version '$VERSION': must be strict semver X.Y.Z" >&2
  exit 1
fi

if ! [[ "$COMMIT" =~ ^[0-9a-f]{40}$ ]]; then
  echo "Invalid --commit '$COMMIT': must be 40 lowercase hex characters" >&2
  exit 1
fi

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

mkdir -p "$OUTPUT"
OUTPUT="$(cd "$OUTPUT" && pwd)"
STAGE_DIR="$OUTPUT/stage"

rm -rf "$STAGE_DIR"
mkdir -p "$STAGE_DIR/app"

cd "$REPO_ROOT"

dotnet restore Cabinet.slnx --locked-mode >&2

dotnet publish Cabinet.Service/Cabinet.Service.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained false \
  -p:Version="$VERSION" \
  -p:SourceRevisionId="$COMMIT" \
  -p:ContinuousIntegrationBuild=true \
  -o "$STAGE_DIR/app" >&2

jq -n \
  --arg version "$VERSION" \
  --arg commit "$COMMIT" \
  '{version: $version, commit: $commit}' \
  > "$STAGE_DIR/release-manifest.json"

if [ -d "deploy" ]; then
  mkdir -p "$STAGE_DIR/deploy"
  (cd deploy && tar -cf - --exclude='./tests' .) | (cd "$STAGE_DIR/deploy" && tar -xf -)
fi

ZIP_PATH="$OUTPUT/cabinet-$VERSION.zip"
rm -f "$ZIP_PATH"

(
  cd "$STAGE_DIR"
  find . -type f | sed 's|^\./||' | sort | zip -X -q "$ZIP_PATH" -@
)

CHECKSUM_PATH="$ZIP_PATH.sha256"
(
  cd "$OUTPUT"
  sha256sum "$(basename "$ZIP_PATH")" > "$(basename "$CHECKSUM_PATH")"
)

echo "$ZIP_PATH"
echo "$CHECKSUM_PATH"
echo "$STAGE_DIR"
