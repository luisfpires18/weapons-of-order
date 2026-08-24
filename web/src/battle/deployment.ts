import type { Army, ArmyIntent, ArmyUnit } from "@/battle/api";
import type { Hex } from "@/battle/hex";
import { sameHex } from "@/battle/hex";

/**
 * Editing an army, as pure functions over the intent the server is sent.
 *
 * Placing, moving, removing, reserving and reordering all produce a whole army rather than a
 * change to one — which is the shape the endpoint takes, and the reason there is no sequence of
 * taps that can leave a deployment half-moved. It also means every rule here can be checked
 * without a server, a query client or a rendered screen.
 *
 * Nothing here decides anything authoritative. The server validates the same rules again and its
 * answer is what the screen then shows; these exist so the interface can offer a legal action and
 * refuse an illegal one before the player takes it.
 */

/** The army as it currently stands, in the shape a save takes. */
export function intentOf(army: Army): ArmyIntent {
  return {
    active: army.units
      .filter((unit): unit is ArmyUnit & { hex: Hex } => unit.role === "active" && unit.hex !== null)
      .map((unit) => ({ unitId: unit.unitId, column: unit.hex.column, row: unit.hex.row })),
    reserves: army.units
      .filter((unit) => unit.role === "reserve")
      .sort((one, other) => (one.reserveOrder ?? 0) - (other.reserveOrder ?? 0))
      .map((unit) => unit.unitId),
  };
}

/**
 * Puts a Unit on a hex.
 *
 * It leaves wherever it was first, so moving a deployed Unit and deploying a reserve are the same
 * call. A Unit already standing on that hex is displaced to unplaced rather than being quietly
 * overwritten — the hex holds one Unit, and which one is the player's most recent answer.
 */
export function place(intent: ArmyIntent, unitId: string, hex: Hex): ArmyIntent {
  const cleared = withoutUnit(intent, unitId);

  return {
    active: [
      ...cleared.active.filter((placement) => !sameHex(placement, hex)),
      { unitId, column: hex.column, row: hex.row },
    ],
    reserves: cleared.reserves,
  };
}

/** Takes a Unit out of the army entirely, back to the roster. */
export function unplace(intent: ArmyIntent, unitId: string): ArmyIntent {
  return withoutUnit(intent, unitId);
}

/** Moves a Unit to the back of the reserve queue, wherever it was before. */
export function reserve(intent: ArmyIntent, unitId: string): ArmyIntent {
  const cleared = withoutUnit(intent, unitId);

  return { active: cleared.active, reserves: [...cleared.reserves, unitId] };
}

/**
 * Shifts a reserve one place up or down the queue.
 *
 * Queue order decides which reserve is called first and which rear hex it enters through, so it
 * is a real decision rather than a listing preference. A move past either end is a no-op.
 */
export function moveReserve(intent: ArmyIntent, unitId: string, offset: number): ArmyIntent {
  const from = intent.reserves.indexOf(unitId);
  const to = from + offset;

  if (from < 0 || to < 0 || to >= intent.reserves.length) {
    return intent;
  }

  const reserves = [...intent.reserves];
  reserves.splice(from, 1);
  reserves.splice(to, 0, unitId);

  return { active: intent.active, reserves };
}

/** An army with nobody in it. Saving this is how a deployment is cleared. */
export function empty(): ArmyIntent {
  return { active: [], reserves: [] };
}

/** The Unit standing on a hex, if any. */
export function unitAt(army: Army, hex: Hex): ArmyUnit | undefined {
  return army.units.find((unit) => unit.role === "active" && sameHex(unit.hex, hex));
}

/** The Units the player owns and has not put anywhere. */
export function unplaced(army: Army): ArmyUnit[] {
  return army.units.filter((unit) => unit.role === "unplaced");
}

/** The reserve queue, in order. */
export function reserves(army: Army): ArmyUnit[] {
  return army.units
    .filter((unit) => unit.role === "reserve")
    .sort((one, other) => (one.reserveOrder ?? 0) - (other.reserveOrder ?? 0));
}

export function deployed(army: Army): ArmyUnit[] {
  return army.units.filter((unit) => unit.role === "active");
}

/**
 * Why a Unit cannot be deployed right now, or nothing if it can.
 *
 * Returned as a reason rather than a boolean so the interface can say which limit is in the way.
 * A Unit already on the battlefield is always deployable — moving it does not add to the count.
 */
export function deploymentBlocker(army: Army, unitId: string): string | null {
  const unit = army.units.find((entry) => entry.unitId === unitId);

  if (unit?.role === "active") {
    return null;
  }

  return deployed(army).length >= army.limits.active
    ? `Only ${army.limits.active} units may be deployed at once.`
    : null;
}

/** Why a Unit cannot join the reserve queue, or nothing if it can. */
export function reserveBlocker(army: Army, unitId: string): string | null {
  const unit = army.units.find((entry) => entry.unitId === unitId);

  if (unit?.role === "reserve") {
    return null;
  }

  return reserves(army).length >= army.limits.reserve
    ? `Only ${army.limits.reserve} units may wait in reserve.`
    : null;
}

function withoutUnit(intent: ArmyIntent, unitId: string): ArmyIntent {
  return {
    active: intent.active.filter((placement) => placement.unitId !== unitId),
    reserves: intent.reserves.filter((reserved) => reserved !== unitId),
  };
}
