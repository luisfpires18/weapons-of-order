# Weapons of Order: Units & Combat Foundations Canon

## 1. Unit types

There are two high-level unit types.

### Regular Unit
A recruitable troop/unit identity defined by the creator. Multiple copies may exist.

### Hero Unit
A unique named character. Heroes are unique. The creator defines every Hero.

Claude/agents must never invent Units or Heroes to fill gameplay gaps.

## 2. Fixed tiers

Units and Heroes can have fixed 1-star, 2-star, or 3-star tiers.

These are fixed classification tiers, broadly analogous to common/rare/epic importance. They are NOT TFT-style upgrade stars and do not increase through duplicate copies.

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

## 7. Specializations and synergies

Specializations are creator-authored data.

Synergies are creator-authored data and live separately from Unit definitions.

Future synergies may reference specific classes, Mounted, kingdom counts, or other tags, but exact synergy rules are not yet defined.

Do not invent 2/4/6 bonuses or pair synergies yet.

## 8. Core combat stats

Universal stats are deliberately limited to:

### HP
Maximum/current health.

### Power
Single offensive scaling stat. Do not split into Attack Power and Special Power.

Physical-looking attacks and Rune-powered attacks can both scale from Power using different formulas.

### Defense
Single general defensive stat for now.

Defense is intentionally difficult to obtain and comes heavily from equipped armor.

Do not create separate universal Armor and Magic Resistance stats unless explicitly approved later.

Exact mitigation formula is not yet canonized.

### Attack Interval
Actual time between automatic attacks. Lower values attack faster.

Examples discussed:
- 3.0 seconds = slow
- 0.5 seconds = extremely fast

The UI may show a filling attack timer while replaying predetermined combat events.

### Critical Chance
Universal chance for a critical attack.

Critical Damage is not currently a separate universal stat.

### Movement Speed
Universal movement stat.

Its exact battlefield/grid/pathing interpretation has not yet been designed. Do not invent it.

## 9. Non-core mechanics

Do not currently add these as universal stats:
- Dodge
- Block
- Critical Damage
- Armor Penetration
- Magic Penetration
- Attack Power
- Special Power
- Armor
- Magic Resistance

They may later exist as shield/equipment mechanics, Rune effects, class mechanics, or temporary effects.

## 10. Energy

There is one combat resource only.

Energy range: 0 to 100.

Exact Energy generation is not yet locked.

### Normal weapon / L0 Dormant
At 100 Energy, perform a Heavy Attack.

### L1 Conduit
At 100 Energy, the Rune transforms the ordinary Heavy Attack into a Rune-powered Heavy/Special Attack.

Example concept:
Normal sword Heavy Slash -> Fire Conduit Heavy Fire Slash.

Normal auto attacks are not generally Rune-infused yet at L1.

### L2 Aspect
At L2, Rune power also transforms the normal attack pattern.

- Auto attacks become Rune-infused/Rune-powered.
- The 100-Energy attack remains a larger/stronger Rune special.

Example Fire pattern:
`Fire auto -> Fire auto -> Fire auto -> 100 Energy -> stronger Fire special`

### L3
Combat behavior intentionally remains undefined.

Do not invent it.

## 11. Damage model

Physical and magical/Rune concepts may exist as attack properties, but they do not currently require separate offensive or defensive core stats.

Exact damage-type framework is unresolved.

## 12. Battle architecture

Combat is autobattler-style.

The battle outcome is resolved into deterministic logs/state transitions and then replayed/presented visually.

The player does not change equipment or manually cast skills mid-battle under the current direction.

## 13. Future large-scale war

A future war layer may represent thousands of soldiers, but the current tactical autobattler should not be designed around simulating 10,000 individual entities.

Stacks/formations/aggregate armies can be designed separately later.

## 14. Intentionally unresolved

Do not silently decide:
- exact kingdom rosters
- exact Hero roster
- exact specialization names/mappings
- kingdom synergies
- specialization synergies
- formation/grid size
- squad size
- targeting rules
- movement rules
- range rules
- damage formula
- Defense mitigation formula
- crit multiplier
- Energy generation
- Heavy Attack scaling
- exact Rune L1/L2 combat effects
- unit acquisition/progression beyond fixed tier
- large-scale war representation
