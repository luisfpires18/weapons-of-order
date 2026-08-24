#!/usr/bin/env bash
#
# Tests for the Azure CLI output parsing in az-output.sh.
#
#   infra/azure/lib/az-output.test.sh
#
# Needs no Azure subscription, no sign-in and no network: every case is recorded output. The
# point is that the availability checks in bootstrap.sh can be changed and verified without
# provisioning anything, because the failure they exist to catch is a false negative — the
# check reporting that .NET 10 is missing while Azure is listing it.
set -uo pipefail

# shellcheck source=./az-output.sh
. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/az-output.sh"

failures=0

check() {
    local description="$1" expected="$2" actual="$3"

    if [ "$expected" = "$actual" ]; then
        printf 'ok    %s\n' "$description"
    else
        printf 'FAIL  %s\n       expected: %s\n       actual:   %s\n' \
            "$description" "$expected" "$actual" >&2
        failures=$((failures + 1))
    fi
}

# Yes or no, so a false negative reads as one in the output rather than as an exit code.
found() {
    if printf '%s' "$1" | az_contains_exactly "$2"; then echo yes; else echo no; fi
}

# --- Fixtures ------------------------------------------------------------------------------

# What `az webapp list-runtimes --os-type linux --output tsv` answers today: the identifier
# plus five descriptive columns. This is the shape that broke the original check.
runtimes_with_columns=$'DOTNETCORE|10.0\t2028-12-01\tLinux\t.NET\tActive\t10.0 (LTS)
DOTNETCORE|9.0\t2026-05-12\tLinux\t.NET\tActive\t9.0 (STS)
DOTNETCORE|8.0\t2026-11-10\tLinux\t.NET\tActive\t8.0 (LTS)
NODE|22-lts\t2027-04-30\tLinux\tNode\tActive\tNode 22 LTS'

# What it used to answer: a bare array of identifiers, one per line, no other columns.
runtimes_bare=$'DOTNETCORE|10.0
DOTNETCORE|9.0
NODE|22-lts'

# A region that only has a preview build of the version being asked for.
runtimes_preview_only=$'DOTNETCORE|10.0-preview\t2026-01-01\tLinux\t.NET\tPreview\t10.0 preview
DOTNETCORE|9.0\t2026-05-12\tLinux\t.NET\tActive\t9.0 (STS)'


# --- The identifier must be read out of the first column, not the whole line ----------------

check "current multi-column output yields identifiers only" \
    $'DOTNETCORE|10.0\nDOTNETCORE|9.0\nDOTNETCORE|8.0\nNODE|22-lts' \
    "$(printf '%s\n' "$runtimes_with_columns" | az_runtime_identifiers)"

check ".NET 10 is found in the current multi-column output" \
    "yes" \
    "$(found "$(printf '%s\n' "$runtimes_with_columns" | az_runtime_identifiers)" 'DOTNETCORE|10.0')"

check ".NET 10 is still found in the older bare-array output" \
    "yes" \
    "$(found "$(printf '%s\n' "$runtimes_bare" | az_runtime_identifiers)" 'DOTNETCORE|10.0')"

check "a genuinely absent runtime is reported absent" \
    "no" \
    "$(found "$(printf '%s\n' "$runtimes_bare" | az_runtime_identifiers)" 'DOTNETCORE|11.0')"

# --- Exactness. A near-miss must not pass for the real thing --------------------------------

check "a preview build does not satisfy a check for the release" \
    "no" \
    "$(found "$(printf '%s\n' "$runtimes_preview_only" | az_runtime_identifiers)" 'DOTNETCORE|10.0')"

check "the dot in an identifier is not treated as a regex wildcard" \
    "no" \
    "$(found $'DOTNETCORE|10X0' 'DOTNETCORE|10.0')"

check "the pipe in an identifier is not treated as alternation" \
    "no" \
    "$(found $'DOTNETCORE' 'DOTNETCORE|10.0')"

# --- Carriage returns from an Azure CLI on Windows ------------------------------------------

check "a trailing carriage return does not defeat the comparison" \
    "yes" \
    "$(found "$(printf 'DOTNETCORE|10.0\r\tLinux\r\n' | az_runtime_identifiers)" 'DOTNETCORE|10.0')"

# --- Reading the parameter file, which is what decides what is being looked for --------------

parameters="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/main.bicepparam"

check "the runtime stack is read from main.bicepparam" \
    'DOTNETCORE|10.0' \
    "$(az_bicepparam_value linuxRuntimeStack "$parameters")"

check "the App Service tier is read from main.bicepparam" \
    'Free' \
    "$(az_bicepparam_value appServicePlanTier "$parameters")"

check "an absent parameter yields nothing rather than a stale value" \
    "" \
    "$(az_bicepparam_value notAParameter "$parameters")"

# --------------------------------------------------------------------------------------------

echo
if [ "$failures" -gt 0 ]; then
    echo "$failures check(s) failed."
    exit 1
fi

echo "All checks passed."
