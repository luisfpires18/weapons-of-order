# Weapons of Order

Forging-centered fantasy autobattler, developed browser-first.

Design and architecture authority lives in `docs/`; start with [`CLAUDE.md`](CLAUDE.md) for
the source-of-truth order. This file covers only how to run and validate the code locally.

## Layout

```text
web/                                  React + TypeScript + Vite client
  src/battle/                         Deployment, playback, and the PixiJS battlefield
server/
  content/                            Creator-editable game content — see its README
  src/WeaponsOfOrder.Api/             ASP.NET Core host, endpoints, configuration
    Auth/                             Accounts, sessions, account notifications
    Battle/                           Army deployment, combat stats, the battle API
    Content/                          Loading and validating server/content
    Forge/                            Ordinary forging: rules, balance data, endpoints
    Preparation/                      Inventory, Units and weapon loadouts
    Security/                         Antiforgery and authorization conventions
  src/WeaponsOfOrder.Combat/          The deterministic combat simulator — no dependencies
  src/WeaponsOfOrder.Infrastructure/  EF Core / SQLite persistence and migrations
    Gameplay/                         Player-owned game entities
  tests/WeaponsOfOrder.Api.Tests/     API, account and configuration tests
  tests/WeaponsOfOrder.Combat.Tests/  Combat rules, against no host and no database
art/                                  Shared art, aliased into the client as @art
infra/azure/                          Staging infrastructure as Bicep — see its README
scripts/                              Artifact build and deployment smoke test
.data/                                The local SQLite database. Git-ignored, disposable.
```

## Prerequisites

| Tool     | Version   | Pinned by                              |
| -------- | --------- | -------------------------------------- |
| Node     | 22.23.2   | [`.nvmrc`](.nvmrc)                     |
| pnpm     | 10.34.0   | `packageManager` in `web/package.json` |
| .NET SDK | 10.0.200+ | [`global.json`](global.json)           |

There is no database to install. Browser V1 stores its data in a SQLite file.

Enable corepack once so pnpm resolves to the pinned version:

```bash
corepack enable
```

## First-time setup

```bash
pnpm --dir web install
```

```bash
dotnet tool restore
```

That is all. The API creates and migrates its own database on the first run.

## Run it

Two terminals. The API first:

```bash
dotnet run --project server/src/WeaponsOfOrder.Api
```

Then the client:

```bash
pnpm --dir web dev
```

- Client: <http://localhost:1337>
- API: <http://localhost:5180>
- Health seam: <http://localhost:1337/api/health>

### The combat simulator

`server/src/WeaponsOfOrder.Combat` has no `PackageReference` and no `ProjectReference`, and that
emptiness is the point: no ASP.NET, no EF Core, no database driver, no Identity, no HTTP, nothing that knows
a browser exists. It takes a `BattleInput` and returns a `BattleResult` with a complete event log,
and reads nothing else — no clock, no ambient randomness. The same input replays event for event.

Its tests run against it alone:

```bash
dotnet test server/tests/WeaponsOfOrder.Combat.Tests
```

Vite proxies `/api` to the API, so the browser stays on **one origin** in development —
the same topology as deployment, where ASP.NET Core serves the built client from
`wwwroot` alongside `/api`. There is no CORS configuration and no API base URL to set.

To exercise that single-origin path locally, build the client into the API's `wwwroot`
and run the API alone:

```bash
pnpm --dir web build --outDir ../server/src/WeaponsOfOrder.Api/wwwroot --emptyOutDir
```

## Database

SQLite, through EF Core. Nothing to install, nothing to start, and nothing to pay for in
staging. The development database is a git-ignored file:

```text
.data/weapons-of-order.db
```

A relative `Data Source` is resolved against the application's content root, so it opens the
same file wherever `dotnet run` is invoked from. Delete the file to start over; there is
nothing in it that is not a prototype.

In Development and in staging the application applies pending migrations while it starts
(`Database:MigrateOnStartup`). It is **off** by default, so an environment has to ask for it,
and it is only appropriate here because Browser V1 is a single instance with its own file.
Migrations only — never `EnsureCreated`, never a drop, and no seeding.

To add a migration:

```bash
dotnet ef migrations add <Name> --project server/src/WeaponsOfOrder.Infrastructure --startup-project server/src/WeaponsOfOrder.Api --output-dir Persistence/Migrations
```

To apply one by hand:

```bash
dotnet ef database update --project server/src/WeaponsOfOrder.Infrastructure --startup-project server/src/WeaponsOfOrder.Api
```

Every other environment supplies its own connection string, and the API fails at startup if
none is configured rather than falling back to a default:

```bash
export ConnectionStrings__WeaponsOfOrder="Data Source=/home/data/weapons-of-order.db"
```

`dotnet user-secrets` also works for local overrides.

**PostgreSQL remains the intended store for a real production environment.** It is not
implemented, and Browser V1 does not half-implement it: the application is ordinary EF Core
with no provider abstraction over it, so the change is a provider registration and a
migration history regenerated from the model. See
[`TECH_STACK.md`](docs/architecture/TECH_STACK.md) and the boundary written down in
[`AZURE_STAGING.md`](docs/deployment/AZURE_STAGING.md).

## Accounts

Sign-in is email + password on ASP.NET Core Identity, with the session held in an
`HttpOnly` cookie. There is no token in `localStorage`, and mutating requests carry an
antiforgery token in the `X-WoO-Antiforgery` header, which the client reads from
`GET /api/auth/session`. Full rules are in
[`AUTH_SECURITY.md`](docs/architecture/AUTH_SECURITY.md).

A confirmed email address is required before sign-in. **No email provider is configured
yet**, so in development the confirmation and reset links are captured in memory instead of
sent: `GET /api/dev/account-notifications` lists the most recent ones, and the auth screens
show an "Open the captured link" action.

Both exist **only** when the API runs in the Development environment, and
`Auth:Development:ExposeNotifications` switches the endpoint off. A link is a bearer
credential, so it is never written to a log in any environment.

Deployed environments send real mail instead, through the provider named by the `Email`
section of `appsettings.json` — Azure Communication Services in staging, authenticated as the
host's managed identity so no provider key exists. With no provider configured, a message is
dropped and only the fact that it was dropped is recorded; the public responses are identical
either way, so nothing about the delivery path tells a caller whether an address has an
account.

`Auth:ClientBaseUrl` must be set to the absolute origin of the browser client — https, or
http only for a loopback host. The application refuses to start without it. Account links are
never built from the request, because the `Host` header is attacker-controlled.

Other security settings — password policy, lockout, rate limits, cookie lifetime — live under
the `Auth` section of `appsettings.json`.

## The forge

`/forge` is the first playable system: choose a recipe, pay its materials, heat the workpiece,
strike it, and keep what you made. The server owns all of it — the browser can ask to start
heating, to stop, or to strike, and nothing else. It never submits a temperature, a heat band,
a craftsmanship or an owner.

Everything tunable about it lives under the `Forge` section of `appsettings.json`: the recipe
catalogue, the heat scale and rates, how many blows a weapon takes, the quality thresholds and
the opening material stock. **Those values are prototype balance data, not canon.** The
structure they sit inside — Metal/Wood/Leather, the four heat bands, one Strike action,
Common/Rare/Epic — comes from `.claude/skills/blacksmithing/`.

Materials are granted the first time a player opens the forge, because no economy exists yet.
That grant is one method in `ForgeService` and is meant to be replaced by a real source.

## Inventory, units and equipment

`/inventory` lists what a player owns and where each item is. `/units` is the preparation
screen: pick one of your units, and put one of your own unequipped weapons into one of its two
hands. Every unit has exactly two weapon slots, a weapon consumes one or two of them, and one
physical item can be in one place at a time.

The server owns all of it. The browser names a unit and an item; ownership comes from the
session cookie, a unit or item belonging to somebody else is answered as one that does not
exist, and the rules are held up by database constraints rather than by checks the service is
trusted to remember — the equipment row's primary key is the item, and each hand has its own
filtered unique index, so two requests racing for one slot cannot both win.

Units are **not** defined in code. `server/content/units.json` holds the creator's Unit
definitions and `server/content/weapons.json` holds the wield metadata equipping needs. A
player-owned unit stores the definition's stable key and nothing else copied from it, so
renaming a unit, changing its tier, its armour limit or its Mounted state is a content edit —
no C# change, no React change, no EF migration. See
[`server/content/README.md`](server/content/README.md) for how to edit and add definitions,
and what happens when the content is wrong.

Every account is granted one unit per definition marked `Starter: true`, once, on first read.
That grant is a placeholder because recruitment does not exist yet, and it is recorded on the
unit row separately from the definition key so an account can still hold duplicate copies of a
Regular Unit later.

## Validation

The same checks [CI](.github/workflows/ci.yml) runs. The backend tests need nothing
running: they create their own SQLite database under the temporary directory and migrate it,
and the account tests still exercise Identity against the real provider rather than a
substitute store.

```bash
dotnet format server/WeaponsOfOrder.slnx --verify-no-changes
```

```bash
dotnet build server/WeaponsOfOrder.slnx --configuration Release
```

```bash
dotnet test server/WeaponsOfOrder.slnx --configuration Release
```

Frontend:

```bash
pnpm --dir web lint
```

```bash
pnpm --dir web typecheck
```

```bash
pnpm --dir web test
```

```bash
pnpm --dir web build
```

To point the backend tests at a different database, set `WOO_TEST_CONNECTION_STRING`.

## PWA

The client ships a web app manifest and a Workbox service worker that precaches the
versioned static shell. Browser V1 is an online game: there is no runtime caching rule,
so no API response is ever stored by the service worker, and no gameplay works offline.

The service worker is disabled under `vite dev`. To check installability, run
`pnpm --dir web build` then `pnpm --dir web preview`.

## Deployment

Deployed environments serve the whole game from **one origin**: ASP.NET Core hosts the built
React client, the PWA files and `/api` from the same process, so there is no CORS
configuration and no API base URL anywhere. Development gets the same topology through the
Vite proxy.

One command builds exactly what is deployed:

```bash
scripts/publish-artifact.sh
```

On Windows, where pnpm is reached through corepack:

```bash
PNPM='corepack pnpm@10.34.0' scripts/publish-artifact.sh
```

It builds the client into the API's `wwwroot`, publishes on top, and then asserts the result
carries the game content and the PWA files and carries no local development configuration.
Neither the client build output nor `artifacts/` is ever committed.

The staging environment — an Azure App Service on the Free tier, its SQLite file on
persistent storage, Application Insights and account email — is defined in
[`infra/azure/`](infra/azure/README.md) and documented
in [`AZURE_STAGING.md`](docs/deployment/AZURE_STAGING.md): how to provision it, what it costs,
what the GitHub `staging` Environment needs, how migrations are applied, and how to tear it
all down.

[CI](.github/workflows/ci.yml) validates every pull request and deploys only after both
validation jobs pass, on a push to `master` or a manual run. Migrations are applied by the
deployment, never on application startup.

**None of this affects running the game locally.** No Azure sign-in, no vault and no staging
secret is needed for `dotnet run` and `pnpm dev`.

The icons in `web/public/icons/` and `web/public/favicon.svg` are **neutral geometric
placeholders**, not artwork. Replace them when a real mark exists.
