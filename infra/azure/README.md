# Azure infrastructure

Everything the Weapons of Order staging environment is made of, as code. No part of it is
reconstructed by clicking through the portal.

```text
bootstrap.sh                     provisions the whole environment in one command
main.bicep                       the whole environment, subscription-scoped
main.bicepparam                  region, SKUs, versions and names — no secrets
modules/
  monitoring.bicep               Log Analytics + workspace-based Application Insights
  database.bicep                 PostgreSQL Flexible Server + the staging database
  database-firewall.bicep        the permanent App Service rules
  email.bicep                    Communication Services + an Azure-managed email domain
  app.bicep                      the Linux App Service that serves client and /api
  deployment-identity.bicep      the GitHub OIDC identity and its two scoped roles
database/
  create-runtime-role.sql        creates the restricted application role — run once
  grant-runtime-role.sql         idempotent grants — re-run after every migration
  database-role.test.sh          runs both against a real PostgreSQL, on throwaway names
lib/
  az-output.sh                   parsing for Azure CLI listings, used by bootstrap.sh
  az-output.test.sh              its tests — no subscription, no network, creates nothing
  az-cli-syntax.test.sh          static check on the Azure CLI commands this repo runs
  psql-tls.sh                    where libpq should find its trusted roots
  psql-tls.test.sh               its tests, and a scan of every connection string
```

The full procedure, the GitHub Environment configuration and the teardown path are in
[`docs/deployment/AZURE_STAGING.md`](../../docs/deployment/AZURE_STAGING.md). This file covers
only the shape of the templates.

## Validate without an Azure subscription

Compiles and lints the whole graph. Needs no sign-in.

```bash
az bicep build --file infra/azure/main.bicep --stdout > /dev/null
```

```bash
WOO_PG_ADMIN_PASSWORD=x WOO_PG_APP_PASSWORD=y az bicep build-params --file infra/azure/main.bicepparam --stdout > /dev/null
```

`bootstrap.sh` refuses to provision unless the App Service stack and the PostgreSQL major
version it reads out of `main.bicepparam` are actually offered in the region. Those checks
read Azure CLI listings, whose column shape has changed before and will change again, so the
parsing lives in `lib/az-output.sh` and is tested against recorded output from both shapes:

```bash
infra/azure/lib/az-output.test.sh
```

A second check reads the `az postgres flexible-server firewall-rule` commands out of the
scripts, the workflow and the documented manual steps, and fails if any of them names the
server with `--name` or the rule with `--rule-name`. That is not a hypothetical: the CLI takes
`--server-name` for the server and `--name` for the rule, the repository had both wrong, and
nothing caught it until a real provisioning run reached the database-role step. A wrong flag
is not a syntax error, so `bash -n` will never see it.

```bash
infra/azure/lib/az-cli-syntax.test.sh
```

A third checks TLS. `sslmode=verify-full` reads like the whole answer and is not: libpq also
has to be told where the trusted roots are, and its default is a per-user file no fresh
machine has. `lib/psql-tls.sh` resolves that to the platform CA store, and the check scans
every Azure PostgreSQL connection string in the repository — libpq and Npgsql — and fails if
one stops verifying the server, drops to a mode that only encrypts, or sets
`Trust Server Certificate`.

```bash
infra/azure/lib/psql-tls.test.sh
```

All three run in CI's `Infra` job, which the packaging job depends on — so a broken
provisioning script cannot reach a deployment.

## Exercising the bootstrap SQL

The two SQL scripts cannot be checked by reading them. psql substitutes its variables in
ordinary SQL and **not** inside a quoted or dollar-quoted body, so a `DO $$ … $$` block
using `:'role'` reaches the server with the colons intact and fails there — which is
invisible until a server parses it, and is exactly how it was found: halfway through a real
provisioning run.

So they are run for real, against the local development PostgreSQL:

```bash
infra/azure/database/database-role.test.sh
```

It creates a database and a role named after its own process id, exercises creation, a
re-run with a different password, every privilege the application needs and every one it
must not have, and drops both however it exits. It never touches an existing database, an
existing role or any developer data, and it skips cleanly when no PostgreSQL is running.

It is not in the `Infra` CI job, which has no database. The `Server` job does have one, so
wiring it there is a one-line addition whenever that is wanted.

## Preview against a real subscription

```bash
az deployment sub what-if --location westeurope --name woo-staging --template-file infra/azure/main.bicep --parameters infra/azure/main.bicepparam
```

## Conventions

**Secrets never live here.** The two `@secure()` parameters are read from the environment by
`main.bicepparam`, so the parameter file is safe to commit and the values stay in a password
manager:

```bash
export WOO_PG_ADMIN_PASSWORD='...'
export WOO_PG_APP_PASSWORD='...'
```

**Names are deterministic.** Resources Azure requires to be globally unique take a
six-character hash of the subscription and resource group, so a redeployment reuses the same
names instead of generating new ones and a different environment gets different ones.

**Everything expensive is a parameter.** `appServicePlanSku`, `postgresSkuName`,
`postgresStorageGb`, `logDailyQuotaGb` and `location` are the levers, with staging-sized
defaults. Changing a SKU is a parameter change, not an edit to a resource.

**Roles are scoped to resources, not to the group.** The deployment identity gets Website
Contributor on the site and a custom firewall-only role on the database server. The site
identity gets permission to send email through one Communication Services resource. Neither
can touch anything else in the subscription.

## What is deliberately not here

- **A PostgreSQL role.** Resource Manager cannot create one, so `database/` holds SQL instead.
- **A private endpoint or VNet.** Migrations run from a GitHub-hosted runner whose address is
  not known in advance; the server is public with an explicit allow list and the deployment
  opens and closes its own rule. Access mode cannot be changed after creation, so this is a
  decision rather than a default.
- **Key Vault.** The one runtime secret is a database password delivered straight into an
  application setting by this template. A vault would add a hop without removing the secret.
- **A production environment, a deployment slot, a custom domain, Front Door, Container Apps,
  Kubernetes or Redis.** None is needed to run the game, and the ones that cost money would
  cost it every day.
