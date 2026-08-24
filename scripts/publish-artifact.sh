#!/usr/bin/env bash
#
# Builds the one deployable Weapons of Order artifact: the published ASP.NET Core
# application with the built React client, the PWA files and the creator's game content
# inside it.
#
#   scripts/publish-artifact.sh [output-directory]
#
# Default output is artifacts/staging, which is git-ignored. The same script runs in CI and
# on a developer machine, so what is deployed is what was tested.
#
# The client is built into the API project's wwwroot rather than into web/dist, because that
# is the directory ASP.NET Core serves from and the .NET SDK collects at publish time. Both
# are git-ignored; neither is ever committed.
#
# Nothing here reads a secret, so this can be run on any checkout.
set -euo pipefail

readonly ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
readonly API_PROJECT="server/src/WeaponsOfOrder.Api"
readonly CLIENT_OUTPUT="$ROOT/$API_PROJECT/wwwroot"
readonly CONFIGURATION="${CONFIGURATION:-Release}"

# `pnpm --dir web` matches the README. Split into words so PNPM can carry arguments, which
# is what a corepack shim needs:
#
#   PNPM='corepack pnpm@10.34.0' scripts/publish-artifact.sh
read -r -a PNPM_COMMAND <<< "${PNPM:-pnpm}"

OUTPUT_DIR="${1:-$ROOT/artifacts/staging}"
mkdir -p "$OUTPUT_DIR"
OUTPUT_DIR="$(cd "$OUTPUT_DIR" && pwd)"

cd "$ROOT"

echo "==> Installing client dependencies"
"${PNPM_COMMAND[@]}" --dir web install --frozen-lockfile

echo "==> Building the client into $CLIENT_OUTPUT"
rm -rf "$CLIENT_OUTPUT"
# --outDir is relative to web/. --emptyOutDir is required because the directory is outside
# the Vite project root, which Vite otherwise refuses to clear.
"${PNPM_COMMAND[@]}" --dir web build --outDir ../server/src/WeaponsOfOrder.Api/wwwroot --emptyOutDir

echo "==> Publishing the application into $OUTPUT_DIR"
rm -rf "${OUTPUT_DIR:?}/"*
dotnet publish "$API_PROJECT" \
    --configuration "$CONFIGURATION" \
    --output "$OUTPUT_DIR"

echo "==> Verifying the artifact"

fail() {
    echo "ARTIFACT CHECK FAILED: $1" >&2
    exit 1
}

require() {
    [ -e "$OUTPUT_DIR/$1" ] || fail "missing $1"
    echo "    ok  $1"
}

refuse() {
    [ ! -e "$OUTPUT_DIR/$1" ] || fail "$1 must not be in the artifact: $2"
    echo "    ok  no $1"
}

# The application itself.
require "WeaponsOfOrder.Api.dll"
require "WeaponsOfOrder.Combat.dll"
require "WeaponsOfOrder.Infrastructure.dll"
require "appsettings.json"

# The creator's game content. Units and weapons are read from these at startup, so an
# artifact without them starts and then fails validation.
require "content/units.json"
require "content/weapons.json"

# The client, its PWA files, and at least one hashed asset bundle.
require "wwwroot/index.html"
require "wwwroot/manifest.webmanifest"
require "wwwroot/favicon.svg"
[ -n "$(find "$OUTPUT_DIR/wwwroot/assets" -name '*.js' -print -quit 2>/dev/null)" ] \
    || fail "no hashed JavaScript bundle in wwwroot/assets"
echo "    ok  wwwroot/assets/*.js"

# vite-plugin-pwa emits the service worker at the root of the build so its scope is the
# whole origin. A worker under /assets would only control /assets.
[ -e "$OUTPUT_DIR/wwwroot/sw.js" ] || fail "missing wwwroot/sw.js"
echo "    ok  wwwroot/sw.js"

# Local development configuration, and anything that looks like a local secret.
refuse "appsettings.Development.json" "it carries the local development database credentials"
refuse ".env" "environment files are never deployed"
refuse ".env.local" "environment files are never deployed"
refuse "appsettings.Local.json" "local overrides are never deployed"

echo
echo "Artifact ready: $OUTPUT_DIR"
echo "Run it standalone with:"
echo "  ASPNETCORE_ENVIRONMENT=Staging \\"
echo "  ConnectionStrings__WeaponsOfOrder='...' \\"
echo "  Auth__ClientBaseUrl='https://...' \\"
echo "  dotnet $OUTPUT_DIR/WeaponsOfOrder.Api.dll"
