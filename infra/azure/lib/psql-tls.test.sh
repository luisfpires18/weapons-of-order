#!/usr/bin/env bash
#
# Tests for psql-tls.sh, and a static check that every Azure PostgreSQL connection in this
# repository verifies the server properly.
#
#   infra/azure/lib/psql-tls.test.sh
#
# Needs no Azure subscription, no sign-in, no network and no PostgreSQL. Creates nothing.
#
# It exists because `sslmode=verify-full` reads like the whole answer and is not: libpq also
# needs to be told where the trusted roots are, and its default is a per-user file that does
# not exist on a fresh machine. Provisioning failed on exactly that, and the tempting fix --
# dropping to a weaker sslmode -- would have made the error go away while removing the check
# that proves the server answering is Azure's. The scan below is what stops that fix from
# being made quietly.
set -uo pipefail

readonly HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly ROOT="$(cd "$HERE/../../.." && pwd)"

# shellcheck source=./psql-tls.sh
. "$HERE/psql-tls.sh"

failures=0

fail() {
    printf 'FAIL  %s\n' "$1" >&2
    failures=$((failures + 1))
}

pass() { printf 'ok    %s\n' "$1"; }

check() {
    local description="$1" expected="$2" actual="$3"

    if [ "$expected" = "$actual" ]; then
        pass "$description"
    else
        fail "$description
       expected: ${expected:-<nothing>}
       actual:   ${actual:-<nothing>}"
    fi
}

# --- Reading the client version -------------------------------------------------------------

echo "Reading the psql version"

check "a Debian-family version line" "16" \
    "$(printf 'psql (PostgreSQL) 16.4 (Ubuntu 16.4-0ubuntu0.24.04.1)\n' | psql_major_from_version_output)"

check "a plain version line" "15" \
    "$(printf 'psql (PostgreSQL) 15.7\n' | psql_major_from_version_output)"

check "a two-digit major is not truncated" "18" \
    "$(printf 'psql (PostgreSQL) 18.1\n' | psql_major_from_version_output)"

check "unrecognised output yields nothing rather than a wrong number" "" \
    "$(printf 'psql: command not found\n' | psql_major_from_version_output)"

# --- Choosing the trust source ---------------------------------------------------------------

echo
echo "Choosing the trust source"

# PostgreSQL 16 and later understand the system store directly.
psql_major_version() { echo 16; }
check "PostgreSQL 16 uses the system store" "system" "$(azure_postgres_sslrootcert)"

psql_major_version() { echo 18; }
check "PostgreSQL 18 uses the system store" "system" "$(azure_postgres_sslrootcert)"

# Older clients need a path. It must still be a real CA bundle: the same roots, named
# differently, never a weaker mode.
bundle="$(mktemp)"
trap 'rm -f "$bundle"' EXIT

psql_major_version() { echo 15; }
AZURE_POSTGRES_CA_BUNDLES="/nonexistent/one $bundle /nonexistent/two"
check "an older client falls back to a readable CA bundle" "$bundle" "$(azure_postgres_sslrootcert)"

AZURE_POSTGRES_CA_BUNDLES="/nonexistent/one /nonexistent/two"
result="$(azure_postgres_sslrootcert)"
status=$?
if [ "$status" -ne 0 ] && [ -z "$result" ]; then
    pass "no trust store at all is an error, not a weaker connection"
else
    fail "expected a non-zero status and no output, got status $status and '$result'"
fi

psql_major_version() { echo ""; }
AZURE_POSTGRES_CA_BUNDLES="$bundle"
check "an unreadable psql version still finds a bundle" "$bundle" "$(azure_postgres_sslrootcert)"

# --- The connection string --------------------------------------------------------------------

echo
echo "Building the connection string"

check "the trust source travels with the command" \
    "host=psql-x.postgres.database.azure.com port=5432 dbname=db user=woo_admin sslmode=verify-full sslrootcert=system" \
    "$(azure_postgres_conninfo psql-x.postgres.database.azure.com db woo_admin system)"

conninfo="$(azure_postgres_conninfo host db user system)"
case "$conninfo" in
    *password=*) fail "the connection string carries a password; it belongs in PGPASSWORD" ;;
    *) pass "no password in the connection string" ;;
esac

# --- The repository -----------------------------------------------------------------------------
#
# This file is excluded from the scan: it deliberately contains the weak values the scan
# rejects, as the fixtures below.

echo
echo "Checking the repository"

readonly SELF="infra/azure/lib/psql-tls.test.sh"

# A libpq connection string, as opposed to prose about one, is a line carrying both a host and
# an sslmode. Prose naming a mode in passing has no host and is not an instruction to connect.
libpq_conninfo_lines() {
    sed -e :a -e '/\\$/N; s/\\\n/ /; ta' "$1" \
        | grep -F 'sslmode=' \
        | grep -F 'host=' || true
}

npgsql_lines() {
    sed -e :a -e '/\\$/N; s/\\\n/ /; ta' "$1" | grep -F 'SSL Mode=' || true
}

libpq_problems() {
    local line="$1"

    printf '%s' "$line" | grep -qF -- 'sslmode=verify-full' \
        || echo "does not use sslmode=verify-full"

    printf '%s' "$line" | grep -qE -- 'sslrootcert=[^[:space:]"]' \
        || echo "uses verify-full without naming a trust source (sslrootcert=)"

    if printf '%s' "$line" | grep -qE -- 'sslmode=(require|prefer|allow|disable)'; then
        echo "uses an sslmode that does not verify the server"
    fi
}

npgsql_problems() {
    local line="$1" lowered

    # Matched with shell patterns rather than grep: a settings key can be written in any case,
    # and `grep -qiF` is not reliable on every platform this is run from.
    lowered="$(printf '%s' "$line" | tr '[:upper:]' '[:lower:]')"

    case "$lowered" in
        *"ssl mode=verifyfull"*) ;;
        *) echo "does not use SSL Mode=VerifyFull" ;;
    esac

    case "$lowered" in
        *"trust server certificate"*)
            echo "sets Trust Server Certificate, which disables verification" ;;
    esac

    # Npgsql validates against the operating system's trust store on its own and has no
    # sslrootcert keyword at all. Borrowing libpq's spelling would be silently ignored, so the
    # connection would look configured and would not be.
    case "$lowered" in
        *sslrootcert*)
            echo "carries libpq's sslrootcert, which is not an Npgsql keyword" ;;
    esac
}

libpq_found=0
npgsql_found=0

mapfile -t files < <(cd "$ROOT" && git ls-files '*.sh' '*.yml' '*.yaml' '*.md' '*.sql' '*.bicep')

for file in "${files[@]}"; do
    [ "$file" != "$SELF" ] || continue

    while IFS= read -r line; do
        libpq_found=$((libpq_found + 1))
        problems="$(libpq_problems "$line")"
        if [ -z "$problems" ]; then
            pass "$file: libpq connection verifies against a named trust source"
        else
            while IFS= read -r problem; do
                fail "$file: $problem
       in: $line"
            done <<<"$problems"
        fi
    done < <(libpq_conninfo_lines "$ROOT/$file")

    while IFS= read -r line; do
        npgsql_found=$((npgsql_found + 1))
        problems="$(npgsql_problems "$line")"
        if [ -z "$problems" ]; then
            pass "$file: Npgsql connection uses VerifyFull"
        else
            while IFS= read -r problem; do
                fail "$file: $problem
       in: $line"
            done <<<"$problems"
        fi
    done < <(npgsql_lines "$ROOT/$file")
done

[ "$libpq_found" -gt 0 ] \
    && pass "found and checked $libpq_found libpq connection(s)" \
    || fail "no libpq connection string was found at all; if the psql steps were removed, delete this check"

[ "$npgsql_found" -gt 0 ] \
    && pass "found and checked $npgsql_found Npgsql connection(s)" \
    || fail "no Npgsql connection string was found at all; if the connection moved, update this check"

# --- The checker's own rules ----------------------------------------------------------------------

echo
echo "Checking the checker"

check_rule() {
    local description="$1" expected="$2" actual
    actual="$(libpq_problems "$3" | tr '\n' ';' | sed 's/;$//')"
    check "$description" "$expected" "$actual"
}

check_rule "a correct libpq connection is accepted" "" \
    'host=x.postgres.database.azure.com port=5432 dbname=d user=u sslmode=verify-full sslrootcert=system'

check_rule "the exact regression is caught" \
    "uses verify-full without naming a trust source (sslrootcert=)" \
    'host=x.postgres.database.azure.com port=5432 dbname=d user=u sslmode=verify-full'

check_rule "dropping to require is caught" \
    "does not use sslmode=verify-full;uses verify-full without naming a trust source (sslrootcert=);uses an sslmode that does not verify the server" \
    'host=x port=5432 sslmode=require'

check_rule "disabling TLS is caught" \
    "does not use sslmode=verify-full;uses verify-full without naming a trust source (sslrootcert=);uses an sslmode that does not verify the server" \
    'host=x port=5432 sslmode=disable'

check_rule "an empty trust source is not a trust source" \
    "uses verify-full without naming a trust source (sslrootcert=)" \
    'host=x sslmode=verify-full sslrootcert='

check_npgsql_rule() {
    local description="$1" expected="$2" actual
    actual="$(npgsql_problems "$3" | tr '\n' ';' | sed 's/;$//')"
    check "$description" "$expected" "$actual"
}

check_npgsql_rule "a correct Npgsql connection is accepted" "" \
    'Host=x.postgres.database.azure.com;Port=5432;Database=d;Username=u;Password=p;SSL Mode=VerifyFull'

check_npgsql_rule "trusting any certificate is caught" \
    "sets Trust Server Certificate, which disables verification" \
    'Host=x;SSL Mode=VerifyFull;Trust Server Certificate=true'

check_npgsql_rule "a weaker Npgsql mode is caught" \
    "does not use SSL Mode=VerifyFull" \
    'Host=x;SSL Mode=Require'

check_npgsql_rule "libpq's keyword in an Npgsql string is caught" \
    "carries libpq's sslrootcert, which is not an Npgsql keyword" \
    'Host=x;SSL Mode=VerifyFull;sslrootcert=system'

# --- Result -------------------------------------------------------------------------------------

echo
if [ "$failures" -gt 0 ]; then
    echo "$failures check(s) failed."
    exit 1
fi

echo "All checks passed."
