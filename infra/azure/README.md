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
