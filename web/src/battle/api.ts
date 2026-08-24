import { z } from "zod";
import { readProblem } from "@/api/problem";
import type { AntiforgeryTokens } from "@/auth/session";
import { postJson } from "@/auth/session";

/** Same-origin, like every other call. See `auth/session.ts`. */
export const BATTLE_URLS = {
  army: "/api/battle/army",
  simulate: "/api/battle/simulate",
} as const;

const hexSchema = z.object({ column: z.number(), row: z.number() });

const battlefieldSchema = z.object({
  columns: z.number(),
  rows: z.number(),
  deploymentColumns: z.number(),
});

const limitsSchema = z.object({
  active: z.number(),
  reserve: z.number(),
  army: z.number(),
});

/**
 * The six universal combat stats, as the server totalled them.
 *
 * Displayed and never sent. Every number here is the server's answer to what a Unit and its
 * loadout come to, and the browser has no opinion the server would listen to.
 */
const combatStatsSchema = z.object({
  hp: z.number(),
  power: z.number(),
  defense: z.number(),
  attackIntervalSeconds: z.number(),
  criticalChance: z.number(),
  range: z.number(),
  mounted: z.boolean(),
});

const armyWeaponSchema = z.object({
  itemId: z.string(),
  name: z.string(),
  craftsmanship: z.enum(["common", "rare", "epic"]),
});

const armyUnitSchema = z.object({
  unitId: z.string(),
  definitionKey: z.string(),
  name: z.string(),
  kingdom: z.string(),
  tier: z.number(),
  mounted: z.boolean(),
  weapons: z.array(armyWeaponSchema),
  stats: combatStatsSchema,
  role: z.enum(["active", "reserve", "unplaced"]),
  hex: hexSchema.nullable(),
  reserveOrder: z.number().nullable(),
  reserveEntryHex: hexSchema.nullable(),
});

const armySchema = z.object({
  battlefield: battlefieldSchema,
  limits: limitsSchema,
  units: z.array(armyUnitSchema),
  ready: z.boolean(),
});

const combatantSchema = z.object({
  id: z.string(),
  side: z.enum(["player", "opponent"]),
  unitId: z.string().nullable(),
  name: z.string(),
  stats: combatStatsSchema,
  reserveOrder: z.number().nullable(),
  reserveEntryHex: hexSchema.nullable(),
  endState: z.enum(["active", "reserve", "dead"]),
  finalHp: z.number(),
  finalEnergy: z.number(),
  finalHex: hexSchema.nullable(),
});

/**
 * The event log, as a discriminated union on `kind`.
 *
 * Every event carries the simulated time it happened at, and two events sharing a time happened
 * at the same simulation moment. Playback has to present them as one — that is the difference
 * between a mutual last kill reading as a Draw and reading as somebody winning by a frame.
 */
const eventSchema = z.discriminatedUnion("kind", [
  z.object({ kind: z.literal("deployed"), time: z.number(), id: z.string(), hex: hexSchema }),
  z.object({ kind: z.literal("reserve"), time: z.number(), id: z.string(), hex: hexSchema }),
  z.object({
    kind: z.literal("moved"),
    time: z.number(),
    id: z.string(),
    from: hexSchema,
    to: hexSchema,
  }),
  z.object({
    kind: z.literal("attack"),
    time: z.number(),
    attackerId: z.string(),
    targetId: z.string(),
    attack: z.enum(["normal", "heavy"]),
    critical: z.boolean(),
    damage: z.number(),
    targetHp: z.number(),
    attackerEnergy: z.number(),
  }),
  z.object({ kind: z.literal("died"), time: z.number(), id: z.string(), hex: hexSchema }),
  z.object({
    kind: z.literal("ended"),
    time: z.number(),
    outcome: z.enum(["playervictory", "opponentvictory", "draw"]),
    reason: z.enum(["elimination", "mutualelimination", "maximumduration", "noprogress"]),
  }),
]);

const battleResultSchema = z.object({
  outcome: z.enum(["playervictory", "opponentvictory", "draw"]),
  reason: z.enum(["elimination", "mutualelimination", "maximumduration", "noprogress"]),
  durationMilliseconds: z.number(),
  seed: z.string(),
  battlefield: battlefieldSchema,
  combatants: z.array(combatantSchema),
  events: z.array(eventSchema),
});

export type Army = z.infer<typeof armySchema>;
export type ArmyUnit = z.infer<typeof armyUnitSchema>;
export type ArmyLimits = z.infer<typeof limitsSchema>;
export type CombatStats = z.infer<typeof combatStatsSchema>;
export type BattleResult = z.infer<typeof battleResultSchema>;
export type BattleCombatant = z.infer<typeof combatantSchema>;
export type BattleEvent = z.infer<typeof eventSchema>;
export type AttackEvent = Extract<BattleEvent, { kind: "attack" }>;

/**
 * The army as the player wants it.
 *
 * A whole replacement rather than a list of edits, matching the endpoint. Placing, moving,
 * removing and reordering are one shape, and a refused save leaves the saved army untouched.
 */
export type ArmyIntent = {
  active: { unitId: string; column: number; row: number }[];
  reserves: string[];
};

export async function fetchArmy(signal?: AbortSignal): Promise<Army> {
  return armySchema.parse(await getJson(BATTLE_URLS.army, signal));
}

export async function postArmy(
  intent: ArmyIntent,
  tokens: AntiforgeryTokens,
  signal?: AbortSignal,
): Promise<Army> {
  return armySchema.parse(await postJson(BATTLE_URLS.army, intent, tokens, signal));
}

/**
 * Starts a battle.
 *
 * The body is empty on purpose. The army is the one the account has saved, the opposition and
 * the seed are the server's, and there is nothing the browser could send that would be believed.
 */
export async function postSimulate(
  tokens: AntiforgeryTokens,
  signal?: AbortSignal,
): Promise<BattleResult> {
  return battleResultSchema.parse(await postJson(BATTLE_URLS.simulate, {}, tokens, signal));
}

async function getJson(url: string, signal?: AbortSignal): Promise<unknown> {
  const response = await fetch(url, { headers: { Accept: "application/json" }, signal });

  if (!response.ok) {
    throw await readProblem(response);
  }

  return response.json();
}
