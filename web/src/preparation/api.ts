import { z } from "zod";
import { readProblem } from "@/api/problem";
import type { AntiforgeryTokens } from "@/auth/session";
import { postJson } from "@/auth/session";

/** Same-origin, like every other call. See `auth/session.ts`. */
export const PREPARATION_URLS = {
  inventory: "/api/inventory/items",
  units: "/api/units",
} as const;

export function equipUrl(unitId: string): string {
  return `${PREPARATION_URLS.units}/${unitId}/equip`;
}

export function unequipUrl(unitId: string): string {
  return `${PREPARATION_URLS.units}/${unitId}/unequip`;
}

const craftsmanshipSchema = z.enum(["common", "rare", "epic"]);

/**
 * Which hands a weapon fills. One number for a 1-slot weapon, both for a 2-slot one — never
 * the same item listed twice.
 */
const slotsSchema = z.array(z.number());

const equippedOnSchema = z.object({
  unitId: z.string(),
  unitName: z.string(),
  slots: slotsSchema,
});

const inventoryItemSchema = z.object({
  id: z.string(),
  name: z.string(),
  weaponType: z.string(),
  craftsmanship: craftsmanshipSchema,
  origin: z.string(),
  forgedAt: z.string(),
  slotCost: z.number().nullable(),
  equippable: z.boolean(),
  equippedOn: equippedOnSchema.nullable(),
});

const unitWeaponSchema = z.object({
  itemId: z.string(),
  name: z.string(),
  weaponType: z.string(),
  craftsmanship: craftsmanshipSchema,
  slots: slotsSchema,
});

/**
 * A unit as the server resolved it through the creator's content.
 *
 * There is no class, specialisation, level or power score, because none of those exist. What
 * the screen has to work with is identity, the structural facts, and the loadout.
 */
const unitSchema = z.object({
  id: z.string(),
  definitionKey: z.string(),
  name: z.string(),
  type: z.enum(["regular", "hero"]),
  kingdom: z.string(),
  tier: z.number(),
  maxArmor: z.enum(["light", "medium", "heavy"]),
  mounted: z.boolean(),
  weaponSlots: z.number(),
  weapons: z.array(unitWeaponSchema),
});

export type InventoryItem = z.infer<typeof inventoryItemSchema>;
export type Unit = z.infer<typeof unitSchema>;
export type UnitWeapon = z.infer<typeof unitWeaponSchema>;

export async function fetchInventory(signal?: AbortSignal): Promise<InventoryItem[]> {
  return z.array(inventoryItemSchema).parse(await getJson(PREPARATION_URLS.inventory, signal));
}

export async function fetchUnits(signal?: AbortSignal): Promise<Unit[]> {
  return z.array(unitSchema).parse(await getJson(PREPARATION_URLS.units, signal));
}

/**
 * Equipping and unequipping answer with the unit as it now stands, so the loadout the player
 * sees is the one the server decided rather than one the browser assembled from what it asked
 * for. The inventory is refetched separately: the item's whereabouts changed too.
 */
export async function postEquip(
  unitId: string,
  body: { itemId: string; slot?: number },
  tokens: AntiforgeryTokens,
  signal?: AbortSignal,
): Promise<Unit> {
  return unitSchema.parse(await postJson(equipUrl(unitId), body, tokens, signal));
}

export async function postUnequip(
  unitId: string,
  body: { itemId: string },
  tokens: AntiforgeryTokens,
  signal?: AbortSignal,
): Promise<Unit> {
  return unitSchema.parse(await postJson(unequipUrl(unitId), body, tokens, signal));
}

async function getJson(url: string, signal?: AbortSignal): Promise<unknown> {
  const response = await fetch(url, { headers: { Accept: "application/json" }, signal });

  if (!response.ok) {
    throw await readProblem(response);
  }

  return response.json();
}
