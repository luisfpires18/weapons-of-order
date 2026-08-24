import { describe, expect, it } from "vitest";
import type { BattleResult } from "@/battle/api";
import {
  FADE_MILLISECONDS,
  STEP_MILLISECONDS,
  durationLabel,
  frameAt,
  playbackLength,
  survivors,
} from "@/battle/playback";
import { battleResult } from "@/testing/battle";

/**
 * Reading the server's log.
 *
 * Playback's whole job is to say where everybody is and how they are doing at a given moment,
 * from a log that already contains the answer. These tests are what keep it from acquiring an
 * opinion — nothing here computes damage, a target or a winner, and there is nowhere it could.
 */
function combatant(result: BattleResult, time: number, id: string) {
  const found = frameAt(result, time).combatants.find((entry) => entry.id === id);

  if (!found) {
    throw new Error(`No combatant ${id} in the battle.`);
  }

  return found;
}

describe("the opening moment", () => {
  it("puts both armies on the board at time zero", () => {
    const frame = frameAt(battleResult(), 0);

    expect(frame.combatants.map((entry) => entry.id)).toEqual(["P0", "O0"]);
    expect(combatant(battleResult(), 0, "P0").hex).toEqual({ column: 3, row: 3 });
    expect(combatant(battleResult(), 0, "O0").hex).toEqual({ column: 4, row: 3 });
    expect(frame.ended).toBe(false);
  });

  it("starts every Energy bar empty", () => {
    // Before the first blow lands. Canon has one bar and it begins at nothing.
    const frame = frameAt(battleResult(), -1);

    expect(frame.combatants.every((entry) => entry.energy === 0)).toBe(true);
  });
});

describe("what the log says happened", () => {
  it("takes HP and Energy from the event rather than working them out", () => {
    // Both blows land at time zero, and each event carries the result the server decided.
    const struck = combatant(battleResult(), 0, "O0");
    const striker = combatant(battleResult(), 0, "P0");

    expect(struck.hp).toBe(40);
    expect(striker.hp).toBe(70);
    expect(striker.energy).toBe(10);
  });

  it("keeps both blows of one timestamp, because they are one moment", () => {
    const frame = frameAt(battleResult(), 0);

    expect(frame.strikes).toHaveLength(2);
    expect(frame.strikes.map((strike) => strike.attackerId).sort()).toEqual(["O0", "P0"]);
  });

  it("lets a blow fade rather than leaving it on the board", () => {
    expect(frameAt(battleResult(), 0).strikes).toHaveLength(2);
    expect(frameAt(battleResult(), 400).strikes).toHaveLength(0);
  });

  it("reports a Heavy critical as both of those things", () => {
    const strike = frameAt(battleResult(), 1000).strikes.find(
      (entry) => entry.attackerId === "P0" && entry.time === 1000,
    );

    expect(strike?.attack).toBe("heavy");
    expect(strike?.critical).toBe(true);
    expect(strike?.attackerEnergy).toBe(0);
  });
});

describe("movement", () => {
  it("is drawn as a step from one hex to the next", () => {
    const stepping = combatant(battleResult(), 500 + STEP_MILLISECONDS / 2, "P0");

    expect(stepping.from).toEqual({ column: 3, row: 3 });
    expect(stepping.hex).toEqual({ column: 3, row: 2 });
    expect(stepping.step).toBeGreaterThan(0);
    expect(stepping.step).toBeLessThan(1);
  });

  it("has arrived once the step is over", () => {
    const arrived = combatant(battleResult(), 500 + STEP_MILLISECONDS + 10, "P0");

    expect(arrived.from).toBeNull();
    expect(arrived.step).toBe(1);
    expect(arrived.hex).toEqual({ column: 3, row: 2 });
  });

  it("does not smear a Unit across the board when the clock jumps", () => {
    // A scrub to the end must not re-run an old step: the Unit is where it finished.
    const settled = combatant(battleResult(), 5_000, "P0");

    expect(settled.from).toBeNull();
    expect(settled.hex).toEqual({ column: 3, row: 2 });
  });
});

describe("death", () => {
  it("leaves a body that fades rather than a Unit that vanishes", () => {
    const fresh = combatant(battleResult(), 1_000, "O0");
    const half = combatant(battleResult(), 1_000 + FADE_MILLISECONDS / 2, "O0");
    const gone = combatant(battleResult(), 1_000 + FADE_MILLISECONDS, "O0");

    expect(fresh.state).toBe("dead");
    expect(fresh.hp).toBe(0);
    expect(fresh.fade).toBeCloseTo(0);
    expect(half.fade).toBeCloseTo(0.5);
    expect(gone.fade).toBe(1);
  });

  it("leaves the body where it fell", () => {
    expect(combatant(battleResult(), 1_000, "O0").hex).toEqual({ column: 4, row: 3 });
  });
});

describe("the end", () => {
  it("is not reported before it happens", () => {
    expect(frameAt(battleResult(), 999).ended).toBe(false);
    expect(frameAt(battleResult(), 1_000).ended).toBe(true);
  });

  it("runs on past the last event so the last body can fade", () => {
    const result = battleResult();

    expect(playbackLength(result)).toBe(result.durationMilliseconds + FADE_MILLISECONDS);
  });

  it("counts who is still standing without recomputing the battle", () => {
    expect(survivors(battleResult(), "player")).toHaveLength(1);
    expect(survivors(battleResult(), "opponent")).toHaveLength(0);
  });
});

describe("a reserve that never entered", () => {
  const waiting = battleResult({
    combatants: [
      ...battleResult().combatants,
      {
        id: "P1",
        side: "player",
        unitId: "unit-mounted",
        name: "Mounted",
        stats: {
          hp: 100,
          power: 10,
          defense: 5,
          attackIntervalSeconds: 1,
          criticalChance: 0,
          range: 1,
          movementSpeed: 1.4,
        },
        reserveOrder: 0,
        reserveEntryHex: { column: 0, row: 0 },
        endState: "reserve",
        finalHp: 100,
        finalEnergy: 0,
        finalHex: null,
      },
    ],
  });

  it("is alive and off the board for the whole battle", () => {
    const reserve = combatant(waiting, 1_000, "P1");

    expect(reserve.state).toBe("waiting");
    expect(reserve.hex).toBeNull();
    expect(reserve.hp).toBe(100);
    expect(reserve.reserveOrder).toBe(0);
    expect(reserve.reserveEntryHex).toEqual({ column: 0, row: 0 });
  });
});

describe("reading the clock", () => {
  it("says the duration in seconds", () => {
    expect(durationLabel(0)).toBe("0.0s");
    expect(durationLabel(1_500)).toBe("1.5s");
    expect(durationLabel(12_340)).toBe("12.3s");
  });
});
