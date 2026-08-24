#!/usr/bin/env bash
#
# External checks against a deployed Weapons of Order environment.
#
#   scripts/smoke-staging.sh https://app-woo-staging-xxxxxx.azurewebsites.net
#
# Read-only. Nothing here creates an account, forges anything or writes a row, so it is safe
# to run against staging on every deployment without accumulating junk. The full vertical
# loop is verified in a browser, not here.
#
# Azure accepting a ZIP is not evidence the game is running. This is what turns a deployment
# into a claim that can fail.
set -euo pipefail

BASE_URL="${1:-${STAGING_BASE_URL:-}}"
[ -n "$BASE_URL" ] || { echo "usage: $0 <base-url>" >&2; exit 2; }
BASE_URL="${BASE_URL%/}"

case "$BASE_URL" in
    https://*) ;;
    *) echo "Refusing to smoke-test a non-https origin: $BASE_URL" >&2; exit 2 ;;
esac

# Retry rather than sleep: an App Service that has just taken a new package is restarting,
# and how long that takes is not a constant worth guessing.
readonly TIMEOUT_SECONDS="${SMOKE_TIMEOUT_SECONDS:-300}"
readonly INTERVAL_SECONDS=5

failures=0

note() { printf '    %s\n' "$1"; }

fail() {
    printf 'FAIL  %s\n' "$1" >&2
    failures=$((failures + 1))
}

pass() { printf 'ok    %s\n' "$1"; }

# Polls until the command succeeds or the budget runs out.
await() {
    local description="$1"
    shift

    local deadline=$((SECONDS + TIMEOUT_SECONDS))
    local attempt=0

    while [ "$SECONDS" -lt "$deadline" ]; do
        attempt=$((attempt + 1))
        if "$@"; then
            pass "$description (after ${attempt} attempt(s))"
            return 0
        fi
        sleep "$INTERVAL_SECONDS"
    done

    fail "$description did not succeed within ${TIMEOUT_SECONDS}s"
    return 1
}

status_of() { curl --silent --show-error --location --max-time 20 --output /dev/null --write-out '%{http_code}' "$1"; }

healthy() {
    local body
    body="$(curl --silent --max-time 20 "$1")" || return 1
    printf '%s' "$body" | grep -q '"status":"Healthy"'
}

echo "Smoke testing $BASE_URL"
echo

# --- Liveness: the process is up and answering. -----------------------------------------
await "liveness /api/health reports Healthy" healthy "$BASE_URL/api/health"

# --- Readiness: it can open its database. Separate on purpose; a database fault is not a
# --- reason to restart the process, so this is checked here rather than by the platform.
await "readiness /api/health/ready reports Healthy" healthy "$BASE_URL/api/health/ready"

# --- The React application is actually served, not just the API. -------------------------
document="$(curl --silent --show-error --location --max-time 30 "$BASE_URL/")" || document=""

if printf '%s' "$document" | grep -qi '<div id="root"'; then
    pass "the root URL serves the React document"
else
    fail "the root URL did not serve the React document"
fi

asset_path="$(printf '%s' "$document" | grep -o '/assets/[A-Za-z0-9._-]*\.js' | head -n 1)"

if [ -n "$asset_path" ]; then
    asset_status="$(status_of "$BASE_URL$asset_path")"
    if [ "$asset_status" = "200" ]; then
        pass "the production bundle loads ($asset_path)"
    else
        fail "the production bundle $asset_path answered $asset_status"
    fi
else
    fail "the document referenced no hashed /assets bundle"
fi

# --- Mixed content. An https page pulling an http asset is blocked by the browser and
# --- would make the game silently unusable rather than visibly broken.
if printf '%s' "$document" | grep -q 'src="http://\|href="http://'; then
    fail "the document references a plain-http asset"
else
    pass "no plain-http asset references"
fi

# --- PWA. -------------------------------------------------------------------------------
for pwa_path in /manifest.webmanifest /sw.js; do
    pwa_status="$(status_of "$BASE_URL$pwa_path")"
    if [ "$pwa_status" = "200" ]; then
        pass "$pwa_path is served"
    else
        fail "$pwa_path answered $pwa_status"
    fi
done

manifest_type="$(curl --silent --location --max-time 20 --output /dev/null --write-out '%{content_type}' "$BASE_URL/manifest.webmanifest")"
case "$manifest_type" in
    application/manifest+json*) pass "the manifest is served as application/manifest+json" ;;
    *) fail "the manifest content type is '$manifest_type'" ;;
esac

# --- Deep links. A direct navigation to a client route must reach React, not a 404. -------
for route in /login /forge /inventory /units /battle; do
    route_status="$(status_of "$BASE_URL$route")"
    if [ "$route_status" = "200" ]; then
        pass "deep link $route serves the application"
    else
        fail "deep link $route answered $route_status"
    fi
done

# --- An unmatched API route stays an API route. ------------------------------------------
api_404="$(status_of "$BASE_URL/api/not-a-real-endpoint")"
if [ "$api_404" = "404" ]; then
    pass "unmatched /api routes are 404, not the SPA document"
else
    fail "/api/not-a-real-endpoint answered $api_404"
fi

# --- The development-only account notification endpoint publishes confirmation and reset
# --- links. Its presence here would be a complete account takeover for anyone who asked.
dev_status="$(status_of "$BASE_URL/api/dev/account-notifications")"
if [ "$dev_status" = "404" ]; then
    pass "the development notification endpoint is absent"
else
    fail "/api/dev/account-notifications answered $dev_status and must not exist here"
fi

# --- Cookie attributes, read from the one endpoint that issues a cookie without a session.
cookie_headers="$(curl --silent --show-error --location --max-time 20 --dump-header - --output /dev/null "$BASE_URL/api/auth/session" | grep -i '^set-cookie:' || true)"

if [ -z "$cookie_headers" ]; then
    fail "GET /api/auth/session issued no cookie"
else
    if printf '%s' "$cookie_headers" | grep -qi 'secure'; then
        pass "cookies are marked Secure"
    else
        fail "a cookie was issued without Secure"
        note "$cookie_headers"
    fi

    if printf '%s' "$cookie_headers" | grep -qi 'httponly'; then
        pass "cookies are marked HttpOnly"
    else
        fail "a cookie was issued without HttpOnly"
    fi

    if printf '%s' "$cookie_headers" | grep -qi 'samesite=lax'; then
        pass "cookies are marked SameSite=Lax"
    else
        fail "a cookie was issued without SameSite=Lax"
        note "$cookie_headers"
    fi
fi

# --- Transport. --------------------------------------------------------------------------
hsts="$(curl --silent --location --max-time 20 --dump-header - --output /dev/null "$BASE_URL/api/health" | grep -i '^strict-transport-security:' || true)"
if [ -n "$hsts" ]; then
    pass "HSTS is present, so forwarded headers reached the application"
else
    fail "no Strict-Transport-Security header: the application did not see the request as https"
fi

plain_http_status="$(curl --silent --max-time 20 --output /dev/null --write-out '%{http_code}' "http://${BASE_URL#https://}/api/health" || true)"
case "$plain_http_status" in
    301|302|307|308) pass "plain http redirects ($plain_http_status) rather than serving or looping" ;;
    200) fail "plain http served content instead of redirecting" ;;
    *) note "plain http answered $plain_http_status" ;;
esac

echo
if [ "$failures" -gt 0 ]; then
    echo "$failures check(s) failed."
    exit 1
fi

echo "All checks passed."
