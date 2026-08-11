# Weapons of Order: Open Questions

These are intentionally unresolved. Existing or historical code must not silently answer them.

**Important:** a tunable balance value is not automatically a design blocker. Browser V1 implementation should proceed with configurable/data-driven temporary values where the structural rule is already locked.

Only resolve structural canon when the creator explicitly decides it. Balance may be iterated from the running game.

## Combat/equipment tuning - not blockers

Combat Math v1 is structurally locked in `.claude/skills/combat/`.

Weapon/loadout structure is locked in `.claude/skills/units/references/weapon-registry.md`.

These values remain tuning work and should generally be balanced through implementation/playtesting rather than prolonged design discussion:
- Unit base-stat budgets by fixed star tier;
- weapon Power/Crit budgets;
- Light/Medium/Heavy weapon weight assignment;
- armor HP/Defense budgets by slot/class;
- Light/Medium/Heavy armor Attack Interval modifiers;
- weapon-weight/loadout Attack Interval modifiers;
- minimum Attack Interval floor;
- specific weapon range exceptions beyond current defaults;
- normal crafting costs/quality thresholds;
- Runeforging/recoil probabilities where structure is already defined;
- exact maximum battle duration;
- exact no-progress window duration.

Current Power scale, Defense constant, Energy gain, Heavy coefficient, and Crit multiplier are v1 tunable balance values rather than permanent sacred numbers.

AI/agents may propose/configure reasonable starting values and adjust from test evidence. Do not write those temporary values back as immutable lore/canon unless the creator explicitly locks them.

Possible future mechanics still requiring an actual structural decision if introduced:
- shield Block;
- Dodge.

Do not introduce them merely to fill a stat table.

## Combat rules still open

Targeting foundation is locked:
- closest valid enemy first;
- on equal distance, prefer lower final Defense / less-armored target;
- distance always overrides the armor preference;
- exact-equal candidates require no further authored gameplay priority for v1.

Equal shortest paths likewise require no additional gameplay preference; deterministic pathfinder ordering is implementation detail.

Battle-end and simulation-termination foundation is locked:
- defeat occurs only when every active Unit and every reserve Unit is dead;
- a living reserve blocked from entering is not defeated and prevents army defeat;
- simultaneous attacks at the same authoritative timestamp resolve as one batch from the pre-batch state;
- a Unit killed in that batch still completes its already-valid same-timestamp attack;
- simultaneous elimination of both armies is a Draw;
- every battle has a configurable hard maximum simulated duration and no-progress window;
- if either guard expires while neither army is defeated, the result is a Draw;
- timeout/stalemate never reclassifies a living Unit or blocked reserve as dead;
- v1 progress means HP change, Unit death, or successful reserve entry; movement/retargeting/pathing/failed entry attempts do not reset the no-progress window.

Still unresolved but not required before the first combat prototype:
- exact movement timing values for Mounted vs non-Mounted Units;
- future special targeting overrides such as Assassin-style behavior;
- exact reinforcement entry delay;
- exact progressive event-delivery/network protocol for competitive multiplayer.

## Units and classes

Creator-authored content remains intentionally open:
- exact kingdom rosters;
- exact Hero roster;
- final Rune-family combat class names beyond approved examples.

Exact mundane specialization names and equipment/loadout mappings are expected to be **config/data-driven** so the creator can change them without rewriting combat code. Their absence must not block architecture work.

Still structurally open:
- acquisition/recruitment rules;
- progression beyond fixed star tier, if any.

## Weapon crafting / paired Runeforging

Still open:
- slot cost for weapon types not explicitly fixed by the weapon registry;
- failure/destruction behavior when two weapons are Runeforged as one paired operation;
- whether paired Runeforging changes success odds/cost compared with independent operations;
- weapon-specific special attacks beyond the shared combat foundation, if any.

These do not block the first ordinary Forge slice.

## Synergies

Still open:
- what produces a synergy: kingdom, specialization, weapon type, Mounted tag, Rune family, or combinations;
- threshold model, if any;
- whether changing equipment/class changes active synergies;
- exact bonuses/scaling.

Synergies remain a later separate system and should not block Browser V1 foundation work.

## Runes and Aura

Aura mastery ownership is structurally locked: mastery belongs to the wielder per Rune identity, and compatible weapons manifest that mastery up to their category ceiling.

### Intentionally deferred until the game is playable

**Exact mastery progression/turnover for reaching L1 and L2 is intentionally undefined.**

The creator wants to see the actual game before deciding where progression should cross those thresholds. Do not invent a permanent progression curve now.

Prototype code may expose configurable/test mastery states so L0/L1/L2 behavior can be exercised without defining the final progression system.

### Rune effects

Exact L1/L2 Rune effects do not need to be fully authored before a prototype.

For the first Runes needed by implementation, agents may use simple clearly provisional/configurable effects that demonstrate the system, provided they:
- respect the locked L0/L1/L2 structure;
- do not create new canonical Rune identities;
- are not presented as final canon;
- can be replaced through data/config without rewriting the combat architecture.

Still genuinely open:
- remaining purified/corrupted Rune definitions;
- full Runestone color/visual registry;
- L3 Dreadform/Ascendant combat behavior;
- final Chaos/Order soul mechanics and consequences.

## Large-scale army layer

Deferred:
- when/if tactical Units aggregate into Formations/Squads;
- representation of mixed mundane/Runeforged/Artifact composition;
- casualty allocation inside formations;
- special Rune wielders inside aggregate formations.

Do not force formations to be homogeneous by Rune/loadout merely to simplify implementation.

## Game/product structure

Still open for later product design:
- exact core long-term session/progression loop beyond the first playable vertical slice;
- full PvE structure;
- PvP structure;
- matchmaking;
- seasons/ladder;
- rewards and long-term account progression;
- economy/trading between players.

These no longer block beginning development.

## Browser platform / architecture

No longer open for Browser V1:
- platform: browser first, desktop web + mobile PWA together;
- React + TypeScript + Vite;
- PixiJS for future combat rendering;
- ASP.NET Core .NET 10;
- EF Core + PostgreSQL;
- ASP.NET Core Identity + secure browser cookies;
- server-authoritative state/combat;
- GitHub Actions + Azure target;
- Steam deferred.

See `docs/architecture/TECH_STACK.md` and `docs/architecture/AUTH_SECURITY.md`.

Small implementation/provider choices such as the production transactional-email provider may remain configurable without reopening the architecture.

## Implementation plan

The design phase is no longer required to finish every future system before coding.

Follow `docs/implementation/BUILD_PLAN.md` one task at a time.

When the running game exposes a real design problem, return here and resolve that specific problem rather than trying to pre-balance the whole game on paper.
