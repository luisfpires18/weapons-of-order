---
name: combat
description: Weapons of Order autobattler battlefield, hex movement, collision, targeting, range, combat stats, damage math, Energy/Heavy/L1/L2 attack progression, deployment limits, reserves, and server-authoritative deterministic simulation. Use whenever work touches battle rules, pathing, targeting, replay architecture, reinforcement flow, or combat math.
---

# Combat

Read `references/combat-canon.md` before designing or implementing battle behavior.

Also read:
- `.claude/skills/units/references/unit-canon.md` when Unit/Hero identity or Mounted matters.
- `.claude/skills/runes-aura/references/rune-canon.md` when Aura changes attacks/classes.

## Core battlefield rules

- Combat is automatic once battle begins.
- No equipment changes or manual skill casting during battle.
- Battlefield is an 8x7 offset hex grid.
- Each side deploys within its own 4x7 half.
- One unit per hex; occupied hexes are impassable.
- Default target is nearest valid enemy by hex distance.
- Melee requires a reachable adjacent attack position.
- Ranged units do not need adjacent space and may attack over frontliners.
- If a target leaves range, pursue one hex; if still out of range, retarget.
- 8 active units and 16 total army slots are current v1 tunable values.
- Reserves are ordered before combat, have preferred rear-row entry hexes, and enter after a short configurable delay when active slots open.
- A blocked reserve waits; another reserve may enter if it has an open active slot and unblocked assigned entry.

## Universal combat stats

Only:
- HP
- Power
- Defense
- Attack Interval
- Critical Chance
- Movement Speed

Final stats are additive from Unit base stats plus approved weapon/armor modifiers.

Defense is difficult to obtain and comes heavily from armor.

Mounted units are slightly faster than non-mounted units for now.

## Combat math v1

Current tunable baseline:
- 1 Power = 5 raw auto damage.
- Heavy Attack = 2.5x auto raw damage.
- Critical = 2x raw attack damage.
- Defense reduction = `Defense / (Defense + 100)`.
- Calculation order: Power -> attack coefficient -> crit -> Defense -> round -> minimum 1 damage.
- No Physical/Magical split.

Attack Interval starts from the Unit's base value and is modified by armor class/weight and weapon weight/handling. Exact modifiers and the minimum interval floor remain tunable/unresolved.

Armor slots currently used for additive item stats:
- Head
- Shoulders
- Chest
- Gloves
- Legs
- Boots

## Energy

One bar only: `0..100`.

Current v1 baseline:
- successful auto attack grants +10 Energy
- no extra Energy from crits, being hit, or movement
- no overflow above 100
- at 100, Heavy/Special triggers and Energy resets to 0

At 100:
- normal/L0 -> Heavy Attack
- L1 -> Rune-transformed Heavy/Special
- L2 -> Rune-infused autos plus a stronger 100-Energy Rune special

Autos and ordinary Heavy attacks can crit. Rune-specific L1/L2 effects/coefficients remain creator-authored future rules.

L3 combat behavior is deferred.

## Architecture

- Server is authoritative.
- Simulation is deterministic from the authoritative battle snapshot/seed.
- The server may resolve faster than real time while the client progressively reveals the event timeline.
- For an MVP, returning the full battle log at once is acceptable.

## Do not invent

Keep these unresolved unless the creator explicitly decides them:
- exact Unit/equipment stat budgets
- exact armor/weapon Attack Interval modifiers
- minimum Attack Interval floor
- exact weapon ranges by weapon type
- exact Rune-specific L1/L2 combat effects
- special targeting overrides
- deterministic path/target tie-breaks
- exact reinforcement delay
- timeout/overtime/stalemate behavior
- shield Block or Dodge mechanics
- exact progressive-delivery/network implementation
