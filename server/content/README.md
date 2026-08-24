# Game content

Creator-editable game data. These files are read by the API at startup; changing a value here
needs **no C# change, no React change and no EF Core migration**.

| File           | What it holds                                              |
| -------------- | ---------------------------------------------------------- |
| `units.json`   | Unit definitions: identity, kingdom, fixed tier, maximum armour, Mounted, base combat stats |
| `weapons.json` | Weapon metadata: canonical type, wield slot cost, and the registry's combat values |

Edit a file, restart the API, reload the page. The API also watches these files while it is
running, so an edit is usually picked up on the next request without a restart; restart if you
want to be certain.

## Stable keys

`units.json` entries are identified by `Key`. A player-owned Unit row stores that key, its own
instance id, and nothing else copied from the definition — so `DisplayName`, `Tier`,
`MaxArmor`, `Mounted` and `Starter` can all be changed freely and every existing Unit picks the
new values up immediately.

The key itself is the part that must stay still:

- **renaming a key orphans every player-owned Unit that references it.** The API then refuses
  to list that account's Units and says which key is missing, rather than quietly resolving the
  row to some other definition;
- to rename what a Unit is *called*, change `DisplayName` and leave `Key` alone;
- keys use the `kingdom.name` shape (`arkazia.melee`), lower case, up to 64 characters.

The same applies to `weapons.json`: `Type` is matched against the weapon type recorded on a
forged item, so it is the stable part and `DisplayName` is copy.

## Adding a Unit

Append an entry to `UnitContent.Units`:

```json
{
  "Key": "arkazia.example",
  "DisplayName": "Example",
  "Type": "Regular",
  "Kingdom": "Arkazia",
  "Tier": 1,
  "MaxArmor": "Heavy",
  "Mounted": false,
  "Starter": true,
  "Combat": {
    "Hp": 200,
    "Power": 8,
    "Defense": 5,
    "AttackIntervalSeconds": 1.5,
    "CriticalChance": 0.08
  }
}
```

| Field         | Accepted values                                     |
| ------------- | --------------------------------------------------- |
| `Key`         | unique, non-empty, ≤ 64 characters, no whitespace    |
| `DisplayName` | non-empty, ≤ 64 characters                           |
| `Type`        | `Regular` or `Hero`                                  |
| `Kingdom`     | any value listed in `UnitContent.Kingdoms`           |
| `Tier`        | `1`, `2` or `3` — a fixed classification tier, not an upgrade level |
| `MaxArmor`    | `Light`, `Medium` or `Heavy`                         |
| `Mounted`     | `true` or `false`                                    |
| `Starter`     | `true` grants every account one of these; `false` or absent grants none |
| `Combat`      | required; `Hp`, `Power`, `Defense`, `AttackIntervalSeconds`, `CriticalChance` |

A new kingdom needs its name added to `UnitContent.Kingdoms` first — that list is what makes a
typo in `Kingdom` a startup failure instead of a new kingdom nobody meant to create.

`Starter: true` grants each account exactly one instance, once. The grant is recorded on the
Unit row under its own key, separately from the definition key, so:

- adding a fourth starter later grants it once to every existing account with no schema change;
- removing `Starter: true` stops future grants and leaves Units already granted alone;
- **duplicates stay possible.** Nothing constrains an account to one Unit per definition. When
  recruitment exists it will create Units with no starter grant recorded, and an account can
  hold as many copies of a Regular Unit as it acquires.

## What is *not* in these files

- **Specialisations / classes.** Canon derives the current class from `Unit + loadout`, and the
  creator has not authored the names or the mappings. Nothing in the application invents one,
  and `Melee` / `Ranged` / `Mounted` are identity labels with no behaviour attached — there are
  no weapon proficiency restrictions anywhere in the code.
- **Range and Movement Speed on a Unit.** Four of canon's six universal stats are authored per
  Unit; the other two are not, on purpose. **Range** belongs to the equipped weapon — there are no
  weapon proficiency restrictions and no Unit is inherently ranged, so a Unit reaches as far as what
  it is holding. **Movement Speed** is derived from `Mounted`, because canon's one inherent movement
  distinction for v1 is that Mounted Units are slightly faster, and authoring a speed per Unit would
  quietly create the extra movement tiers it says not to invent.
- **Armour items.** `MaxArmor` is a Unit's structural limit and is published by the API, but no
  armour item content exists yet and none is invented here. Defense therefore comes entirely from a
  Unit's own `Combat.Defense`, which is why it is small.
- **Global combat tuning.** The Power scale, the Defense curve, the Heavy and critical multipliers,
  Energy, movement intervals, the deployment limits, the reinforcement delay and the two
  termination guards are balance rather than content, and live under `Combat` in
  `server/src/WeaponsOfOrder.Api/appsettings.json`. So does the training opposition, which is a
  battle harness rather than roster content.

## Temporary prototype values

`Tier: 1` and `MaxArmor: Heavy` on the three current entries are **placeholders, not canon**.
The creator has deferred authoring real tiers and armour limits; `Heavy` is deliberately
permissive so nothing is blocked by a guess. Change them when the real values are authored —
that is a content edit, nothing else.

**Every number under `Combat`, and every one on a weapon except `SlotCost` and `Range`, is the
same.** Canon fixes the six universal stats and says in as many words that the budgets are balance
work; these exist so the battle prototype can be played and are expected to be replaced by a real
balance pass. `SlotCost` and `Range` are the two that are not guesses: slot cost is canonical wield
data, and the registry's v1 range defaults are 1 hex for an ordinary melee weapon and 3 for the
Ranged Weapons family.

## When content is wrong

The API refuses to start and names the problem. It never repairs content quietly: a duplicate
key, an unknown `Type` or `MaxArmor`, a tier outside 1–3, a kingdom that is not in `Kingdoms`,
a slot cost outside 1–2, a missing display name, a missing or impossible `Combat` block, an
unknown weapon `Weight`, a `Range` below 1 or a `CriticalChance` outside 0–1 all fail validation
with the offending entry in the message.

## Where the file is read from

`server/content/`. The API looks there relative to its own content root, and falls back to a
`content/` directory beside the built application, which is where publishing puts it.
