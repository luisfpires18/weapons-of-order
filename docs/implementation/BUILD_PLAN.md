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

**Status: THIS BRANCH / AWAITING REVIEW**

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

Exact prompt is written after Task 4 exists.

---

## Task 6 - Army deployment + combat prototype

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

---

## Task 7 - Azure staging + deployment pipeline

### Goal

Make the proven Browser V1 local loop reproducibly deployable to a production-like staging environment.

This intentionally comes **after** the first complete local gameplay loop. CI exists from Task 1; this task adds hosting/deployment infrastructure rather than basic engineering validation.

### Scope

Establish:
- GitHub Actions deployment stage on top of the existing validation workflow;
- Azure App Service staging target;
- Azure PostgreSQL configuration;
- environment configuration/secrets model;
- EF Core migration deployment procedure;
- Application Insights/usable server telemetry;
- HTTPS/production cookie/proxy correctness;
- safe staging database separation.

Do not introduce Kubernetes, microservices or Redis.

### Acceptance

- merged approved code can be deployed reproducibly to staging;
- staging contains the working local vertical loop, not only an infrastructure shell;
- staging uses production-like HTTPS/auth configuration;
- secrets are not committed;
- failed validation blocks deployment;
- database migration procedure is explicit/repeatable.

---

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
