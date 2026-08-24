#!/usr/bin/env bash
#
# Exercises the bootstrap SQL against a real PostgreSQL.
#
#   infra/azure/database/database-role.test.sh
#
# Defaults to the local development server from docker-compose.yml. Override with the usual
# libpq environment variables, or with PSQL to point at a particular client:
#
#   PGHOST=localhost PGPORT=5433 PGUSER=woo_dev PGPASSWORD=woo_dev \
#     infra/azure/database/database-role.test.sh
#
# Creates a throwaway database and a throwaway role, both named with the process id, and drops
# both on the way out however it exits. It never touches an existing database, an existing
# role, or any developer data. It does not touch Azure.
#
# It exists because these scripts cannot be checked by reading them. The bug it was written
# for -- psql variables inside a dollar-quoted DO block, which the server receives with the
# colons intact -- is invisible until a server parses it, and it surfaced halfway through a
# real provisioning run.
set -uo pipefail

readonly HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# The Windows PostgreSQL client is not on PATH under Git Bash; look for it before giving up.
if [ -n "${PSQL:-}" ]; then
    :
elif command -v psql >/dev/null 2>&1; then
    PSQL=psql
elif [ -x "/c/Program Files/PostgreSQL/18/bin/psql.exe" ]; then
    PSQL="/c/Program Files/PostgreSQL/18/bin/psql.exe"
else
    echo "SKIP: no psql on PATH. Set PSQL to one, or run this where a client is installed." >&2
    exit 0
fi

# Never wait at a password prompt or on an unreachable host: this is a test, and a hang
# reads as a pass that never arrives.
export PGCONNECT_TIMEOUT="${PGCONNECT_TIMEOUT:-10}"
export PGHOST="${PGHOST:-localhost}"
export PGPORT="${PGPORT:-5433}"
export PGUSER="${PGUSER:-woo_dev}"
export PGPASSWORD="${PGPASSWORD:-woo_dev}"
readonly MAINTENANCE_DB="${PGDATABASE:-weapons_of_order}"

readonly TEST_DB="woo_roletest_$$"
readonly TEST_ROLE="woo_roletest_app_$$"
readonly ADMIN_ROLE="$PGUSER"

# Two different values, so the second run proves the ALTER path actually changed the password
# rather than leaving the first one in place. Not secrets; they exist for one process and are
# dropped with the role. They are still never printed.
readonly FIRST_PASSWORD="woo-test-$$-first"
readonly SECOND_PASSWORD="woo-test-$$-second"

failures=0

fail() { printf 'FAIL  %s\n' "$1" >&2; failures=$((failures + 1)); }
pass() { printf 'ok    %s\n' "$1"; }

# One value back from a query, with nothing around it.
query() {
    local database="$1" sql="$2"
    "$PSQL" --quiet --no-align --tuples-only --dbname "$database" --command "$sql"
}

check() {
    local description="$1" expected="$2" actual="$3"
    if [ "$expected" = "$actual" ]; then
        pass "$description"
    else
        fail "$description
       expected: $expected
       actual:   $actual"
    fi
}

# True when the statement is refused. Used to prove the runtime role cannot do things, which
# is the more important half of least privilege.
refused() {
    local database="$1" role_password="$2" sql="$3"
    PGPASSWORD="$role_password" "$PSQL" --quiet --dbname "$database" --username "$TEST_ROLE" \
        --set=ON_ERROR_STOP=1 --command "$sql" >/dev/null 2>&1
    [ $? -ne 0 ]
}

permitted() {
    local database="$1" role_password="$2" sql="$3"
    PGPASSWORD="$role_password" "$PSQL" --quiet --dbname "$database" --username "$TEST_ROLE" \
        --set=ON_ERROR_STOP=1 --command "$sql" >/dev/null 2>&1
}

cleanup() {
    "$PSQL" --quiet --dbname "$MAINTENANCE_DB" \
        --command "DROP DATABASE IF EXISTS \"$TEST_DB\" WITH (FORCE)" >/dev/null 2>&1
    "$PSQL" --quiet --dbname "$MAINTENANCE_DB" \
        --command "DROP ROLE IF EXISTS \"$TEST_ROLE\"" >/dev/null 2>&1
}
trap cleanup EXIT

# --- Setup ---------------------------------------------------------------------------------

if ! "$PSQL" --quiet --dbname "$MAINTENANCE_DB" --command 'SELECT 1' >/dev/null 2>&1; then
    echo "SKIP: no PostgreSQL at $PGHOST:$PGPORT. Start it with: docker compose up -d" >&2
    exit 0
fi

echo "Against PostgreSQL at $PGHOST:$PGPORT, database $TEST_DB, role $TEST_ROLE"
echo

cleanup
"$PSQL" --quiet --dbname "$MAINTENANCE_DB" --command "CREATE DATABASE \"$TEST_DB\"" >/dev/null \
    || { echo "Could not create the test database." >&2; exit 1; }

# The script under test, run the way bootstrap.sh runs it.
run_bootstrap_sql() {
    local password="$1"
    "$PSQL" --quiet --dbname "$TEST_DB" \
        --set=ON_ERROR_STOP=1 \
        --set=runtime_role="$TEST_ROLE" \
        --set=admin_role="$ADMIN_ROLE" \
        --set=runtime_password="$password" \
        --file "$HERE/create-runtime-role.sql"
}

# --- First run: the role does not exist yet --------------------------------------------------

echo "Creating the role"

output="$(run_bootstrap_sql "$FIRST_PASSWORD" 2>&1)"
status=$?

if [ "$status" -eq 0 ]; then
    pass "the script runs without error"
else
    fail "the script failed:
$output"
fi

# The bug this file was written for. Named explicitly so a regression is unmistakable.
case "$output" in
    *'syntax error at or near ":"'*)
        fail "psql variables reached the server uninterpolated -- a variable is inside a quoted body again" ;;
    *) pass "no uninterpolated psql variable reached the server" ;;
esac

check "the role exists and can log in" "t" \
    "$(query "$TEST_DB" "SELECT rolcanlogin FROM pg_roles WHERE rolname = '$TEST_ROLE'")"

# --- No password anywhere in the output ------------------------------------------------------

for secret in "$FIRST_PASSWORD" "$SECOND_PASSWORD"; do
    case "$output" in
        *"$secret"*) fail "the password appeared in psql's output" ;;
    esac
done
pass "the password is not echoed"

# --- Least privilege ---------------------------------------------------------------------------

echo
echo "Checking the role's attributes"

check "not a superuser"   "f" "$(query "$TEST_DB" "SELECT rolsuper       FROM pg_roles WHERE rolname = '$TEST_ROLE'")"
check "cannot create databases" "f" "$(query "$TEST_DB" "SELECT rolcreatedb   FROM pg_roles WHERE rolname = '$TEST_ROLE'")"
check "cannot create roles"     "f" "$(query "$TEST_DB" "SELECT rolcreaterole FROM pg_roles WHERE rolname = '$TEST_ROLE'")"
check "cannot replicate"        "f" "$(query "$TEST_DB" "SELECT rolreplication FROM pg_roles WHERE rolname = '$TEST_ROLE'")"
check "cannot bypass row-level security" "f" \
    "$(query "$TEST_DB" "SELECT rolbypassrls FROM pg_roles WHERE rolname = '$TEST_ROLE'")"

check "may connect to this database" "t" \
    "$(query "$TEST_DB" "SELECT has_database_privilege('$TEST_ROLE', current_database(), 'CONNECT')")"

# --- The password actually works, which is what proves the CREATE path -------------------------

echo
echo "Connecting as the role"

if permitted "$TEST_DB" "$FIRST_PASSWORD" 'SELECT 1'; then
    pass "the created password works"
else
    fail "could not connect with the password the script set"
fi

# --- Idempotency: a second run must reset the password, not fail --------------------------------

echo
echo "Re-running with a different password"

output="$(run_bootstrap_sql "$SECOND_PASSWORD" 2>&1)"
status=$?

if [ "$status" -eq 0 ]; then
    pass "a second run succeeds against an existing role"
else
    fail "the second run failed:
$output"
fi

if permitted "$TEST_DB" "$SECOND_PASSWORD" 'SELECT 1'; then
    pass "the new password works"
else
    fail "the second run did not reset the password"
fi

if refused "$TEST_DB" "$FIRST_PASSWORD" 'SELECT 1'; then
    pass "the previous password no longer works"
else
    fail "the previous password still works after a reset"
fi

# The attributes must survive a re-run, including one granted by hand in between.
"$PSQL" --quiet --dbname "$TEST_DB" --command "ALTER ROLE \"$TEST_ROLE\" CREATEDB" >/dev/null 2>&1
run_bootstrap_sql "$SECOND_PASSWORD" >/dev/null 2>&1
check "a privilege granted by hand is taken back by a re-run" "f" \
    "$(query "$TEST_DB" "SELECT rolcreatedb FROM pg_roles WHERE rolname = '$TEST_ROLE'")"

# --- What the role may and may not do with the schema ---------------------------------------------

echo
echo "Checking table privileges"

"$PSQL" --quiet --dbname "$TEST_DB" --command \
    'CREATE TABLE existing_table (id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY, value text)' >/dev/null

# Granted only after the table exists, which is the ordinary bootstrap order.
"$PSQL" --quiet --dbname "$TEST_DB" \
    --set=ON_ERROR_STOP=1 \
    --set=runtime_role="$TEST_ROLE" \
    --set=admin_role="$ADMIN_ROLE" \
    --file "$HERE/grant-runtime-role.sql" >/dev/null \
    || fail "grant-runtime-role.sql failed"

pass "grant-runtime-role.sql runs after it"

for statement in \
    "INSERT INTO existing_table (value) VALUES ('x')" \
    "SELECT * FROM existing_table" \
    "UPDATE existing_table SET value = 'y'" \
    "DELETE FROM existing_table"
do
    if permitted "$TEST_DB" "$SECOND_PASSWORD" "$statement"; then
        pass "may ${statement%% *}"
    else
        fail "could not ${statement%% *}, which the application needs"
    fi
done

for statement in \
    "CREATE TABLE forbidden_table (id integer)" \
    "TRUNCATE existing_table" \
    "DROP TABLE existing_table" \
    "ALTER TABLE existing_table ADD COLUMN sneaked text" \
    "CREATE ROLE another_role"
do
    if refused "$TEST_DB" "$SECOND_PASSWORD" "$statement"; then
        pass "refused: ${statement}"
    else
        fail "ALLOWED, and must not be: ${statement}"
    fi
done

# --- A table added later, which is what ALTER DEFAULT PRIVILEGES is for -----------------------------

echo
echo "Checking a table created after the grants ran"

"$PSQL" --quiet --dbname "$TEST_DB" --command \
    'CREATE TABLE later_table (id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY, value text)' >/dev/null

if permitted "$TEST_DB" "$SECOND_PASSWORD" "INSERT INTO later_table (value) VALUES ('x')"; then
    pass "a migration's new table is already writable"
else
    fail "a table created after the grants is not writable; ALTER DEFAULT PRIVILEGES did not apply"
fi

if permitted "$TEST_DB" "$SECOND_PASSWORD" "SELECT * FROM later_table"; then
    pass "a migration's new table is already readable"
else
    fail "a table created after the grants is not readable"
fi

# --- Result -----------------------------------------------------------------------------------------

echo
if [ "$failures" -gt 0 ]; then
    echo "$failures check(s) failed."
    exit 1
fi

echo "All checks passed."
