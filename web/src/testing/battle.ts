import type { Army, ArmyIntent, ArmyUnit, BattleEvent, BattleResult } from "@/battle/api";
import { BATTLE_URLS } from "@/battle/api";

/**
 * A stand-in for the battle API, small enough to read and faithful enough to drive the screen.
 *
 * It exists to exercise the parts of the loop that live in the browser: which control is offered
 * in which state, that a tap on a hex sends the army the player meant, that a returned log plays
 * back and can be replayed. It decides nothing the real server decides — no stats, no opponent,
 * no outcome — and the event logs the tests hand it are written by hand for exactly that reason.
 *
 * The real rules are proven against a real database in the API tests, against nothing at all in the
 * simulator's own tests, and against a running stack in the browser sweep.
 */

export const BATTLEFIELD: Army["battlefield"] = { columns: 8, rows: 7, deploymentColumns: 4 };

export const LIMITS: Army["limits"] = { active: 8, reserve: 8, army: 16 };

export function armyUnit(overrides: Partial<ArmyUnit> = {}): ArmyUnit {
  return {
    unitId: "unit-1",
    definitionKey: "arkazia.melee",
    name: "Melee",
    kingdom: "Arkazia",
    tier: 1,
    mounted: false,
    weapons: [],
    stats: {
      hp: 240,
      power: 9,
      defense: 8,
      attackIntervalSeconds: 1.5,
      criticalChance: 0.08,
      range: 1,
      movementSpeed: 1,
    },
    role: "unplaced",
    hex: null,
    reserveOrder: null,
    reserveEntryHex: null,
    ...overrides,
  };
}

export function army(units: ArmyUnit[]): Army {
  return {
    battlefield: BATTLEFIELD,
    limits: LIMITS,
    units,
    ready: units.some((unit) => unit.role === "active"),
  };
}

/** Three Units, none of them placed. What a new account sees. */
export function starterArmy(): Army {
  return army([
    armyUnit({ unitId: "unit-melee", name: "Melee", definitionKey: "arkazia.melee" }),
    armyUnit({ unitId: "unit-ranged", name: "Ranged", definitionKey: "arkazia.ranged" }),
    armyUnit({
      unitId: "unit-mounted",
      name: "Mounted",
      definitionKey: "arkazia.mounted",
      mounted: true,
    }),
  ]);
}

/**
 * A short battle: two Units trade blows and one of them falls.
 *
 * Written out rather than generated, so a test asserting on playback is asserting against a log
 * whose every moment is on the page beside it.
 */
export function battleResult(overrides: Partial<BattleResult> = {}): BattleResult {
  const events: BattleEvent[] = [
    { kind: "deployed", time: 0, id: "P0", hex: { column: 3, row: 3 } },
    { kind: "deployed", time: 0, id: "O0", hex: { column: 4, row: 3 } },
    {
      kind: "attack",
      time: 0,
      attackerId: "P0",
      targetId: "O0",
      attack: "normal",
      critical: false,
      damage: 60,
      targetHp: 40,
      attackerEnergy: 10,
    },
    {
      kind: "attack",
      time: 0,
      attackerId: "O0",
      targetId: "P0",
      attack: "normal",
      critical: false,
      damage: 30,
      targetHp: 70,
      attackerEnergy: 10,
    },
    { kind: "moved", time: 500, id: "P0", from: { column: 3, row: 3 }, to: { column: 3, row: 2 } },
    {
      kind: "attack",
      time: 1000,
      attackerId: "P0",
      targetId: "O0",
      attack: "heavy",
      critical: true,
      damage: 40,
      targetHp: 0,
      attackerEnergy: 0,
    },
    { kind: "died", time: 1000, id: "O0", hex: { column: 4, row: 3 } },
    { kind: "ended", time: 1000, outcome: "playervictory", reason: "elimination" },
  ];

  return {
    outcome: "playervictory",
    reason: "elimination",
    durationMilliseconds: 1000,
    seed: "8675309",
    battlefield: BATTLEFIELD,
    combatants: [
      {
        id: "P0",
        side: "player",
        unitId: "unit-melee",
        name: "Melee",
        stats: {
          hp: 100,
          power: 12,
          defense: 8,
          attackIntervalSeconds: 1,
          criticalChance: 0.1,
          range: 1,
          movementSpeed: 1,
        },
        reserveOrder: null,
        reserveEntryHex: null,
        endState: "active",
        finalHp: 70,
        finalEnergy: 0,
        finalHex: { column: 3, row: 2 },
      },
      {
        id: "O0",
        side: "opponent",
        unitId: null,
        name: "Opponent 1",
        stats: {
          hp: 100,
          power: 6,
          defense: 5,
          attackIntervalSeconds: 1.5,
          criticalChance: 0.08,
          range: 1,
          movementSpeed: 1,
        },
        reserveOrder: null,
        reserveEntryHex: null,
        endState: "dead",
        finalHp: 0,
        finalEnergy: 10,
        finalHex: { column: 4, row: 3 },
      },
    ],
    events,
    ...overrides,
  };
}

/**
 * Answers the battle endpoints from an army it keeps, applying a save the way the server would.
 *
 * The rules it applies are the ones the screen is allowed to rely on — a Unit is in one place, a
 * hex holds one Unit — and nothing more. It does not check ownership, limits or the deployment
 * half, because those are the server's answers and a test that got them from here would be
 * testing the fake.
 */
export function fakeBattleApi(options: { army?: Army; result?: BattleResult } = {}) {
  let current = options.army ?? starterArmy();
  const result = options.result ?? battleResult();
  const saves: ArmyIntent[] = [];
  let battles = 0;

  const apply = (intent: ArmyIntent) => {
    saves.push(intent);

    const units = current.units.map((unit): ArmyUnit => {
      const placement = intent.active.find((entry) => entry.unitId === unit.unitId);

      if (placement) {
        return {
          ...unit,
          role: "active",
          hex: { column: placement.column, row: placement.row },
          reserveOrder: null,
          reserveEntryHex: null,
        };
      }

      const queue = intent.reserves.indexOf(unit.unitId);

      if (queue >= 0) {
        return {
          ...unit,
          role: "reserve",
          hex: null,
          reserveOrder: queue,
          reserveEntryHex: { column: 0, row: queue % current.battlefield.rows },
        };
      }

      return { ...unit, role: "unplaced", hex: null, reserveOrder: null, reserveEntryHex: null };
    });

    current = { ...current, units, ready: units.some((unit) => unit.role === "active") };
  };

  const handle = (url: string, init?: RequestInit): Response | undefined => {
    if (!url.startsWith("/api/battle")) return undefined;

    if (url.endsWith(BATTLE_URLS.simulate)) {
      battles++;
      return json(result);
    }

    if (url.endsWith(BATTLE_URLS.army)) {
      if (init?.method === "POST" && typeof init.body === "string") {
        apply(JSON.parse(init.body) as ArmyIntent);
      }

      return json(current);
    }

    return undefined;
  };

  return {
    handle,
    saves,
    get army() {
      return current;
    },
    get battles() {
      return battles;
    },
  };
}

function json(body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { "Content-Type": "application/json" },
  });
}
