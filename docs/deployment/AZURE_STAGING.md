# Weapons of Order: Azure staging

How the Browser V1 game is deployed to a production-like staging environment, and how to
rebuild that environment from nothing.

There is no production environment. Staging is the only deployed environment, and it is not
a slot on something else — it is a Web App of its own in a resource group of its own.

## Architecture

One public origin serves the whole game:

```text
https://<web app>.azurewebsites.net
├── /                       React + Vite client, PWA manifest, service worker
├── /login /forge …         client routes, served the React document by SPA fallback
└── /api/…                  ASP.NET Core, same process, same origin
```

There is no CORS configuration, no separate frontend host and no API subdomain, in staging
or in development. Vite proxies `/api` locally to the same effect.

```text
Azure subscription
└── rg-weaponsoforder-staging                (West Europe)
    ├── plan-woo-staging                     App Service plan, Linux B1
    ├── app-woo-staging-<suffix>             Web App, .NET 10, system-assigned identity
    ├── psql-woo-staging-<suffix>            PostgreSQL Flexible Server 18, Burstable B1ms
    │   └── weapons_of_order_staging         the staging database
    ├── log-woo-staging                      Log Analytics, 30 days, 1 GiB/day cap
    ├── appi-woo-staging                     Application Insights over that workspace
    ├── acs-woo-staging-<suffix>             Communication Services (account email)
    ├── acsemail-woo-staging-<suffix>        Email Communication Service + Azure-managed domain
    └── id-woo-staging-deploy                user-assigned identity GitHub Actions deploys as
```

The `<suffix>` is a six-character hash of the subscription and resource group name. Azure
requires those names to be globally unique; the hash keeps them stable across redeployments
instead of random.

### Which resources cost money

| Resource | Recurring cost |
| --- | --- |
| App Service plan (B1 Linux) | **Yes.** The largest line. Charged whether or not anyone is playing. |
| PostgreSQL Flexible Server (Burstable B1ms + 32 GiB) | **Yes.** Compute plus storage; backup up to the storage size is included. |
| Log Analytics / Application Insights | Only past the free ingestion allowance, and capped at 1 GiB/day. |
| Communication Services email | Per message and per MB. Negligible at staging volume. |
| Managed identities, role definitions, resource group | Free. |

Everything is parameterized. `appServicePlanSku`, `postgresSkuName`, `postgresStorageGb`,
`logDailyQuotaGb` and `location` are the levers.

Stopping the Web App does **not** stop the App Service plan charge. To stop paying, tear the
resource group down — see [Teardown](#teardown).

## Prerequisites

| Tool | Why |
| --- | --- |
| Azure CLI 2.60+ | Provisioning and deployment. `az bicep install` on first use. |
| `psql` (PostgreSQL 16+ preferred) | Creating the application's database role. 16 and later can verify against the platform CA store directly; older clients are handled, see [Transport](#transport). |
| .NET SDK per `global.json` | `dotnet ef` for migrations. |
| Node + pnpm per `.nvmrc` / `packageManager` | Building the client into the artifact. |
| A password manager | Two database passwords live there and nowhere else in the repository. |

An Azure account that can create resource groups, role assignments and a custom role
definition in the target subscription. Subscription Owner is enough; Contributor alone is
not, because the deployment creates role assignments.

## Provisioning

### The short path

`infra/azure/bootstrap.sh` is steps 1 through 7 below in one command. It checks the
subscription, registers the providers, confirms the runtime and database versions exist in
the region, shows a `what-if`, asks before creating anything, deploys, creates the
application's database role, and prints every value the GitHub Environment needs.

It runs anywhere the Azure CLI is signed in — including the **Azure Portal's Cloud Shell**,
which already has `az`, `bicep`, `psql` and `python3`, and is already authenticated as you.
No local Azure sign-in and no portal clicking is required.

```bash
git clone https://github.com/luisfpires18/weapons-of-order.git && cd weapons-of-order
```

```bash
export WOO_PG_ADMIN_PASSWORD="$(openssl rand -base64 30)"
```

```bash
export WOO_PG_APP_PASSWORD="$(openssl rand -base64 30)"
```

```bash
infra/azure/bootstrap.sh
```

Put both passwords in a password manager before continuing — the script does not print them
back, and the administrator one is needed again in step 8.

The rest of this section is what that script does, for when something goes wrong or a step
needs to be run on its own.

### 1. Sign in and choose the subscription

```bash
az login
```

```bash
az account set --subscription "<subscription name or id>"
```

```bash
az account show --output table
```

### 2. Register the resource providers

Only needed once per subscription. A provider that is not registered fails the deployment
with a message that does not obviously say so.

```bash
az provider register --namespace Microsoft.Web --wait
```

```bash
az provider register --namespace Microsoft.DBforPostgreSQL --wait
```

```bash
az provider register --namespace Microsoft.Communication --wait
```

```bash
az provider register --namespace Microsoft.OperationalInsights --wait
```

```bash
az provider register --namespace Microsoft.Insights --wait
```

```bash
az provider register --namespace Microsoft.ManagedIdentity --wait
```

### 3. Confirm the runtime and database versions are available in the region

Do not assume. The stack list and the supported PostgreSQL versions both move.

```bash
az webapp list-runtimes --os-type linux --output tsv | cut -f1 | grep '^DOTNETCORE'
```

```bash
az postgres flexible-server list-skus --location westeurope --query "[].supportedServerVersions[].name" --output tsv | sort -u
```

Read the **first column only**. `az webapp list-runtimes` has answered a bare list of
identifiers and now answers a row per runtime with the identifier first and descriptive
columns after it, so a check written against the whole line reports that .NET 10 is missing
while Azure is listing it. `bootstrap.sh` reads the first field for that reason;
`infra/azure/lib/az-output.test.sh` covers both shapes without needing a subscription.

If .NET 10 is genuinely absent from the App Service stack list, **do not downgrade the
repository's target framework.** Publish self-contained instead: build with
`dotnet publish -r linux-x64 --self-contained`, set the site to a generic Linux stack, and
point `appStartupCommand` at the produced executable. That decision is the creator's to make;
record the reason if it is ever taken.

### 4. Generate the two database passwords

Two, not one. The application never connects with the administrator credential.

```bash
export WOO_PG_ADMIN_PASSWORD="$(openssl rand -base64 30)"
```

```bash
export WOO_PG_APP_PASSWORD="$(openssl rand -base64 30)"
```

Put both in a password manager now. They are needed again for the GitHub Environment, and
the runtime one is needed in step 7. They are never committed, never printed into a log, and
never written into a parameter file.

### 5. Preview the deployment

`what-if` prints what would be created before anything is.

```bash
az deployment sub what-if --location westeurope --name woo-staging --template-file infra/azure/main.bicep --parameters infra/azure/main.bicepparam
```

Validate the template on its own at any time — this needs no Azure sign-in:

```bash
az bicep build --file infra/azure/main.bicep --stdout > /dev/null
```

### 6. Provision

```bash
az deployment sub create --location westeurope --name woo-staging --template-file infra/azure/main.bicep --parameters infra/azure/main.bicepparam
```

Read the outputs; they are every value the GitHub Environment needs.

```bash
az deployment sub show --name woo-staging --query properties.outputs --output json
```

### 7. Create the application's database role

Azure Resource Manager cannot create a PostgreSQL role, so this is the one part of the
environment that is not in Bicep. Run it once, from an address the server's firewall allows —
add your own temporarily:

```bash
az postgres flexible-server firewall-rule create --resource-group rg-weaponsoforder-staging --server-name <postgres server> --name bootstrap --start-ip-address "$(curl -s https://api.ipify.org)" --end-ip-address "$(curl -s https://api.ipify.org)"
```

```bash
psql "host=<postgres server>.postgres.database.azure.com port=5432 dbname=weapons_of_order_staging user=woo_admin sslmode=verify-full sslrootcert=system" -v ON_ERROR_STOP=1 -v runtime_role=woo_app -v admin_role=woo_admin -v runtime_password="$WOO_PG_APP_PASSWORD" -f infra/azure/database/create-runtime-role.sql
```

```bash
az postgres flexible-server firewall-rule delete --resource-group rg-weaponsoforder-staging --server-name <postgres server> --name bootstrap --yes
```

### 8. Configure the GitHub `staging` Environment

Create an Environment named exactly `staging` in the repository settings. The federated
credential is bound to that name, so a workflow job that does not declare it cannot obtain an
Azure token at all — which is also what makes the Environment's own protection rules real
rather than decorative.

**Variables** — none of these is a secret. An identity client id without its federated trust
proves nothing, and a resource name is not a credential.

| Variable | Value | Purpose |
| --- | --- | --- |
| `AZURE_CLIENT_ID` | `deploymentIdentityClientId` output | Which identity to exchange the OIDC token for. |
| `AZURE_TENANT_ID` | `tenantId` output | Which directory to exchange it in. |
| `AZURE_SUBSCRIPTION_ID` | `subscriptionId` output | Which subscription to act in. |
| `AZURE_RESOURCE_GROUP` | `rg-weaponsoforder-staging` | Scope for every command in the job. |
| `AZURE_WEBAPP_NAME` | `webAppName` output | The site the package is deployed to. |
| `AZURE_POSTGRES_SERVER` | `databaseServerName` output | The server the temporary firewall rule is opened on. |
| `POSTGRES_DATABASE` | `weapons_of_order_staging` | The database migrations are applied to. |
| `POSTGRES_ADMIN_LOGIN` | `woo_admin` | The role migrations run as. |
| `POSTGRES_RUNTIME_LOGIN` | `woo_app` | The role the post-migration grants are re-applied to. |

**Secrets** — one.

| Secret | Value | Purpose |
| --- | --- | --- |
| `POSTGRES_ADMIN_PASSWORD` | `$WOO_PG_ADMIN_PASSWORD` | Applies migrations. Nothing else uses it. |

There is deliberately **no Azure client secret**. Deployment authenticates by exchanging a
short-lived GitHub OIDC token, so there is no long-lived Azure credential in this repository
to leak or rotate.

The runtime database password is not here either: the application receives it as an App
Service application setting, written by Bicep at provisioning time.

## What the deployment identity is allowed to do

| Scope | Role | Why |
| --- | --- | --- |
| The Web App | Website Contributor (built-in) | Publish a package, restart, read configuration. |
| The PostgreSQL server | `Weapons of Order PostgreSQL firewall` (custom) | Create, read and delete firewall rules. Nothing else. |

Not Contributor on the resource group, and not Owner on anything. A compromised workflow can
redeploy the site and open a firewall rule; it cannot delete the database, read the
administrator credential out of the server, or touch anything else in the subscription.

The custom role is defined in `infra/azure/main.bicep`. Creating it requires
`Microsoft.Authorization/roleDefinitions/write` at subscription scope, which is why
provisioning needs Owner and deployment does not.

## Deployment flow

```text
pull request ──► Web ─┐
                      ├─► Package ────────────────────────► (stops here)
                      Server ─┘

push to master ─► Web ─┐
   or manual run       ├─► Package ─► Deploy staging
                      Server ─┘        ├─ open the database to this runner
                                       ├─ dotnet ef database update
                                       ├─ re-apply the runtime role grants
                                       ├─ close the database again  (always)
                                       ├─ az webapp deploy
                                       └─ scripts/smoke-staging.sh
```

A pull request never deploys. `Deploy staging` needs `Package`, which needs both validation
jobs, so an unvalidated commit cannot reach staging by any path — including a manual run,
which validates the selected ref first and prints which commit it is deploying.

The deployment job takes a concurrency group of its own with cancellation disabled, so a
second push cannot cancel a run that is midway through a migration. Pull-request runs are
still cancelled when superseded.

### Manual deployment of a branch

Actions → CI → Run workflow → pick the ref. The chosen ref is validated and then deployed,
and the run summary names the commit. Useful for trying a branch on real staging before
merging it; it deploys to the same staging as master, so the last run wins.

### Redeploying, and rolling back

There is no rollback button. Re-run the workflow on the commit you want:

```bash
gh workflow run ci.yml --ref <branch-or-tag>
```

Or from the Actions UI, re-run the whole CI run for that commit.

**Schema rollback is not automatic and must not be treated as if it were.** Redeploying older
code does not undo a migration, and `dotnet ef database update <EarlierMigration>` runs the
`Down` methods, which drop columns and the player data in them. Going back past a schema
change is a deliberate new migration written for the purpose, not a reversal — and if the
older code cannot read the newer schema, the answer is to fix forward.

## Database

### Separation

| Environment | Database |
| --- | --- |
| Local development | `weapons_of_order` on `localhost:5433` (docker-compose) |
| Local/CI tests | `weapons_of_order_tests` |
| CI migration check | `weapons_of_order` on the job's throwaway PostgreSQL service |
| Staging | `weapons_of_order_staging` on the Azure Flexible Server |

Nothing is shared. Staging has its own server, its own database, and its own credentials.

### Credentials

Two roles, on purpose:

- **`woo_admin`** — the server administrator. Applies migrations from the deployment
  workflow. Its password is a GitHub Environment secret.
- **`woo_app`** — what the running application connects as. `SELECT`, `INSERT`, `UPDATE`,
  `DELETE` on the tables and `USAGE`, `SELECT` on the sequences. No `CREATE`, no `TRUNCATE`,
  no ownership, no `CREATEROLE`. Its password is written into an App Service application
  setting by Bicep and exists nowhere else.

`ALTER DEFAULT PRIVILEGES FOR ROLE woo_admin` means a table a future migration adds is
already granted to `woo_app`. The workflow re-runs `grant-runtime-role.sql` after every
migration anyway, which is idempotent and removes the failure where staging boots and then
cannot read its own new table.

The application therefore cannot change the schema, cannot empty a table, and cannot rewrite
the migration history. It also holds no Azure subscription credential of any kind — the site
identity can send account email through one Communication Services resource and reach nothing
else.

### Network

The server is not open to the internet, and there is no `0.0.0.0` "allow all Azure services"
rule — that rule admits every Azure resource in every tenant, which is a wider surface than
the public internet minus a password.

Two kinds of rule exist:

- **Permanent**: one per App Service outbound address, written from the site's
  `possibleOutboundIpAddresses`. Redeploy the Bicep if the plan is ever scaled or moved; the
  symptom of a stale list is readiness that starts failing after a plan change.
- **Temporary**: the deployment workflow opens its own runner's address, applies migrations,
  and deletes the rule in a step that runs even when the migration failed. The rule is named
  after the run, so one left behind by a hard-killed job is identifiable.

That temporary window is the trade-off for migrating from a GitHub-hosted runner whose
address is not known in advance. The alternatives are a self-hosted runner or a VNet the rest
of this environment has no use for.

### Transport

`require_secure_transport` is on at the server, and every client verifies the certificate
and the hostname. Nothing connects with a mode that only encrypts.

| Client | Setting | Trusted roots |
| --- | --- | --- |
| The application, and `dotnet ef` | `SSL Mode=VerifyFull` | The operating system store,
which Npgsql uses on its own. |
| `psql` | `sslmode=verify-full sslrootcert=system` | The same store, named explicitly. |

The difference is not an inconsistency, it is the two libraries. Npgsql validates against
the platform's trust store by default and has no `sslrootcert` keyword at all. libpq has
one, and with it unset it looks for `~/.postgresql/root.crt` and refuses to connect when
that file is absent — which on a fresh Cloud Shell it always is:

```text
psql: error: connection to server at "psql-....postgres.database.azure.com" failed:
root certificate file "/home/<user>/.postgresql/root.crt" does not exist
```

`sslrootcert=system` is the fix: it names the platform CA store, which already carries the
public root Azure's certificate chains to. There is nothing to download and nothing to pin —
pinning an intermediate would only turn Azure's next rotation into an outage. libpq has
understood `system` since PostgreSQL 16; on an older client `infra/azure/lib/psql-tls.sh`
falls back to the platform CA bundle by path, which is the same roots named differently, and
refuses to run at all if it can find neither.

Lowering the mode is **not** a fix for a missing root store, and
`infra/azure/lib/psql-tls.test.sh` fails the build if any connection in this repository
tries it.

### Migrations

Migrations never run on application startup. They are applied by
`dotnet ef database update` from the deployment job, using the migration history that already
exists — the same command CI already proves against a clean PostgreSQL on every run.

A migration bundle was considered and not used: the EF tool is already pinned in
`.config/dotnet-tools.json`, the runner already has the SDK, and a bundle would add a second
artifact and a second code path to keep in step with the first for no gain here.

If the migration fails, the job fails, the firewall rule is still removed, and **the package
is never deployed** — staging keeps running the schema and the code it already had.

The procedure works from an empty database and from one that already holds earlier schema and
player data. Ordinary deployment never resets staging data.

Apply migrations by hand, if you must:

```bash
export ConnectionStrings__WeaponsOfOrder="Host=<server>.postgres.database.azure.com;Port=5432;Database=weapons_of_order_staging;Username=woo_admin;Password=$WOO_PG_ADMIN_PASSWORD;SSL Mode=VerifyFull"
```

```bash
dotnet ef database update --project server/src/WeaponsOfOrder.Infrastructure --startup-project server/src/WeaponsOfOrder.Api
```

## The deployment artifact

One command builds exactly what is deployed:

```bash
scripts/publish-artifact.sh
```

It installs the client dependencies from the lockfile, builds the client into the API
project's `wwwroot`, publishes the application on top, and then asserts the result contains
the application assemblies, `server/content`'s game data, `index.html`, a hashed asset bundle,
the PWA manifest and the service worker — and that it does **not** contain
`appsettings.Development.json` or anything resembling a local environment file.

Neither `web/dist` nor the API's `wwwroot` nor `artifacts/` is committed. The client build
output is generated on every run.

On Windows, where pnpm is reached through corepack:

```bash
PNPM='corepack pnpm@10.34.0' scripts/publish-artifact.sh
```

Run the result standalone, with no Vite and no source tree involved:

```bash
ASPNETCORE_ENVIRONMENT=Staging ASPNETCORE_HTTP_PORTS=5181 ConnectionStrings__WeaponsOfOrder='Host=localhost;Port=5433;Database=weapons_of_order;Username=woo_dev;Password=woo_dev' Auth__ClientBaseUrl='https://staging.example' Hosting__UseForwardedHeaders=true dotnet artifacts/staging/WeaponsOfOrder.Api.dll
```

## Application configuration

Every value is an App Service application setting, written by Bicep. Nothing is committed.

| Setting | Value | Why |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Staging` | Not Development: no notification endpoint, no captured links, no developer exception page, Secure cookies. |
| `ConnectionStrings__WeaponsOfOrder` | `woo_app` credentials, `SSL Mode=VerifyFull` | The restricted role, over verified TLS. |
| `Auth__ClientBaseUrl` | `https://<site host>` | Account links are built from this and never from the request. |
| `Hosting__UseForwardedHeaders` | `true` | This process is behind the App Service front end. |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | from Application Insights | Telemetry is off without it. |
| `Email__Provider` | `AzureCommunicationServices` | |
| `Email__SenderAddress` | `DoNotReply@<managed domain>` | |
| `Email__AzureCommunicationServices__Endpoint` | the ACS endpoint | Authenticated as the site's managed identity — no access key exists. |
| `SCM_DO_BUILD_DURING_DEPLOYMENT` | `false` | The artifact is already published. |

The application refuses to start if `Auth:ClientBaseUrl` is missing, is not `https` outside
Development, or if an email provider is selected without a sender address and a way to reach
it. Those are startup failures on purpose: the alternative is discovering them on somebody's
first password reset, as a 500 that only appears for addresses that exist.

### HTTPS and forwarded headers

The site is `httpsOnly`, so plain HTTP is redirected at the platform edge. The application
does **not** also run `UseHttpsRedirection` — a redirect behind a proxy that already redirects
is how loops happen.

Behind App Service the process is handed a plain HTTP request from a front end, so
`UseForwardedHeaders` runs first in the pipeline, before anything reads the scheme or the
caller's address. It honours `X-Forwarded-Proto` and `X-Forwarded-For` and not `Host`, with
`ForwardLimit = 1`: App Service **appends** the real caller to whatever the client sent, so
the rightmost entry is the trustworthy one and a client's own `X-Forwarded-For: 1.2.3.4` is
discarded. Account links come from `Auth:ClientBaseUrl` regardless, so `Host` has nothing to
gain by being forwarded.

This is what makes the session cookie `Secure`, the rate limits partition per real caller
rather than per front end, and `Strict-Transport-Security` appear at all. The smoke test
checks for that header for exactly that reason.

## Account email

Azure Communication Services with an **Azure-managed domain**, so staging needs no DNS work
and no Weapons of Order domain.

The site's system-assigned managed identity is granted *Communication and Email Service
Owner* on that one resource, so the application authenticates without an access key. There is
no email secret anywhere.

Confirmation and reset behaviour is unchanged: Identity's own tokens, generic public
responses either way, no account enumeration, and the link — a single-use bearer credential —
is handed to the provider and to nothing else. It is never logged, and it never reaches
telemetry: a span processor strips query strings before export, because
`/confirm-email?token=…` is a request this server answers.

**A managed domain is test-only.** It is throttled to roughly 5 messages a minute and 10 an
hour, and higher quotas need a verified custom domain. That is enough for staging and is not
a player-facing configuration.

Sender: `DoNotReply@<generated>.azurecomm.net`. Expect it in the spam folder; the domain has
no reputation.

If email delivery ever has to be turned off, set `Email__Provider` to `None`. Messages are
then dropped and recorded as dropped, and **account confirmation still applies** — the
account simply cannot be confirmed. There is no unauthenticated "confirm any account"
endpoint in a deployed environment and there must never be one; if a staging account has to be
confirmed without email, do it as a deliberate `UPDATE` on `AspNetUsers.EmailConfirmed` for
that one row, from the administrator connection, and write down that you did.

## Telemetry

Application Insights through the Azure Monitor OpenTelemetry distro — the current
recommendation — not the retired classic SDK. Requests, dependencies, unhandled exceptions and
`ILogger` output flow to `appi-woo-staging`, over a Log Analytics workspace capped at 1 GiB a
day so the one unbounded cost in this environment is bounded rather than watched.

App Service platform, console and HTTP logs are routed to the same workspace. That is how a
container that exits before the application starts is diagnosed; Application Insights never
saw such a process at all.

```bash
az monitor app-insights query --app appi-woo-staging --resource-group rg-weaponsoforder-staging --analytics-query "requests | where timestamp > ago(1h) | summarize count() by resultCode"
```

```bash
az monitor app-insights query --app appi-woo-staging --resource-group rg-weaponsoforder-staging --analytics-query "exceptions | where timestamp > ago(1h) | project timestamp, type, outerMessage"
```

```bash
az webapp log tail --name <web app> --resource-group rg-weaponsoforder-staging
```

Never logged, in any environment: passwords, session cookies, antiforgery tokens, reset and
confirmation tokens, connection strings, Azure secrets. Query strings are stripped from spans
before export.

## Health and readiness

| Endpoint | Includes the database | Used by |
| --- | --- | --- |
| `/api/health` | No | App Service health check. |
| `/api/health/ready` | Yes | Deployment smoke test, and answering "is it actually working". |

App Service is pointed at **liveness**, not readiness, on purpose: the platform recycles an
instance that fails its health check, and a database outage is not something restarting the
process fixes. Readiness is checked by the deployment instead, which fails the run if the
application never becomes ready.

## Smoke test

```bash
scripts/smoke-staging.sh https://<web app>.azurewebsites.net
```

Read-only, so it can run on every deployment without accumulating junk in staging. It retries
with a timeout rather than sleeping a guessed number of seconds, and it checks liveness,
readiness, that the React document is served, that its hashed bundle loads, that there are no
plain-HTTP asset references, that the manifest and service worker are served with the right
content type, that `/login`, `/forge`, `/inventory`, `/units` and `/battle` all reach the
application, that an unmatched `/api` route is still a 404, that the development notification
endpoint is absent, that cookies are `Secure`, `HttpOnly` and `SameSite=Lax`, that HSTS is
present, and that plain HTTP redirects rather than serving or looping.

It does not sign in and does not play the game. The full vertical loop — forge, inventory,
equipment, deployment, battle, replay, reload, sign out, sign back in — is verified in a real
browser.

## Teardown

Staging costs money every day it exists. The whole environment is one resource group and
nothing else uses it:

```bash
az group delete --name rg-weaponsoforder-staging --yes --no-wait
```

That **permanently destroys**: the Web App and its configuration, the PostgreSQL server and
every staging account, forged item, unit loadout and army in it, the backups, the Log
Analytics workspace and all telemetry, the Communication Services resources and the generated
email domain (a new one gets a new address), and the deployment identity.

Two things survive the group and are cleaned up separately:

```bash
az role definition delete --name "Weapons of Order PostgreSQL firewall (rg-weaponsoforder-staging)"
```

The GitHub `staging` Environment keeps its variables and secrets; they point at resources that
no longer exist. Delete it, or leave it and re-provision — the names are deterministic apart
from `<suffix>`, which is derived from the subscription and resource group and so comes back
the same.

Re-provisioning is steps 4 through 8 again. Nothing about staging exists only in the portal.
