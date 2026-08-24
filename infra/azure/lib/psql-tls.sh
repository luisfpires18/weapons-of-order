#!/usr/bin/env bash
#
# Where libpq should find its trusted roots when connecting to Azure PostgreSQL.
#
# Sourced by infra/azure/bootstrap.sh and by the deployment workflow's grant step, and tested
# on its own by psql-tls.test.sh.
#
# The problem this solves: verify-full alone does not tell libpq *what* to verify against.
# With sslrootcert unset, libpq looks for ~/.postgresql/root.crt and fails outright when it is
# absent, which on a fresh Cloud Shell it always is:
#
#     psql: error: connection to server at "psql-....postgres.database.azure.com" failed:
#     root certificate file "/home/<user>/.postgresql/root.crt" does not exist
#
# The answer is to name the platform's own CA store. Azure PostgreSQL's certificate chains to
# a public root that any current trust store already carries, so there is nothing to download
# and nothing to pin -- pinning an intermediate would only turn Azure's next rotation into an
# outage.
#
# What this must never do is answer with anything that verifies less. Encrypting without
# checking who answered is no defence against an attacker who can reach the DNS name, and
# turning verification off is worse. Neither is a fix for a missing root store; both are a
# different, quieter bug.

# Candidate system CA bundles, most specific first. Overridable so the fallback below can be
# exercised in a test without a PostgreSQL 15 to hand.
: "${AZURE_POSTGRES_CA_BUNDLES:=/etc/ssl/certs/ca-certificates.crt /etc/pki/tls/certs/ca-bundle.crt /etc/ssl/ca-bundle.pem /etc/ssl/cert.pem}"

# The value for libpq's sslrootcert, or nothing at all with a non-zero status when no trust
# store can be found. A caller that cannot get an answer must stop, not connect anyway.
azure_postgres_sslrootcert() {
    # "system" means the platform's own CA store. libpq has understood it since PostgreSQL 16;
    # older clients need sslrootcert to name a file, which is what the fallback below does.
    # The fallback verifies against the same roots -- it is a different way of saying where
    # they are, not a weaker check.
    local major
    major="$(psql_major_version)"

    if [ -n "$major" ] && [ "$major" -ge 16 ]; then
        echo system
        return 0
    fi

    local bundle
    # Word splitting is the point: the variable holds a list of paths.
    # shellcheck disable=SC2086
    for bundle in $AZURE_POSTGRES_CA_BUNDLES; do
        if [ -r "$bundle" ]; then
            echo "$bundle"
            return 0
        fi
    done

    return 1
}

# The major version of the psql on PATH, or nothing.
psql_major_version() {
    psql --version 2>/dev/null | psql_major_from_version_output
}

# The major version out of a `psql --version` line, or nothing.
#
# Parsed rather than assumed: the line reads "psql (PostgreSQL) 16.4 (Ubuntu 16.4-0ubuntu0.1)"
# on Debian-family images and "psql (PostgreSQL) 16.4" elsewhere. Split from the function
# above so it can be tested without installing four versions of PostgreSQL.
psql_major_from_version_output() {
    sed -n 's/^psql (PostgreSQL) \([0-9][0-9]*\).*/\1/p' | head -n 1
}

# The full libpq connection string for an Azure PostgreSQL database, with the trust source in
# it.
#
# Built here rather than at each call site so the security requirement travels with the
# command instead of depending on an environment variable somebody has to remember to export.
# The password is deliberately absent: it goes in PGPASSWORD, so it never reaches a process
# list or a shell history.
azure_postgres_conninfo() {
    local host="$1" database="$2" user="$3" sslrootcert="$4"

    printf 'host=%s port=5432 dbname=%s user=%s sslmode=verify-full sslrootcert=%s' \
        "$host" "$database" "$user" "$sslrootcert"
}
