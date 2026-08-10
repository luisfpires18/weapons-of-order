# Weapons of Order: Unit Canon

## 1. Unit types

There are two high-level unit types.

### Regular Unit
A recruitable troop/unit identity defined by the creator. Multiple copies may exist.

### Hero Unit
A unique named character. Heroes are unique. The creator defines every Hero.

Agents must never invent Units or Heroes to fill gameplay gaps.

## 2. Fixed tiers

Units and Heroes can have fixed 1-star, 2-star, or 3-star tiers.

These are fixed classification tiers, broadly analogous to common/rare/epic importance. They are NOT TFT-style upgrade stars and do not increase through duplicate copies.

Exact stat differences by tier remain balance work.

## 3. Unit identity vs combat class

The underlying Unit and its current combat class are separate.

A Unit is the persistent identity/chassis.

The current combat class/specialization is determined by its equipped loadout.

Pattern:
`Unit + loadout -> specialization`

The creator defines every specialization name and every loadout mapping. Do not infer or invent them.

## 4. No weapon proficiency restrictions

Units do not have weapon proficiency lists.

Any Unit can equip any weapon outside combat. This is intentional because weapon choice is a primary way the player changes a Unit's class.

Heroes also may equip any weapon.

Weapons and armor are chosen before combat. Equipment cannot be changed during battle.

## 5. Armor allowance

Weapons are unrestricted, but armor is restricted by the Unit's maximum armor class.

Armor classes:
- Light
- Medium
- Heavy

A max-Heavy unit can equip Light, Medium, or Heavy. A max-Medium unit can equip Light or Medium. A max-Light unit can equip only Light.

Exact Hero armor limits are creator-defined when their data is authored.

## 6. Rune-transformed classes

Some Runeforged weapons physically transform during basic Runeforging at L0 Dormant.

When that transformation changes the weapon into a Rune-family combat form, the Unit's combat class changes immediately from L0.

Explicitly discussed:
- Mystic -> Wizard from L0
- Animal -> Shapeshifter from L0

Other Rune-family class names remain creator-defined. Do not invent them.

A Mystic Staff wielder is already a Wizard-style class at L0. Do not leave them as an incompatible mundane class until L2.

The underlying Unit identity, kingdom, and fixed tier remain.

### Mounted
If a Rune transformation creates a class incompatible with Mounted, the Unit loses Mounted/dismounts for that class.

Mounted is currently a simple Unit/class state used by combat for slightly higher movement speed. Do not invent additional inherent Mounted rules unless approved.

## 7. Specializations and synergies

Specializations are creator-authored data.

Synergies are creator-authored data and live separately from Unit definitions.

Future synergies may reference specific classes, Mounted, kingdom counts, or other tags, but exact synergy rules are not yet defined.

Do not invent threshold bonuses or pair synergies yet.

## 8. Future large-scale representation

The current tactical autobattler uses individual Units/Heroes.

A future army-scale layer may represent thousands of soldiers through aggregate Formations/Squads.

Do not require such formations to be homogeneous by Rune/loadout. Future formation data may contain mixed equipment/Rune composition internally.

Do not let this future abstraction complicate the current individual-unit combat model.

## 9. Intentionally unresolved

Do not silently decide:
- exact kingdom rosters
- exact Hero roster
- exact specialization names/mappings
- exact Unit base-stat budgets by fixed tier
- Unit acquisition/recruitment
- Unit progression beyond fixed tier
- synergy rules
- final Rune-family combat class names beyond approved examples
- large-scale Formation/Squad simulation details
