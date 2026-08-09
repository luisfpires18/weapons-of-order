# Weapons of Order: Game Vision

Status: **Project-wide direction**

This document contains broad facts that apply across multiple systems. Detailed subsystem rules live in project skills.

## Core fantasy

The player builds power through **forging, equipment, Runeforging, Units, Hero Units, and battle preparation**.

Forging and Runes are not secondary crafting systems. They are central to the identity and progression of the game.

## Combat direction

Combat is an **autobattler**.

The player prepares Units, Heroes, weapons, armor, and positioning before combat. The battle itself is automatically resolved under game rules and presented through a deterministic battle log/state-transition style playback.

Current rule:
- no equipment changes during combat;
- no manual skill casting that changes an already-resolved outcome.

The exact battlefield, squad size, targeting, movement, range, and formulas are still being designed.

## Units and equipment

There are:
- Regular Units
- Hero Units

Regular Units may have multiple copies. Heroes are unique.

Weapon choice is intentionally flexible: Units do not have weapon-proficiency restrictions. Equipped weapons/loadouts determine their current combat specialization/class.

Armor is restricted separately through each Unit's maximum armor class: Light, Medium, or Heavy.

Rune-conditioned weapon transformations can change the Unit's combat class from L0. A transformation can remove incompatible states such as Mounted.

All roster names, Hero names, specialization names, and kingdom-specific content are creator-defined.

## Forging direction

Normal blacksmithing is a frequent, compact interaction rather than a long simulation.

Ordinary physical craftsmanship uses:
- Common
- Rare
- Epic

Armor remains mundane. Armor is not Runeforged.

Runeforging is the dangerous magical progression system and can destroy the weapon during forging operations.

## Rune and Aura direction

Runestone = physical vessel.

Rune = magical identity/power contained by the Runestone.

Weapon category and Aura Level are separate.

Current category ceilings:
- Enhanced Weapon -> up to L1 Conduit
- Artifact Weapon -> up to L2 Aspect
- Weapon of Chaos / Order -> up to L3 Dreadform / Ascendant

Mastery/use awakens Aura Levels available to the weapon. Aura awakening is not itself another weapon-destruction forge roll.

L3 gameplay remains intentionally deferred.

## Combat-stat direction

Current universal core stats:
- HP
- Power
- Defense
- Attack Interval
- Critical Chance
- Movement Speed

There is one combat resource:
- Energy, 0 to 100

At 100 Energy:
- normal/L0 -> Heavy Attack
- L1 -> Rune-transformed Heavy/Special Attack
- L2 -> Rune-infused auto attacks, plus a stronger Rune special at 100 Energy

Defense is intentionally hard to obtain and comes heavily from equipped armor.

## Synergies

Synergies are intended as a future pre-battle/team-building layer, potentially using threshold-style bonuses, but their exact rules are not currently locked.

Synergy definitions must live separately from Unit definitions.

Do not invent synergy thresholds or bonuses yet.

## Scale

Start with a small, understandable tactical battle system.

A future larger war layer may represent very large armies, but the current autobattler should not be designed around simulating thousands of individual entities.

If large-scale war is introduced later, formations/stacks/aggregate armies can be a separate system.

## Visual direction

The existing title/landing screen is the approved starting visual baseline:
- dark medieval-fantasy atmosphere
- forge/fire/rune imagery
- clean modern readability layered over the fantasy art

Faction colors should be contextual rather than globally forcing one kingdom's palette onto the whole game.

Arkazia's canonical faction colors are red and black when Arkazia is being represented as a faction.
