---
name: units-combat
description: Weapons of Order Units, Hero Units, fixed star tiers, equipment-defined classes, armor limits, Mounted transformations, core combat stats, Energy, Heavy Attacks, and L1/L2 attack progression. Use for foundational unit/combat design or implementation.
---

# Units & Combat Foundations

Read `references/units-combat-canon.md` before designing or implementing this subsystem.

Read `.claude/skills/runes-aura/references/rune-canon.md` as well when Rune transformation/Aura affects the Unit.

## Creator-defined content

Never invent canonical:
- Unit names
- Hero names
- specialization/class names
- kingdom rosters
- specialization mappings
- synergies

## Core model

- Regular Units can have multiple copies.
- Heroes are unique.
- 1/2/3 stars are fixed tiers, not upgrade stars.
- There are no weapon-proficiency restrictions.
- Any Unit/Hero may equip any weapon outside combat.
- Equipment cannot change during battle.
- Loadout determines current mundane combat specialization/class.
- Each Unit has a maximum armor class: Light, Medium, or Heavy.
- Rune-conditioned transformations can replace the combat class from L0.
- Incompatible transformations remove Mounted.

## Universal combat stats

Only:
- HP
- Power
- Defense
- Attack Interval
- Critical Chance
- Movement Speed

Defense is difficult to obtain and comes heavily from armor.

Do not introduce universal Attack Power, Special Power, Armor, Magic Resistance, Dodge, Block, Crit Damage, or Penetration without approval.

## Energy

One bar only: `0..100`.

At 100:
- normal/L0 -> Heavy Attack
- L1 -> Rune-transformed Heavy/Special
- L2 -> Rune-infused auto attacks and a stronger 100-Energy Rune special

L3 combat behavior is deferred.

Battlefield shape, movement, targeting, range, damage formula, Defense formula, Energy generation, and squad size are unresolved. Check `docs/design/OPEN_QUESTIONS.md` instead of inheriting old implementation assumptions.
