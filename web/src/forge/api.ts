import { z } from "zod";
import { readProblem } from "@/api/problem";
import type { AntiforgeryTokens } from "@/auth/session";
import { postJson } from "@/auth/session";

/** Same-origin, like every other call. See `auth/session.ts`. */
export const FORGE_URLS = {
  state: "/api/forge/state",
  items: "/api/forge/items",
  begin: "/api/forge/begin",
  heat: "/api/forge/heat",
  strike: "/api/forge/strike",
  abandon: "/api/forge/abandon",
} as const;

const bandSchema = z.enum(["cold", "workable", "ideal", "burning"]);

const craftsmanshipSchema = z.enum(["common", "rare", "epic"]);

const materialsSchema = z.object({
  metal: z.number(),
  wood: z.number(),
  leather: z.number(),
});

const recipeSchema = z.object({
  key: z.string(),
  name: z.string(),
  weaponType: z.string(),
  cost: materialsSchema,
  affordable: z.boolean(),
});

const sessionSchema = z.object({
  id: z.string(),
  recipeKey: z.string(),
  name: z.string(),
  weaponType: z.string(),
  status: z.enum(["active", "completed", "ruined", "abandoned"]),
  temperature: z.number(),
  band: bandSchema,
  heating: z.boolean(),
  observedAt: z.string(),
  strikesTaken: z.number(),
  strikesRequired: z.number(),
  strikes: z.array(z.object({ ordinal: z.number(), band: bandSchema })),
  burnSeconds: z.number(),
  craftsmanship: craftsmanshipSchema.nullable(),
  itemId: z.string().nullable(),
});

const tuningSchema = z.object({
  maxTemperature: z.number(),
  workableFrom: z.number(),
  idealFrom: z.number(),
  burningFrom: z.number(),
  heatRatePerSecond: z.number(),
  coolRatePerSecond: z.number(),
  burnGraceSeconds: z.number(),
  strikeCooldownSeconds: z.number(),
});

const forgeStateSchema = z.object({
  materials: materialsSchema,
  recipes: z.array(recipeSchema),
  session: sessionSchema.nullable(),
  tuning: tuningSchema,
});

const forgedItemSchema = z.object({
  id: z.string(),
  recipeKey: z.string(),
  name: z.string(),
  weaponType: z.string(),
  craftsmanship: craftsmanshipSchema,
  origin: z.string(),
  forgedAt: z.string(),
});

export type Materials = z.infer<typeof materialsSchema>;
export type ForgeRecipe = z.infer<typeof recipeSchema>;
export type ForgeSession = z.infer<typeof sessionSchema>;
export type ForgeState = z.infer<typeof forgeStateSchema>;
export type ForgedItem = z.infer<typeof forgedItemSchema>;
export type Craftsmanship = z.infer<typeof craftsmanshipSchema>;

export async function fetchForgeState(signal?: AbortSignal): Promise<ForgeState> {
  return forgeStateSchema.parse(await getJson(FORGE_URLS.state, signal));
}

export async function fetchForgedItems(signal?: AbortSignal): Promise<ForgedItem[]> {
  return z.array(forgedItemSchema).parse(await getJson(FORGE_URLS.items, signal));
}

/**
 * Every forge mutation answers with the whole forge state, so the client never has to guess
 * what an action did — it is told, by the side that decided.
 */
export async function postForgeAction(
  url: string,
  body: unknown,
  tokens: AntiforgeryTokens,
  signal?: AbortSignal,
): Promise<ForgeState> {
  return forgeStateSchema.parse(await postJson(url, body, tokens, signal));
}

async function getJson(url: string, signal?: AbortSignal): Promise<unknown> {
  const response = await fetch(url, { headers: { Accept: "application/json" }, signal });

  if (!response.ok) {
    throw await readProblem(response);
  }

  return response.json();
}
