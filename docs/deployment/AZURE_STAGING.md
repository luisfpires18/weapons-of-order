# Weapons of Order: Azure staging

How the Browser V1 game is deployed to a staging environment, and how to rebuild that
environment from nothing.

There is no production environment. Staging is the only deployed environment, and it is not
a slot on something else — it is a Web App of its own in a resource group of its own.

Browser V1 is a prototype with one player. Staging is sized for that: a Free App Service
plan, and a SQLite file instead of a database server. **PostgreSQL remains the intended
direction for a real production environment** and is not implemented here; see
[Where PostgreSQL comes back](#where-postgresql-comes-back).

## Architecture

One public origin serves the whole game, and the whole game is one process:

```text
https://<web app>.azurewebsites.net
├── /                       React + Vite client, PWA manifest, service worker
├── /login /forge …         client routes, served the React document by SPA fallback
└── /api/…                  ASP.NET Core, same process, same origin
                            └── SQLite at /home/data/weapons-of-order.db
```

There is no CORS configuration, no separate frontend host and no API subdomain, in staging
or in development. Vite proxies `/api` locally to the same effect.

```text
Azure subscription
└── rg-weaponsoforder-staging                (West Europe)
    ├── plan-woo-staging                     App Service plan, Linux F1 (Free)
    ├── app-woo-staging-<suffix>             Web App, .NET 10, system-assigned identity
    │   └── /home/data/weapons-of-order.db   the database, on persistent storage
    ├── log-woo-staging                      Log Analytics, 30 days, 1 GiB/day cap
    ├── appi-woo-staging                     Application Insights over that workspace
    ├── acs-woo-staging-<suffix>             Communication Services (account email)
    ├── acsemail-woo-staging-<suffix>        Email Communication Service + Azure-managed domain
    └── id-woo-staging-deploy                user-assigned identity GitHub Actions deploys as
```

The `<suffix>` is a six-character hash of the subscription and resource group name. Azure
requires those names to be globally unique; the hash keeps them stable across redeployments
instead of random.

### What this costs

| Resource | Cost |
| --- | --- |
| App Service plan (F1 Linux) | **Free tier.** No hourly charge. |
| Database | **No resource at all.** A file on storage the plan already includes. |
| Log Analytics / Application Insights | Usage-based past the free ingestion allowance, and capped at 1 GiB/day so it cannot run away. |
| Communication Services email | Usage-based, per message and per MB. Negligible at staging volume. |
| Managed identity, resource group | Free. |

The environment therefore has **no fixed monthly cost**, and what remains is bounded or
proportional to use. Prices move; check the Azure pricing calculator rather than trusting a
number written down here.

`appServicePlanSku`/`appServicePlanTier`, `logDailyQuotaGb`, `databasePath` and `location`
are all parameters. Moving to B1 buys Always On and a platform health probe.

### What the Free tier costs instead

- **No Always On.** The site is unloaded when idle, so the first request after a quiet
  period pays a cold start — and, on the first request after a deployment, the migration too.
  The deployment's smoke test waits accordingly.
- **A daily CPU quota.** Exceed it and the app is stopped until the next day.
- **No platform health check.** That feature starts at Basic, so the Bicep leaves
  `healthCheckPath` unset on Free. Readiness is proven by the deployment smoke test instead.
- **No SLA.** It is a prototype.

## Persistence

### Where the database is, and why there

```text
/home/data/weapons-of-order.db
```

`/home` is the App Service instance's persistent share. It survives restarts, redeployments
and platform maintenance, and it is persistent by default for a built-in Linux runtime —
there is no storage setting to turn on. (`WEBSITES_ENABLE_APP_SERVICE_STORAGE` is a custom
container setting and does not apply here.)

The path is deliberately **outside** the deployed application. A zip deployment replaces the
application directory wholesale, so a database under it would be replaced with it — the game
would come back with every account, forged item and army gone. Nothing in the deployment
pipeline writes to `/home/data`, and `scripts/publish-artifact.sh` fails if a `.db`, `.db-wal`
or `.db-shm` file is ever found inside the package.

The directory is created by the application at startup if it does not exist; SQLite creates a
missing file but not a missing directory.

### What is guaranteed, and what is not

- A redeploy preserves accounts, forged items, unit loadouts and armies. There is a test for
  that: `Player_data_survives_an_application_restart`.
- The database is **not backed up**. Prototype data is disposable by decision. Deleting the
  resource group deletes it.
- SQLite here is a **single-instance** store. Scaling the App Service plan out to two
  instances would give each its own file and quietly split the game in two. Do not.

**This whole arrangement is a prototype-only compromise, and it is deliberate.** A SQLite file
on a network-backed share is not a production persistence architecture: it is single-instance
by construction, it cannot use write-ahead logging, it is not backed up, and the data in it is
disposable by decision. It is here because Browser V1 has one player and no traffic, and a
database server would be a daily cost and a moving part that buys nothing at that size.
PostgreSQL replaces it for real production — see
[Where PostgreSQL comes back](#where-postgresql-comes-back).

The database runs in SQLite's default rollback-journal mode, and the application sets it
back to that at startup. That step is necessary rather than cosmetic: **EF Core's SQLite
provider turns write-ahead logging on for itself when it creates a database**, so simply not
asking for WAL would still leave staging in it.

WAL is the better mode on a local disk and the wrong one here. Its index lives in shared
memory, which SQLite documents as unavailable across a network filesystem — and `/home` is
Azure Storage behind a share, not a local disk. The rollback journal plus the busy timeout in
the connection string (`Default Timeout=30`, so a write arriving during another one waits
rather than failing) is what this prototype needs. A test asserts startup does not leave the
database in WAL.

### Migrations

The application applies its own migrations while starting, before it serves anything:

```text
Database__MigrateOnStartup = true
```

Off by default in `appsettings.json`. An environment has to ask for it. This one does,
because its shape makes it safe: one instance, no horizontal scale, and one file on that
instance's own storage. The migration and the code arrive together, and there is no second
process to race.

Migrations only — never `EnsureCreated`, never a drop, never a recreate, and no seeding. A
failure is allowed to stop the process: an application that starts and then fails every query
against a schema that is not there is much harder to see than a container that refuses to
come up. The deployment's readiness check opens the database, so a failed migration fails the
run.

The history is a single SQLite baseline. It was rebuilt from the current EF Core model when
the provider changed, because there was no deployed data to preserve and PostgreSQL-shaped
migrations cannot describe a SQLite database.

## Prerequisites

| Tool | Why |
| --- | --- |
| Azure CLI 2.60+ | Provisioning. `az bicep install` on first use. |
| .NET SDK per `global.json` | Building the server. |
| Node + pnpm per `.nvmrc` / `packageManager` | Building the client into the artifact. |

No database client, no passwords, and nothing to put in a password manager.

An Azure account that can create resource groups and role assignments in the target
subscription. Subscription Owner is enough; Contributor alone is not, because the deployment
creates role assignments.

## Provisioning

### The short path

`infra/azure/bootstrap.sh` is the whole procedure in one command. It checks the subscription,
registers the providers, confirms the .NET runtime is offered in the region, shows a
`what-if`, asks before creating anything, deploys, and prints every value the GitHub
Environment needs.

It runs anywhere the Azure CLI is signed in — including the **Azure Portal's Cloud Shell**,
which already has `az`, `bicep` and `python3`, and is already authenticated as you. No local
Azure sign-in and no portal clicking is required.

```bash
git clone https://github.com/luisfpires18/weapons-of-order.git && cd weapons-of-order
```

```bash
infra/azure/bootstrap.sh
```

The rest of this section is what that script does, for when something goes wrong or a step
needs to be run on its own.

### 1. Sign in and choose the subscription

```bash
az login
```

```bash
az account set --subscription "<subscription name or id>"
```

### 2. Register the resource providers

Only needed once per subscription. A provider that is not registered fails the deployment
with a message that does not obviously say so.

```bash
az provider register --namespace Microsoft.Web --wait
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

### 3. Confirm the runtime is available in the region

Do not assume; the stack list moves. Read the **first column only** — `az webapp
list-runtimes` has answered a bare list of identifiers and now answers a row per runtime with
the identifier first and descriptive columns after it, so a check written against the whole
line reports that .NET 10 is missing while Azure is listing it.

```bash
az webapp list-runtimes --os-type linux --output tsv | cut -f1 | grep '^DOTNETCORE'
```

If .NET 10 is genuinely absent, **do not downgrade the repository's target framework.**
Publish self-contained instead: build with `dotnet publish -r linux-x64 --self-contained`,
set the site to a generic Linux stack, and point `appStartupCommand` at the produced
executable. That decision is the creator's to make; record the reason if it is ever taken.

### 4. Preview and provision

```bash
az deployment sub what-if --location westeurope --name woo-staging --template-file infra/azure/main.bicep --parameters infra/azure/main.bicepparam
```

```bash
az deployment sub create --location westeurope --name woo-staging --template-file infra/azure/main.bicep --parameters infra/azure/main.bicepparam
```

```bash
az deployment sub show --name woo-staging --query properties.outputs --output json
```

Validate the template on its own at any time — this needs no Azure sign-in:

```bash
az bicep build --file infra/azure/main.bicep --stdout > /dev/null
```

### 5. Configure the GitHub `staging` Environment

Create an Environment named exactly `staging` in the repository settings. The federated
credential is bound to that name, so a workflow job that does not declare it cannot obtain an
Azure token at all — which is also what makes the Environment's own protection rules real
rather than decorative.

**Variables** — five, and **none of them is a secret**. An identity client id without its
federated trust proves nothing, and a resource name is not a credential.

| Variable | Value | Purpose |
| --- | --- | --- |
| `AZURE_CLIENT_ID` | `deploymentIdentityClientId` output | Which identity to exchange the OIDC token for. |
| `AZURE_TENANT_ID` | `tenantId` output | Which directory to exchange it in. |
| `AZURE_SUBSCRIPTION_ID` | `subscriptionId` output | Which subscription to act in. |
| `AZURE_RESOURCE_GROUP` | `rg-weaponsoforder-staging` | Scope for every command in the job. |
| `AZURE_WEBAPP_NAME` | `webAppName` output | The site the package is deployed to. |

**Secrets: none.**

There is no Azure client secret — deployment exchanges a short-lived GitHub OIDC token. There
is no database secret either, because there is no database server: the connection string is a
file path, written into an application setting by Bicep.

### The federated subject, and why it carries numbers

Entra decides whether to exchange a GitHub token by comparing the token's `sub` claim against
the subject configured on the federated credential. The comparison is literal. There is no
pattern, no prefix match and no normalisation, so a subject that is *nearly* right is exactly
as rejected as one that is entirely wrong:

```text
AADSTS700213: No matching federated identity record found for presented assertion subject
```

GitHub issues this repository's tokens with the **immutable** subject, which names the owner
and the repository and then pins each to its numeric id:

```text
repo:luisfpires18@29492601/weapons-of-order@1329180174:environment:staging
```

The shape is `repo:<owner>@<ownerId>/<name>@<repositoryId>:environment:<environment>`. The
older form, `repo:<owner>/<name>:environment:<environment>`, is **not** what this repository
presents and must not be configured for it — it is what produced the AADSTS700213 above.

The ids are the whole point of the change. A repository name and an account name can be
renamed and the freed name claimed by somebody else, at which point a credential trusting the
name alone would trust a repository the owner no longer controls. An id is never reissued.
Neither id is a secret: GitHub serves both unauthenticated.

`infra/azure/main.bicepparam` holds the four parts, and
`infra/azure/modules/deployment-identity.bicep` assembles the subject from them rather than
storing it as one opaque string, so the environment or the repository can be changed in one
place. Read the ids back from GitHub with:

```bash
gh api users/luisfpires18 --jq .id
```

```bash
gh api repos/luisfpires18/weapons-of-order --jq .id
```

The deployment prints the subject it configured, as the `deploymentFederatedSubject` output —
compare it against the `sub` of a token the workflow actually presents rather than against
what it ought to be:

```bash
az deployment sub show --name woo-staging --query properties.outputs.deploymentFederatedSubject.value --output tsv
```

A wrong subject is invisible to everything that runs before a deployment: the template
compiles, `what-if` is clean, and the failure arrives only at `azure/login` against a real
token. `infra/azure/oidc-subject.test.sh` is the guard, and runs in CI's `Infra` job — it
needs no subscription and no sign-in, and it fails if the configured subject stops matching
the immutable form or if the mutable form reappears anywhere in the repository.

```bash
infra/azure/oidc-subject.test.sh
```

## What the deployment identity is allowed to do

| Scope | Role | Why |
| --- | --- | --- |
| The Web App | Website Contributor (built-in) | Publish a package, restart, read configuration. |

That is the entire list. Not Contributor on the resource group, and not Owner on anything. A
compromised workflow can redeploy the site; it cannot touch the telemetry, the email
resources, or anything else in the subscription. The database is a file inside the site, so
there is nothing else for a deployment to reach.

## Deployment flow

```text
pull request ──► Web ─┐
             Server ─┼─► Package ────────────────────────► (stops here)
              Infra ─┘

push to master ─► Web ─┐
   or manual run       │
              Server ─┼─► Package ─► Deploy staging
               Infra ─┘               ├─ az webapp deploy
                                      ├─ the app migrates its own database while starting
                                      └─ scripts/smoke-staging.sh
```

A pull request never deploys. `Deploy staging` needs `Package`, which needs all three
validation jobs, so an unvalidated commit cannot reach staging by any path — including a
manual run, which validates the selected ref first and prints which commit it is deploying.

The deployment job takes a concurrency group of its own with cancellation disabled: the
application migrates while it starts, and a second deployment arriving mid-restart would
replace the binaries underneath a migration that is still running. Pull-request runs are
still cancelled when superseded.

There is no migration step in the workflow, and nothing in it holds a database credential.
The readiness check is what proves the migration succeeded, because readiness opens the
database.

### Manual deployment of a branch

Actions → CI → Run workflow → pick the ref. The chosen ref is validated and then deployed,
and the run summary names the commit. It deploys to the same staging as master, so the last
run wins.

### Redeploying, and rolling back

Re-run the workflow on the commit you want:

```bash
gh workflow run ci.yml --ref <branch-or-tag>
```

**Schema rollback is not automatic.** Redeploying older code does not undo a migration, and
`dotnet ef database update <EarlierMigration>` runs the `Down` methods, which drop columns and
the player data in them. Going back past a schema change is a deliberate new migration
written for the purpose, not a reversal — and if the older code cannot read the newer schema,
the answer is to fix forward. In this prototype one more answer is available: the data is
disposable, so deleting the file and starting again is a legitimate choice.

## Application configuration

Every value is an App Service application setting, written by Bicep. Nothing is committed.

| Setting | Value | Why |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Staging` | Not Development: no notification endpoint, no captured links, no developer exception page, Secure cookies. |
| `ConnectionStrings__WeaponsOfOrder` | `Data Source=/home/data/weapons-of-order.db;Default Timeout=30` | Persistent storage, outside the deployed application. The timeout is SQLite's busy timeout. |
| `Database__MigrateOnStartup` | `true` | One instance, one file on it. See [Migrations](#migrations). |
| `Auth__ClientBaseUrl` | `https://<site host>` | Account links are built from this and never from the request. |
| `Hosting__UseForwardedHeaders` | `true` | This process is behind the App Service front end. |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | from Application Insights | Telemetry is off without it. |
| `Email__Provider` | `AzureCommunicationServices` | |
| `Email__SenderAddress` | `DoNotReply@<managed domain>` | |
| `Email__AzureCommunicationServices__Endpoint` | the ACS endpoint | Authenticated as the site's managed identity — no access key exists. |
| `SCM_DO_BUILD_DURING_DEPLOYMENT` | `false` | The artifact is already published. |

The application refuses to start if `Auth:ClientBaseUrl` is missing or is not `https` outside
Development, if the connection string is missing, or if an email provider is selected without
a sender address and a way to reach it. Those are startup failures on purpose.

### HTTPS and forwarded headers

The site is `httpsOnly`, so plain HTTP is redirected at the platform edge. The application
does **not** also run `UseHttpsRedirection` — a redirect behind a proxy that already redirects
is how loops happen.

Behind App Service the process is handed a plain HTTP request from a front end, so
`UseForwardedHeaders` runs first in the pipeline, before anything reads the scheme or the
caller's address. It honours `X-Forwarded-Proto` and `X-Forwarded-For` and not `Host`, with
`ForwardLimit = 1`: App Service **appends** the real caller to whatever the client sent, so
the rightmost entry is the trustworthy one and a client's own `X-Forwarded-For: 1.2.3.4` is
discarded. Account links come from `Auth:ClientBaseUrl` regardless.

This is what makes the session cookie `Secure`, the rate limits partition per real caller
rather than per front end, and `Strict-Transport-Security` appear at all. The smoke test
checks for that header for exactly that reason.

## The deployment artifact

One command builds exactly what is deployed:

```bash
scripts/publish-artifact.sh
```

It installs the client dependencies from the lockfile, builds the client into the API
project's `wwwroot`, publishes the application on top, and then asserts the result contains
the application assemblies, `server/content`'s game data, `index.html`, a hashed asset bundle,
the PWA manifest and the service worker — and that it contains **no database file** and no
local development configuration.

Neither `web/dist` nor the API's `wwwroot` nor `artifacts/` nor `.data/` is committed.

On Windows, where pnpm is reached through corepack:

```bash
PNPM='corepack pnpm@10.34.0' scripts/publish-artifact.sh
```

Run the result standalone, with no Vite and no source tree involved:

```bash
ASPNETCORE_ENVIRONMENT=Staging ASPNETCORE_HTTP_PORTS=5181 ConnectionStrings__WeaponsOfOrder='Data Source=/tmp/woo-staging/weapons-of-order.db' Database__MigrateOnStartup=true Auth__ClientBaseUrl='https://staging.example' Hosting__UseForwardedHeaders=true dotnet artifacts/staging/WeaponsOfOrder.Api.dll
```

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
hour, and higher quotas need a verified custom domain. That is enough for staging.

Sender: `DoNotReply@<generated>.azurecomm.net`. Expect it in the spam folder; the domain has
no reputation.

## Telemetry

Application Insights through the Azure Monitor OpenTelemetry distro — the current
recommendation — not the retired classic SDK. Requests, dependencies, unhandled exceptions and
`ILogger` output flow to `appi-woo-staging`, over a Log Analytics workspace capped at 1 GiB a
day so the one open-ended cost in this environment is bounded rather than watched.

App Service platform, console and HTTP logs are routed to the same workspace. That is how a
container that exits before the application starts is diagnosed — which on a tier with cold
starts is the failure to expect.

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
| `/api/health` | No | Liveness. Not wired to a platform probe on Free, which has no health check feature. |
| `/api/health/ready` | Yes | Deployment smoke test, and answering "is it actually working". |

Readiness opening the database is what makes it the proof that the startup migration
succeeded.

## Smoke test

```bash
scripts/smoke-staging.sh https://<web app>.azurewebsites.net
```

Read-only, so it can run on every deployment without accumulating junk in staging. It retries
with a bounded timeout rather than sleeping a guessed number of seconds — the deployment
allows seven minutes, because the first request after a deployment on the Free tier pays for a
cold start and the migration together.

It checks liveness, readiness, that the React document is served, that its hashed bundle
loads, that there are no plain-HTTP asset references, that the manifest and service worker are
served with the right content type, that `/login`, `/forge`, `/inventory`, `/units` and
`/battle` all reach the application, that an unmatched `/api` route is still a 404, that the
development notification endpoint is absent, that cookies are `Secure`, `HttpOnly` and
`SameSite=Lax`, that HSTS is present, and that plain HTTP redirects rather than serving or
looping.

It does not sign in and does not play the game. The full vertical loop — forge, inventory,
equipment, deployment, battle, replay, reload, sign out, sign back in — is verified in a real
browser.

## Where PostgreSQL comes back

SQLite is a prototype decision, not the end state. A real production environment gets
PostgreSQL, and the boundary is deliberate rather than accidental:

- The application is ordinary EF Core. There is no repository abstraction hiding the
  provider — swapping it is a provider registration and a migration history, which is exactly
  what this change was.
- **`Database:MigrateOnStartup` must go back to `false`.** It is safe here only because there
  is one instance. Two instances starting together would both migrate one database.
- The migration history would be regenerated from the then-current model. There is no
  expectation that the SQLite baseline transfers.
- `UtcTicksConverter` is a SQLite accommodation — SQLite cannot order by a `DateTimeOffset`.
  PostgreSQL has a native type with an offset and should drop it.
- Any staging data worth carrying over is a one-time export, handled then. Prototype data is
  disposable now.

None of that is implemented in this task, and none of it should be until production is
actually being designed.

## Teardown

The whole environment is one resource group and nothing else uses it:

```bash
az group delete --name rg-weaponsoforder-staging --yes --no-wait
```

That **permanently destroys**: the Web App and its configuration, the SQLite database and
every staging account, forged item, unit loadout and army in it, the Log Analytics workspace
and all telemetry, the Communication Services resources and the generated email domain (a new
one gets a new address), and the deployment identity.

Nothing survives outside the group. The GitHub `staging` Environment keeps its variables;
they point at resources that no longer exist. Delete it, or leave it and re-provision — but
if you re-provision, **read the new bootstrap output**, because the deployment identity is
recreated and `AZURE_CLIENT_ID` changes.

Re-provisioning is `infra/azure/bootstrap.sh` again. Nothing about staging exists only in the
portal.

## One-time move off the PostgreSQL environment

The earlier Task 7 environment provisioned a B1 App Service plan, a PostgreSQL Flexible
Server, firewall machinery and a subscription-level custom role. Those resources are no
longer in the template, and **Bicep will not delete them just because they disappeared from
it** — an incremental deployment leaves unknown resources alone. Nothing meaningful was ever
deployed to that environment, so the clean move is to remove it and provision again.

Run these yourself, in this order. Read each one before running it; the first destroys a
resource group.

**1. Check what is actually there**, so the delete is not a surprise:

```bash
az resource list --resource-group rg-weaponsoforder-staging --query "[].{name:name,type:type}" --output table
```

**2. Delete the resource group.** This destroys the PostgreSQL server, the B1 plan, the Web
App, telemetry, the Communication Services resources and the deployment identity, along with
every role assignment scoped to them:

```bash
az group delete --name rg-weaponsoforder-staging --yes
```

Without `--no-wait` on purpose: the custom role below cannot be removed until its assignments
are gone with the group.

**3. Remove the obsolete custom role.** It was defined at subscription scope, so it outlives
the group. The name is built from the resource group name in the old template — confirm it
before deleting, rather than trusting this document:

```bash
az role definition list --custom-role-only true --query "[?contains(roleName, 'Weapons of Order')].{name:roleName,id:name}" --output table
```

```bash
az role definition delete --name "Weapons of Order PostgreSQL firewall (rg-weaponsoforder-staging)"
```

If step 3 reports that assignments still exist, the group delete has not finished. Wait and
retry.

**4. Provision the new environment:**

```bash
infra/azure/bootstrap.sh
```

**5. Update the GitHub `staging` Environment from the new output.** The deployment identity is
a new resource, so `AZURE_CLIENT_ID` is different, and the Web App name has a different
suffix if the resource group name changed. Do not reuse the old values.

Also **delete the old `POSTGRES_*` variables and the `POSTGRES_ADMIN_PASSWORD` secret** from
that Environment. Nothing reads them any more, and a stored database password that nothing
uses is only a liability.
