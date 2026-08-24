import type { ForgedItem, ForgeState } from "@/forge/api";
import { FORGE_URLS } from "@/forge/api";
import type { HeatBandName } from "@/forge/heat";

/** The published balance values, as `appsettings.json` has them. */
export const TUNING: ForgeState["tuning"] = {
  maxTemperature: 100,
  workableFrom: 40,
  idealFrom: 65,
  burningFrom: 85,
  heatRatePerSecond: 30,
  coolRatePerSecond: 18,
  burnGraceSeconds: 3,
  strikeCooldownSeconds: 0.35,
};

export const SWORD: ForgeState["recipes"][number] = {
  key: "weapon.sword",
  name: "Sword",
  weaponType: "Sword",
  cost: { metal: 3, wood: 1, leather: 0 },
  affordable: true,
};

type Session = NonNullable<ForgeState["session"]>;

export function forgeSession(overrides: Partial<Session> = {}): Session {
  return {
    id: "session-1",
    recipeKey: SWORD.key,
    name: SWORD.name,
    weaponType: SWORD.weaponType,
    status: "active",
    temperature: 0,
    band: "cold",
    heating: false,
    observedAt: "2026-08-12T09:00:00+00:00",
    strikesTaken: 0,
    strikesRequired: 3,
    strikes: [],
    burnSeconds: 0,
    craftsmanship: null,
    itemId: null,
    ...overrides,
  };
}

export function idleForge(overrides: Partial<ForgeState> = {}): ForgeState {
  return {
    materials: { metal: 24, wood: 12, leather: 8 },
    recipes: [SWORD],
    session: null,
    tuning: TUNING,
    ...overrides,
  };
}

/**
 * A stand-in for the forge API, small enough to read and faithful enough to drive the screen.
 *
 * It exists to exercise the parts of the loop that live in the browser: which control is
 * offered in which state, what a landed blow looks like, that a finished sword reaches the
 * list beside the anvil. It decides nothing the real server decides — the band each blow
 * lands in is scripted by the test, because the point of these assertions is what the screen
 * does with the server's answer, not what the answer should have been. The real rule is
 * proven against a real database in the API tests and against a running stack in the browser
 * sweep.
 */
export function fakeForge(options: { bands?: HeatBandName[]; state?: Partial<ForgeState> } = {}) {
  const bands = options.bands ?? ["ideal", "ideal", "ideal"];
  let state = idleForge(options.state);
  let items: ForgedItem[] = [];
  const calls: { url: string; body: unknown }[] = [];

  const craftsmanshipFor = (landed: HeatBandName[]) => {
    const score = landed.reduce(
      (total, band) => total + (band === "ideal" ? 2 : band === "workable" ? 1 : 0),
      0,
    );
    return score >= 5 ? "epic" : score >= 3 ? "rare" : "common";
  };

  const begin = () => {
    state = {
      ...state,
      materials: {
        metal: state.materials.metal - SWORD.cost.metal,
        wood: state.materials.wood - SWORD.cost.wood,
        leather: state.materials.leather - SWORD.cost.leather,
      },
      session: forgeSession({ observedAt: new Date().toISOString() }),
    };
  };

  const setHeating = (heating: boolean) => {
    if (!state.session) return;

    state = {
      ...state,
      session: {
        ...state.session,
        heating,
        temperature: heating ? state.session.temperature : 0,
        band: heating ? state.session.band : "cold",
        observedAt: new Date().toISOString(),
      },
    };
  };

  const strike = () => {
    const session = state.session;
    if (!session || session.status !== "active") return;

    const ordinal = session.strikesTaken + 1;
    const band = bands[ordinal - 1] ?? "cold";
    const strikes = [...session.strikes, { ordinal, band }];
    const finished = ordinal >= session.strikesRequired;
    const craftsmanship = finished ? craftsmanshipFor(strikes.map((entry) => entry.band)) : null;

    const forged: ForgedItem | null = finished
      ? {
          id: `item-${items.length + 1}`,
          recipeKey: SWORD.key,
          name: SWORD.name,
          weaponType: SWORD.weaponType,
          craftsmanship: craftsmanship ?? "common",
          origin: "ordinaryforge",
          forgedAt: new Date().toISOString(),
        }
      : null;

    if (forged) {
      items = [forged, ...items];
    }

    state = {
      ...state,
      session: {
        ...session,
        status: finished ? "completed" : "active",
        strikesTaken: ordinal,
        strikes,
        craftsmanship,
        itemId: forged?.id ?? null,
        heating: finished ? false : session.heating,
        observedAt: new Date().toISOString(),
      },
    };
  };

  const handle = (url: string, init?: RequestInit): Response | undefined => {
    if (!url.startsWith("/api/forge")) return undefined;

    if (url.endsWith(FORGE_URLS.items)) return json(items);
    if (url.endsWith(FORGE_URLS.state)) return json(state);

    const body = typeof init?.body === "string" ? (JSON.parse(init.body) as unknown) : undefined;
    calls.push({ url, body });

    if (url.endsWith(FORGE_URLS.begin)) begin();
    if (url.endsWith(FORGE_URLS.heat)) setHeating(Boolean((body as { heating?: boolean })?.heating));
    if (url.endsWith(FORGE_URLS.strike)) strike();
    if (url.endsWith(FORGE_URLS.abandon) && state.session) {
      state = { ...state, session: { ...state.session, status: "abandoned", heating: false } };
    }

    return json(state);
  };

  return {
    handle,
    calls,
    get state() {
      return state;
    },
    get items() {
      return items;
    },
  };
}

function json(body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { "Content-Type": "application/json" },
  });
}
