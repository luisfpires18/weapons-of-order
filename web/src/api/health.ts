import { z } from "zod";

/**
 * Same-origin by design: Vite proxies `/api` to the ASP.NET Core host in development,
 * and deployed environments serve the client and the API from one origin. There is no
 * API base-URL configuration to get wrong.
 */
export const HEALTH_URL = "/api/health";

const healthSchema = z.object({
  service: z.string(),
  status: z.string(),
  checks: z.record(z.string(), z.string()),
});

export type Health = z.infer<typeof healthSchema>;

/** Confirms the client is talking to a Weapons of Order API. */
export async function fetchHealth(signal?: AbortSignal): Promise<Health> {
  const response = await fetch(HEALTH_URL, {
    headers: { Accept: "application/json" },
    signal,
  });

  if (!response.ok) {
    throw new Error(`Health request failed: ${response.status}`);
  }

  return healthSchema.parse(await response.json());
}
