# Weapons of Order: Browser V1 Build Plan

This is the active implementation plan after the core game-system definition phase.

It is intentionally task-based rather than a speculative long roadmap.

## Working method

Work **one task at a time**.

For each task:
1. inspect current `master` and the relevant design/architecture/skill sources;
2. create/work on a dedicated branch;
3. implement only the task scope plus fixes required to make that scope coherent;
4. run the task's validation;
5. report decisions, tests and any genuinely unresolved blocker;
6. creator reviews/squash-merges;
7. only then prepare the next task prompt.

Do not silently pull future tasks into the current branch.

Balance values that are already declared tunable should not block implementation. Keep them configurable/data-driven and use reasonable temporary v1 values when the design authority permits that.

The priority is to reach a complete **local gameplay loop** before spending prompts on full production Azure infrastructure.

---

## Task 0 - Implementation foundation

**Status: MERGED**

Purpose:
- lock Browser V1 platform/stack;
- lock authentication/security architecture;
- lock responsive desktop + mobile PWA shell direction;
- install reusable frontend-design and webapp-testing skills;
- preserve/correct Rune/Aura/weapon source references;
- establish this build plan;
- lock finite deterministic combat termination before simulation work begins.

Authority:
- `docs/architecture/TECH_STACK.md`
- `docs/architecture/AUTH_SECURITY.md`
- `docs/design/APP_LAYOUT.md`
- `docs/design/VISUAL_BASELINE.md`
- `.claude/skills/frontend-design/`
- `.claude/skills/webapp-testing/`

No production gameplay feature should be added as part of Task 0.

---

## Task 1 - Browser application foundation + CI

**Status: MERGED**

### Goal

Turn the approved React/Vite title-screen repository into a clean local browser-game application foundation without redesigning the approved landing screen.

### Scope

Agent must first inspect what already exists and reuse it where it remains valid rather than blindly replacing the repository.

Establish the minimum coherent structure for:
- React + TypeScript + Vite client;
- ASP.NET Core .NET 10 backend;
- frontend/backend development workflow;
- EF Core + PostgreSQL connection foundation;
- local development database setup;
- environment/configuration separation;
- PWA manifest/service-worker foundation;
- frontend and backend test commands;
- **GitHub Actions validation from the beginning** for build/tests/lint/typecheck as applicable.

### Toolchain pinning

The repository toolchain must be reproducible.

Current Browser V1 pinning is:
- root `.nvmrc`: Node `22.23.2`;
- `web/package.json` `engines.node`: `>=22.22.0 <23`;
- `web/package.json` `packageManager`: `pnpm@10.34.0`;
- `web/package.json` `engines.pnpm`: `10.34.0`.

Task 1 must preserve/verify these pins and make CI use the intended versions rather than silently using runner defaults.

### Legacy route audit

The existing routes predate the current Browser V1 architecture:
- `/hub`
- `/barracks`
- `/forge`
- `/arrange`
- `/dungeon`
- `/vault`
- `/ladder`

They are **historical placeholders, not approved application architecture**.

Task 1 must inventory them and remove/reconcile stale placeholder behavior rather than preserving it blindly.

Do not invent replacement gameplay screens merely to keep every old URL alive.

The approved `/` title screen remains intact. Authentication-aware routing is completed in Tasks 2-3.

### Not in scope

The production gameplay model is not implemented here.

Do not invent Units, weapons, resources, Runes, kingdoms, or gameplay fixtures merely to demonstrate the stack.

Do not configure full Azure staging yet.

### Acceptance

A developer/agent can:
- start the app locally from documented commands;
- see the existing approved landing screen;
- reach a simple backend health/API seam;
- connect the backend to local PostgreSQL;
- run frontend validation/tests;
- run backend validation/tests;
- build both sides cleanly;
- run the same core checks in GitHub Actions;
- install/open the PWA shell where the local browser supports it without pretending offline gameplay works;
- confirm legacy placeholder routes are explicitly handled rather than silently becoming the new menu architecture.

### Validation

- toolchain/version check;
- frontend lint/typecheck/test/build;
- backend format/build/test;
- database/migration sanity check;
- GitHub Actions validation;
- browser smoke test desktop + mobile using `webapp-testing`;
- no console errors on the approved landing screen.

---

## Task 2 - Login + account security

**Status: MERGED**

### Goal

Implement the real Browser V1 account/session foundation.

### Authority

Read `docs/architecture/AUTH_SECURITY.md` before implementation.

### Scope

Implement:
- ASP.NET Core Identity + EF Core/PostgreSQL persistence;
- register;
- login;
- logout;
- current-session/current-user endpoint/seam;
- forgot/reset-password architecture;
- email-confirmation architecture with development-safe email/token handling until production email delivery is configured;
- same-origin secure cookie session model;
- CSRF protection for authenticated mutations;
- auth endpoint rate limiting/lockout protections;
- protected-route behavior in React;
- login/register/reset UI matching the approved WoO visual language.

Reconcile the title-screen `ENTER WORLD` flow with authentication. It must no longer be a public bypass into a legacy game placeholder.

By the end of this task, no game-facing route may be considered protected merely because the frontend hides its link; server and routing authorization must enforce access.

Do not add Steam login or social login.

Do not implement game data merely to prove authorization.

### Acceptance

- a real account survives restart through PostgreSQL;
- valid user can register/login/logout;
- invalid login is handled safely;
- protected endpoints/routes reject unauthenticated access;
- cookie/session does not depend on localStorage bearer tokens;
- CSRF behavior works for protected mutations;
- desktop and mobile auth flows are usable;
- logout clears private UI state and returns to unauthenticated experience;
- `ENTER WORLD` follows the real auth/session flow rather than `/hub` placeholder behavior.

### Validation

Focused backend/frontend tests plus Playwright/browser flow for:
- registration;
- login failure;
- login success;
- protected route;
- logout;
- direct legacy URL behavior;
- mobile layout;
- desktop layout;
- browser console errors.

---

## Task 3 - Authenticated game shell + menus

**Status: MERGED**

### Goal

Build the first real post-login game shell for desktop web and mobile PWA.

### Authority

Read:
- `docs/design/APP_LAYOUT.md`;
- `docs/design/VISUAL_BASELINE.md`;
- `.claude/skills/frontend-design/SKILL.md`.

### Scope

Implement:
- authenticated application shell;
- responsive desktop primary navigation;
- responsive mobile/PWA bottom navigation/menu treatment;
- account/settings/logout access;
- route structure for only currently implemented/near-term destinations;
- reusable buttons/forms/panels/navigation tokens derived from the approved title-screen visual baseline;
- loading/error/empty foundations as needed by shell/auth.

Finish the route cleanup started in Tasks 1-2. Old `/hub`, `/barracks`, `/dungeon`, `/ladder`, etc. names are not preserved merely for historical compatibility. A legacy URL may redirect only when there is an intentional current destination; otherwise it should resolve cleanly as not found/removed.

Do not populate the shell with fake dashboard metrics, fake resources, invented kingdoms, or dead speculative menu items.

### Asset policy

Do **not** wait for button sprites. Core buttons remain responsive CSS/HTML controls.

If implementation reveals a decorative asset that would materially improve the approved direction, report the exact requested asset to the creator rather than inventing final art.

### Acceptance

- authenticated desktop shell feels visually continuous with landing screen;
- mobile PWA layout is a deliberate design rather than a squeezed desktop shell;
- keyboard/focus behavior works for normal controls;
- primary navigation is functional;
- no important action depends on hover;
- landing screen remains visually intact;
- no unauthenticated legacy placeholder route bypasses the authenticated shell.

### Validation

Browser sweep at minimum:
- representative desktop viewport;
- representative mobile viewport;
- navigation;
- logout;
- direct protected URL handling;
- legacy URL handling;
- visual screenshots inspected;
- no unexpected console errors.

---

## Task 4 - Forge vertical slice + minimal inventory seam

**Status: MERGED**

### Goal

Begin the actual game with the most characteristic already-defined system: forging.

### Authority

Read `.claude/skills/blacksmithing/` and only the supporting game skills actually needed.

### Scope direction

Implement one small end-to-end ordinary-forge slice rather than every forge feature at once:
- real authenticated player-owned state;
- one reusable forge interaction path based on canonical Heat + Strike rules;
- server-authoritative result;
- persistence;
- responsive desktop/mobile UI;
- no invented canonical weapon names/content beyond creator-approved/configured catalogue data.

### Minimal inventory seam

A forged item must have somewhere real to go in this task.

Implement only the smallest player-owned inventory boundary necessary to prove the loop:
- forged item receives a stable identity/ownership record;
- successful forge result is stored in that player's inventory/item collection;
- the result can be retrieved/listed sufficiently to prove persistence after reload/restart;
- authorization prevents another account from reading/claiming the item.

Do **not** turn this into the full Inventory/Equipment feature early. Filtering, rich inventory UI, Unit equipping and loadout management belong to Task 5.

### Acceptance

Locally, a logged-in player can complete one forge interaction and see the resulting persisted player-owned item afterward.

This is the first proof that the application foundation supports actual gameplay rather than only infrastructure.

---

## Task 5 - Inventory + Units + equipment

**Status: MERGED**

### Goal

Turn the minimal Forge inventory seam into the first real preparation system and connect forged equipment to Units/loadouts.

Authority includes:
- `.claude/skills/units/`;
- `.claude/skills/units/references/weapon-registry.md`;
- relevant combat stat authority;
- blacksmithing where item provenance matters.

Scope should include only enough real inventory/Unit/equipment behavior to prepare an army for the first combat prototype.

Exact specialization names/loadout mappings remain configurable creator-authored data rather than hardcoded combat architecture.

Do not build the entire roster/acquisition/progression system merely to equip an item.

### What this branch built

- creator-editable Unit and weapon content in `server/content/`, validated at startup and
  reloaded on save, with the three placeholder Arkazia definitions the creator specified;
- persistent player-owned Unit instances that reference a definition key rather than copying
  the definition, granted once per account through a temporary starter grant;
- persistent two-slot weapon loadouts held up by database constraints rather than by
  look-before-you-write checks;
- `/inventory` and `/units` behind the session guard, in desktop and mobile presentations;
- the first cross-system loop: forge a sword, find it in the inventory, put it in a unit's
  hands, and have it still be there after a reload and a fresh sign-in.

Recruitment, progression, specialisation names, armour items and combat remain out of scope.

---

## Task 6 - Army deployment + combat prototype

**Status: MERGED**

### Goal

Complete the first **local gameplay loop** and prove the already-defined Combat V1 rules in a running game.

### Direction

- army/deployment preparation;
- server-authoritative deterministic C# simulation;
- 8x7 hex battlefield foundation;
- reserves and assigned entry behavior;
- React pre/post-battle UI;
- PixiJS visual playback;
- temporary clearly non-final Unit visuals until actual combat sprite requirements are known.

### Required termination/timing behavior

The prototype must implement the combat canon's finite simulation rules:
- hard maximum simulated battle duration;
- no-progress window;
- unresolved guard expiry -> Draw without killing survivors;
- same-timestamp attacks resolve as one simultaneous batch;
- mutual lethal same-timestamp attacks can produce a deterministic Draw.

Tests must include at minimum:
- ordinary victory by all enemy Units/reserves dying;
- blocked living reserve preventing defeat;
- permanently blocked/no-progress situation terminating as Draw;
- hard-duration cap terminating a cyclic/otherwise progressing stalemate as Draw;
- mutual lethal same-timestamp attacks producing Draw.

### Sprite policy

**Combat sprites should not be generated before this task establishes real battlefield scale/camera/animation requirements.**

Once those measurements exist, give the creator a concrete asset specification for ChatGPT image generation.

### Acceptance

A player can locally move through the currently implemented preparation loop into a deterministic battle, watch it resolve, and reach a result without any simulation path being able to run forever.

At this point we have enough actual game to judge whether Forge -> equipment -> deployment -> combat is enjoyable before investing in production hosting work.

### What this branch built

- **`server/src/WeaponsOfOrder.Combat`**, a deterministic simulator with no package or project
  references at all: no ASP.NET, no EF Core, no Npgsql, no Identity, no HTTP. It takes a
  `BattleInput` and returns a `BattleResult` with a complete event log, and reads nothing else —
  no clock, no ambient randomness. The same input replays event for event.
- the locked battlefield: an offset hex grid of 8 columns x 7 rows, a half of 4 columns x 7 rows per side, one Unit per
  hex, occupied hexes impassable, and body blocking as a real consequence.
- the locked v1 combat rules: the Power -> coefficient -> critical -> Defense -> round -> minimum
  pipeline, the single 0-100 Energy bar with a Heavy attack at full, nearest-then-least-armoured
  targeting, one-hex-at-a-time pursuit, ordered reserves with assigned rear-column entry hexes and
  no fallback, and both finite guards ending an unresolved battle as a Draw without killing
  survivors.
- same-timestamp attacks resolved as one batch from the pre-batch state, so a Unit killed at time
  T still lands the attack it had committed for time T and a mutual last kill is a Draw.
- one persisted army per account, held up by database constraints rather than by
  look-before-you-write checks, and a thin authenticated battle API over the simulator.
- `/battle` behind the session guard: an accessible DOM hex grid to deploy on, and a PixiJS 8
  surface that plays the server's event log back with pause, replay and speed.
- the first complete local loop: forge a sword, equip it, deploy an army, fight, and watch it
  resolve.

Runeforging, Runes, Aura, classes, recruitment, progression, rewards, PvP, armour and battle
persistence remain out of scope. The opposition is a clearly labelled configuration-only training
harness, not roster content.

---

## Task 7 - Azure staging + deployment pipeline

**Status: THIS BRANCH / AWAITING CREATOR REVIEW**

### Goal

Make the proven Browser V1 local loop reproducibly deployable to a production-like staging environment.

This intentionally comes **after** the first complete local gameplay loop. CI exists from Task 1; this task adds hosting/deployment infrastructure rather than basic engineering validation.

### Scope

Establish:
- GitHub Actions deployment stage on top of the existing validation workflow;
- Azure App Service staging target;
- persistent staging database configuration;
- environment configuration/secrets model;
- EF Core migration deployment procedure;
- Application Insights/usable server telemetry;
- HTTPS/production cookie/proxy correctness;
- safe staging database separation.

Do not introduce Kubernetes, microservices or Redis.

### Creator decision during this task: SQLite and the Free tier

Task 7 originally provisioned Azure Database for PostgreSQL Flexible Server and a B1 App
Service plan. It worked end to end, and it cost money every day to run a prototype with one
player and no traffic.

The creator's explicit decision, which supersedes the PostgreSQL staging assumptions above:

- **Browser V1 prototype uses SQLite** — local development, CI and Azure staging alike.
- **Azure staging runs on the F1 Free App Service plan**, with the SQLite file on the
  instance's persistent `/home` storage, surviving redeployments.
- **PostgreSQL remains the intended store for real production**, to be designed when
  production is actually designed. Prototype data is disposable; there is no requirement to
  preserve the PostgreSQL migration history or migrate staging data.

The priority is a prototype that is simple and cheap. Complexity is not preserved merely
because it was already built — the PostgreSQL implementation stays in git history for
reference when production work begins.

### Acceptance

- merged approved code can be deployed reproducibly to staging;
- staging contains the working local vertical loop, not only an infrastructure shell;
- staging uses production-like HTTPS/auth configuration;
- secrets are not committed;
- failed validation blocks deployment;
- database migration procedure is explicit/repeatable.

---

### What this branch built

- **`infra/azure/`**, the whole staging environment as Bicep: a resource group of its own, a
  Linux App Service on the **Free tier**, Application Insights over a daily-capped Log
  Analytics workspace, Azure Communication Services for account email, and the federated
  identity GitHub Actions deploys as. There is no database resource and no secret anywhere in
  it. `infra/azure/bootstrap.sh` runs the lot — including the `what-if` — from one command
  that works in the Azure Portal's Cloud Shell, and takes no arguments at all.
- **SQLite as the prototype's store**, through ordinary EF Core with no abstraction over it.
  Development keeps its file in a git-ignored `.data/`; CI creates one under the runner's
  temporary directory; staging keeps one at `/home/data/weapons-of-order.db`, on App
  Service's persistent share and deliberately outside the deployed application, so a
  redeployment cannot replace it. Nothing needs installing to run or test the game any more.
- **a single SQLite migration baseline**, rebuilt from the current model. Every schema
  semantic the game relies on came with it: Identity's tables, the forge, inventory, units,
  equipment, deployment, the partial indexes that hold one weapon per hand and one Unit per
  hex, and the check constraints on materials and placements.
- **`Database:MigrateOnStartup`**, off by default and on for Browser V1. One instance, one
  file on that instance, so the schema travels with the code and the application applies it
  before serving anything. Migrations only, never `EnsureCreated`, never a drop, no seeding,
  and a failure stops the process rather than leaving it answering against a schema that is
  not there. Documented as something a real PostgreSQL production environment must turn off.
- **one origin, one artifact.** `scripts/publish-artifact.sh` builds the client into the
  API's `wwwroot` and publishes the application on top, then asserts the result carries the
  assemblies, `server/content`, `index.html`, a hashed bundle, the manifest and the service
  worker — and carries no database file and no local development configuration. ASP.NET Core
  serves the client, the PWA files and `/api` from one process, with SPA fallback so
  `/battle` typed into the address bar reaches React, hashed assets cached immutably and the
  files that keep their name across releases not cached at all.
- **the deployment stage on top of the existing CI.** A pull request validates and packages
  and stops. A push to master, or a manual run of a chosen ref, deploys and smoke-tests — and
  cannot start until the web, server and infrastructure validation jobs and the packaging job
  have all passed. No migration step, no database credential, and nothing in the workflow that
  touches a database: readiness is what proves the startup migration succeeded, because
  readiness opens the database.
- **no secret in the repository, and none in the GitHub Environment either.** GitHub Actions
  authenticates by exchanging a short-lived OIDC token for a user-assigned identity whose
  federated subject names the `staging` Environment and whose only permission is Website
  Contributor on one site. Five non-secret variables, zero secrets.
- **the deployment half of AUTH_SECURITY.md**, which that document had left open: forwarded
  headers first in the pipeline with `ForwardLimit = 1`, so the Secure cookie and the
  rate-limit partitions see the real scheme and the real caller while a client's own
  `X-Forwarded-For` is discarded; HSTS outside Development; and startup that refuses to boot
  when `Auth:ClientBaseUrl` is not an https origin in a deployed environment.
- **real account email in deployed environments** through Azure Communication Services on an
  Azure-managed domain, authenticated as the site's managed identity so no provider key
  exists. The link — a single-use bearer credential — still never reaches a log, and never
  reaches telemetry either: a span processor strips query strings before export, because
  `/confirm-email?token=…` is a request this server answers.
- **usable server telemetry** through the Azure Monitor OpenTelemetry distro rather than the
  retired classic SDK, plus App Service platform and console logs routed to the same
  workspace — which on a tier with cold starts is how a container that exits before the
  application starts is diagnosed at all.
- **`docs/deployment/AZURE_STAGING.md`**: architecture, what it costs and what the Free tier
  costs instead, where the database lives and why there, provisioning, the GitHub
  Environment's exact variables, the migration policy, redeploy, telemetry queries, browser
  verification, the boundary at which PostgreSQL comes back, a teardown path that says
  precisely what deleting the resource group destroys, and the one-time steps for leaving the
  earlier PostgreSQL environment behind.

Local development is untouched in every other respect. `dotnet run` and `pnpm dev` need no
Azure sign-in, no vault and no staging secret, and the Development-only notification outbox
remains Development-only.

No gameplay was added, and no gameplay behaviour changed.

## After Task 7

Do not pre-author a large roadmap now.

At that point we will have:
- accounts;
- responsive desktop/PWA shell;
- Forge gameplay;
- persisted inventory;
- Units/loadouts;
- army deployment;
- deterministic combat prototype;
- CI;
- staging deployment.

Use the actual game to decide the next system and where balance/progression needs refinement.

This is specifically where unresolved points such as exact Aura mastery turnover are expected to become easier to answer.

## Agent completion standard

A task is not complete merely because unit tests pass.

For frontend-facing work, use the installed `webapp-testing` skill and visually inspect the rendered result on desktop and mobile.

For visual design work, use `frontend-design` and preserve the approved WoO visual baseline rather than reverting to generic AI UI conventions.

Do not claim a command/test/browser sweep was run unless it was actually run.
