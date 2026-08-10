# Weapons of Order: Open Questions

These are intentionally unresolved. Existing or historical code must not silently answer them.

Only resolve an item when the creator explicitly decides it.

## Combat math and equipment tuning

Combat Math v1 is structurally locked in `.claude/skills/combat/`.

Weapon/loadout structure is locked in `.claude/skills/units/references/weapon-registry.md`.

Still unresolved:
- Exact Unit base-stat budgets by fixed star tier?
- Exact weapon Power/Crit budgets?
- Exact Light/Medium/Heavy weight assignment for every weapon?
- Exact armor stat budgets by slot/class?
- Exact Light/Medium/Heavy armor Attack Interval modifiers?
- Exact weapon-weight/loadout Attack Interval modifiers?
- Minimum Attack Interval floor?
- Specific weapon range exceptions beyond the 1-hex melee / 3-hex Ranged-family defaults?
- Whether shields use Block chance/effectiveness and how it works?
- Whether Dodge becomes a class/effect mechanic and how it works?

Current Power scale, Defense constant, Energy gain, Heavy coefficient, and Crit multiplier are v1 tunable balance values rather than permanent sacred numbers.

## Combat rules still open

Targeting foundation is locked:
- closest valid enemy first;
- on equal distance, prefer lower final Defense / less-armored target;
- distance always overrides the armor preference;
- exact-equal candidates require no further authored gameplay priority for v1.

Equal shortest paths likewise require no additional gameplay preference; deterministic pathfinder ordering is implementation detail.

Still unresolved:
- Exact movement timing values for Mounted vs non-Mounted Units?
- Exact special targeting overrides such as future Assassin-style behavior?
- Exact reinforcement entry delay?
- Timeout/overtime/stalemate behavior when living Units or reserves cannot make progress?
- Exact progressive event-delivery/network protocol for competitive multiplayer?

Battle-end foundation is locked:
- defeat occurs only when every active Unit and every reserve Unit is dead;
- a living reserve blocked from entering is not defeated and prevents army defeat;
- simultaneous elimination of both armies is a Draw.

## Units and classes

- Exact kingdom rosters. Creator-defined only.
- Exact Hero roster. Creator-defined only.
- Exact specialization/class names and required loadouts. Creator-defined only.
- Acquisition/recruitment rules?
- Progression beyond fixed star tier, if any?
- Final Rune-family combat class names beyond explicitly established examples such as Wizard and Shapeshifter?

## Weapon crafting / paired Runeforging

- Exact slot cost for weapon types not explicitly fixed by the weapon registry?
- Exact failure/destruction behavior when two weapons are Runeforged as one paired operation?
- Whether paired Runeforging changes success odds/cost compared with two independent operations?
- Exact weapon-specific special attacks, if any?

## Synergies

- What produces a synergy: kingdom, specialization, weapon type, Mounted tag, Rune family, or combinations?
- Threshold model, if any?
- Whether changing equipment/class changes active synergies?
- Exact bonuses and scaling?

Synergies stay in separate content definitions from Unit definitions.

## Blacksmithing and equipment balance

- Exact resource costs?
- Common/Rare/Epic quality thresholds and stat effects?
- Armor protection/weight/mobility balance values?
- Runeforging destruction chances?
- Rune Extraction cost/risk?
- Blacksmith progression/mastery curve?
- Artifact blood-bond outcome probabilities and recoil chances?

## Runes and Aura

Aura mastery ownership is now structurally locked: mastery belongs to the wielder per Rune identity, and compatible weapons manifest that mastery up to their category ceiling.

Still unresolved:
- Exact L1/L2 combat effects per Rune?
- Exact mastery progression/thresholds to awaken Aura Levels?
- Remaining purified/corrupted Rune definitions?
- Full Runestone color/visual registry?
- L3 Dreadform/Ascendant combat behavior?
- Final Chaos/Order soul mechanics and consequences?

## Large-scale army layer

- When/if individual tactical Units are aggregated into Formations/Squads?
- How mixed mundane/Runeforged/Artifact weapon composition is represented inside a Formation?
- How casualties choose which internal cohort/equipment is lost?
- How special Rune wielders behave inside aggregated formations?

Do not force formations to be homogeneous by Rune/loadout merely to simplify implementation.

## Game structure

- Core session/run structure?
- PvE structure?
- PvP structure?
- Matchmaking?
- Seasons/ladder?
- Rewards and long-term account progression?
- Economy/trading between players?
- Persistence and backend architecture?
- Authentication/account requirements?

## Roadmap

Do not create a production roadmap from these unknowns.

Build the roadmap after the systems needed for the first playable loop are sufficiently defined.
