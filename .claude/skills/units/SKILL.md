---
name: units
description: Weapons of Order Unit and Hero identity, fixed star tiers, equipment-defined classes, armor limits, Mounted state, and creator-authored roster/class rules. Use when designing or implementing Units, Heroes, classes/specializations, equipment eligibility, armor allowance, or roster data.
---

# Units

Read `references/unit-canon.md` before designing or implementing this subsystem.

Read `.claude/skills/combat/references/combat-canon.md` when the task depends on battlefield behavior or combat stats.

Read `.claude/skills/runes-aura/references/rune-canon.md` when Rune transformation/Aura changes a Unit's combat class.

## Creator authority

Never invent canonical:
- Unit names
- Hero names
- specialization/class names
- kingdom rosters
- specialization/loadout mappings
- synergies

If required content has not been defined by the creator, leave it unresolved or ask.

## Core model

- Regular Units may have multiple copies.
- Heroes are unique.
- 1/2/3 stars are fixed tiers, not upgrade stars.
- There are no weapon proficiency restrictions.
- Any Unit or Hero may equip any weapon outside combat.
- Equipment cannot change during battle.
- Mundane loadout determines the current combat specialization/class.
- Each Unit has a maximum armor class: Light, Medium, or Heavy.
- Rune-conditioned weapon transformations can replace the current class from L0.
- If the transformed class is incompatible with Mounted, remove Mounted.
- Underlying Unit/Hero identity, kingdom, and fixed tier remain.

## Data policy

Exact kingdom rosters, Hero definitions, class names, specialization mappings, and synergies should be creator-authored content/data rather than inferred by gameplay code.

Do not reintroduce weapon proficiency lists to solve a class-design problem.
