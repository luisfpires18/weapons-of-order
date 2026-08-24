-- Grants the application role read/write access to everything the schema currently holds,
-- and arranges for the same to apply to whatever a future migration adds.
--
-- Idempotent, and run by the deployment workflow after every migration:
--
--   psql "host=<server>.postgres.database.azure.com port=5432 dbname=weapons_of_order_staging user=woo_admin sslmode=verify-full sslrootcert=system" \
--     -v ON_ERROR_STOP=1 -v runtime_role=woo_app -v admin_role=woo_admin \
--     -f infra/azure/database/grant-runtime-role.sql
--
-- ALTER DEFAULT PRIVILEGES is what keeps this coherent as the schema grows: a table created
-- later by the migration role is already granted, so a deployment that adds a table does not
-- also need somebody to remember this file. Re-running it anyway costs nothing and closes
-- the gap if a table ever arrives by another route.
--
-- Note what is absent. No CREATE on the schema, so the application cannot add or drop a
-- table; no TRUNCATE, so it cannot empty one; no ownership, so it cannot ALTER one. Since
-- PostgreSQL 15 the public schema does not grant CREATE to PUBLIC either, so there is no
-- inherited way around that.

\set ON_ERROR_STOP on

GRANT USAGE ON SCHEMA public TO :"runtime_role";

GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO :"runtime_role";

GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO :"runtime_role";

ALTER DEFAULT PRIVILEGES FOR ROLE :"admin_role" IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO :"runtime_role";

ALTER DEFAULT PRIVILEGES FOR ROLE :"admin_role" IN SCHEMA public
    GRANT USAGE, SELECT ON SEQUENCES TO :"runtime_role";
