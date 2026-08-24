-- Creates the restricted role the running application connects as, and grants it exactly
-- what the application needs.
--
-- Run ONCE at bootstrap, connected to the staging database as the server administrator.
-- Azure Resource Manager cannot create a PostgreSQL role, so this is the one piece of the
-- environment that is not in Bicep.
--
--   psql "host=<server>.postgres.database.azure.com port=5432 dbname=weapons_of_order_staging user=woo_admin sslmode=verify-full" \
--     -v ON_ERROR_STOP=1 \
--     -v runtime_role=woo_app \
--     -v admin_role=woo_admin \
--     -v runtime_password="$WOO_PG_APP_PASSWORD" \
--     -f infra/azure/database/create-runtime-role.sql
--
-- Safe to re-run: it resets the password rather than failing on an existing role.
--
-- The point of the separation is that the running application cannot change the schema. It
-- can read and write the game's rows; it cannot CREATE, DROP or ALTER a table, and it cannot
-- rewrite the migration history to make a rollback look like it never happened. Migrations
-- run as the administrator from the deployment workflow, which is the only place schema
-- changes come from.

\set ON_ERROR_STOP on

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'runtime_role') THEN
        EXECUTE format('ALTER ROLE %I LOGIN PASSWORD %L', :'runtime_role', :'runtime_password');
    ELSE
        EXECUTE format('CREATE ROLE %I LOGIN PASSWORD %L', :'runtime_role', :'runtime_password');
    END IF;
END
$$;

-- No CREATEDB, no CREATEROLE, no BYPASSRLS, no superuser, and no membership of
-- azure_pg_admin. Stated rather than assumed, so an inherited grant is undone by a re-run.
ALTER ROLE :"runtime_role" NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;

-- current_database() rather than another psql variable: the script is always run against
-- the database it is granting on, and one fewer parameter is one fewer way to get it wrong.
DO $$
BEGIN
    EXECUTE format('GRANT CONNECT ON DATABASE %I TO %I', current_database(), :'runtime_role');
END
$$;

\ir grant-runtime-role.sql
