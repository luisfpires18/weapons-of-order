# Weapons of Order: Combat Canon

## 1. Battle structure

Combat is an autobattler.

The player decides strategy before battle through:
- army selection
- equipment
- starting deployment
- reserve order
- reserve preferred entry points

Once battle begins, there is no player intervention that changes the outcome.

The battle is one continuous fight rather than TFT-style planning rounds.

## 2. Battlefield

The battlefield uses an **offset hexagonal grid**.

Dimensions:
- 8 rows
- 7 columns
- 56 total hexes

Each player controls one half for deployment:
- 4 rows x 7 columns
- 28 deployment hexes per side

Deployment is freeform within the player's own deployment half.

Only one Unit may occupy a hex at a time.

Starting positions lock when combat begins.

Distance and movement are measured by adjacent-hex traversal, not straight-line Euclidean distance.

## 3. Active deployment and army limit

Current v1 tunable values:
- maximum active Units: 8
- maximum total army Units: 16
- therefore up to 8 reserves when fielding 8 starters

These numbers are initial balance targets, not permanent sacred limits.

The structural distinction is canonical:
- **Deployment Limit** = how many Units may be active on the battlefield at once.
- **Army Limit** = starters + reserves available to that battle.

## 4. Collision and pathing

Units have physical collision.

A hex occupied by an ally or enemy is impassable.

Units cannot move through allies.

This intentionally permits body blocking and **melee jail**, where a melee Unit can become trapped behind its own frontline and must spend time finding another valid route.

When a target is outside attack range, the Unit paths toward a valid attack position one adjacent hex at a time using the shortest currently valid route.

Exact pathfinding tie-break rules must eventually be deterministic, but the exact tie-break is not yet canonized.

## 5. Movement Speed

Movement Speed controls how quickly Units traverse adjacent hexes / the delay and playback timing between movement hops.

For now there is only one inherent distinction:
- non-Mounted Units use standard movement speed
- Mounted Units are slightly faster

Exact numerical values are balance data and remain unresolved.

Do not currently add inherent movement-speed categories or item/trait modifiers unless explicitly approved later.

## 6. Default targeting

Default AI prioritizes the **closest valid enemy** measured by hex distance.

Target selection must ultimately use deterministic tie-breaking when multiple enemies are equally valid/close.

### Melee

Standard melee range is 1 hex.

A melee Unit cannot simply choose the geometrically closest enemy if there is no physically reachable adjacent attack hex.

If the closest enemy is completely surrounded or otherwise unreachable, skip it and target the closest physically reachable enemy instead.

### Ranged

Ranged Units target the closest enemy within their weapon's hex range.

They do not require a free adjacent hex beside the enemy and may attack over/interact past frontline Units according to range.

## 7. Retargeting and pursuit

### Death / untargetable

If a target dies or becomes untargetable/off-board, the attacker immediately drops that target and selects the new closest valid enemy.

### Target leaves attack range

If the current target moves out of attack range:
1. attacker pursues exactly one adjacent hex toward the target;
2. range is checked again;
3. if the target is still outside attack range, the attacker drops it;
4. attacker reacquires the closest currently valid enemy.

This prevents Units from endlessly chasing one target across the whole battlefield.

## 8. Range

Attack range is measured strictly in hexes.

Conceptual range bands currently discussed:
- 1 = melee
- 2 = short
- 3 = medium
- 4 = long
- 5+ = very long

These are descriptive bands, not yet exact weapon assignments.

Do not assume 5 hexes covers the full 8x7 board. Exact weapon ranges remain to be authored later.

## 9. Special targeting

Role/class-specific targeting overrides may exist, such as a future Assassin-style opening rule.

Default behavior remains nearest-valid-enemy unless an explicit creator-authored override applies.

No exact special targeting rules are currently canonized.

## 10. Core combat stats

Universal stats are deliberately limited to:

### HP
Maximum/current health.

### Power
Single offensive scaling stat.

Do not split into Attack Power and Special Power.

Physical-looking and Rune-powered attacks may both scale from Power using attack-specific formulas.

### Defense
Single general defensive stat for now.

Defense is intentionally hard to obtain and comes heavily from equipped armor.

Do not create separate universal Armor and Magic Resistance stats unless explicitly approved later.

Exact mitigation formula remains unresolved.

### Attack Interval
Actual time between automatic attacks. Lower values attack faster.

### Critical Chance
Universal chance for critical attacks.

Critical Damage is not currently a separate universal stat.

### Movement Speed
Controls adjacent-hex traversal timing. Mounted is slightly faster than non-Mounted for now.

## 11. Non-core mechanics

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

They may later exist as equipment, shield, class, Rune, buff/debuff, or attack-specific mechanics.

## 12. Energy and attack progression

There is one combat resource only.

Energy range:
- 0 to 100

Exact generation rules are not yet locked.

### Normal / L0 Dormant
At 100 Energy, perform a Heavy Attack.

### L1 Conduit
At 100 Energy, the Rune transforms the ordinary Heavy Attack into a Rune-powered Heavy/Special Attack.

Example concept:
- normal Heavy Slash
- Fire Conduit -> Heavy Fire Slash

Normal auto attacks remain primarily mundane at L1 unless a specific Rune rule says otherwise.

### L2 Aspect
At L2, Rune power also transforms normal auto attacks.

Therefore:
- auto attacks become Rune-infused/Rune-powered
- the 100-Energy attack remains a larger/stronger Rune special

Example Fire pattern:
`Fire auto -> Fire auto -> Fire auto -> 100 Energy -> stronger Fire special`

### L3
Combat behavior intentionally remains undefined.

Do not invent it.

## 13. Reserves

Reserves are prepared before battle in an ordered queue.

Each reserve also has a preferred entry hex on the player's rear row.

When active Unit count falls below the Deployment Limit, reinforcement opportunities open.

### Entry timing

A reserve does not teleport in on the same instant a Unit dies.

Use a short configurable reinforcement entry delay. Exact duration remains tunable.

Concept:
`death -> active slot opens -> short delay -> reserve attempts entry`

### Entry hex

The reserve attempts to enter through its assigned preferred rear-row hex.

If that hex is free:
- reserve enters there
- becomes active
- begins normal AI behavior

If that hex is occupied:
- that reserve waits off-board
- it does not magically choose another spawn hex

### Multiple open slots

Queue order determines which reserves are called first, but battlefield blockage may alter actual arrival order.

If Reserve A was called first but its assigned entry hex is blocked, and another active slot exists for Reserve B whose assigned entry is free, Reserve B may enter while Reserve A remains pending.

This is intentional strategic uncertainty.

### No inherited death position

A reserve never spawns at the hex where the dead Unit fell.

All reinforcements enter from their assigned rear-row entry point and move normally from there.

This makes Mounted reinforcement speed meaningful.

## 14. Battle end

A side is not defeated merely because all currently active Units are dead if valid reserves remain available to enter.

Current structural rule:
- battle ends when a side has no active Units and no remaining reserve capable of continuing the army.

Exact edge cases involving permanently blocked reserves, timeout, stalemate, or overtime are not yet canonized.

## 15. Server authority and deterministic simulation

Combat is server-authoritative.

The client does not perform authoritative combat math, targeting, pathing, RNG, damage, or reinforcement decisions.

Conceptual flow:
1. server snapshots both armies and all pre-battle decisions;
2. server assigns/records the authoritative RNG seed/state;
3. server runs movement, targeting, attacks, RNG, deaths, and reserves;
4. server produces the authoritative battle result and event timeline;
5. client renders that timeline.

The same authoritative starting snapshot and RNG state must produce the same simulation result.

## 16. Pre-resolved computation and progressive reveal

Weapons of Order does not need TFT's wall-clock live server simulation merely because TFT uses it.

The current preferred architecture is:
- server may simulate the complete battle faster than real time;
- authoritative events/results are generated server-side;
- the client progressively receives/reveals events according to battle playback time.

This preserves suspense without requiring the server to spend the full visual battle duration calculating in real time.

For an early MVP, returning the complete event log at once is acceptable if simpler. Progressive delivery/reveal can be added before competitive multiplayer if needed.

Do not expose the simulation outcome to gameplay logic on the client as authoritative state.

## 17. Replays and asynchronous combat

Because the authoritative battle is represented by a snapshot/seed/event timeline, the model naturally supports deterministic replays and asynchronous battles.

A defending player does not need to be online for the server to resolve a battle against a stored defensive setup.

Exact PvP/session architecture remains a separate future system.

## 18. Future large-scale armies

The current tactical battlefield uses individual Units/Heroes.

A future army-scale layer may aggregate large numbers of soldiers into Formations/Squads.

Such aggregate formations are allowed to contain mixed equipment/Rune composition internally. Do not force one Formation per Rune/loadout merely to simplify data.

The exact way mixed cohorts, special Rune wielders, casualties, and aggregate stats behave is deferred.

## 19. Intentionally unresolved

Do not silently decide:
- exact damage formula
- exact Defense mitigation formula
- physical/magical damage taxonomy
- default crit multiplier
- exact Energy generation
- Heavy Attack coefficient
- which attacks can crit
- shield Block mechanics
- Dodge mechanics
- exact movement timing
- deterministic pathfinding tie-breaks
- deterministic equal-distance target tie-breaks
- exact weapon ranges by weapon type
- exact special targeting overrides
- exact reinforcement delay
- timeout/overtime/stalemate rules
- exact Rune-specific L1/L2 attacks
- exact progressive event delivery/network protocol
- large-scale Formation/Squad combat simulation
