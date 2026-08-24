#!/usr/bin/env bash
#
# Tests for the GitHub federated credential subject in the Azure templates.
#
#   infra/azure/oidc-subject.test.sh
#
# Needs no Azure subscription, no sign-in, no network and no Bicep CLI: it reads the
# templates as text. The failure it exists to catch cannot be caught by anything else in the
# repository, because the template compiles either way and the mismatch only appears against
# a real assertion, as
#
#   AADSTS700213: No matching federated identity record found for presented assertion subject
#
# GitHub presents the immutable subject for this repository — owner and repository names each
# followed by their numeric id — and Entra matches the configured subject literally. The
# older mutable form is what was configured when that error was hit in production, so this
# checks both that the immutable form is built and that the mutable one has not come back.
set -uo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
azure="$root/infra/azure"
module="$azure/modules/deployment-identity.bicep"
main="$azure/main.bicep"
parameters="$azure/main.bicepparam"

# shellcheck source=./lib/az-output.sh
. "$azure/lib/az-output.sh"

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

# --- What the module builds the subject out of ---------------------------------------------

# The interpolated expression as written, not the value: this is the shape that must not
# revert, independently of what any one parameter happens to be set to.
subject_expression="$(sed -n "s/^var federatedSubject = '\(.*\)'$/\1/p" "$module" | head -n 1)"

check "the module builds the subject in the immutable form" \
    'repo:${repositoryOwner}@${gitHubRepositoryOwnerId}/${repositoryName}@${gitHubRepositoryId}:environment:${gitHubEnvironment}' \
    "$subject_expression"

# Assembled rather than written out, so this file does not itself contain the string the
# repository-wide search below is looking for.
legacy_expression="repo:\${gitHubRepository}:environment:\${gitHubEnvironment}"

check "the mutable expression is not what the credential is configured with" \
    "no" \
    "$(if [ "$subject_expression" = "$legacy_expression" ]; then echo yes; else echo no; fi)"

check "the credential's subject property is the assembled variable, not a literal" \
    "    subject: federatedSubject" \
    "$(grep -n '^    subject:' "$module" | head -n 1 | cut -d: -f2-)"

# --- Rendering that expression with the configured parameters ------------------------------

repository="$(az_bicepparam_value gitHubRepository "$parameters")"
owner_id="$(az_bicepparam_value gitHubRepositoryOwnerId "$parameters")"
repository_id="$(az_bicepparam_value gitHubRepositoryId "$parameters")"
environment="$(az_bicepparam_value gitHubEnvironment "$parameters")"

check "the repository is read from main.bicepparam" 'luisfpires18/weapons-of-order' "$repository"
check "the owner id is read from main.bicepparam" '29492601' "$owner_id"
check "the repository id is read from main.bicepparam" '1329180174' "$repository_id"
check "the environment is read from main.bicepparam" 'staging' "$environment"

rendered="$subject_expression"
rendered="${rendered//'${repositoryOwner}'/${repository%%/*}}"
rendered="${rendered//'${repositoryName}'/${repository##*/}}"
rendered="${rendered//'${gitHubRepositoryOwnerId}'/$owner_id}"
rendered="${rendered//'${gitHubRepositoryId}'/$repository_id}"
rendered="${rendered//'${gitHubEnvironment}'/$environment}"

# The one assertion that has to hold character for character against the token GitHub mints.
check "the configured subject is the one GitHub actually presents" \
    'repo:luisfpires18@29492601/weapons-of-order@1329180174:environment:staging' \
    "$rendered"

# A part the substitutions above do not know about would otherwise render as a literal
# `${...}` and be configured that way without anything complaining.
check "every part of the expression was substituted" \
    "no" \
    "$(case "$rendered" in *'${'*) echo yes ;; *) echo no ;; esac)"

# --- The identifiers reach the module ------------------------------------------------------

declared() {
    if grep -qxF "param $1 string" "$module"; then echo yes; else echo no; fi
}

passed() {
    if grep -qxF "    $1: $1" "$main"; then echo yes; else echo no; fi
}

check "the module declares the owner id" "yes" "$(declared gitHubRepositoryOwnerId)"
check "the module declares the repository id" "yes" "$(declared gitHubRepositoryId)"
check "main.bicep passes the owner id to the module" "yes" "$(passed gitHubRepositoryOwnerId)"
check "main.bicep passes the repository id to the module" "yes" "$(passed gitHubRepositoryId)"

# --- Nowhere in the repository is the mutable subject presented as the right one -------------

legacy_literal="repo:$repository:environment:$environment"

matches_for() {
    local needle="$1" hits
    hits="$(cd "$root" && git ls-files -z | xargs -0 grep -lF -- "$needle" 2>/dev/null)"
    if [ -n "$hits" ]; then printf '%s' "$hits" | tr '\n' ' '; else echo "none"; fi
}

check "the mutable subject appears in no tracked file" "none" "$(matches_for "$legacy_literal")"
check "the mutable expression appears in no tracked file" "none" "$(matches_for "$legacy_expression")"

# --------------------------------------------------------------------------------------------

echo
if [ "$failures" -gt 0 ]; then
    echo "$failures check(s) failed."
    exit 1
fi

echo "All checks passed."
