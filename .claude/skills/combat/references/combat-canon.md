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

When multiple valid routes are equally short, there is no additional authored gameplay preference between them. The deterministic simulation/pathfinder may choose consistently as an implementation detail; do not invent a tactical priority such as always preferring left/right/up/down.

## 5. Movement Speed

Movement Speed remains a universal stat for future extensibility.

For v1, there is only one inherent distinction:
- non-Mounted Units use standard movement speed
- Mounted Units are slightly faster

Exact numerical values remain tunable.

Do not currently invent extra movement-speed tiers, equipment penalties, or item/trait movement modifiers unless explicitly approved later.

## 6. Default targeting

Default AI prioritizes the **closest valid enemy** measured by hex distance.

Target priority is:
1. closest valid enemy by hex distance;
2. if multiple enemies are equally closest, prefer the enemy with **lower final Defense** as the current mechanical expression of being less armored;
3. if distance and Defense are both equal, there is no additional authored gameplay priority. The deterministic simulation may choose consistently as an implementation detail.

Distance always has priority over armor/Defense. A more heavily armored enemy that moves closer than a lightly armored enemy becomes the target normally.

### Melee

Standard melee range is 1 hex.

A melee Unit cannot simply choose the geometrically closest enemy if there is no physically reachable adjacent attack hex.

If the closest enemy is completely surrounded or otherwise unreachable, skip it and target the closest physically reachable enemy instead.

### Ranged

Ranged Units target the closest enemy within their weapon's hex range.

They do not require a free adjacent hex beside the enemy and may attack over frontline Units according to range.

## 7. Retargeting and pursuit

### Death / untargetable

If a target dies or becomes untargetable/off-board, the attacker immediately drops that target and selects the new closest valid enemy using the same targeting priority.

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

## 10. Core combat stats and equipment totals

Universal stats are deliberately limited to:
- HP
- Power
- Defense
- Attack Interval
- Critical Chance
- Movement Speed

Final combat stats are built from the Unit's base stats plus equipped item contributions.

Conceptually:
`Final Stat = Unit Base + Weapon Modifiers + Armor Modifiers`

### HP

Units have base HP. Armor and other approved equipment may add HP.

Current armor slots:
- Head
- Shoulders
- Chest
- Gloves
- Legs
- Boots

Armor-slot stats add together normally.

### Power

Power is the single offensive scaling stat.

Do not split it into Attack Power and Special Power.

Current v1 tuning baseline:
- 1 Power = 5 raw auto-attack damage before attack coefficients, criticals, and Defense.

This `5` is a balance value, not a sacred permanent constant.

### Defense

Defense is a single general mitigation stat.

Defense is intentionally hard to obtain and comes heavily from equipped armor.

Do not create separate Armor and Magic Resistance stats.

Current v1 mitigation curve:
`Damage Reduction = Defense / (Defense + 100)`

The `100` denominator is a tunable balance constant.

Examples:
- 3 Defense ~= 2.9% reduction
- 10 Defense ~= 9.1%
- 25 Defense = 20%
- 50 Defense ~= 33.3%
- 100 Defense = 50%

This diminishing-return curve prevents easy immunity while allowing every point of rare Defense to matter.

### Attack Interval

Attack Interval is the actual time between automatic attacks. Lower values attack faster.

A Unit has a base Attack Interval, but final interval is influenced by:
- armor class/weight
- weapon weight/handling
- one-handed / dual-wield / two-handed characteristics where authored

Structural rule:
`Final Attack Interval = Unit Base Interval + approved armor/weapon modifiers`

Exact Light/Medium/Heavy and weapon modifiers remain tunable content/balance data.

Heavier armor and heavier/two-handed weapons may generally increase interval; lighter or faster weapon setups may reduce it.

Do not hardcode every armor+weapon combination as a separate combat rule.

An eventual minimum Attack Interval/floor is required to prevent pathological stacking, but the exact floor is not yet locked.

### Critical Chance

Critical Chance is additive from Unit/equipment sources where authored.

Current global critical multiplier:
- 2x raw attack damage

Critical Damage is not a separate universal stat.

### Movement Speed

Movement Speed remains present for future systems. For current v1, Mounted is simply somewhat faster than non-Mounted.

## 11. Damage calculation order

Current v1 attack calculation order:

1. Build final Power from Unit + equipment.
2. Convert Power to raw auto damage using the global Power scale.
3. Apply the attack coefficient, such as Heavy 2.5x.
4. If the attack crits, multiply raw damage by 2x.
5. Apply Defense mitigation.
6. Round final damage to the nearest whole number.
7. A successful damaging hit deals at least 1 damage.

Conceptually:
`Raw Auto = Final Power * 5`

`Raw Attack = Raw Auto * Attack Coefficient`

`Raw Critical = Raw Attack * 2` when critical

`Final Damage = round(Raw CriticalOrNormal * (1 - Defense/(Defense+100)))`

Then clamp a successful damaging hit to a minimum of 1 damage.

There is currently no Physical/Magical defensive split. Fire, sword, Mystic, and other attacks may differ by effects and coefficients, but they use the same Power/Defense foundation unless a future explicit mechanic says otherwise.

## 12. Heavy attacks, criticals, and Energy

There is one combat resource only.

Energy range:
- 0 to 100

### Energy generation

Current v1 rule:
- each successful auto attack grants +10 Energy to the attacker
- critting an auto does not grant extra Energy
- being hit does not grant Energy
- movement does not grant Energy
- Energy does not overflow past 100

At 100, the Unit's next eligible 100-Energy attack is performed and Energy resets to 0.

The exact +10 value is tunable later, but this is the current implementation baseline.

### Normal / L0 Dormant

At 100 Energy, perform a Heavy Attack.

Current ordinary Heavy coefficient:
- 2.5x normal auto raw damage

Example before Defense:
- normal hit 50
- Heavy 125
- Heavy crit 250

Autos and ordinary Heavy attacks can crit.

### L1 Conduit

At 100 Energy, the Rune transforms the ordinary Heavy Attack into a Rune-powered Heavy/Special Attack.

Example concept:
- normal Heavy Slash
- Fire Conduit -> Heavy Fire Slash

The ordinary 2.5x Heavy baseline is the current starting point, but exact Rune-specific coefficients/effects may later modify it.

Normal auto attacks remain primarily mundane at L1 unless a specific Rune rule says otherwise.

### L2 Aspect

At L2, Rune power also transforms normal auto attacks.

Therefore:
- auto attacks become Rune-infused/Rune-powered
- the 100-Energy attack remains a larger/stronger Rune special

Example Fire pattern:
`Fire auto -> Fire auto -> Fire auto -> 100 Energy -> stronger Fire special`

Exact Rune-specific L2 auto/special effects remain to be authored.

### L3

Combat behavior intentionally remains undefined.

Do not invent it.

## 13. Non-core mechanics

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

## 14. Reserves

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

A blocked reserve remains alive while waiting.

### Multiple open slots

Queue order determines which reserves are called first, but battlefield blockage may alter actual arrival order.

If Reserve A was called first but its assigned entry hex is blocked, and another active slot exists for Reserve B whose assigned entry is free, Reserve B may enter while Reserve A remains pending.

This is intentional strategic uncertainty.

### No inherited death position

A reserve never spawns at the hex where the dead Unit fell.

All reinforcements enter from their assigned rear-row entry point and move normally from there.

This makes Mounted reinforcement speed meaningful.

## 15. Battle end and finite termination

An army is defeated **only when every Unit belonging to that battle is dead**, including:
- all active Units;
- all reserve Units.

Therefore:
- zero active Units does not mean defeat while any reserve is still alive;
- a living reserve whose assigned entry hex is blocked is still alive and prevents defeat;
- inability to enter the battlefield does not count as death and does not itself cause defeat.

If both armies have every remaining Unit die as part of the same authoritative timestamp batch, the battle result is a **Draw**.

### Finite simulation guards

A server-authoritative battle must never be allowed to simulate forever.

Every battle has two configurable termination guards:
- **Maximum Battle Duration:** a hard cap on the authoritative **simulated combat clock**.
- **No-Progress Window:** a cap on how long simulated combat may continue without meaningful progress.

For v1, meaningful progress is any of:
- an HP value changes because damage or healing is applied;
- a Unit dies;
- a reserve successfully enters the battlefield.

These do **not** count as progress by themselves:
- movement;
- retargeting;
- path recalculation;
- waiting;
- a blocked reserve attempting and failing to enter.

The hard duration cap exists even if the no-progress window repeatedly resets, so cyclic combat can never create an infinite pre-resolution loop.

After completing the authoritative resolution batch for a timestamp:
1. resolve deaths and normal victory/defeat;
2. resolve simultaneous full elimination as a Draw;
3. if neither army is defeated and either termination guard has expired, end the battle as a **Draw**.

A timeout/stalemate Draw does **not** kill surviving Units, does not remove blocked reserves, and does not reinterpret a living reserve as dead. The final battle snapshot records those survivors as alive.

The exact maximum-duration and no-progress-duration values are tuning/configuration, not permanent canon.

## 16. Server authority, deterministic timing, and simultaneous attacks

Combat is server-authoritative.

The client does not perform authoritative combat math, targeting, pathing, RNG, damage, reinforcement, termination, or victory decisions.

Conceptual flow:
1. server snapshots both armies and all pre-battle decisions;
2. server assigns/records the authoritative RNG seed/state;
3. server runs movement, targeting, attacks, RNG, deaths, reserves, and termination guards on an authoritative simulated combat clock;
4. server produces the authoritative battle result and event timeline;
5. client renders that timeline.

The same authoritative starting snapshot and RNG state must produce the same simulation result.

### Same-timestamp attack resolution

Attacks scheduled for the same authoritative timestamp resolve as one **simultaneous attack batch**.

For a batch at timestamp `T`:
1. determine which scheduled attacks are valid from the combat state immediately before `T`;
2. those valid attacks are committed to the batch;
3. calculate their hit/crit/damage/effects from that same pre-batch state;
4. apply the batch's resulting damage/effects together;
5. resolve HP totals, deaths, Energy/results, and battle-end state only after all committed attacks in that batch have resolved.

Consequences:
- a Unit killed by another attack at timestamp `T` still completes its own attack already committed for timestamp `T`;
- one same-timestamp attacker does not gain first-strike survival priority merely because its event happened to serialize first;
- mutual lethal attacks can deterministically eliminate both armies and produce a Draw.

A stable internal ordering may be used solely for deterministic RNG consumption, logging, and event serialization. That ordering must not change the simultaneous state-application rule above.

Future explicitly authored reaction/counter mechanics may define their own timing relationship, but must do so deliberately rather than relying on arbitrary event-list order.

## 17. Pre-resolved computation and progressive reveal

Weapons of Order does not need TFT's wall-clock live server simulation merely because TFT uses it.

The current preferred architecture is:
- server may simulate the complete battle faster than real time;
- authoritative events/results are generated server-side;
- the client progressively receives/reveals events according to battle playback time.

This preserves suspense without requiring the server to spend the full visual battle duration calculating in real time.

For an early MVP, returning the complete event log at once is acceptable if simpler. Progressive delivery/reveal can be added before competitive multiplayer if needed.

Pre-resolution must always terminate through:
- ordinary defeat;
- simultaneous-elimination Draw; or
- the finite simulation guards.

Do not expose the simulation outcome to gameplay logic on the client as authoritative state.

## 18. Replays and asynchronous combat

Because the authoritative battle is represented by a snapshot/seed/event timeline, the model naturally supports deterministic replays and asynchronous battles.

A defending player does not need to be online for the server to resolve a battle against a stored defensive setup.

Exact PvP/session architecture remains a separate future system.

## 19. Future large-scale armies

The current tactical battlefield uses individual Units/Heroes.

A future army-scale layer may aggregate large numbers of soldiers into Formations/Squads.

Such aggregate formations are allowed to contain mixed equipment/Rune composition internally. Do not force one Formation per Rune/loadout merely to simplify data.

The exact way mixed cohorts, special Rune wielders, casualties, and aggregate stats behave is deferred.

## 20. Intentionally unresolved

Do not silently decide:
- exact Unit/equipment stat budgets
- exact Light/Medium/Heavy Attack Interval modifiers
- exact weapon-weight Attack Interval modifiers
- minimum Attack Interval floor
- exact weapon ranges by weapon type
- exact Rune-specific L1/L2 coefficients and effects
- shield Block mechanics
- Dodge mechanics
- exact movement timing values
- exact special targeting overrides
- exact reinforcement delay
- exact progressive event delivery/network protocol
- large-scale Formation/Squad combat simulation

Equal shortest paths and exact-equal targeting candidates do not require additional authored gameplay priorities for v1; deterministic implementation ordering is sufficient.

The existence and Draw outcome of both finite termination guards are locked. Their exact durations remain tunable balance/configuration values.

Current numeric combat baselines such as Power scale 5, Defense constant 100, +10 Energy, 2.5x Heavy, and 2x Crit are v1 balance values and may be iterated without changing the underlying system structure.
