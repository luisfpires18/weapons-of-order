# Weapons of Order

Weapons of Order is a forging-centered fantasy autobattler being developed as a browser-first game.

Players forge weapons and mundane armor, Runeforge weapons with Runes, equip Units and Hero Units before battle, and then watch an automatically resolved battle play back from authoritative combat events/state transitions.

Pre-battle preparation matters. Players do not swap equipment or manually cast combat abilities during a battle under the current combat design.

The current title/landing screen is an **approved visual baseline**. Do not redesign it unless the task explicitly asks for a redesign.

## Source-of-truth order

When sources conflict, use this order:

1. The creator's explicit current decision.
2. Canon references inside the relevant `.claude/skills/` custom game-system skill.
3. Relevant locked architecture/design document.
4. `docs/design/GAME_VISION.md`.
5. `docs/design/SYSTEM_INDEX.md` and `docs/design/OPEN_QUESTIONS.md`.
6. Existing implementation/code.
7. Older drafts or historical implementation ideas.

A newer explicit creator decision overrides an older draft or implementation.

Never infer that existing code is canon merely because it exists.

Older rune/Aura/weapon source files must be interpreted through `docs/design/SOURCE_CORRECTIONS.md` when reused.

## Required implementation context

Before substantial implementation work, read the documents relevant to the task rather than loading everything.

Project-wide implementation foundation:
- `docs/architecture/TECH_STACK.md`
- `docs/architecture/AUTH_SECURITY.md` when authentication/security/account data is involved
- `docs/design/APP_LAYOUT.md` when frontend layout/navigation/responsiveness is involved
- `docs/design/VISUAL_BASELINE.md` when frontend visual styling, navigation, panels or controls are involved
- `docs/implementation/BUILD_PLAN.md` for task order/scope

Before gameplay implementation:
1. read `docs/design/GAME_VISION.md`;
2. read `docs/design/SYSTEM_INDEX.md`;
3. load only the custom game-system skill(s) needed by the task;
4. check `docs/design/OPEN_QUESTIONS.md` before filling a gap with an assumption.

Do not load every game-system skill for every task.

## Browser V1 platform

Browser V1 is the active product target.

Develop:
- desktop web;
- mobile-responsive web/PWA;
- one shared React application and backend.

Steam is deferred. Do not add Steam, Electron, Steamworks, Steam authentication, native mobile apps, or Unity to Browser V1 tasks unless the creator explicitly changes direction.

Tech-stack authority is `docs/architecture/TECH_STACK.md`.

## External reusable skills

Two upstream Anthropic skills are vendored into this repository from `anthropics/skills` at upstream commit `f17010c9bb483898c1d9c9f42dde2b3a98889434`:
- `.claude/skills/frontend-design/`
- `.claude/skills/webapp-testing/`

They are general implementation tooling, **not Weapons of Order game canon**.

Use `frontend-design` for significant UI creation/restyling. It does not override the creator's visual direction, `APP_LAYOUT.md`, or `VISUAL_BASELINE.md`; the approved WoO title screen and creator instructions remain the brief.

Use `webapp-testing` for real browser verification, screenshots, responsive checks and UI-flow debugging. Passing unit tests alone is not enough evidence that a responsive frontend task is visually correct.

Treat vendored upstream skill files as third-party source. Do not casually rewrite them as project-specific instructions; project-specific rules belong in project docs/custom skills.

## Creator authority and naming

The creator defines canonical names and rosters.

Do not invent as canon:
- Units;
- Hero Units;
- combat specialization/class names;
- kingdom roster entries;
- Runes or Rune derivations;
- synergy names/effects;
- settlements, factions, characters, or other lore names.

Examples may be used only when clearly labeled as examples and never written into canonical game data without approval.

Exact specialization/loadout mappings and similar authored content should remain configuration/data rather than hardcoded assumptions where the design says they are configurable.

## Design vs balance

Core system structure has now reached the point where Browser V1 implementation can begin.

Do not stop implementation merely because a numeric balance value remains tunable when the canon explicitly treats that value as balance/configuration.

When a structural system question is genuinely unresolved:
- keep the uncertainty explicit;
- check/add `docs/design/OPEN_QUESTIONS.md` if project-relevant;
- do not implement a convenient assumption as permanent canon.

When the creator locks a subsystem decision:
- update the relevant custom skill reference if one exists;
- update `docs/design/SYSTEM_INDEX.md` if status changed;
- remove/revise stale open questions.

Create a new custom project skill only when a subsystem has stable reusable rules Claude repeatedly needs. Broad project-wide architecture belongs in normal docs.

## Architecture baseline

Browser V1 uses the architecture defined in `docs/architecture/TECH_STACK.md`.

High-level locked direction:
- React + TypeScript + Vite client;
- desktop + mobile PWA simultaneously;
- PixiJS for the future combat rendering surface;
- ASP.NET Core .NET 10 backend;
- EF Core + PostgreSQL;
- ASP.NET Core Identity + secure cookie browser sessions;
- server-authoritative game state;
- deterministic server-side combat simulation;
- modular monolith first;
- GitHub Actions + Azure target;
- Steam later.

Do not resurrect old gameplay contracts, old fixed Rune effects, old unit schemas, or old roadmaps merely because they existed in previous commits.

## Visual implementation rules

The approved landing/title screen is the visual anchor for the rest of Browser V1. Its concrete composition/color/shape language is recorded in `docs/design/VISUAL_BASELINE.md`.

For menus/navigation/buttons:
- derive/reuse the established visual tokens/colors rather than introducing a generic SaaS palette;
- do not make one kingdom's faction colors the universal UI theme;
- build responsive desktop and mobile variants together;
- use real accessible HTML/CSS controls for core buttons rather than image/sprite buttons.

The creator does **not** need to generate button sprites for the initial application shell.

When a real generated decorative/game asset becomes useful, state exactly what asset is needed rather than inventing final-looking art.

Combat sprite generation is intentionally deferred until the real PixiJS battlefield prototype determines scale/camera/animation requirements.

## Engineering discipline

- Prefer simple implementations matching the approved system.
- Do not over-engineer future systems.
- Keep tunable balance values data-driven when practical.
- Keep game rules out of purely visual UI components.
- Keep authoritative game decisions on the server.
- Add focused tests for approved rules.
- Do not build placeholder gameplay content with invented canonical names.
- Preserve the approved landing screen unless the task explicitly changes it.
- For frontend changes, validate both desktop and mobile rendered behavior.
- Do not claim tests/browser sweeps/commands were run unless they actually were.
- Follow `docs/implementation/BUILD_PLAN.md` one task at a time rather than implementing future tasks early.
