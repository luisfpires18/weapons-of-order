# Weapons of Order: Browser V1 Tech Stack

This document is the implementation authority for the browser-first product architecture.

## Product/platform decision

V1 is a **browser game**.

The same browser product must support:
- desktop web;
- mobile web;
- installable mobile/desktop PWA behavior where supported.

Steam is intentionally deferred. Do not add Electron, Steamworks, Steam authentication, Steam achievements, or Steam-specific packaging during Browser V1 unless the creator explicitly reopens that scope.

The architecture should remain capable of adding a Steam client later without replacing the game backend or player account model.

## Client

Use:
- React
- TypeScript
- Vite

The approved title/landing screen already in the repository is the visual baseline and must be preserved unless explicitly redesigned.

React owns:
- application shell/navigation;
- authentication UI;
- Forge/Runeforge UI;
- inventory;
- Units/Heroes;
- equipment/loadouts;
- army/deployment management;
- account/settings;
- normal HUD and menus.

### Combat rendering

Use **PixiJS** for the 2D battlefield/combat renderer when combat implementation begins.

React should host the battlefield surface and surrounding UI, while PixiJS owns high-frequency visual rendering such as:
- hex battlefield;
- Unit sprites;
- movement animation;
- attacks/projectiles;
- combat VFX;
- Aura/Rune visual playback.

PixiJS is a renderer, not the authority for combat rules. Authoritative combat rules remain server-side.

Do not introduce Unity/WebGL into Browser V1.

## PWA

Desktop web and mobile PWA are developed simultaneously rather than treating mobile as a later port.

PWA requirements:
- valid web app manifest;
- installable application shell where browser support allows it;
- responsive layout from the first authenticated screen;
- safe-area handling for mobile devices;
- touch-sized controls;
- portrait-first mobile management screens unless a screen explicitly benefits from landscape;
- no assumption of mouse hover for required actions.

A service worker may cache versioned static application assets for load performance.

It must **not** make authoritative gameplay offline and must not cache authenticated API responses in a way that can expose stale/private player data.

Browser V1 is an online game. Offline-first gameplay is not a requirement.

## Server

Use:
- ASP.NET Core on .NET 10;
- C#;
- EF Core;
- PostgreSQL.

The backend is authoritative for:
- authentication/session identity;
- account/player data;
- inventory and ownership;
- forging outcomes;
- equipment validity;
- progression;
- battle snapshots;
- combat simulation/results;
- persistence.

Never trust client-submitted calculated totals, ownership claims, combat results, forging results, or other authoritative game state.

## Combat simulation boundary

Combat should become a deterministic C# domain component/library with a boundary conceptually like:

`BattleSnapshot + authoritative RNG state -> BattleResult + EventTimeline`

The client renders the result/timeline. It does not decide authoritative targeting, damage, movement, RNG, deaths, reinforcement entry, or victory.

Follow `.claude/skills/combat/` for actual combat canon.

## Database

Use PostgreSQL as the primary relational store.

Use EF Core migrations for schema changes.

Do not add additional persistence technologies until a demonstrated requirement exists.

Not required for V1:
- Redis;
- document databases;
- event-sourcing infrastructure;
- distributed caches;
- message brokers.

These can be reconsidered if measured requirements justify them.

## Authentication

Use ASP.NET Core Identity backed by PostgreSQL.

Browser authentication uses same-origin secure cookies. Do not store long-lived bearer/JWT access tokens in `localStorage` for the normal browser session.

Full rules are in `AUTH_SECURITY.md`.

## Application topology

Prefer one public origin for Browser V1.

Conceptually:

`weaponsoforder.com`

serves/routes:
- React application/static assets;
- `/api/...` backend endpoints;
- `/auth/...` account flows where applicable.

Development may run frontend/backend on separate local ports when useful, but production should avoid unnecessary cross-origin complexity.

The precise domain/subdomain can change without changing the architecture.

## Azure target

Initial production/staging target:
- Azure App Service for the ASP.NET Core application and built frontend;
- Azure Database for PostgreSQL Flexible Server;
- Application Insights for server/application telemetry;
- Azure configuration/secrets facilities appropriate to the deployed environment.

Do not introduce Kubernetes for V1.

Azure Container Apps/microservices are not required unless the application later develops a demonstrated need for independent workers/services/scaling boundaries.

## CI/CD

Use GitHub Actions.

The pipeline should eventually perform, at minimum:
- dependency restore/install;
- frontend lint/typecheck/tests/build;
- backend format/build/tests;
- migration validation where practical;
- browser/E2E checks at the appropriate stage;
- deploy to the configured Azure environment only after required checks pass.

Production deployment details are introduced as an explicit implementation task rather than silently mixed into unrelated feature work.

## Architecture style

Start as a **modular monolith**, not microservices.

Keep clear boundaries between:
- web/API concerns;
- authentication;
- game/application services;
- game domain rules;
- persistence;
- deterministic combat simulation;
- React UI;
- PixiJS rendering.

Simple boundaries are valuable. Distributed architecture is not.

## Explicitly deferred

Do not implement in Browser V1 foundation work:
- Steam client;
- Steam login/linking;
- Electron;
- native mobile app;
- Unity;
- microservices;
- Kubernetes;
- Redis unless a measured need appears;
- real-time networking merely because the genre is multiplayer;
- offline authoritative gameplay.

## Change rule

Do not replace a technology in this stack because another option is fashionable or personally preferred.

A stack change requires either:
- explicit creator decision; or
- a concrete technical blocker with evidence and a proposed migration.
