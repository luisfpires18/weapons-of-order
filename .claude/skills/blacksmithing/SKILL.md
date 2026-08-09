---
name: blacksmithing
description: Weapons of Order blacksmithing, armor smithing, Runeforging, Enhanced/Artifact weapon progression, Rune Extraction, blood bonds, recoil, and forging risk. Use whenever work touches forging UI/gameplay, weapon or armor crafting, Runestones in the forge, Artifact creation, or blacksmith mastery.
---

# Blacksmithing

Read `references/blacksmith-canon.md` before designing or implementing this subsystem.

Also read `.claude/skills/runes-aura/references/rune-canon.md` when the task depends on Rune/Aura rules.

## Authority

Do not invent canon.

The creator's newest explicit decision overrides older drafts and implementation.

If a needed rule is unresolved, keep it configurable/WIP or ask. Do not settle it silently.

## Non-negotiable separations

- Common / Rare / Epic = physical craftsmanship quality.
- Blood Bond quality = Artifact binding/recoil quality.
- Enhanced / Artifact / Chaos / Order = weapon categories.
- L0 / L1 / L2 / L3 = Aura states.
- Rune = magical identity/power.
- Runestone = physical vessel.

## Implementation principles

- Normal blacksmithing is quick and repeatable: `choose -> pay -> Heat -> Strike -> result`.
- Ordinary armor stays mundane; no armor Runeforging.
- Runeforging is the dangerous magical progression system.
- A Runeforging operation may destroy the weapon; Aura mastery awakening does not use that forge-destruction roll.
- Artifact creation requires the intended wielder's blood.
- A clean Artifact blood bond has no recoil; imperfect surviving bonds may recoil.
- Rune Extraction is an advanced blacksmith technique; exact risk/cost remains tunable.
- Keep balance values data-driven when practical.
- Never add undefined Runes or conditioned weapon transformations to make implementation convenient.

Read the reference for the complete current rules and WIP boundaries.
