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

## Task 0 - Implementation foundation

**Status: THIS BRANCH / READY TO MERGE**

Purpose:
- lock Browser V1 platform/stack;
- lock authentication/security architecture;
- lock responsive desktop + mobile PWA shell direction;
- install reusable frontend-design and webapp-testing skills;
- establish this build plan.

Authority:
- `docs/architecture/TECH_STACK.md`
- `docs/architecture/AUTH_SECURITY.md`
- `docs/design/APP_LAYOUT.md`
- `.claude/skills/frontend-design/`
- `.claude/skills/webapp-testing/`

No production gameplay code should be added as part of Task 0.

---

## Task 1 - Browser application foundation

### Goal

Turn the approved React/Vite title-screen repository into a clean browser-game application foundation without redesigning the approved landing screen.

### Scope

Agent must first inspect what already exists and reuse it rather than blindly replacing the repository.

Establish the minimum coherent structure for:
- React + TypeScript + Vite client;
- ASP.NET Core .NET 10 backend;
- frontend/backend development workflow;
- EF Core + PostgreSQL connection foundation;
- local development database setup;
- environment/configuration separation;
- PWA manifest/service-worker foundation;
- frontend and backend test commands;
- basic GitHub Actions validation for build/tests where appropriate.

The production gameplay model is not implemented here.

Do not invent Units, weapons, resources, Runes, kingdom data, or gameplay fixtures merely to demonstrate the stack.

### Acceptance

A developer/agent can:
- start the app locally from documented commands;
- see the existing approved landing screen;
- reach a simple backend health/API seam;
- connect the backend to local PostgreSQL;
- run frontend validation/tests;
- run backend validation/tests;
- build both sides cleanly;
- install/open the PWA shell where the local browser supports it without pretending offline gameplay works.

### Validation

- frontend lint/typecheck/test/build;
- backend format/build/test;
- database/migration sanity check;
- browser smoke test desktop + mobile using `webapp-testing`;
- no console errors on the approved landing screen.

---

## Task 2 - Login + account security

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

Do not add Steam login.

Do not add social login.

Do not implement game data yet merely to prove authorization.

### Acceptance

- a real account survives restart through PostgreSQL;
- valid user can register/login/logout;
- invalid login is handled safely;
- protected endpoints/routes reject unauthenticated access;
- cookie/session does not depend on localStorage bearer tokens;
- CSRF behavior works for protected mutations;
- desktop and mobile auth flows are usable;
- logout clears private UI state and returns to unauthenticated experience.

### Validation

Focused backend/frontend tests plus Playwright/browser flow for:
- registration;
- login failure;
- login success;
- protected route;
- logout;
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
- landing screen remains visually intact.

### Validation

Browser sweep at minimum:
- representative desktop viewport;
- representative mobile viewport;
- navigation;
- logout;
- direct protected URL handling;
- visual screenshots inspected;
- no unexpected console errors.

---

## Task 4 - Azure staging + deployment pipeline

### Goal

Make the Browser V1 foundation continuously deployable before gameplay complexity grows.

### Scope

Establish:
- GitHub Actions validation/deployment flow;
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
- staging uses production-like HTTPS/auth configuration;
- secrets are not committed;
- failed validation blocks deployment;
- database migration procedure is explicit/repeatable.

---

## Task 5 - Forge vertical slice

### Goal

Begin the actual game with the most characteristic, already-defined system: forging.

### Authority

Read `.claude/skills/blacksmithing/` and only the supporting game skills actually needed.

### Scope direction

Implement one small end-to-end forge slice rather than every forge feature at once:
- real authenticated player-owned state;
- one reusable forge interaction path based on canonical Heat + Strike rules;
- server-authoritative result;
- persistence;
- responsive desktop/mobile UI;
- no invented canonical weapon names/content beyond creator-approved/configured catalogue data.

Exact prompt/scope is written only when Tasks 1-4 show the real codebase shape.

---

## Task 6 - Inventory + Units + equipment

### Goal

Connect forged equipment to the player-owned Unit/loadout systems.

Authority will include:
- `.claude/skills/units/`;
- weapon registry;
- relevant combat stat authority;
- blacksmithing where item provenance matters.

Implement incrementally. Do not build the entire roster/progression system just to equip an item.

Exact prompt is deferred until the Forge slice exists.

---

## Task 7 - Deployment + combat prototype

### Goal

Prove the already-defined Combat V1 rules in an actual running game.

Direction:
- server-authoritative deterministic C# simulation;
- 8x7 hex battlefield foundation;
- React pre/post-battle UI;
- PixiJS visual playback;
- temporary clearly non-final Unit visuals until actual combat sprite requirements are known.

**Combat sprites should not be generated before this task establishes real battlefield scale/camera/animation requirements.**

At that point, give the creator a concrete asset specification for ChatGPT image generation.

---

## After Task 7

Do not pre-author a large roadmap now.

At that point we will have:
- accounts;
- deployed browser architecture;
- responsive PWA shell;
- Forge gameplay;
- player inventory/loadouts;
- real combat prototype.

Use the actual game to decide the next system and where balance/progression needs refinement.

This is specifically where unresolved points such as exact Aura mastery turnover are expected to become easier to answer.

## Agent completion standard

A task is not complete merely because unit tests pass.

For frontend-facing work, use the installed `webapp-testing` skill and visually inspect the rendered result on desktop and mobile.

For visual design work, use `frontend-design` and preserve the approved WoO visual baseline rather than reverting to generic AI UI conventions.

Do not claim a command/test/browser sweep was run unless it was actually run.
