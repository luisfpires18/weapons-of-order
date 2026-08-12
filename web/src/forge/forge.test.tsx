// @vitest-environment jsdom
import { cleanup, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import { renderApp, SIGNED_IN, SIGNED_OUT } from "@/testing/renderApp";
import { fakeForge, forgeSession, idleForge, SWORD } from "@/testing/forge";

beforeAll(() => {
  // Pointer capture is how a finger that slides off the heat control keeps holding it. A
  // headless DOM has no pointers to capture, so these stand in for the calls.
  Element.prototype.setPointerCapture ??= () => {};
  Element.prototype.releasePointerCapture ??= () => {};
  Element.prototype.hasPointerCapture ??= () => false;
});

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

/**
 * The gauge animates between server answers by running the published temperature model on
 * every frame. That motion is decoration and its arithmetic is asserted in `heat.test.ts`;
 * here the frame loop is held still so each assertion is about the state the server sent.
 */
function freezeFrames() {
  vi.stubGlobal("requestAnimationFrame", () => 0);
  vi.stubGlobal("cancelAnimationFrame", () => {});
}

function forge(server = fakeForge(), at = "/forge") {
  freezeFrames();
  return { server, ...renderApp(SIGNED_IN, { at, api: server.handle }) };
}

async function beginForge(user: ReturnType<typeof userEvent.setup>) {
  await user.click(await screen.findByRole("button", { name: "Start a sword" }));
  return screen.findByRole("button", { name: "Strike" });
}

describe("reaching the forge", () => {
  it("is offered in the primary navigation and opens from it", async () => {
    freezeFrames();
    const server = fakeForge();
    renderApp(SIGNED_IN, { api: server.handle });
    const user = userEvent.setup();

    const nav = await screen.findByRole("navigation", { name: "Primary" });
    await user.click(within(nav).getByRole("link", { name: "Forge" }));

    expect(await screen.findByRole("heading", { level: 1, name: "Forge" })).toBeTruthy();
    expect(within(nav).getByRole("link", { name: "Forge" }).getAttribute("aria-current")).toBe("page");
  });

  it("sends a visitor without a session to sign in, carrying where they were going", async () => {
    freezeFrames();
    renderApp(SIGNED_OUT, { at: "/forge", api: fakeForge().handle });

    await waitFor(() =>
      expect(screen.getByTestId("location").textContent).toBe("/login?next=%2Fforge"),
    );
    expect(screen.queryByRole("heading", { level: 1, name: "Forge" })).toBeNull();
  });
});

describe("an empty anvil", () => {
  it("shows the stock, the recipe and its cost before anything is started", async () => {
    forge();

    expect(await screen.findByRole("button", { name: "Start a sword" })).toBeTruthy();
    expect(screen.getByRole("heading", { level: 1, name: "Forge" })).toBeTruthy();
    expect(screen.getByText("Costs 3 Metal and 1 Wood.")).toBeTruthy();

    // Metal, Wood, Leather, in that order.
    expect(screen.getAllByRole("definition").map((amount) => amount.textContent)).toEqual([
      "24",
      "12",
      "8",
    ]);

    expect(await screen.findByText(/Nothing forged yet/)).toBeTruthy();
  });

  it("reads the workpiece as cold iron", async () => {
    forge();

    const gauge = await screen.findByRole("meter", { name: "Workpiece temperature" });
    expect(gauge.getAttribute("aria-valuenow")).toBe("0");
    expect(gauge.getAttribute("aria-valuetext")).toBe("Cold");
  });

  it("refuses to start what the player cannot pay for, and says why", async () => {
    forge(
      fakeForge({
        state: {
          materials: { metal: 0, wood: 0, leather: 0 },
          recipes: [{ ...SWORD, affordable: false }],
        },
      }),
    );

    const start = await screen.findByRole("button", { name: "Start a sword" });
    expect(start.hasAttribute("disabled")).toBe(true);
    expect(screen.getByText(/You need 3 Metal and 1 Wood and do not have it/)).toBeTruthy();
  });
});

describe("working the iron", () => {
  it("charges the recipe and puts the anvil controls in reach", async () => {
    const { server } = forge();
    const user = userEvent.setup();

    await beginForge(user);

    expect(server.state.session?.status).toBe("active");
    expect(screen.getByRole("button", { name: "Hold to heat" })).toBeTruthy();
    expect(screen.getAllByRole("definition").map((amount) => amount.textContent)).toEqual([
      "21",
      "11",
      "8",
    ]);

    // Three blows are asked for, and none has landed.
    const blows = within(screen.getByRole("list", { name: "Blows" }));
    expect(blows.getAllByText("—")).toHaveLength(3);
  });

  it("holds the iron in the fire while the control is held, and lets go on release", async () => {
    const { server } = forge();
    const user = userEvent.setup();

    await beginForge(user);
    const heat = screen.getByRole("button", { name: "Hold to heat" });

    await user.pointer({ keys: "[MouseLeft>]", target: heat });
    await waitFor(() =>
      expect(screen.getByRole("button", { name: "In the fire" }).getAttribute("aria-pressed")).toBe(
        "true",
      ),
    );

    await user.pointer({ keys: "[/MouseLeft]", target: heat });
    await waitFor(() => expect(server.state.session?.heating).toBe(false));

    // A press and its release, in that order, and nothing in between.
    expect(server.calls.filter((call) => call.url.endsWith("/api/forge/heat"))).toEqual([
      { url: "/api/forge/heat", body: { heating: true } },
      { url: "/api/forge/heat", body: { heating: false } },
    ]);
  });

  it("holds the iron from the keyboard too, without repeating on key repeat", async () => {
    const { server } = forge();
    const user = userEvent.setup();

    await beginForge(user);
    const heat = screen.getByRole("button", { name: "Hold to heat" });
    heat.focus();

    // Held down, with the three repeat events a held key actually produces.
    await user.keyboard("[Space>3]");
    await waitFor(() => expect(server.state.session?.heating).toBe(true));

    await user.keyboard("[/Space]");
    await waitFor(() => expect(server.state.session?.heating).toBe(false));

    expect(server.calls.filter((call) => call.url.endsWith("/api/forge/heat"))).toEqual([
      { url: "/api/forge/heat", body: { heating: true } },
      { url: "/api/forge/heat", body: { heating: false } },
    ]);
  });

  it("reports the band each blow landed in", async () => {
    forge(fakeForge({ bands: ["ideal", "workable", "cold"] }));
    const user = userEvent.setup();

    const strike = await beginForge(user);

    await user.click(strike);
    await waitFor(() =>
      expect(screen.getByRole("status").textContent).toBe("Ideal strike, blow 1 of 3"),
    );
    expect(within(screen.getByRole("list", { name: "Blows" })).getByText("Ideal")).toBeTruthy();

    await user.click(strike);
    await waitFor(() =>
      expect(screen.getByRole("status").textContent).toBe("Workable strike, blow 2 of 3"),
    );
  });
});

describe("what comes off the anvil", () => {
  it("names the craftsmanship and adds the sword to the player's work", async () => {
    const { server } = forge(fakeForge({ bands: ["ideal", "ideal", "ideal"] }));
    const user = userEvent.setup();

    const strike = await beginForge(user);
    await user.click(strike);
    await user.click(await screen.findByRole("button", { name: "Strike" }));
    await user.click(await screen.findByRole("button", { name: "Strike" }));

    expect(await screen.findByText("Finished")).toBeTruthy();
    expect(server.items).toHaveLength(1);

    const work = await screen.findByRole("list", { name: "Forged items" });
    expect(within(work).getByText("Epic")).toBeTruthy();
    expect(within(work).getByText(/Sword/)).toBeTruthy();

    expect(screen.getByRole("button", { name: "Start another sword" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Strike" })).toBeNull();
  });

  it("gives poor timing a poorer sword rather than no sword", async () => {
    forge(fakeForge({ bands: ["cold", "cold", "cold"] }));
    const user = userEvent.setup();

    const strike = await beginForge(user);
    await user.click(strike);
    await user.click(await screen.findByRole("button", { name: "Strike" }));
    await user.click(await screen.findByRole("button", { name: "Strike" }));

    const work = await screen.findByRole("list", { name: "Forged items" });
    expect(within(work).getByText("Common")).toBeTruthy();
  });
});

describe("coming back to the forge", () => {
  it("resumes the workpiece the server still has, without paying for it again", async () => {
    const server = fakeForge();
    // The player started this in an earlier visit and struck it once.
    server.handle("/api/forge/begin", { method: "POST", body: "{}" });
    server.handle("/api/forge/strike", { method: "POST", body: "{}" });

    forge(server);

    expect(await screen.findByRole("button", { name: "Strike" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Start a sword" })).toBeNull();
    // Charged once, by the visit that started it.
    expect(screen.getAllByRole("definition").map((amount) => amount.textContent)).toEqual([
      "21",
      "11",
      "8",
    ]);
    expect(within(screen.getByRole("list", { name: "Blows" })).getByText("Ideal")).toBeTruthy();
  });

  it("shows a workpiece that burned through, and offers a fresh one", async () => {
    forge(
      fakeForge({
        state: {
          session: forgeSession({ status: "ruined", temperature: 100, band: "burning", burnSeconds: 3.4 }),
        },
      }),
    );

    expect(await screen.findByText("Burned through")).toBeTruthy();
    expect(screen.getByText(/Its materials are\s+spent/)).toBeTruthy();
    expect(screen.getByRole("button", { name: "Start another sword" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Strike" })).toBeNull();
  });

  it("treats a workpiece that was set aside as an empty anvil", async () => {
    forge(fakeForge({ state: { session: forgeSession({ status: "abandoned" }) } }));

    expect(await screen.findByRole("button", { name: "Start a sword" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Strike" })).toBeNull();
  });
});

describe("when the forge cannot be read", () => {
  it("says so and offers to try again rather than showing an empty anvil", async () => {
    freezeFrames();
    renderApp(SIGNED_IN, {
      at: "/forge",
      api: (url) =>
        url.startsWith("/api/forge")
          ? new Response(JSON.stringify({ title: "Nope.", detail: "The forge is out.", code: "x" }), {
              status: 500,
              headers: { "Content-Type": "application/problem+json" },
            })
          : undefined,
    });

    expect(await screen.findByRole("alert")).toHaveProperty("textContent", "The forge is out.");
    expect(screen.getByRole("button", { name: "Try again" })).toBeTruthy();
  });
});

describe("the forge state contract", () => {
  it("describes an idle forge with no workpiece", () => {
    expect(idleForge().session).toBeNull();
    expect(idleForge().recipes.at(0)?.key).toBe("weapon.sword");
  });
});
