# Azure infrastructure

Everything the Weapons of Order staging environment is made of, as code. No part of it is
reconstructed by clicking through the portal.

```text
bootstrap.sh                     provisions the whole environment in one command
main.bicep                       the whole environment, subscription-scoped
main.bicepparam                  region, SKUs, versions and names — no secrets
oidc-subject.test.sh             pins the GitHub federated subject — no subscription, no CLI
modules/
  monitoring.bicep               Log Analytics + workspace-based Application Insights
  email.bicep                    Communication Services + an Azure-managed email domain
  app.bicep                      the Linux App Service that serves client, /api and the database
  deployment-identity.bicep      the GitHub OIDC identity, its immutable subject and its one scoped role
lib/
  az-output.sh                   parsing for Azure CLI listings, used by bootstrap.sh
  az-output.test.sh              its tests — no subscription, no network, creates nothing
```

There is no database module. Browser V1 keeps its data in a SQLite file on the App Service
instance's persistent `/home` share, so there is no server to provision, no password to hold
and no firewall to manage. PostgreSQL remains the intended direction for a real production
environment and is not built here.

The full procedure, the GitHub Environment configuration, the persistence rules and the
teardown path are in [`docs/deployment/AZURE_STAGING.md`](../../docs/deployment/AZURE_STAGING.md).
This file covers only the shape of the templates.

## Validate without an Azure subscription

Compiles and lints the whole graph. Needs no sign-in.

```bash
az bicep build --file infra/azure/main.bicep --stdout > /dev/null
```

```bash
az bicep build-params --file infra/azure/main.bicepparam --stdout > /dev/null
```

`bootstrap.sh` refuses to provision unless the App Service stack it reads out of
`main.bicepparam` is actually offered in the region. That check reads an Azure CLI listing,
whose column shape has changed before and will change again, so the parsing lives in
`lib/az-output.sh` and is tested against recorded output from both shapes:

```bash
infra/azure/lib/az-output.test.sh
```

The federated credential has a failure of the same kind and is checked the same way. Its
subject is compared literally by Entra against the `sub` of the token GitHub presents, which
for this repository is the immutable form — owner and repository names each followed by their
numeric id:

```text
repo:luisfpires18@29492601/weapons-of-order@1329180174:environment:staging
```

Nothing that runs before a deployment can tell a wrong subject from a right one: the template
compiles either way and Entra only refuses against a real token, as `AADSTS700213`. So the
subject is asserted as text instead, needing neither a subscription nor the Bicep CLI:

```bash
infra/azure/oidc-subject.test.sh
```

Both run in CI's `Infra` job, which the packaging job depends on — so a broken provisioning
script cannot reach a deployment.

## Conventions

**Nothing has to be supplied.** `main.bicep` takes no `@secure()` parameter at all: no
password to generate, no database credential, and no Azure client secret — the deployment
authenticates with a short-lived GitHub OIDC token, and the connection string is a file path.
The one `@secure()` in the tree is internal, carrying the Application Insights connection
string from the monitoring module into an application setting.

**Names are deterministic.** Resources Azure requires to be globally unique take a
six-character hash of the subscription and resource group, so a redeployment reuses the same
names instead of generating new ones and a different environment gets different ones.

**Everything expensive is a parameter.** `appServicePlanSku`/`appServicePlanTier`,
`logDailyQuotaGb` and `location` are the levers, with prototype-sized defaults: the plan is
the Free tier, so the environment has no fixed monthly cost.

**Roles are scoped to resources, not to the group.** The deployment identity gets Website
Contributor on the site and nothing else. The site identity gets permission to send email
through one Communication Services resource. Neither can touch anything else in the
subscription.

**The database path is outside the deployed application.** `/home/data/weapons-of-order.db`,
not somewhere under the site's application directory, because a zip deployment replaces that
directory wholesale. This is the one invariant in the whole environment that would lose player
data if it were got wrong.

## What is deliberately not here

- **A database server.** One player, no traffic, and a file does the job. Reconsider when
  production is designed, not before.
- **Key Vault.** There is no runtime secret left for it to hold.
- **A private endpoint or VNet.** Nothing to put in one.
- **A production environment, a deployment slot, a custom domain, Front Door, Container Apps,
  Kubernetes or Redis.** None is needed to run the game.
