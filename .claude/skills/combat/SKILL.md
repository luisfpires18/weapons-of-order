---
name: combat
description: Weapons of Order autobattler battlefield, hex movement, collision, targeting, range, combat stats, damage math, Energy/Heavy/L1/L2 attack progression, weapon timing, deployment limits, reserves, deterministic battle termination, simultaneous attack resolution, and server-authoritative deterministic simulation. Use whenever work touches battle rules, pathing, targeting, replay architecture, reinforcement flow, weapon attack behavior, or combat math.
---

# Combat

Read `references/combat-canon.md` before designing or implementing battle behavior.

Also read:
- `.claude/skills/units/references/unit-canon.md` when Unit/Hero identity or Mounted matters.
- `.claude/skills/units/references/weapon-registry.md` when weapon type, range, slot use, dual wield, weapon weight, shield behavior, or Runeforged weapon pairs matter.
- `.claude/skills/runes-aura/references/rune-canon.md` when Aura changes attacks/classes.

## Core battlefield rules

- Combat is automatic once battle begins.
- No equipment changes or manual skill casting during battle.
- Battlefield is an offset hex grid of 8 columns x 7 rows.
- Each side deploys within its own half of 4 columns x 7 rows; the armies face each other across the column axis.
- One unit per hex; occupied hexes are impassable.
- Default target is nearest valid enemy by hex distance.
- If multiple enemies are equally closest, prefer the one with lower final Defense; distance always wins over this armor preference.
- If distance and Defense are both equal, no additional authored gameplay priority is required for v1; deterministic implementation ordering is sufficient.
- Melee requires a reachable adjacent attack position.
- Ranged units do not need adjacent space and may attack over frontliners.
- If a target leaves range, pursue one hex; if still out of range, retarget.
- Multiple equally short paths have no authored tactical priority; deterministic pathfinder ordering is an implementation detail.
- 8 active units and 16 total army slots are current v1 tunable values.
- Reserves are ordered before combat, have preferred rear-column entry hexes, and enter after a short configurable delay when active slots open.
- A blocked reserve waits alive; another reserve may enter if it has an open active slot and unblocked assigned entry.
- An army is defeated only when every active Unit and every reserve Unit is dead.
- A living blocked reserve prevents defeat.
- If both armies lose all remaining Units in the same authoritative timestamp batch, the result is a Draw.
- A battle may never simulate forever: a configurable maximum simulated duration and configurable no-progress window both terminate an otherwise unresolved fight as a Draw.
- A timeout/stalemate Draw does not kill, remove, or reinterpret surviving active Units or blocked reserves.

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

Weapon defaults are centralized in the weapon registry. Current v1 defaults include melee range 1 hex and Ranged-family range 3 hexes.

For two equipped 1-slot weapons, both contribute full stats and autos alternate hands. Each alternating hand attack is a real auto for crit/Energy purposes.

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

## Deterministic timing and termination

The simulation uses an authoritative combat clock.

Attacks with the same authoritative timestamp resolve as one simultaneous batch:
- eligibility is determined from the state immediately before that timestamp;
- every already-valid attack in the batch resolves even if its attacker is killed by another attack in the same batch;
- damage/effects are calculated from the same pre-batch state and then applied together;
- deaths are resolved after the batch is applied;
- if both armies are fully eliminated by that batch, the result is a Draw.

Stable implementation ordering may be used inside a batch for deterministic RNG consumption/event serialization, but must not give one same-timestamp attack first-strike survival priority over another.

Finite simulation guards:
- configurable maximum simulated battle duration;
- configurable no-progress window.

For v1, progress means an HP change, a Unit death, or a reserve successfully entering the battlefield. Movement, retargeting, path recalculation, and blocked reserve-entry attempts do not reset the no-progress window.

After each timestamp batch, resolve ordinary victory/defeat first. If neither side is defeated and a termination guard has expired, end the battle as a Draw while preserving all surviving Unit/reserve state.

Exact duration values are tuning/configuration, not permanent canon.

## Architecture

- Server is authoritative.
- Simulation is deterministic from the authoritative battle snapshot/seed.
- The server may resolve faster than real time while the client progressively reveals the event timeline.
- For an MVP, returning the full battle log at once is acceptable.
- Pre-resolution must always terminate through defeat, simultaneous elimination Draw, or the finite simulation guards.

## Do not invent

Keep these unresolved unless the creator explicitly decides them:
- exact Unit/equipment stat budgets
- exact armor/weapon Attack Interval modifiers
- exact per-weapon Light/Medium/Heavy assignment
- minimum Attack Interval floor
- weapon-specific range exceptions beyond registry defaults
- exact Rune-specific L1/L2 combat effects
- special targeting overrides
- exact reinforcement delay
- shield Block or Dodge mechanics
- exact progressive-delivery/network implementation

The exact maximum-duration and no-progress-duration numbers remain configurable balance values; their existence and Draw outcome are locked.
