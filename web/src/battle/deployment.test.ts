import { describe, expect, it } from "vitest";
import {
  deploymentBlocker,
  empty,
  intentOf,
  moveReserve,
  place,
  reserve,
  reserveBlocker,
  reserves,
  unitAt,
  unplace,
  unplaced,
} from "@/battle/deployment";
import { army, armyUnit, starterArmy } from "@/testing/battle";

/**
 * Editing an army.
 *
 * These are the rules the interface can apply before the player is refused. The server applies
 * them again — and it, not this, is what actually holds them — but a screen that lets somebody
 * make a move it knows will fail is a screen that wastes their time.
 */
describe("reading the current army", () => {
  it("turns a placed army back into the intent that would produce it", () => {
    const current = army([
      armyUnit({ unitId: "a", role: "active", hex: { column: 1, row: 2 } }),
      armyUnit({ unitId: "b", role: "reserve", reserveOrder: 1 }),
      armyUnit({ unitId: "c", role: "reserve", reserveOrder: 0 }),
      armyUnit({ unitId: "d" }),
    ]);

    expect(intentOf(current)).toEqual({
      active: [{ unitId: "a", column: 1, row: 2 }],
      // Queue order, not roster order: it decides who is called first and where they come in.
      reserves: ["c", "b"],
    });
  });

  it("finds who is standing on a hex, and nobody where nobody is", () => {
    const current = army([armyUnit({ unitId: "a", role: "active", hex: { column: 3, row: 3 } })]);

    expect(unitAt(current, { column: 3, row: 3 })?.unitId).toBe("a");
    expect(unitAt(current, { column: 3, row: 4 })).toBeUndefined();
  });

  it("separates the roster from the army", () => {
    const current = army([
      armyUnit({ unitId: "a", role: "active", hex: { column: 0, row: 0 } }),
      armyUnit({ unitId: "b", role: "reserve", reserveOrder: 0 }),
      armyUnit({ unitId: "c" }),
    ]);

    expect(unplaced(current).map((unit) => unit.unitId)).toEqual(["c"]);
    expect(reserves(current).map((unit) => unit.unitId)).toEqual(["b"]);
  });
});

describe("placing a Unit", () => {
  it("puts it on the hex", () => {
    const intent = place(intentOf(starterArmy()), "unit-melee", { column: 2, row: 5 });

    expect(intent.active).toEqual([{ unitId: "unit-melee", column: 2, row: 5 }]);
  });

  it("moves it rather than cloning it", () => {
    const first = place(intentOf(starterArmy()), "unit-melee", { column: 2, row: 5 });
    const moved = place(first, "unit-melee", { column: 0, row: 0 });

    expect(moved.active).toEqual([{ unitId: "unit-melee", column: 0, row: 0 }]);
  });

  it("takes it out of the reserve queue on the way", () => {
    const waiting = reserve(intentOf(starterArmy()), "unit-mounted");
    const deployed = place(waiting, "unit-mounted", { column: 1, row: 1 });

    expect(deployed.reserves).toEqual([]);
    expect(deployed.active).toEqual([{ unitId: "unit-mounted", column: 1, row: 1 }]);
  });

  it("displaces whoever was already there rather than stacking on them", () => {
    const first = place(intentOf(starterArmy()), "unit-melee", { column: 2, row: 2 });
    const second = place(first, "unit-ranged", { column: 2, row: 2 });

    expect(second.active).toEqual([{ unitId: "unit-ranged", column: 2, row: 2 }]);
  });
});

describe("the reserve queue", () => {
  it("appends to the back", () => {
    const intent = reserve(reserve(intentOf(starterArmy()), "unit-melee"), "unit-ranged");

    expect(intent.reserves).toEqual(["unit-melee", "unit-ranged"]);
  });

  it("takes a Unit off the battlefield when it joins", () => {
    const deployed = place(intentOf(starterArmy()), "unit-melee", { column: 3, row: 3 });
    const waiting = reserve(deployed, "unit-melee");

    expect(waiting.active).toEqual([]);
    expect(waiting.reserves).toEqual(["unit-melee"]);
  });

  it("reorders one place at a time", () => {
    const queued = ["a", "b", "c"].reduce(reserve, empty());

    expect(moveReserve(queued, "c", -1).reserves).toEqual(["a", "c", "b"]);
    expect(moveReserve(queued, "a", 1).reserves).toEqual(["b", "a", "c"]);
  });

  it("refuses to move past either end rather than wrapping round", () => {
    const queued = ["a", "b"].reduce(reserve, empty());

    expect(moveReserve(queued, "a", -1)).toBe(queued);
    expect(moveReserve(queued, "b", 1)).toBe(queued);
    expect(moveReserve(queued, "missing", 1)).toBe(queued);
  });
});

describe("taking a Unit out", () => {
  it("removes it from wherever it was", () => {
    const deployed = place(intentOf(starterArmy()), "unit-melee", { column: 3, row: 3 });
    const waiting = reserve(deployed, "unit-ranged");
    const cleared = unplace(unplace(waiting, "unit-melee"), "unit-ranged");

    expect(cleared).toEqual({ active: [], reserves: [] });
  });

  it("has an empty army as a thing a player can ask for", () => {
    expect(empty()).toEqual({ active: [], reserves: [] });
  });
});

describe("the limits, before the server has to say so", () => {
  const full = army([
    ...Array.from({ length: 8 }, (_, index) =>
      armyUnit({
        unitId: `active-${index}`,
        role: "active",
        hex: { column: index % 4, row: Math.floor(index / 4) },
      }),
    ),
    ...Array.from({ length: 8 }, (_, index) =>
      armyUnit({ unitId: `reserve-${index}`, role: "reserve", reserveOrder: index }),
    ),
    armyUnit({ unitId: "spare" }),
  ]);

  it("says which limit is in the way rather than only that something is", () => {
    expect(deploymentBlocker(full, "spare")).toContain("8 units may be deployed");
    expect(reserveBlocker(full, "spare")).toContain("8 units may wait in reserve");
  });

  it("lets a Unit already on the battlefield move, because moving adds nobody", () => {
    expect(deploymentBlocker(full, "active-0")).toBeNull();
  });

  it("lets a reserve stay in the queue it is already in", () => {
    expect(reserveBlocker(full, "reserve-0")).toBeNull();
  });

  it("gets out of the way when there is room", () => {
    expect(deploymentBlocker(starterArmy(), "unit-melee")).toBeNull();
    expect(reserveBlocker(starterArmy(), "unit-melee")).toBeNull();
  });
});
