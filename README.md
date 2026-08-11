# Weapons of Order

Forging-centered fantasy autobattler, developed browser-first.

Design and architecture authority lives in `docs/`; start with [`CLAUDE.md`](CLAUDE.md) for
the source-of-truth order. This file covers only how to run and validate the code locally.

## Layout

```text
web/                                  React + TypeScript + Vite client
server/
  src/WeaponsOfOrder.Api/             ASP.NET Core host, endpoints, configuration
    Auth/                             Accounts, sessions, account notifications
    Security/                         Antiforgery and authorization conventions
  src/WeaponsOfOrder.Infrastructure/  EF Core / PostgreSQL persistence and migrations
  tests/WeaponsOfOrder.Api.Tests/     API, account and configuration tests
art/                                  Shared art, aliased into the client as @art
docker-compose.yml                    Local development PostgreSQL
```

## Prerequisites

| Tool       | Version   | Pinned by                              |
| ---------- | --------- | -------------------------------------- |
| Node       | 22.23.2   | [`.nvmrc`](.nvmrc)                     |
| pnpm       | 10.34.0   | `packageManager` in `web/package.json` |
| .NET SDK   | 10.0.200+ | [`global.json`](global.json)           |
| PostgreSQL | 18        | [`docker-compose.yml`](docker-compose.yml) |

Enable corepack once so pnpm resolves to the pinned version:

```bash
corepack enable
```

## First-time setup

```bash
docker compose up -d
```

```bash
pnpm --dir web install
```

```bash
dotnet tool restore
```

```bash
dotnet ef database update --project server/src/WeaponsOfOrder.Infrastructure --startup-project server/src/WeaponsOfOrder.Api
```

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

Vite proxies `/api` to the API, so the browser stays on **one origin** in development —
the same topology as deployment, where ASP.NET Core serves the built client from
`wwwroot` alongside `/api`. There is no CORS configuration and no API base URL to set.

To exercise that single-origin path locally, build the client into the API's `wwwroot`
and run the API alone:

```bash
pnpm --dir web build --outDir ../server/src/WeaponsOfOrder.Api/wwwroot --emptyOutDir
```

## Database

`docker compose up -d` starts PostgreSQL 18 on **host port 5433** — not 5432, so it does
not collide with a natively installed PostgreSQL.

The development credentials in `docker-compose.yml` and
`server/src/WeaponsOfOrder.Api/appsettings.Development.json` are local-only and
intentionally committed so a fresh clone needs no configuration. Every other environment
supplies its own connection string:

```bash
export ConnectionStrings__WeaponsOfOrder="Host=...;Database=...;Username=...;Password=..."
```

`dotnet user-secrets` also works for local overrides. The API fails at startup if no
connection string is configured rather than falling back to a default.

Migrations are applied explicitly, never on startup:

```bash
dotnet ef migrations add <Name> --project server/src/WeaponsOfOrder.Infrastructure --startup-project server/src/WeaponsOfOrder.Api --output-dir Persistence/Migrations
```

```bash
dotnet ef database update --project server/src/WeaponsOfOrder.Infrastructure --startup-project server/src/WeaponsOfOrder.Api
```

## Accounts

Sign-in is email + password on ASP.NET Core Identity, with the session held in an
`HttpOnly` cookie. There is no token in `localStorage`, and mutating requests carry an
antiforgery token in the `X-WoO-Antiforgery` header, which the client reads from
`GET /api/auth/session`. Full rules are in
[`AUTH_SECURITY.md`](docs/architecture/AUTH_SECURITY.md).

A confirmed email address is required before sign-in. **No email provider is configured
yet**, so in development the confirmation and reset links are captured in memory instead of
sent:

- `GET /api/dev/account-notifications` lists the most recent links;
- the same links are written to the API log;
- the auth screens show an "Open the captured link" action.

All three exist **only** when the API runs in the Development environment. Every other
environment drops the message and logs that it did, without recording the link — a link is a
bearer credential. Switch the endpoint off with `Auth:Development:ExposeNotifications`.

Security thresholds — password policy, lockout, rate limits, cookie lifetime — live under the
`Auth` section of `appsettings.json`.

## Validation

The same checks [CI](.github/workflows/ci.yml) runs.

The backend tests need PostgreSQL running: they exercise Identity against the real provider
and migrate their own `weapons_of_order_tests` database on first use. Run
`docker compose up -d` first, or point `WOO_TEST_CONNECTION_STRING` somewhere else.

Client:

```bash
pnpm --dir web lint && pnpm --dir web typecheck && pnpm --dir web test && pnpm --dir web build
```

Server:

```bash
dotnet format server/WeaponsOfOrder.slnx --verify-no-changes && dotnet build server/WeaponsOfOrder.slnx && dotnet test server/WeaponsOfOrder.slnx
```

## PWA

The client ships a web app manifest and a Workbox service worker that precaches the
versioned static shell. Browser V1 is an online game: there is no runtime caching rule,
so no API response is ever stored by the service worker, and no gameplay works offline.

The service worker is disabled under `vite dev`. To check installability, run
`pnpm --dir web build` then `pnpm --dir web preview`.

The icons in `web/public/icons/` and `web/public/favicon.svg` are **neutral geometric
placeholders**, not artwork. Replace them when a real mark exists.
