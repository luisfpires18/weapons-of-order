# Weapons of Order: Blacksmith and Runeforging Canon

This file records the current Blacksmith-system decisions agreed with the creator.

It has higher authority for this subsystem than older draft notes when they conflict.

## 1. Concept separation

There are four separate dimensions.

### Physical craftsmanship

Normal smithing quality:

- Common
- Rare
- Epic

There is no Legendary tier for ordinary blacksmith craftsmanship.

This quality describes how well the physical item was made. It is not a magical rarity and does not determine Aura Level.

### Weapon category

- Normal Weapon
- Enhanced Weapon
- Artifact Weapon
- Weapon of Chaos
- Weapon of Order

### Aura Level

- L0: Dormant
- L1: Conduit
- L2: Aspect
- L3: Dreadform for Chaos / Ascendant for Order

Weapon category determines the maximum Aura Level the weapon is capable of reaching.

### Rune identity

A Rune defines the magical identity/power of the Runeforged weapon.

Do not invent runes. Use the project's defined rune list.

---

## 2. Normal Blacksmithing

Normal weapons are created through ordinary forging.

They contain no Rune and have no Aura Level.

### Design goal

Blacksmithing happens frequently, so the interaction must remain fast and simple.

Core loop:

`Resources -> choose item -> Heat -> Strike -> result`

Do not turn normal forging into a long realistic smithing simulator.

### Resources

Use a small reusable resource vocabulary where possible:

- Metal
- Wood
- Leather

Exact costs are balance/data decisions and are not canonized here.

Different weapon types consume different combinations.

### Heat

Use a simple readable heat state:

- Cold
- Workable
- Ideal
- Too Hot / Burning

Good strikes are made around the Ideal region.

Bad temperature control reduces physical craftsmanship.

### Strike

Prefer one primary Hammer/Strike action rather than several complicated attack buttons.

A forge should require only a few meaningful strikes.

### Normal-forge failure

Routine forging should be forgiving because the player will do it often.

Poor execution normally results in lower craftsmanship rather than total failure.

Extreme mishandling, such as badly burning/ruining the workpiece, may ruin the forge.

A ruined forge may recover part of the materials as scrap/recoverable material.

Exact thresholds and recovery percentages remain balance decisions.

### Craftsmanship quality

Normal item quality is:

- Common
- Rare
- Epic

Quality is influenced by actual forging performance.

Do not model this as a completely disconnected random loot rarity roll.

Exact stat multipliers and target distribution are not canonized yet.

---

## 3. Weapon breadth

The Blacksmith system should support any ordinary weapon category allowed by the game's weapon catalogue.

Do not make separate minigames for every weapon.

Weapon-specific data can vary:

- resource cost
- difficulty
- base stats
- visual/template
- allowed wield configuration

The common forging loop should remain reusable.

---

## 4. Armor smithing

Armor is worth including, but it is intentionally a support system.

Armor remains mundane equipment.

There is no armor Runeforging, Enhanced Armor, Artifact Armor, Chaos Armor, or Order Armor.

This protects the identity of Material Runes, whose Aspect state can envelop the wielder in material-based living armor.

### Armor dimensions

Armor class:

- Light
- Medium
- Heavy

Core mundane materials:

- Leather
- Iron
- Steel

Style/specialization:

- Defensive
- Balanced
- Mobile

These are separate axes.

This prevents Steel from becoming a single final armor item that makes all armor decisions obsolete.

Example concept:

- Heavy Steel Defensive
- Heavy Steel Balanced
- Heavy Steel Mobile

They trade protection and weight/mobility differently.

### Armor craftsmanship

Armor uses the same:

- Common
- Rare
- Epic

craftsmanship quality as ordinary weapons.

It should reuse the same short Heat + Strike interaction rather than introducing another minigame.

Exact armor/weight formulas remain balance decisions.

---

## 5. Rune and Runestone

A Runestone and a Rune are different concepts.

### Runestone

The physical vessel/resource found in the world.

### Rune

The magical glyph/power contained by the Runestone.

The Rune changes the appearance of its Runestone.

Color therefore belongs primarily to the individual Rune's magical identity, not to its Rune Family.

Example principle:

- Fire can make its Runestone red/orange.
- Water can make its Runestone blue.

Both can still belong to the Elemental family.

Rune family may be presented through UI text, shape language, markings, or not encoded visually at all.

Do not force all runes in one family to share a generic family color.

### Runeforging visual fantasy

During Runeforging:

1. The Runestone is presented.
2. The Rune/glyph is extracted/released from it.
3. The Runestone is consumed, emptied, cracked, or otherwise loses the Rune.
4. The Rune is inscribed/bound into the weapon.
5. The weapon visibly carries the Rune.

The exact animation is an art/UX implementation detail.

---

## 5A. Conditioned weapon transformation during basic Runeforging

Some Rune families automatically reshape the weapon as part of the initial Runeforging operation.

This occurs immediately when the weapon becomes Runeforged at L0 Dormant.

It is NOT an L1 Conduit awakening effect and does not wait for weapon mastery.

Current established conditioned forms:

- Mystic Runes -> the weapon reshapes into a Staff Weapon, excluding Quarterstaff.
- Animal Runes -> the weapon reshapes into Claw Gauntlets with a spiked vambrace.
- Physical Runes -> the weapon reshapes into Pummelers or a Three-Section Staff.
- Material Runes -> the weapon reshapes into a weapon outside the Fist, Staff, and Flexible weapon categories.

For Rune families not explicitly given a conditioned weapon rule in the project's source material, do not invent one.

The transformation preserves the identity/history of the same weapon. It is not treated as discarding the weapon and creating a separate unrelated item.

## 6. Enhanced Weapons

An Enhanced Weapon is created through Runeforging with one Rune.

The Rune is present immediately after Runeforging.

### Aura capability

An Enhanced Weapon can exist at:

- L0 Dormant
- L1 Conduit

It cannot reach L2.

### L0 Dormant

The Rune is already present in the weapon but has not awakened into its Conduit power.

L0 is not a failed or unfinished forging state.

### L1 Conduit

Reached through mastery/use of the Runeforged weapon.

Awakening from L0 to L1 is not another dangerous forge operation.

There is no random weapon destruction merely because the weapon awakens.

The destruction risk applies during Runeforging operations.

---

## 7. Artifact Weapons

An Artifact Weapon is a higher Runeforging category.

Requirements:

- one Rune
- blood of the intended wielder
- successful Artifact-level Runeforging

Blood Infusion is mandatory for creating/upgrading to an Artifact.

It is not a separate generic technique applied casually to lower-tier weapons.

### Permanent blood bond

The Artifact becomes permanently bound to its intended wielder.

The blood bond is part of what defines an Artifact Weapon.

### Aura capability

An Artifact Weapon can ultimately reach:

- L0 Dormant
- L1 Conduit
- L2 Aspect

L2 is difficult to reach and requires mastery.

An Artifact is capable of Aspect; Artifact creation does not mean the wielder instantly has L2 mastery.

### Upgrading an existing weapon

An existing Enhanced Weapon can be taken further and become an Artifact Weapon.

The same physical weapon and Rune can therefore accumulate history rather than being replaced.

A highly skilled blacksmith is valuable because advanced Runeforging is dangerous.

---

## 8. Blood Bond quality and recoil

Do not reuse Common/Rare/Epic for the blood bond.

Physical craftsmanship and blood-binding quality are separate fields.

Recommended conceptual blood-bond states:

- Perfect
- Stable
- Unstable
- Fractured

The labels can be revisited, but the separation is canon.

### Perfect bond

No recoil damage.

### Imperfect bond

The Artifact remains functional but has a chance to recoil against its wielder.

Recoil is a permanent imperfection of that blood bond unless a future mechanic explicitly establishes a way to repair it.

### Recoil triggers

Recoil can occur on:

- ordinary/auto attacks
- rune-powered/special attacks

These use separate probabilities.

Special attacks can have a different, likely higher, recoil chance than ordinary attacks.

The exact percentages are balance values and are not canonized yet.

### Blacksmith value

A master Artifact blacksmith can create a clean or extremely safe blood bond.

A mediocre blacksmith can still produce a usable Artifact, but may leave it with dangerous recoil.

This creates meaningful player choice:

- train/master the technique
- pay an elite blacksmith
- risk a cheaper/less capable blacksmith and accept recoil

This is also an in-world reason Artifact Weapons are not casually available to everyone.

---

## 9. Runeforging failure and weapon destruction

Runeforging is dangerous at every weapon-category forging stage.

If the Runeforging process goes badly, the Rune can destroy the weapon.

This risk applies while performing Runeforging operations for:

- Enhanced creation
- Artifact creation/upgrade
- Chaos/Order creation/upgrade

It does not mean Aura awakening through mastery randomly destroys the weapon.

Exact base destruction chances are not canonized yet.

They should be tunable based on factors such as:

- Runeforging operation difficulty
- blacksmith/runeforger mastery
- relevant equipment/station quality if the game later supports it
- possibly physical weapon craftsmanship, if explicitly approved during balancing

Do not silently make Common/Rare/Epic control Runeforging survival unless the creator approves that relationship.

---

## 10. Rune Extraction

Rune Extraction is an exceptional high-level blacksmith technique.

A sufficiently skilled blacksmith can remove a Rune from an existing Runeforged weapon and return/rebind it to a Runestone so that the Rune can later be used on another weapon.

This makes rare Runes more persistent and creates another reason to seek master blacksmiths.

Exact extraction failure risk is not canonized yet.

Do not assume extraction is safe or destructive until that is explicitly balanced.

---

## 11. Weapons of Chaos and Order

This system is intentionally not fully finalized.

Current structural direction:

- Weapon of Chaos / Order is above Artifact.
- It involves two Rune components:
  - one normal Rune
  - Chaos or Order
- This is the exception to the ordinary one-Rune-per-weapon rule.
- Blood remains involved.
- Soul involvement is required at this level, but exact soul mechanics remain a separate advanced design topic.
- Existing long-lived weapons may continue upward rather than being discarded.
- A sufficiently capable blacksmith can perform higher Runeforging on an existing weapon.

### Aura capability

- Chaos can reach L3 Dreadform.
- Order can reach L3 Ascendant.

### Current WIP consequence

Artifact uses recoil as its defining imperfect-bond consequence.

For Chaos/Order, do not automatically reuse recoil.

Possible future directions include:

- Chaos corruption/consumption of the wielder
- Order purification/harmony/resonance consequences

These are NOT finalized.

Do not canonize a Chaos/Order soul consequence without creator approval.

---

## 12. Aura mastery vs forging

Keep these systems separate.

### Forging / Runeforging decides

- what category the weapon belongs to
- what Rune is inside it
- whether the weapon survives the operation
- blood bond quality for Artifact
- the maximum Aura Level the weapon is capable of reaching

### Weapon mastery decides

Whether the wielder actually awakens/uses the Aura Levels available to that weapon.

Current conceptual progression:

`L0 Dormant -> L1 Conduit -> L2 Aspect -> L3 Dreadform / Ascendant`

Ceilings:

- Enhanced: max L1
- Artifact: max L2
- Chaos/Order: max L3

Do not describe Enhanced as "L1" or Artifact as "L2" as if category and current Aura state are identical.

An Artifact can exist before its wielder has awakened Aspect.

---

## 13. Canon safety rules

When implementing or proposing changes:

- Do not invent new Runes.
- Do not rename existing Rune families without explicit approval.
- Do not create armor Runeforging.
- Do not add Legendary as normal blacksmith craftsmanship quality.
- Do not make Artifact blood optional.
- Do not make Blood Infusion a generic pre-Artifact technique.
- Do not make Aura awakening itself a random forge/destruction roll.
- Do not collapse physical craftsmanship and blood-bond quality into one rarity field.
- Do not finalize Chaos/Order soul consequences yet.
- Do not assume exact probabilities where none have been approved.

When numbers are needed for prototyping, isolate them as clearly labeled temporary balance values.

---

## 14. Still intentionally tunable

These are implementation/balance questions, not missing structural canon:

- normal forging quality thresholds
- Common/Rare/Epic stat bonuses
- exact material costs
- normal forge ruin threshold and salvage percentage
- armor protection/weight formulas
- blacksmith skill progression curve
- Runeforging destruction probabilities
- Artifact blood-bond outcome probabilities
- auto-attack recoil chance per bond quality
- special-attack recoil chance per bond quality
- Rune Extraction risk/cost
- fees/economy around hiring elite blacksmiths

Keep these data-driven wherever practical.
