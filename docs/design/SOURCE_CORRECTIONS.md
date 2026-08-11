# Weapons of Order: Source Corrections

This file records how the original creator source files map into the current game-design canon.

It exists so older draft wording can remain useful without silently overriding decisions made later.

## Authority rule

When these older source files conflict with current project references, use the current project references:

- `rune_list.md` -> `.claude/skills/runes-aura/references/rune-registry.md`
- `aura_levels.md` -> `.claude/skills/runes-aura/references/rune-canon.md` and `aura-visual-reference.md`
- `runeforged_weapons.md` -> `.claude/skills/units/references/weapon-registry.md`, `.claude/skills/blacksmithing/references/blacksmith-canon.md`, and `.claude/skills/runes-aura/references/rune-canon.md`
- `weapons_of_chaos_and_order.md` -> `.claude/skills/runes-aura/references/chaos-order-weapon-roster.md` plus the current Rune registry/canon

Do not fix a conflict by inventing missing canon. Mark it WIP when the creator has not resolved it.

## `rune_list.md`

Most current Rune names and themes have already been migrated into `rune-registry.md`.

Current corrections/clarifications:

- Use `Necro`, not older `Necrosis`.
- Use `Meteorite`, not older `Metorite`.
- Shadow is a Mystic Rune and is distinct from the unique Primal Rune Dark.
- Crafted Chaos/Order do not function alone as an ordinary one-Rune weapon identity.
- Current Purified Rune list is intentionally incomplete. Do not complete the pattern automatically.
- Current recorded Purified Runes are Eclipse, Tempest, Aegis, Energy, Wood, Quartz, Steel and Ascendant.
- Older names/results not present in the current registry are not automatically canon merely because an older draft contained them.
- Animal Rune examples remain examples; the broad categories are the durable classification.
- Mythical Animal Runes are singular/unique.
- Material Rune rarity terminology is separate from Common/Rare/Epic physical blacksmith craftsmanship.

Regional Rune-distribution notes remain worldbuilding/balance draft material rather than hard spawn tables.

## `aura_levels.md`

The detailed visual/power prose remains valuable, but the old progression wording must be interpreted through the current structural canon.

Corrections:

- Enhanced / Artifact / Chaos / Order are **weapon categories**.
- L0 Dormant / L1 Conduit / L2 Aspect / L3 Dreadform or Ascendant are **Aura states**.
- Do not describe L1 as "an Enhanced weapon" or L2 as "an Artifact weapon" as if category and current state were the same field.
- Aura mastery belongs to the **wielder per Rune identity**, not independently to each weapon object.
- Weapon category limits how much of the wielder's mastery can manifest.
- L1 Conduit is primarily the **weapon's Aura** being mastered/manifested.
- L2 Aspect is where the weapon Aura begins interacting with the wielder's own Aura, creating the body/form manifestation.
- The 25/50/75/100 Aspect stages are useful descriptive lore/visual material but are **not currently locked as the game's progression UI or mastery thresholds**.
- Conditioned weapon transformations happen during basic Runeforging at L0, not when L1/L2 awakens.
- Therefore generic old "blade" wording must be interpreted through the canonical transformed weapon where relevant: Mystic Staff, Animal Claw Gauntlets, Physical Pummeler/Three-Section Staff, and Material restrictions.
- Material L1 remains concentrated on the weapon/material itself; Material L2 is where living material armor/body armament appears.
- Exact L1/L2 combat coefficients/effects remain configurable/prototype content until gameplay demonstrates what is needed.
- L3 gameplay and soul consequences remain deferred/WIP.

## `runeforged_weapons.md`

Useful weapon taxonomy and wield concepts have been migrated into `weapon-registry.md`.

Corrections:

- Every Unit/Hero has two weapon slots.
- 1-slot weapons may pair with any compatible 1-slot weapon; the second slot is not a restricted off-hand proficiency slot.
- Both equipped 1-slot weapons contribute 100% of their authored stats.
- Dual-wield attacks alternate hands rather than resolving as one combined simultaneous hit.
- Bows are 2-slot/two-handed.
- Crossbows may be authored as 2-slot or as 1-slot dual-wieldable versions.
- Trident + Net is a required two-slot pair.
- Shields are weapons and may be dual-wielded.
- A two-item Runeforged set must match **Rune identity + weapon magical category/tier**. Weapon types may differ.
- Mixed Rune identities are invalid as a Runeforged pair.
- Enhanced + Artifact cannot be paired merely by lowering the stronger weapon's manifested Aura.
- Two intended 1-slot weapons may be Runeforged together as a paired operation; separately forged weapons may still pair later if they satisfy the same compatibility rules.
- Aura mastery belongs to the wielder + Rune identity, so swapping to another compatible same-Rune weapon does not require remastering that individual object.
- Blood Infusion is now part of Artifact creation/upgrade and permanently binds the Artifact to its intended wielder.
- The older standalone Soul Infusion concept is not part of the current locked blacksmith foundation.
- Soul involvement is reserved for the Chaos/Order tier and its exact mechanism/consequences remain WIP.

## `weapons_of_chaos_and_order.md`

The named weapons and their creative concepts remain useful reference material, but several source inconsistencies must not leak back into canon.

Corrections:

- Victura uses **Necro** (Spirit + Chaos), not Necrosis.
- Vantashields uses **Meteorite** (Obsidian + Chaos), not the `Metorite` typo.
- Hail is derived from **Ice + Chaos**, not a separate Frost Rune.
- Current high-tier structure requires blood + soul involvement for Chaos/Order weapons; the older sentence that Order weapons "contain no soul" is superseded. The exact Order soul mechanic remains WIP and must not be invented.
- Dreadform and Ascendant are L3 Aura states, not separate ordinary weapon categories.

Still unresolved conflicts from that source:

- `Ruincoils` uses **Entropy (Circuit + Chaos)**, but Entropy is not currently defined in the canonical Corrupted Rune registry. Keep the weapon concept as draft/WIP; do not silently add Entropy.
- `Bulwarkers` uses **Protect (Fortitude + Order)**, but Protect is not currently in the canonical Purified Rune registry. Keep it draft/WIP unless explicitly approved.
- Source-specific Ascendant animal variants may be retained as weapon concept notes, but the canonical derived Rune currently permits Any Animal + Order.
- Several Order weapons/results are explicitly WIP and must remain incomplete.

## Preservation policy

Original source prose is valuable for later lore, VFX, animation and Rune-effect design.

When importing more of it into the game repository:

1. preserve the original concept;
2. route it through these corrections/current canon;
3. keep descriptive inspiration separate from exact combat numbers;
4. never restore a superseded rule simply because it appears in an older source file.
