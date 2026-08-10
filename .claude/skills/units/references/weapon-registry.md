# Weapons of Order: Weapon Registry

This file is the canonical foundation for weapon types, wield slots, weapon combat metadata, dual-wield behavior, and Runeforged pair compatibility.

Creator decisions in newer discussions override older draft wording. Exact balance values that are explicitly marked tunable are not permanent canon.

## 1. Weapon taxonomy

Current weapon families and types:

### Edged Weapons
- Sword
- Dagger
- Axe
- Two-Edged Sword

### Blunt Weapons
- Mace
- Hammer
- Flail
- Club

### Pole Weapons
- Polearm
- Spear
- Glaive
- Lance
- Pike
- Scythe

### Defensive Weapons
- Shield
  - Round Shield
  - Kite Shield
  - Tower Shield
- Trident & Net

### Ranged Weapons
- Bow
- Crossbow
- Javelin
- Throwing-Shield
- Chakram
- Shuriken

### Fist Weapons
- Claw Gauntlets
- Pummelers

### Staff Weapons
- Staff
- Scepter
- Rod
- Quarterstaff

### Flexible Weapons
- Whip
- Three-Section Staff

Do not silently add new canonical weapon families or weapon types. New authored types require creator approval.

## 2. Universal two-slot wield model

Every Unit/Hero has exactly **2 weapon slots**, representing the two hands.

A weapon/loadout item is authored as consuming either:
- **1 slot**, or
- **2 slots**.

A 2-slot weapon occupies the entire weapon loadout.

Two compatible 1-slot weapons may be equipped together. The second slot is not a restricted RPG-style off-hand slot: it may contain any compatible 1-slot weapon.

Examples of valid mundane structures include:
- Sword + Sword
- Sword + Shield
- Axe + Mace
- Shield + Shield

There is no off-hand stat penalty.

When two 1-slot weapons are equipped, **both contribute 100% of their authored stats** to the Unit's final additive equipment totals.

### Explicit wield rules

- Bows are always 2-slot / two-handed.
- Crossbows may exist as a normal 2-slot crossbow or as 1-slot crossbows that can be dual-wielded.
- Trident & Net is a required two-item combination occupying both slots.
- Shields are both defensive and offensive weapons. They are not passive-only off-hand items and may be dual-wielded.

Exact slot cost for weapon types not explicitly fixed above remains authored weapon data rather than a guessed universal rule.

## 3. Weapon combat metadata v1

Keep initial weapon stats deliberately small and readable.

Current weapon metadata may include:
- Power
- Critical Chance
- Weight
- Range
- Slot Cost

Shields may additionally provide:
- Defense

Do not add large generic stat packages to weapons without a later design decision.

Weapon Power should start with low additive values. Exact Power/Crit budgets by weapon type remain balance work.

### Weight

Each weapon may be classified as:
- Light
- Medium
- Heavy

Weight participates in Attack Interval calculation together with the Unit's base interval and equipped armor weight/class.

Both weapons in a two-item loadout participate in the loadout's timing calculation. Exact formulas/modifiers remain tunable.

### Range

Current v1 defaults:
- ordinary melee weapon range: **1 hex**
- weapons in the **Ranged Weapons** family: **3 hexes**

A later explicitly authored weapon/Rune rule may override its default range.

Do not invent longer Bow/Crossbow/etc. ranges merely because a real-world weapon would reach farther.

## 4. Dual-wield attack rhythm

A two-item loadout attacks by alternating the equipped hands rather than resolving both weapons as one simultaneous hit.

Concept:
`left -> interval -> right -> interval -> left -> ...`

Each alternating hand attack is a real auto attack:
- it can crit independently;
- it grants the normal successful-auto Energy gain;
- it uses the Unit's final additive combat stats, including the full stat contribution from both equipped weapons.

Current v1 balance target/example:
- Light armor + dual Light swords can reach roughly **1.0 second between alternating attacks**.

That example is a tuning target, not a requirement that every dual-wield loadout attack every 1.0 second.

Exact armor/weapon interval modifiers and the minimum Attack Interval floor remain balance work.

## 5. Runeforged two-item compatibility

A Runeforged two-item loadout is treated as one coherent Rune weapon set.

Two equipped 1-slot Runeforged weapons must match on:
- **Rune identity**
- **weapon category/tier**

Their mundane weapon types do **not** need to match.

Valid examples:
- Enhanced Fire Sword + Enhanced Fire Shield
- Artifact Fire Axe + Artifact Fire Mace
- Artifact Earth Shield + Artifact Earth Shield

Invalid examples:
- Fire Sword + Earth Shield
- Enhanced Fire Sword + Artifact Fire Shield

Do not solve an invalid pair by lowering the stronger weapon's Aura ceiling. The pair is simply not equip-compatible.

A two-item Runeforged loadout should not mix one Runeforged 1-slot weapon with one mundane 1-slot weapon. Both items must form the matching Runeforged set.

All other item-specific restrictions still apply. For example, an Artifact must still be blood-bound to the wielder who is using it.

## 6. Paired Runeforging

When Runeforging two 1-slot weapons intended to be used together, the blacksmith may choose a **paired Runeforging operation** instead of processing the weapons independently.

The paired operation creates the two weapons as one intended Rune set with the same:
- Rune identity
- weapon category/tier

The two base weapon types may differ.

Example:
`Sword + Shield -> paired Fire Runeforging -> matching Fire Sword + Fire Shield`

Weapons may still be Runeforged separately. Separately created 1-slot Runeforged weapons can later be equipped together if they independently satisfy the same Rune + category compatibility rules.

Exact paired-forge failure/destruction semantics are not yet defined. Do not invent whether one failure destroys one item, both items, or changes Runeforging odds.

## 7. Aura mastery belongs to the wielder

Aura mastery is **not stored as independent mastery on each weapon**.

It belongs to the **wielder, per Rune identity**.

Conceptually:
`Wielder + Rune Identity -> Aura Mastery`

The equipped weapon category determines how much of that mastery can currently manifest:
- Enhanced -> maximum L1 Conduit
- Artifact -> maximum L2 Aspect
- Chaos/Order -> maximum L3 when those rules are eventually finalized

Changing to another compatible weapon of the same Rune does not require mastering that individual object again.

Example:
- wielder has Fire mastery sufficient for L2;
- Enhanced Fire weapon manifests at most L1;
- Artifact Fire weapon/set can manifest L2;
- replacing an Artifact Fire Shield with another compatible Artifact Fire 1-slot weapon does not reset Fire mastery.

### Aura meaning

- **L0 Dormant:** Rune is bound, but active Aura has not been mastered/manifested.
- **L1 Conduit:** the wielder masters and manifests the **weapon's Aura**.
- **L2 Aspect:** the weapon's Aura begins interacting with the **wielder's own Aura**, creating/empowering the wielder-side manifestation.

For a valid two-item Runeforged set, both weapons always manifest the same current Aura Level and behave as one Rune set for that wielder. There is no L1 left-hand + L2 right-hand state.

## 8. Conditioned Runeforged weapon forms

Current established L0 Runeforging transformations remain:

- Mystic -> Staff Weapon, excluding Quarterstaff
- Animal -> Claw Gauntlets + spiked vambrace
- Physical -> Pummeler or Three-Section Staff
- Material -> weapon must be outside Fist, Staff, and Flexible families

No forced L0 weapon form is currently established for Elemental, Nature, Technic, or Primal.

Do not invent one.

These rules must remain consistent with `.claude/skills/runes-aura/references/rune-canon.md`.

## 9. Intentionally unresolved

Do not silently decide:
- exact Power/Crit values for each weapon type
- exact Light/Medium/Heavy weight assignment for every weapon
- exact Attack Interval modifier from each weight/loadout combination
- minimum Attack Interval floor
- exact slot cost for every weapon type not explicitly fixed here
- specific range exceptions to the 1-hex melee / 3-hex Ranged-family defaults
- shield Block mechanics, if any
- paired-Runeforging failure/destruction behavior
- exact weapon-specific special attacks

Keep these as authored data or future balance decisions.