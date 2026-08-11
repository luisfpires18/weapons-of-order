# Weapons of Order: System Index

This is the routing map for game-design and implementation context.

## Status meanings

- **LOCKED FOUNDATION**: enough structure is agreed to guide implementation; balance/details may still be tunable.
- **PARTIAL / WIP**: meaningful decisions exist, but the subsystem is not complete enough to implement broadly.
- **NOT DESIGNED**: do not invent it from older code or genre convention.
- **DEFERRED**: intentionally postponed.

## Systems

| System | Status | Authority / context |
|---|---|---|
| Blacksmithing & armor smithing | LOCKED FOUNDATION | `.claude/skills/blacksmithing/` |
| Runeforging & Artifact blood bond | LOCKED FOUNDATION | `.claude/skills/blacksmithing/` |
| Rune taxonomy & registry | LOCKED FOUNDATION | `.claude/skills/runes-aura/` |
| Aura L0-L2 structure | LOCKED FOUNDATION | `.claude/skills/runes-aura/` |
| Aura visual/family themes | LOCKED FOUNDATION | `.claude/skills/runes-aura/references/aura-visual-reference.md`; exact combat effects remain tunable/authored |
| Wielder Rune mastery + paired weapon Aura behavior | LOCKED FOUNDATION | `.claude/skills/runes-aura/`; `.claude/skills/units/references/weapon-registry.md` |
| Exact Aura mastery turnover/progression | PARTIAL / WIP | deliberately wait for real game/playtesting |
| Chaos/Order named weapon roster concepts | PARTIAL / WIP | `.claude/skills/runes-aura/references/chaos-order-weapon-roster.md`; incomplete Order entries remain explicitly WIP |
| Chaos/Order L3 gameplay | DEFERRED / WIP | `.claude/skills/runes-aura/` |
| Unit/Hero structure | LOCKED FOUNDATION | `.claude/skills/units/` |
| Equipment-defined combat classes | LOCKED FOUNDATION | `.claude/skills/units/`; exact mappings are configurable creator-authored data |
| Weapon taxonomy / slots / dual wield / Runeforged pair compatibility | LOCKED FOUNDATION | `.claude/skills/units/references/weapon-registry.md` |
| Weapon stat/weight balance | LOCKED FOUNDATION | weapon structure locked; exact values are tunable balance data |
| Armor slots/classes/defensive role | LOCKED FOUNDATION | `.claude/skills/blacksmithing/`; `.claude/skills/combat/` |
| Armor stat/interval balance | LOCKED FOUNDATION | exact values are tunable balance data |
| Battlefield/grid/deployment | LOCKED FOUNDATION | `.claude/skills/combat/` |
| Movement/collision/pathing foundation | LOCKED FOUNDATION | `.claude/skills/combat/` |
| Targeting/range foundation | LOCKED FOUNDATION | `.claude/skills/combat/` |
| Active deployment + reserves | LOCKED FOUNDATION | `.claude/skills/combat/` |
| Core combat stats | LOCKED FOUNDATION | `.claude/skills/combat/` |
| Damage/Defense/Crit/Heavy math v1 | LOCKED FOUNDATION | `.claude/skills/combat/` |
| Energy generation + L0/L1/L2 attack progression | LOCKED FOUNDATION | `.claude/skills/combat/` |
| Attack Interval structure | LOCKED FOUNDATION | `.claude/skills/combat/`; exact modifiers are tuning data |
| Server-authoritative deterministic battle/replay model | LOCKED FOUNDATION | `.claude/skills/combat/` |
| Synergies | PARTIAL / WIP | separate future system; no skill yet |
| Unit acquisition/progression | NOT DESIGNED | `OPEN_QUESTIONS.md` |
| Economy beyond crafting foundations | NOT DESIGNED | `OPEN_QUESTIONS.md` |
| Main game loop/modes | PARTIAL / WIP | broad autobattler direction only |
| PvP / matchmaking / seasons | NOT DESIGNED | `OPEN_QUESTIONS.md` |
| Large-scale war/formations | DEFERRED | `GAME_VISION.md`; combat skill records only the current abstraction boundary |
| Browser V1 platform + tech stack | LOCKED FOUNDATION | `docs/architecture/TECH_STACK.md` |
| Browser auth/account/security architecture | LOCKED FOUNDATION | `docs/architecture/AUTH_SECURITY.md` |
| Desktop web + mobile PWA layout foundation | LOCKED FOUNDATION | `docs/design/APP_LAYOUT.md` |
| Approved UI visual baseline | LOCKED FOUNDATION | `docs/design/VISUAL_BASELINE.md` |
| Persistence/database/API architecture | LOCKED FOUNDATION | ASP.NET Core + EF Core + PostgreSQL in `TECH_STACK.md`; implementation pending |
| CI/CD + Azure deployment target | LOCKED FOUNDATION | GitHub Actions + Azure target in `TECH_STACK.md`; implementation task pending |
| Steam client/integration | DEFERRED | Browser V1 first; Steam later |
| Browser V1 implementation plan | LOCKED FOUNDATION | `docs/implementation/BUILD_PLAN.md` |

## Skill policy

Create a custom project skill when:
- the subsystem has stable rules;
- those rules are likely to matter across many future prompts;
- loading them only when relevant saves context.

Do not create a custom game skill just because a topic exists.

Use normal docs when information is:
- project-wide;
- a routing/index concern;
- unresolved design tracking;
- architecture that affects most work.

Use future data/content files when information is primarily authored game content, such as exact kingdom rosters, specialization mappings, Rune records, weapon records, or synergy records.

The vendored `frontend-design` and `webapp-testing` skills are external implementation tooling, not game-system canon.

## Update rule

After a design discussion becomes canon:

1. Update the relevant skill reference, or create/update a normal design/architecture doc if no skill fits.
2. Update this index if the subsystem status changed.
3. Update `OPEN_QUESTIONS.md` so stale uncertainty does not remain.
4. Only then implement the rule in game code.

Implementation work should follow `docs/implementation/BUILD_PLAN.md` one task at a time.
