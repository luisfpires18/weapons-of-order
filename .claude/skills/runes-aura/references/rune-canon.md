# Weapons of Order: Rune & Aura Canon

This file records current decisions from the creator and overrides conflicting older draft wording.

## 1. Rune taxonomy

A **Rune Family** is a classification.

A **Rune** is the actual magical identity/power.

Examples:
- Elemental = family
- Fire = Rune
- Water = Rune

Do not call every Elemental Rune simply an "Elemental Rune" in a way that erases the actual Rune identity.

A **Runestone** is the physical vessel in which a naturally occurring Rune is found before Runeforging.

The Rune's energy permeates the stone. Therefore the stone's magical color/appearance follows the individual Rune, not one shared family color.

Current explicit examples:
- Fire Runestone: red/orange identity is appropriate.
- Water Runestone: blue identity is appropriate.

Do not invent a complete color chart without creator approval.

## 2. Rune origins and derivation classes

### Natural/base Runes

Natural Runes are found in the world as Runestones.

Current natural/base families include:
- Primal
- Material
- Animal
- Elemental
- Nature
- Physical
- Mystic
- Technic

Technic is a late-game/lore family in the current draft.

### Crafted Runes

Chaos and Order are exceptional crafted principles/runes created through forge reactions/experiments.

They do not function alone as an ordinary one-Rune weapon identity.

Their exact creation and soul mechanics remain a higher-tier WIP area.

### Derived Elemental Runes

Elemental fusion occurs during Runeforging.

Current defined results:
- Fire + Water -> Steam
- Wind + Earth -> Sand
- Water + Earth -> Mud
- Fire + Wind -> Lightning
- Water + Wind -> Ice
- Fire + Earth -> Lava

Do not invent additional Elemental fusion outcomes without approval.

The resulting fused Elemental is treated as the weapon's Rune identity rather than casually counting it as multiple independent ordinary Rune identities.

### Corrupted Runes

A normal Rune infused with Chaos can become a Corrupted Rune.

Use only currently defined results from the registry/source.

### Purified Runes

A normal Rune infused with Order can become a Purified Rune.

This list is explicitly incomplete/WIP. Do not fill missing names, domains, or effects.

## 3. One-Rune rule

Ordinary Runeforged weapons use one Rune identity.

Elemental fusion produces a fused Rune identity during Runeforging.

Weapons of Chaos and Order are the exceptional high-tier case involving:
- one normal Rune identity
- plus Chaos or Order

Do not generalize this exception into ordinary multi-Rune weapons.

## 4. Rune uniqueness and availability

### Primal
- Light: unique, one.
- Dark: unique, one.

### Mystic
The current draft defines the Mystic Runes as currently unique, one of each.

There are nine:
- Mind
- Time
- Space
- Sound
- Gravity
- Sigil
- Barrier
- Shadow
- Pulse

### Animal
Animal is not represented as a small closed list of named species in the draft.

It has fixed broad categories:
- Beast
- Avian
- Scale
- Aquatic
- Swarm
- Mythical

Ordinary animal examples are examples, not a declaration that only those species can exist.

Mythical creature Runes are singular/unique.

Do not invent a new Mythical Rune as canon without creator approval.

### Material
Material Runes have their own rune-world rarity labels in the draft.

These rarity labels are NOT blacksmith craftsmanship quality.

For example, Diamond being described as Legendary as a Rune rarity does not restore a Legendary tier to ordinary Common/Rare/Epic weapon craftsmanship.

### Technic
Technic Runes are late-game/lore content in the current draft and should not be introduced early unless the current project plan explicitly calls for them.

## 5. Exact canonical registry spelling

For Rune data, prefer the spellings in `rune-registry.md`.

Known older-source naming mismatches include:
- `Necro` in the Rune list vs `Necrosis` in an older weapon description.
- `Meteorite` in the Rune list vs `Metorite` typo in an older weapon description.

Use:
- Necro
- Meteorite

unless the creator later renames them.

## 6. Runestone visual identity

The Rune spreads its visual identity into the Runestone.

Color is not the family identifier.

The UI may still communicate family using:
- label/text
- iconography
- shape language
- framing
- metadata

but none of these are required to force every Rune in a family to share one color.

## 7. Basic Runeforging and weapon reshaping

The weapon can physically transform at the first/basic Runeforging operation.

This occurs at **L0 Dormant**.

It is not caused by L1 Conduit.

Current forced transformations:

### Mystic
Automatically reshapes into a Staff Weapon.

Quarterstaff is excluded from the Mystic staff form because it is treated as melee.

### Animal
Automatically reshapes into Claw Gauntlets with a spiked vambrace.

### Physical
Automatically reshapes into:
- Pummeler, or
- Three-Section Staff

The exact choice may depend on game data/design. Do not invent a new Physical weapon class.

### Material
Must reshape/remain in a weapon category outside:
- Fist
- Staff
- Flexible

### Other families
No forced L0 weapon category is currently established for Elemental, Nature, Technic, or Primal.

Do not invent one.

### Combat-class consequence

Conditioned Runeforging changes the wielder's current combat class immediately from L0.

A Mystic Staff wielder is already a Wizard-style class at L0. L1 and L2 deepen that Rune class; they do not create it for the first time.

The underlying Unit/Hero identity, kingdom, and fixed tier remain.

If the transformed class is incompatible with being mounted, the Unit loses `Mounted` / dismounts for that class.

Final Rune-class names beyond explicitly established examples remain creator-defined.

## 8. Weapon category vs Aura Level

These are separate systems.

### Normal Weapon
Ordinary forged weapon.
- No Rune
- No Aura Level

### Enhanced Weapon
Created by Runeforging with one Rune.
- Can be L0 Dormant
- Can awaken L1 Conduit
- Cannot reach L2

### Artifact Weapon
Runeforged with one Rune plus mandatory blood of the intended wielder.
- Permanently blood-bound
- Can exist at L0 or L1 before sufficient mastery
- Can awaken L2 Aspect
- Cannot reach L3

### Weapon of Chaos / Weapon of Order
Higher Runeforged category.
- normal Rune + Chaos/Order
- blood involved
- soul involvement exists, exact higher-tier mechanism remains WIP
- can reach L3

Chaos:
- L3 = Dreadform

Order:
- L3 = Ascendant

## 9. Aura progression and mastery

The Aura ladder is:

### L0 Dormant
The Rune is already bound into the weapon.
Its symbols are present/faint.
No active Rune power is being channeled yet.

L0 is a valid Runeforged state, not a failed forge.

### L1 Conduit
Reached through mastery/use.

The weapon channels the Rune's active power.

Enhanced Weapons can reach this level.

### L2 Aspect
Only Artifact-or-higher weapons can reach this level.

Requires the Artifact blood-bound category first, then sufficient mastery to awaken Aspect.

Aspect transforms the wielder according to the Rune's nature.

The detailed source uses staged 25/50/75/100% manifestation concepts. Treat those as current draft detail, not necessarily exact game progression UI unless adopted by the current game design.

### L3
Only Chaos/Order weapons can reach it.

- Chaos -> Dreadform
- Order -> Ascendant

Exact soul consequences and Order/Chaos gameplay costs remain WIP.

## 10. Forging risk vs Aura awakening

Runeforging operations can go badly and destroy the weapon.

Aura awakening through mastery does NOT itself randomly destroy the weapon.

Do not apply Runeforging failure chance to:
- L0 -> L1 mastery awakening
- L1 -> L2 mastery awakening
- later mastery progression

The dangerous roll/process belongs to creating/upgrading the weapon category through Runeforging.

## 11. Material Rune distinction

This is especially important.

At L1 Conduit, the Material Rune's effect is concentrated on the weapon's applicable material/metal portions.

The draft explicitly rejects body plating or unrelated wielder-side material effects at L1.

At L2 Aspect, the Material Rune can manifest living material armor around the wielder and merge the weapon into the armament form.

This is one reason ordinary armor is not Runeforged.

Do not move the L2 body-armament fantasy down into L1.

## 12. Conditioned family behavior and Aura prose

The detailed Aura draft was written across multiple iterations.

If it describes an Animal or Mystic weapon using generic "blade" language that conflicts with the L0 conditioned transformation, keep the effect but apply it to the canonical transformed weapon.

Examples:
- Animal effects manifest through the Claw Gauntlets/animal form.
- Mystic effects manifest through the Staff.
- Physical effects manifest through Pummelers or Three-Section Staff.

The conditioned form does not disappear merely because L1 is activated.

At Aspect, some forms can merge/fuse into the wielder as described by the Aura draft.

## 13. Detailed family themes

Use the source draft for detailed visuals and powers.

High-level identities:

### Elemental
Direct embodiment/channeling of physical elements and defined fused elements.

### Nature
Life, Decay, Poison, Spirit.

### Physical
Force, Fury, Fortitude, Flow.
Martial/aura-body expression.

### Material
Transforms the weapon at Conduit and can become living armament at Aspect.

### Animal
Animal/species embodiment and shapeshifting.
Mythical forms are unique.

### Mystic
Wizard-like manifestation with unique Runes and Staff conditioning.

### Technic
Science/technology/android-style manifestation; late-game introduction.

### Primal
Light and Dark; singular, extremely rare.

Do not collapse these families into generic elemental damage types.

## 14. WIP boundaries

Do not canonize without approval:
- missing Purified Rune names/effects
- additional Corrupted Rune results
- additional Elemental fusion recipes
- exact colors for every Rune
- exact Rune drop rates
- exact regional spawn tables
- exact combat coefficients
- exact Aura mastery thresholds
- exact 25/50/75/100 Aspect progression implementation
- final Chaos corruption mechanic
- final Order purification/resonance mechanic
- final soul-entrapment details
- new Mythical Runes
