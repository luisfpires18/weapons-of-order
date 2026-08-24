#!/usr/bin/env bash
#
# Static check on the Azure CLI commands this repository tells people — and GitHub Actions —
# to run.
#
#   infra/azure/lib/az-cli-syntax.test.sh
#
# Needs no Azure subscription, no sign-in and no network, and creates nothing. It reads the
# tracked files.
#
# It exists because of one specific failure. `az postgres flexible-server firewall-rule` takes
# `--server-name` for the server and `--name` for the rule; this repository used `--name` for
# the server and `--rule-name` for the rule. Every command parsed fine, every script passed
# `bash -n`, and provisioning got all the way to creating the database role before Azure
# answered `the following arguments are required: --server-name/-s`.
#
# A shell syntax check cannot see a wrong flag. This can, and it reads the scripts, the
# workflow and the documented manual commands together, so the three cannot drift apart.
set -uo pipefail

readonly ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

readonly FIREWALL_COMMAND='az postgres flexible-server firewall-rule'

failures=0

fail() {
    printf 'FAIL  %s\n' "$1" >&2
    failures=$((failures + 1))
}

pass() { printf 'ok    %s\n' "$1"; }

# --- What is checked, and where -------------------------------------------------------------

# Every firewall-rule invocation in a file, one per line, with backslash continuations joined
# so a command split over six lines is examined as one.
#
# Anchored at the start of the line, so prose that merely names the command — documentation
# explaining this very check, for one — is not mistaken for an instruction to run it.
firewall_rule_commands() {
    sed -e :a -e '/\\$/N; s/\\\n/ /; ta' "$1" \
        | grep -E "^[[:space:]]*(\\\$ )?${FIREWALL_COMMAND}" || true
}

# Problems with one invocation, one per line, or nothing when it is well formed.
firewall_rule_problems() {
    local command="$1"

    printf '%s' "$command" | grep -qF -- '--resource-group' \
        || echo "missing --resource-group"

    # The server. This is the one the Azure CLI insists on and the repository got wrong.
    printf '%s' "$command" | grep -qF -- '--server-name' \
        || echo "missing --server-name (the PostgreSQL server)"

    # The rule. Matched with boundaries, so --server-name and --rule-name cannot satisfy it.
    printf '%s' "$command" | grep -qE -- '(^|[[:space:]])--name([[:space:]]|=)' \
        || echo "missing --name (the firewall rule)"

    if printf '%s' "$command" | grep -qF -- '--rule-name'; then
        echo "uses --rule-name, which is not an argument of this command"
    fi
}

# --- The checker's own tests, so a broken checker is not mistaken for a clean repository ------

check_fixture() {
    local description="$1" expected="$2" command="$3"
    local actual
    actual="$(firewall_rule_problems "$command" | tr '\n' ';' | sed 's/;$//')"

    if [ "$actual" = "$expected" ]; then
        pass "$description"
    else
        fail "$description
       expected: ${expected:-<no problems>}
       actual:   ${actual:-<no problems>}"
    fi
}

echo "Checking the checker"

check_fixture "a correct create is accepted" "" \
    "$FIREWALL_COMMAND create --resource-group rg --server-name psql --name rule --start-ip-address 1.2.3.4 --end-ip-address 1.2.3.4"

check_fixture "a correct delete is accepted" "" \
    "$FIREWALL_COMMAND delete --resource-group rg --server-name psql --name rule --yes"

check_fixture "the exact regression is caught" \
    "missing --server-name (the PostgreSQL server);uses --rule-name, which is not an argument of this command" \
    "$FIREWALL_COMMAND create --resource-group rg --name psql --rule-name rule --start-ip-address 1.2.3.4"

check_fixture "--server-name alone does not satisfy the rule name" \
    "missing --name (the firewall rule)" \
    "$FIREWALL_COMMAND delete --resource-group rg --server-name psql --yes"

check_fixture "a missing resource group is caught" \
    "missing --resource-group" \
    "$FIREWALL_COMMAND delete --server-name psql --name rule --yes"

# The line filter matters as much as the rule: too loose and documentation about this check
# fails it, too tight and a real command is skipped in silence.
fixture_file="$(mktemp)"
trap 'rm -f "$fixture_file"' EXIT
cat >"$fixture_file" <<FIXTURE
Prose naming the $FIREWALL_COMMAND command mid-sentence is not an invocation.

    $FIREWALL_COMMAND delete --resource-group rg --server-name psql --name rule --yes

$FIREWALL_COMMAND create \\
    --resource-group rg \\
    --server-name psql \\
    --name rule \\
    --start-ip-address 1.2.3.4 --end-ip-address 1.2.3.4
FIXTURE

fixture_count="$(firewall_rule_commands "$fixture_file" | wc -l | tr -d ' ')"
if [ "$fixture_count" = "2" ]; then
    pass "indented and continued commands are found, prose is not"
else
    fail "expected 2 commands in the fixture, found $fixture_count"
fi

# --- The repository ---------------------------------------------------------------------------

echo
echo "Checking the repository"

# Tracked files only, so this never walks node_modules or a build directory.
mapfile -t files < <(cd "$ROOT" && git ls-files '*.sh' '*.yml' '*.yaml' '*.md')

commands_found=0

for file in "${files[@]}"; do
    while IFS= read -r command; do
        commands_found=$((commands_found + 1))

        problems="$(firewall_rule_problems "$command")"
        if [ -z "$problems" ]; then
            pass "$file: $(printf '%s' "$command" | grep -oE 'firewall-rule [a-z]+')"
        else
            while IFS= read -r problem; do
                fail "$file: $problem
       in: $command"
            done <<<"$problems"
        fi
    done < <(firewall_rule_commands "$ROOT/$file")
done

# A repository that stopped running these commands would otherwise pass this file in silence.
if [ "$commands_found" -eq 0 ]; then
    fail "no '$FIREWALL_COMMAND' command was found at all. If the temporary-rule approach was replaced, delete this check; if it was lost by accident, restore it."
else
    pass "found and checked $commands_found firewall-rule command(s)"
fi

# --- Result ------------------------------------------------------------------------------------

echo
if [ "$failures" -gt 0 ]; then
    echo "$failures check(s) failed."
    exit 1
fi

echo "All checks passed."
