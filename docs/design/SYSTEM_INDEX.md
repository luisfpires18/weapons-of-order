# Weapons of Order: System Index

This is the routing map for game-design context.

## Status meanings

- **LOCKED FOUNDATION**: enough structure is agreed to guide implementation; balance details may still be tunable.
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
| Chaos/Order L3 gameplay | DEFERRED / WIP | `.claude/skills/runes-aura/` |
| Unit/Hero structure | LOCKED FOUNDATION | `.claude/skills/units/` |
| Equipment-defined combat classes | LOCKED FOUNDATION | `.claude/skills/units/` |
| Battlefield/grid/deployment | LOCKED FOUNDATION | `.claude/skills/combat/` |
| Movement/collision/pathing foundation | LOCKED FOUNDATION | `.claude/skills/combat/` |
| Targeting/range foundation | LOCKED FOUNDATION | `.claude/skills/combat/` |
| Active deployment + reserves | LOCKED FOUNDATION | `.claude/skills/combat/` |
| Core combat stats | LOCKED FOUNDATION | `.claude/skills/combat/` |
| Energy / Heavy / L1 / L2 attack progression | LOCKED FOUNDATION | `.claude/skills/combat/` |
| Server-authoritative deterministic battle/replay model | LOCKED FOUNDATION | `.claude/skills/combat/` |
| Damage & Defense formulas | NOT DESIGNED | `OPEN_QUESTIONS.md` |
| Exact Energy generation/combat coefficients | NOT DESIGNED | `OPEN_QUESTIONS.md` |
| Synergies | PARTIAL / WIP | separate future system; no skill yet |
| Unit acquisition/progression | NOT DESIGNED | `OPEN_QUESTIONS.md` |
| Economy beyond crafting foundations | NOT DESIGNED | `OPEN_QUESTIONS.md` |
| Main game loop/modes | PARTIAL / WIP | broad autobattler direction only |
| PvP / matchmaking / seasons | NOT DESIGNED | `OPEN_QUESTIONS.md` |
| Large-scale war/formations | DEFERRED | `GAME_VISION.md`; combat skill records only the current abstraction boundary |
| Persistence/database/API | NOT DESIGNED | implementation architecture to follow system design |
| Authentication/accounts | NOT DESIGNED | implementation architecture to follow product needs |
| Roadmap | DEFERRED | rebuild after enough core systems are defined |

## Skill policy

Create a project skill when:
- the subsystem has stable rules;
- those rules are likely to matter across many future prompts;
- loading them only when relevant saves context.

Do not create a skill just because a topic exists.

Use normal docs when information is:
- project-wide;
- a routing/index concern;
- unresolved design tracking;
- architecture that affects most work.

Use future data/content files when information is primarily authored game content, such as exact kingdom rosters, specialization mappings, Rune records, or synergy records.

## Update rule

After a design discussion becomes canon:

1. Update the relevant skill reference, or create a normal design doc if no skill fits.
2. Update this index if the subsystem status changed.
3. Update `OPEN_QUESTIONS.md` so stale uncertainty does not remain.
4. Only then implement the rule in game code.
