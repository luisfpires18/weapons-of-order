---
name: combat
description: Weapons of Order autobattler battlefield, hex movement, collision, targeting, range, combat stats, Energy/Heavy/L1/L2 attack progression, deployment limits, reserves, and server-authoritative deterministic simulation. Use whenever work touches battle rules, pathing, targeting, replay architecture, reinforcement flow, or combat math.
---

# Combat

Read `references/combat-canon.md` before designing or implementing battle behavior.

Also read:
- `.claude/skills/units/references/unit-canon.md` when Unit/Hero identity or Mounted matters.
- `.claude/skills/runes-aura/references/rune-canon.md` when Aura changes attacks/classes.

## Core rules

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
- Server is authoritative.
- Simulation is deterministic from the authoritative battle snapshot/seed.
- The server may resolve faster than real time while the client progressively reveals the event timeline.

## Universal combat stats

Only:
- HP
- Power
- Defense
- Attack Interval
- Critical Chance
- Movement Speed

Defense is difficult to obtain and comes heavily from armor.

Mounted units are slightly faster than non-mounted units for now. Do not invent additional movement-speed tiers or item/trait movement rules unless approved.

## Energy

One bar only: `0..100`.

At 100:
- normal/L0 -> Heavy Attack
- L1 -> Rune-transformed Heavy/Special
- L2 -> Rune-infused autos plus a stronger 100-Energy Rune special

L3 combat behavior is deferred.

## Do not invent

Keep these unresolved unless the creator explicitly decides them:
- exact damage formula
- Defense mitigation formula
- crit multiplier
- Energy generation amounts/rules
- exact movement timing values
- exact weapon ranges by weapon type
- special targeting overrides
- timeout/overtime
- exact Rune L1/L2 combat effects
- win-condition edge cases
- exact progressive-delivery/network implementation
