import type { AttackEvent, BattleCombatant, BattleResult } from "@/battle/api";
import type { Hex } from "@/battle/hex";

/**
 * The battle at one moment, reconstructed from the server's event log.
 *
 * A pure function of the log and a timestamp — no state carried between frames, no clock of its
 * own, and above all no combat rules. It reads what the server already decided and works out
 * where to draw it; it never computes a target, a damage number or a winner. That is what makes
 * playback a renderer rather than a second simulator with its own opinions.
 *
 * Being pure also makes it the part of the client that can be tested without a canvas, which is
 * why the Pixi stage below it holds no state that is not in here.
 */

/** How long a step between hexes takes to draw. A pace, not a simulated duration. */
export const STEP_MILLISECONDS = 260;

/** How long a blow stays visible after it lands. */
export const FLASH_MILLISECONDS = 300;

/** How long a body lingers before it is gone. */
export const FADE_MILLISECONDS = 600;

export type CombatantFrame = {
  id: string;
  side: "player" | "opponent";
  name: string;
  maxHp: number;
  hp: number;
  energy: number;
  /** Where it stands, or where it fell. Null while it has never reached the battlefield. */
  hex: Hex | null;
  /** The hex it is stepping away from, while a step is still being drawn. */
  from: Hex | null;
  /** How far through that step it is, from 0 to 1. */
  step: number;
  state: "waiting" | "active" | "dead";
  /** How far through its fade a body is, from 0 to 1, or null while alive. */
  fade: number | null;
  /** How recently it landed a blow, from 1 down to 0, or null. */
  striking: number | null;
  /** How recently it took one. */
  struck: number | null;
  /** Queue position while it waits off-board. */
  reserveOrder: number | null;
  reserveEntryHex: Hex | null;
};

export type BattleFrame = {
  time: number;
  combatants: CombatantFrame[];
  /** Blows still being drawn, so the stage can strike a line between attacker and target. */
  strikes: AttackEvent[];
  /** Whether the battle has ended by this moment. */
  ended: boolean;
};

/** How long playback runs for: the battle, plus a moment to see how it ended. */
export function playbackLength(result: BattleResult): number {
  return result.durationMilliseconds + FADE_MILLISECONDS;
}

/**
 * The battle as it stood at <code>time</code>.
 *
 * A fold over every event up to that moment. Deliberately not incremental: a scrub backwards, a
 * replay from the beginning and an ordinary frame all go through the same path, so there is no
 * second code path that can disagree with the first about what happened.
 */
export function frameAt(result: BattleResult, time: number): BattleFrame {
  const combatants = new Map<string, CombatantFrame>(
    result.combatants.map((combatant) => [combatant.id, initial(combatant)]),
  );

  const strikes: AttackEvent[] = [];
  let ended = false;

  for (const moment of result.events) {
    if (moment.time > time) {
      break;
    }

    const age = time - moment.time;

    switch (moment.kind) {
      case "deployed":
      case "reserve": {
        const combatant = combatants.get(moment.id);
        if (combatant) {
          combatant.hex = moment.hex;
          combatant.from = null;
          combatant.step = 1;
          combatant.state = "active";
        }
        break;
      }

      case "moved": {
        const combatant = combatants.get(moment.id);
        if (combatant) {
          combatant.hex = moment.to;
          // Only the step still in flight is drawn as a step. An older one has arrived, and
          // carrying it would smear a Unit across the board on a scrub.
          combatant.from = age < STEP_MILLISECONDS ? moment.from : null;
          combatant.step = age < STEP_MILLISECONDS ? age / STEP_MILLISECONDS : 1;
        }
        break;
      }

      case "attack": {
        const attacker = combatants.get(moment.attackerId);
        const target = combatants.get(moment.targetId);

        if (attacker) {
          attacker.energy = moment.attackerEnergy;
          attacker.striking = fresh(age, FLASH_MILLISECONDS);
        }

        if (target) {
          // The server's number, not a subtraction. The client is told the remaining HP so it
          // never has to agree with the damage pipeline about anything.
          target.hp = moment.targetHp;
          target.struck = fresh(age, FLASH_MILLISECONDS);
        }

        if (age < FLASH_MILLISECONDS) {
          strikes.push(moment);
        }
        break;
      }

      case "died": {
        const combatant = combatants.get(moment.id);
        if (combatant) {
          combatant.hp = 0;
          combatant.hex = moment.hex;
          combatant.from = null;
          combatant.step = 1;
          combatant.state = "dead";
          combatant.fade = Math.min(1, age / FADE_MILLISECONDS);
        }
        break;
      }

      case "ended":
        ended = true;
        break;
    }
  }

  return { time, combatants: [...combatants.values()], strikes, ended };
}

/** A combatant before anything has happened to it. */
function initial(combatant: BattleCombatant): CombatantFrame {
  return {
    id: combatant.id,
    side: combatant.side,
    name: combatant.name,
    maxHp: combatant.stats.hp,
    hp: combatant.stats.hp,

    // Energy starts at nothing for everybody. Canon has one bar and it begins empty.
    energy: 0,

    hex: null,
    from: null,
    step: 1,
    state: "waiting",
    fade: null,
    striking: null,
    struck: null,
    reserveOrder: combatant.reserveOrder,
    reserveEntryHex: combatant.reserveEntryHex,
  };
}

/** How recent something is, from 1 at the instant it happened down to 0, or null once stale. */
function fresh(age: number, window: number): number | null {
  return age < window ? 1 - age / window : null;
}

/** What to call an outcome, from the player's side of it. */
export const OUTCOME_LABELS: Record<BattleResult["outcome"], string> = {
  playervictory: "Victory",
  opponentvictory: "Defeat",
  draw: "Draw",
};

/**
 * Why the battle stopped, in words.
 *
 * A guard expiry is reported as what it is rather than dressed up as a result. A player who
 * fought to a standstill should be told that is what happened.
 */
export const REASON_LABELS: Record<BattleResult["reason"], string> = {
  elimination: "One army was wiped out.",
  mutualelimination: "Both armies fell in the same instant.",
  maximumduration: "The battle ran to its time limit without either army falling.",
  noprogress: "Neither army could reach the other, and the battle went nowhere.",
};

/** The battle's length, as a reader would say it. */
export function durationLabel(milliseconds: number): string {
  const seconds = milliseconds / 1000;

  return `${seconds.toFixed(1)}s`;
}

/** How many of a side's Units are still alive at the end.  */
export function survivors(result: BattleResult, side: "player" | "opponent"): BattleCombatant[] {
  return result.combatants.filter(
    (combatant) => combatant.side === side && combatant.endState !== "dead",
  );
}
