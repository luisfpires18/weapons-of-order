-- Creates the restricted role the running application connects as, and grants it exactly
-- what the application needs.
--
-- Run ONCE at bootstrap, connected to the staging database as the server administrator.
-- Azure Resource Manager cannot create a PostgreSQL role, so this is the one piece of the
-- environment that is not in Bicep.
--
--   psql "host=<server>.postgres.database.azure.com port=5432 dbname=weapons_of_order_staging user=woo_admin sslmode=verify-full sslrootcert=system" \
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
--
-- Exercised against a real PostgreSQL, with throwaway names, by database-role.test.sh.

\set ON_ERROR_STOP on

-- Nothing here may print the password. psql does not echo what \gexec runs, and this makes
-- that true regardless of an ECHO the caller set on the command line.
\set ECHO none

-- ---------------------------------------------------------------------------------------
-- Why the two statements below are generated rather than written out.
--
-- psql substitutes its variables in ordinary SQL text and NOT inside a quoted or
-- dollar-quoted body. A `DO $$ ... :'runtime_role' ... $$` block therefore reaches the server
-- with the colons intact, and the server answers `syntax error at or near ":"`. That is what
-- this file used to do.
--
-- So the values are interpolated out here, where substitution does happen, into a statement
-- built by format() -- %I quotes an identifier, %L quotes a literal -- and \gexec runs the
-- result. Neither the role name nor the password is pasted in raw, and neither depends on a
-- quoting rule this file would have to get right by hand.
-- ---------------------------------------------------------------------------------------

-- An existing role has its password reset, so a re-run cannot leave the deployment holding a
-- password the server no longer accepts.
SELECT format('ALTER ROLE %I LOGIN PASSWORD %L', :'runtime_role', :'runtime_password')
FROM pg_roles
WHERE rolname = :'runtime_role'
\gexec

-- An absent one is created. Exactly one of these two produces a row, which is what makes the
-- script idempotent without a conditional block and without an error to catch.
SELECT format('CREATE ROLE %I LOGIN PASSWORD %L', :'runtime_role', :'runtime_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'runtime_role')
\gexec

-- No CREATEDB, no CREATEROLE, no BYPASSRLS, no superuser, and no replication. Stated rather
-- than assumed, so an attribute granted by hand is taken back by a re-run.
--
-- Written out rather than generated: this statement carries no literal, and outside a quoted
-- body ordinary psql substitution applies, where :"..." is a safely quoted identifier.
ALTER ROLE :"runtime_role" NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;

-- current_database() rather than another psql variable: the script is always run against the
-- database it is granting on, and one fewer parameter is one fewer way to get it wrong. It
-- has to be generated because GRANT will not take the database name as an expression.
SELECT format('GRANT CONNECT ON DATABASE %I TO %I', current_database(), :'runtime_role')
\gexec

\ir grant-runtime-role.sql
