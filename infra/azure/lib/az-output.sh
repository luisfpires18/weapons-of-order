#!/usr/bin/env bash
#
# Parsing helpers for Azure CLI listing output.
#
# Sourced by infra/azure/bootstrap.sh, and exercised on its own by az-output.test.sh with
# recorded fixtures — so a change to how these read Azure's output can be checked without an
# Azure subscription and without creating anything.
#
# The reason this is a file rather than three inline pipelines: `az webapp list-runtimes`
# used to answer a bare array of identifier strings and now answers a row per runtime, whose
# first tab-separated column is the identifier and whose remaining columns are descriptive.
# A check written against the whole line silently started reporting that .NET 10 was
# unavailable while Azure was listing it. Anything that reads a CLI listing has to survive
# that class of change, and has to be testable.

# Every runtime identifier from `az webapp list-runtimes --output tsv`, one per line.
#
# Reads the first field only. That is the identifier in both output shapes Azure has used,
# and stays the identifier if more descriptive columns are added later.
az_runtime_identifiers() {
    # tr: an Azure CLI on Windows can emit CRLF, and a trailing carriage return would make
    # every exact comparison below fail for a reason nobody would guess from the message.
    tr -d '\r' | cut -f1 | grep -v '^[[:space:]]*$' || true
}

# True when the argument appears as a whole line on stdin.
#
# Fixed-string and whole-line, both deliberately. A runtime identifier contains `|` and `.`,
# which are regular-expression metacharacters, and `DOTNETCORE|10.0-preview` must not satisfy
# a check for `DOTNETCORE|10.0`.
az_contains_exactly() {
    grep -qxF -- "$1"
}

# The value of a single-quoted Bicep parameter in a .bicepparam file, or nothing.
#
# sed rather than `grep -oP`: PCRE is a GNU extension and this script should run on any
# machine the creator happens to be at, not only on a Cloud Shell.
az_bicepparam_value() {
    local name="$1" file="$2"
    sed -n "s/^param ${name} = '\([^']*\)'.*/\1/p" "$file" | head -n 1
}
