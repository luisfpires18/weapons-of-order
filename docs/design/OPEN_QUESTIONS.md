# Weapons of Order: Open Questions

These are intentionally unresolved. Existing or historical code must not silently answer them.

Only resolve an item when the creator explicitly decides it.

## Combat battlefield

- Battlefield shape: grid, free movement, lanes, or another structure?
- Squad size?
- Deployment/formation rules?
- Exact meaning of Movement Speed?
- Collision, pathing, body-blocking, and movement around allies/enemies?
- How Mounted movement differs from unmounted movement?
- Range model and how melee/ranged Units acquire valid targets?
- Target-selection rules and retargeting behavior?

## Combat math

- Exact damage formula combining Unit Power, weapon, class, and attack coefficients?
- Exact Defense mitigation formula?
- Whether attacks use formal Physical/Magical damage types or a different taxonomy?
- Default critical multiplier?
- Exact Energy generation rules?
- Exact Heavy Attack coefficient/scaling?
- Which attacks can crit?
- Whether shields use Block chance/effectiveness and how it works?
- Whether Dodge becomes a class/effect mechanic and how it works?

## Units and classes

- Exact kingdom rosters. Creator-defined only.
- Exact Hero roster. Creator-defined only.
- Exact specialization/class names and required loadouts. Creator-defined only.
- Exact Unit base-stat budgets by fixed star tier.
- Which stats come primarily from Unit identity versus weapon/armor?
- Acquisition/recruitment rules?
- Progression beyond fixed star tier, if any?
- Final Rune-family combat class names beyond explicitly established examples such as Wizard and Shapeshifter?

## Synergies

- What produces a synergy: kingdom, specialization, weapon type, Mounted tag, Rune family, or combinations?
- Threshold model, if any?
- Whether changing equipment/class changes active synergies?
- Exact bonuses and scaling?

Synergies stay in separate content definitions from Unit definitions.

## Blacksmithing and equipment balance

- Exact resource costs?
- Common/Rare/Epic quality thresholds and stat effects?
- Armor protection/weight/mobility formulas?
- Runeforging destruction chances?
- Rune Extraction cost/risk?
- Blacksmith progression/mastery curve?
- Artifact blood-bond outcome probabilities and recoil chances?

## Runes and Aura

- Exact L1/L2 combat effects per Rune?
- Exact mastery progression to awaken Aura Levels?
- Remaining purified/corrupted Rune definitions?
- Full Runestone color/visual registry?
- L3 Dreadform/Ascendant combat behavior?
- Final Chaos/Order soul mechanics and consequences?

## Game structure

- Core session/run structure?
- PvE structure?
- PvP structure?
- Matchmaking?
- Seasons/ladder?
- Rewards and long-term account progression?
- Economy/trading between players?
- Persistence and backend architecture?
- Authentication/account requirements?

## Roadmap

Do not create a production roadmap from these unknowns.

Build the roadmap after the systems needed for the first playable loop are sufficiently defined.
