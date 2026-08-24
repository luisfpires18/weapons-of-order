// @vitest-environment jsdom
import { cleanup, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ANTIFORGERY_HEADER } from "@/auth/session";
import { army, armyUnit, battleResult, fakeBattleApi, starterArmy } from "@/testing/battle";
import { fakeForge } from "@/testing/forge";
import { fakePreparation } from "@/testing/preparation";
import type { ApiStub } from "@/testing/renderApp";
import { renderApp, SIGNED_IN, SIGNED_OUT } from "@/testing/renderApp";

/**
 * The battle screen as a player uses it.
 *
 * Deployment is a form and playback is a canvas, so what is asserted here is the form and the
 * readable half of the playback — which control is offered in which state, that a tap sends the
 * army the player meant, and that a returned log becomes a result they can read and replay.
 *
 * Nothing here checks a combat rule. The rules live in the simulator and are tested against
 * nothing but themselves; what the browser is responsible for is asking correctly and drawing the
 * answer.
 */

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

beforeEach(() => {
  // jsdom has neither, and the stage asks for both. Neither is load-bearing for these assertions:
  // the canvas never comes up in a headless DOM, which is exactly the state the stage is built to
  // survive.
  vi.stubGlobal(
    "ResizeObserver",
    class {
      observe() {}
      unobserve() {}
      disconnect() {}
    },
  );

  vi.stubGlobal("requestAnimationFrame", (callback: FrameRequestCallback) =>
    setTimeout(() => callback(performance.now()), 16),
  );

  vi.stubGlobal("cancelAnimationFrame", (handle: number) => clearTimeout(handle));

  // jsdom has no canvas implementation and logs a "not implemented" notice for every call. The
  // stage already treats a missing renderer as an ordinary state; this only keeps the test output
  // readable.
  vi.spyOn(HTMLCanvasElement.prototype, "getContext").mockReturnValue(null);
});

function currentPath() {
  return screen.getByTestId("location").textContent;
}

/**
 * The shell reads the forge and preparation endpoints too, so all three stubs are mounted and the
 * first to recognise a URL answers it.
 */
function stack(battle: { handle: ApiStub }): ApiStub {
  const forge = fakeForge();
  const preparation = fakePreparation();

  return (url, init) => battle.handle(url, init) ?? preparation.handle(url, init) ?? forge.handle(url, init);
}

/** Opens the battle screen and waits for the board rather than the heading. */
async function openBattle(api: { handle: ApiStub }) {
  renderApp(SIGNED_IN, { at: "/battle", api: stack(api) });
  await screen.findByRole("group", { name: /Battlefield/ });

  return userEvent.setup();
}

/** One hex, by the name a screen reader would use for it. */
function hex(column: number, row: number) {
  return screen.getByRole("button", { name: new RegExp(`^column ${column + 1}, row ${row + 1}`) });
}

describe("reaching the battle", () => {
  it("requires a session", async () => {
    renderApp(SIGNED_OUT, { at: "/battle", api: stack(fakeBattleApi()) });

    await waitFor(() => expect(currentPath()).toBe("/login?next=%2Fbattle"));
  });

  it("is in the navigation", async () => {
    await openBattle(fakeBattleApi());

    const navigation = screen.getByRole("navigation", { name: "Primary" });

    expect(within(navigation).getAllByRole("link", { name: "Battle" }).length).toBeGreaterThan(0);
  });

  it("draws the whole battlefield and says which half is the player's", async () => {
    await openBattle(fakeBattleApi());

    const board = screen.getByRole("group", { name: /Battlefield/ });

    expect(board.getAttribute("aria-label")).toContain("8 columns by 7 rows");
    expect(board.getAttribute("aria-label")).toContain("first 4 columns");

    // Only the player's half is interactive: 4 columns of 7 rows.
    expect(within(board).getAllByRole("button")).toHaveLength(28);
  });
});

describe("deploying", () => {
  it("puts the chosen unit on the chosen hex", async () => {
    const api = fakeBattleApi();
    const user = await openBattle(api);

    await user.click(screen.getByRole("button", { name: "Melee" }));
    await user.click(hex(2, 3));

    await waitFor(() => expect(api.army.units[0]!.role).toBe("active"));
    expect(api.saves.at(-1)).toEqual({
      active: [{ unitId: "unit-melee", column: 2, row: 3 }],
      reserves: [],
    });

    await screen.findByRole("button", { name: /^column 3, row 4, Melee/ });
  });

  it("sends the antiforgery token the session handed out", async () => {
    const api = fakeBattleApi();
    const { fetchMock } = renderApp(SIGNED_IN, { at: "/battle", api: stack(api) });
    await screen.findByRole("group", { name: /Battlefield/ });
    const user = userEvent.setup();

    await user.click(screen.getByRole("button", { name: "Melee" }));
    await user.click(hex(0, 0));

    await waitFor(() => expect(api.saves).toHaveLength(1));

    const save = fetchMock.mock.calls.find(
      ([, init]) => init?.method === "POST" && String(init.body).includes("unit-melee"),
    );

    expect((save?.[1]?.headers as Record<string, string>)[ANTIFORGERY_HEADER]).toBe("token-1");
  });

  it("moves a unit already on the battlefield rather than cloning it", async () => {
    const api = fakeBattleApi();
    const user = await openBattle(api);

    await user.click(screen.getByRole("button", { name: "Melee" }));
    await user.click(hex(2, 3));
    await screen.findByRole("button", { name: /^column 3, row 4, Melee/ });

    await user.click(screen.getByRole("button", { name: /^column 3, row 4, Melee/ }));
    await user.click(hex(0, 6));

    await waitFor(() =>
      expect(api.saves.at(-1)).toEqual({
        active: [{ unitId: "unit-melee", column: 0, row: 6 }],
        reserves: [],
      }),
    );
  });

  it("shows the server's own stats for the selected unit", async () => {
    const user = await openBattle(fakeBattleApi());

    await user.click(screen.getByRole("button", { name: "Melee" }));

    const panel = screen.getByRole("region", { name: "Selected unit" });

    expect(within(panel).getByText("240")).toBeTruthy();
    expect(within(panel).getByText("1.50s")).toBeTruthy();
    expect(within(panel).getByText("8%")).toBeTruthy();
  });

  it("moves a unit into the reserve queue and says where it will enter", async () => {
    const api = fakeBattleApi();
    const user = await openBattle(api);

    await user.click(screen.getByRole("button", { name: "Mounted" }));
    await user.click(screen.getByRole("button", { name: "To reserve" }));

    await waitFor(() => expect(api.saves.at(-1)?.reserves).toEqual(["unit-mounted"]));

    const queue = await screen.findByRole("list", { name: "Reserve queue" });
    expect(within(queue).getAllByRole("listitem")).toHaveLength(1);

    // The rear column of the player's half, marked on the board before the battle rather than
    // discovered during it.
    await screen.findByRole("button", { name: /reserve entry for Mounted/ });
  });

  it("reorders the reserve queue, because the order decides who is called first", async () => {
    const api = fakeBattleApi();
    const user = await openBattle(api);

    await user.click(screen.getByRole("button", { name: "Mounted" }));
    await user.click(screen.getByRole("button", { name: "To reserve" }));
    await waitFor(() => expect(api.army.units[2]!.role).toBe("reserve"));

    await user.click(screen.getByRole("button", { name: "Ranged" }));
    await user.click(screen.getByRole("button", { name: "To reserve" }));
    await waitFor(() => expect(api.saves.at(-1)?.reserves).toEqual(["unit-mounted", "unit-ranged"]));

    await user.click(
      screen.getByRole("button", { name: "Move Ranged earlier in the reserve queue" }),
    );

    await waitFor(() => expect(api.saves.at(-1)?.reserves).toEqual(["unit-ranged", "unit-mounted"]));
  });

  it("clears the whole deployment in one action", async () => {
    const api = fakeBattleApi();
    const user = await openBattle(api);

    await user.click(screen.getByRole("button", { name: "Melee" }));
    await user.click(hex(1, 1));
    await waitFor(() => expect(api.army.ready).toBe(true));

    await user.click(screen.getByRole("button", { name: "Clear deployment" }));

    await waitFor(() => expect(api.saves.at(-1)).toEqual({ active: [], reserves: [] }));
  });

  it("will not start a battle with nobody deployed", async () => {
    await openBattle(fakeBattleApi());

    const begin = screen.getByRole("button", { name: "Begin battle" });

    expect(begin.hasAttribute("disabled")).toBe(true);
    expect(screen.getByText(/at least one unit on the battlefield/i)).toBeTruthy();
  });

  it("offers the battle once somebody is standing on the board", async () => {
    const deployed = army([
      armyUnit({ unitId: "unit-melee", name: "Melee", role: "active", hex: { column: 3, row: 3 } }),
    ]);

    await openBattle(fakeBattleApi({ army: deployed }));

    expect(screen.getByRole("button", { name: "Begin battle" }).hasAttribute("disabled")).toBe(false);
  });

  it("keeps the deployment the server holds after a reload", async () => {
    const deployed = army([
      armyUnit({ unitId: "unit-melee", name: "Melee", role: "active", hex: { column: 1, row: 5 } }),
      armyUnit({ unitId: "unit-mounted", name: "Mounted", role: "reserve", reserveOrder: 0 }),
    ]);

    await openBattle(fakeBattleApi({ army: deployed }));

    // No save was needed to see it: the army is the server's, not something the screen assembled.
    await screen.findByRole("button", { name: /^column 2, row 6, Melee/ });
    expect(within(screen.getByRole("list", { name: "Reserve queue" })).getAllByRole("listitem")).toHaveLength(1);
  });

  it("says which limit is in the way rather than only refusing", async () => {
    const full = army([
      ...Array.from({ length: 8 }, (_, index) =>
        armyUnit({
          unitId: `active-${index}`,
          name: `Unit ${index}`,
          role: "active",
          hex: { column: index % 4, row: Math.floor(index / 4) },
        }),
      ),
      armyUnit({ unitId: "spare", name: "Spare" }),
    ]);

    const user = await openBattle(fakeBattleApi({ army: full }));

    await user.click(screen.getByRole("button", { name: "Spare" }));

    expect(screen.getByText(/Only 8 units may be deployed at once/)).toBeTruthy();
  });
});

describe("watching the battle", () => {
  const deployed = army([
    armyUnit({ unitId: "unit-melee", name: "Melee", role: "active", hex: { column: 3, row: 3 } }),
  ]);

  async function fight(api = fakeBattleApi({ army: deployed })) {
    const user = await openBattle(api);
    await user.click(screen.getByRole("button", { name: "Begin battle" }));
    await screen.findByRole("list", { name: "Your army" });

    return { user, api };
  }

  it("asks the server to resolve it and draws what came back", async () => {
    const { api } = await fight();

    expect(api.battles).toBe(1);

    // Both armies are readable as text beside the board, so the battle does not depend on a canvas.
    const mine = screen.getByRole("list", { name: "Your army" });
    const theirs = screen.getByRole("list", { name: "Opposition" });

    expect(within(mine).getByText("Melee")).toBeTruthy();
    expect(within(theirs).getByText("Opponent 1")).toBeTruthy();
  });

  it("offers play, replay and a speed, and nothing that would edit the battle", async () => {
    await fight();

    expect(screen.getByRole("button", { name: "Pause" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Replay" })).toBeTruthy();

    const speeds = screen.getByRole("group", { name: "Playback speed" });
    expect(within(speeds).getAllByRole("button").map((button) => button.textContent)).toEqual([
      "1×",
      "2×",
      "4×",
    ]);
  });

  it("pauses and resumes", async () => {
    const { user } = await fight();

    await user.click(screen.getByRole("button", { name: "Pause" }));
    expect(screen.getByRole("button", { name: "Play" })).toBeTruthy();

    await user.click(screen.getByRole("button", { name: "Play" }));
    expect(screen.getByRole("button", { name: "Pause" })).toBeTruthy();
  });

  it("changes the playback rate without asking the server for anything", async () => {
    const { user, api } = await fight();

    await user.click(within(screen.getByRole("group", { name: "Playback speed" })).getByText("4×"));

    expect(
      screen.getByRole("button", { name: "4×" }).getAttribute("aria-pressed"),
    ).toBe("true");
    expect(api.battles).toBe(1);
  });

  it("reaches the result, and says why the battle ended", async () => {
    await fight();

    const result = await screen.findByRole("status", { name: "Battle result" }, { timeout: 5_000 });

    expect(within(result).getByRole("heading", { name: "Victory" })).toBeTruthy();
    expect(result.textContent).toContain("One army was wiped out.");
    expect(result.textContent).toContain("1 of 1 of your units still standing");
  });

  it("replays the same battle rather than fighting a new one", async () => {
    const { user, api } = await fight();

    await screen.findByRole("status", { name: "Battle result" }, { timeout: 5_000 });
    await user.click(screen.getByRole("button", { name: "Replay" }));

    expect(api.battles).toBe(1);
    expect(screen.getByRole("button", { name: "Pause" })).toBeTruthy();
  });

  it("goes back to deployment when the player is done", async () => {
    const { user } = await fight();

    await screen.findByRole("status", { name: "Battle result" }, { timeout: 5_000 });
    await user.click(
      within(screen.getByRole("status", { name: "Battle result" })).getByRole("button", {
        name: "Back to deployment",
      }),
    );

    await screen.findByRole("group", { name: /Battlefield/ });
  });

  it("reports a guard draw as what it was rather than dressing it up", async () => {
    const stalemate = battleResult({
      outcome: "draw",
      reason: "noprogress",
      events: [
        { kind: "deployed", time: 0, id: "P0", hex: { column: 3, row: 3 } },
        { kind: "deployed", time: 0, id: "O0", hex: { column: 4, row: 3 } },
        { kind: "ended", time: 400, outcome: "draw", reason: "noprogress" },
      ],
      durationMilliseconds: 400,
    });

    await fight(fakeBattleApi({ army: deployed, result: stalemate }));

    const result = await screen.findByRole("status", { name: "Battle result" }, { timeout: 5_000 });

    expect(within(result).getByRole("heading", { name: "Draw" })).toBeTruthy();
    expect(result.textContent).toContain("Neither army could reach the other");
  });
});

describe("when something goes wrong", () => {
  it("shows the loading state before the army has been read", async () => {
    renderApp(SIGNED_IN, {
      at: "/battle",
      // Held open, so the screen never gets past pending.
      api: stack({ handle: (url) => (url.startsWith("/api/battle") ? new Promise(() => {}) as never : undefined) }),
    });

    expect(await screen.findByText("Mustering your army")).toBeTruthy();
    expect(screen.queryByRole("group", { name: /Battlefield/ })).toBeNull();
  });

  it("shows the server's own message and offers to try again", async () => {
    let attempts = 0;

    const api: ApiStub = (url) => {
      if (!url.startsWith("/api/battle")) return undefined;
      attempts++;

      return attempts === 1
        ? new Response(JSON.stringify({ detail: "Your army could not be mustered.", code: "boom" }), {
            status: 500,
            headers: { "Content-Type": "application/json" },
          })
        : new Response(JSON.stringify(starterArmy()), {
            status: 200,
            headers: { "Content-Type": "application/json" },
          });
    };

    renderApp(SIGNED_IN, { at: "/battle", api: stack({ handle: api }) });

    expect(await screen.findByText("Your army could not be mustered.")).toBeTruthy();

    await userEvent.setup().click(screen.getByRole("button", { name: "Try again" }));

    await screen.findByRole("group", { name: /Battlefield/ });
  });
});
