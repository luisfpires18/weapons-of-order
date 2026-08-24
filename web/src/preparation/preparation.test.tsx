// @vitest-environment jsdom
import { cleanup, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ANTIFORGERY_HEADER } from "@/auth/session";
import { fakeForge } from "@/testing/forge";
import { fakePreparation, STARTER_UNITS, sword } from "@/testing/preparation";
import type { ApiStub } from "@/testing/renderApp";
import { renderApp, SIGNED_IN, SIGNED_OUT } from "@/testing/renderApp";

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

function currentPath() {
  return screen.getByTestId("location").textContent;
}

/**
 * The preparation screens read the forge's own endpoints too, through the shell — so both
 * stubs are always mounted and the first one to recognise a URL answers it.
 */
function stack(preparation: { handle: ApiStub }): ApiStub {
  const forge = fakeForge();
  return (url, init) => preparation.handle(url, init) ?? forge.handle(url, init);
}

/**
 * Renders the units screen and waits for the roster rather than for the heading: the loading
 * state carries the same heading, so waiting on that would assert against a screen that has
 * not read anything yet.
 */
async function openUnits(api: ApiStub, at = "/units") {
  renderApp(SIGNED_IN, { at, api });
  await screen.findByRole("list", { name: "Your units" });
  return userEvent.setup();
}

describe("inventory", () => {
  it("requires a session", async () => {
    renderApp(SIGNED_OUT, { at: "/inventory", api: stack(fakePreparation()) });

    await waitFor(() => expect(currentPath()).toBe("/login?next=%2Finventory"));
  });

  it("says plainly that nothing is owned yet", async () => {
    renderApp(SIGNED_IN, { at: "/inventory", api: stack(fakePreparation()) });

    expect(await screen.findByText(/You own nothing yet/)).toBeTruthy();
    expect(screen.getByRole("heading", { level: 1, name: "Inventory" })).toBeTruthy();
    expect(screen.queryByRole("list", { name: "Owned items" })).toBeNull();
  });

  it("lists a forged sword with the craftsmanship and provenance it was made with", async () => {
    const api = stack(fakePreparation({ items: [sword({ craftsmanship: "rare" })] }));
    renderApp(SIGNED_IN, { at: "/inventory", api });

    const items = await screen.findByRole("list", { name: "Owned items" });
    const row = within(items).getAllByRole("listitem")[0]!;

    expect(within(row).getByText("Rare")).toBeTruthy();
    expect(row.textContent).toContain("Sword");
    expect(row.textContent).toContain("Ordinary forge");
    expect(row.textContent).toContain("In your pack");
  });

  it("reports which unit is holding an item and in which hand", async () => {
    const preparation = fakePreparation({ items: [sword()] });
    preparation.handle("/api/units/unit-ranged/equip", { body: JSON.stringify({ itemId: "item-sword-1", slot: 2 }) });

    renderApp(SIGNED_IN, { at: "/inventory", api: stack(preparation) });

    const items = await screen.findByRole("list", { name: "Owned items" });
    const row = within(items).getAllByRole("listitem")[0]!;

    expect(row.textContent).toContain("Ranged");
    expect(row.textContent).toContain("Second hand");
    expect(row.textContent).not.toContain("In your pack");
  });

  it("shows the failure rather than an empty pack when the inventory cannot be read", async () => {
    renderApp(SIGNED_IN, {
      at: "/inventory",
      api: () => new Response(JSON.stringify({ detail: "Nope.", code: "boom" }), { status: 500 }),
    });

    expect(await screen.findByText("Nope.")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Try again" })).toBeTruthy();
  });
});

describe("units", () => {
  it("requires a session", async () => {
    renderApp(SIGNED_OUT, { at: "/units", api: stack(fakePreparation()) });

    await waitFor(() => expect(currentPath()).toBe("/login?next=%2Funits"));
  });

  it("shows the three configured units and their mounted state", async () => {
    const user = await openUnits(stack(fakePreparation()));

    const roster = screen.getByRole("list", { name: "Your units" });
    const entries = within(roster).getAllByRole("button");

    expect(entries).toHaveLength(3);
    expect(entries[0]!.textContent).toContain("Melee");
    expect(entries[1]!.textContent).toContain("Ranged");
    expect(entries[2]!.textContent).toContain("Mounted");

    // Mounted is the one field here with structural meaning, and only the unit whose content
    // sets it reports it.
    const workspace = () => screen.getByRole("region", { name: "Selected unit" });
    expect(within(workspace()).getByText("No")).toBeTruthy();

    await user.click(entries[2]!);
    expect(within(workspace()).getByText("Yes")).toBeTruthy();
  });

  it("names no class, specialisation, level or power for a unit", async () => {
    await openUnits(stack(fakePreparation({ items: [sword()] })));

    const forbidden =
      /\b(warrior|ranger|cavalier|knight|swordsman|archer|lancer|class|specialisation|specialization|level|xp|experience|power|rating|score)\b/i;

    expect(document.body.textContent).not.toMatch(forbidden);
  });

  it("publishes the structural facts the content actually holds", async () => {
    const user = await openUnits(stack(fakePreparation()));

    const roster = screen.getByRole("list", { name: "Your units" });
    await user.click(within(roster).getAllByRole("button")[2]!);

    const workspace = within(screen.getByRole("region", { name: "Selected unit" }));

    expect(workspace.getAllByRole("heading", { level: 2 })[0]!.textContent).toBe("Mounted");
    expect(workspace.getByText("Arkazia")).toBeTruthy();
    expect(workspace.getByText("Heavy")).toBeTruthy();
    expect(workspace.getByLabelText("1 star")).toBeTruthy();
    expect(workspace.getByText("Yes")).toBeTruthy();
  });

  it("marks the selected unit and remembers it in the URL", async () => {
    const user = await openUnits(stack(fakePreparation()));

    const roster = screen.getByRole("list", { name: "Your units" });
    const [melee, ranged] = within(roster).getAllByRole("button");

    expect(melee!.getAttribute("aria-pressed")).toBe("true");

    await user.click(ranged!);

    expect(ranged!.getAttribute("aria-pressed")).toBe("true");
    expect(melee!.getAttribute("aria-pressed")).toBe("false");
    expect(currentPath()).toBe("/units?unit=unit-ranged");
  });

  it("starts on the unit the URL names", async () => {
    await openUnits(stack(fakePreparation()), "/units?unit=unit-mounted");

    const roster = screen.getByRole("list", { name: "Your units" });
    expect(within(roster).getAllByRole("button")[2]!.getAttribute("aria-pressed")).toBe("true");
  });

  it("offers both hands when the loadout is empty, and shows them empty", async () => {
    await openUnits(stack(fakePreparation({ items: [sword()] })));

    expect(screen.getAllByText("Empty").length).toBe(2);
    expect(screen.getByRole("button", { name: /Equip Epic Sword to Melee, first hand/ })).toBeTruthy();
    expect(screen.getByRole("button", { name: /Equip Epic Sword to Melee, second hand/ })).toBeTruthy();
  });
});

describe("equipping", () => {
  it("puts a forged sword in a unit's hand and reflects it in the inventory", async () => {
    const api = stack(fakePreparation({ items: [sword()] }));
    const user = await openUnits(api);

    await user.click(screen.getByRole("button", { name: /Equip Epic Sword to Melee, first hand/ }));

    const first = await screen.findByText("First hand");
    const panel = first.parentElement!;
    expect(panel.textContent).toContain("Epic");
    expect(panel.textContent).toContain("Sword");
    expect(within(panel).getByRole("button", { name: /Unequip Epic Sword from first hand/ })).toBeTruthy();

    // It is no longer something the player can hand to anybody, here or anywhere.
    expect(screen.queryByRole("list", { name: "Weapons you can equip" })).toBeNull();
    expect(screen.getByText(/Nothing is free to give this unit/)).toBeTruthy();

    const nav = screen.getByRole("navigation", { name: "Primary" });
    await user.click(within(nav).getByRole("link", { name: "Inventory" }));

    const items = await screen.findByRole("list", { name: "Owned items" });
    expect(items.textContent).toContain("Melee");
    expect(items.textContent).toContain("First hand");
  });

  it("sends the equip request with the antiforgery token the session published", async () => {
    const api = stack(fakePreparation({ items: [sword()] }));
    const { fetchMock } = renderApp(SIGNED_IN, { at: "/units", api });
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: /Equip Epic Sword to Melee, first hand/ }));

    await waitFor(() => expect(screen.getByRole("button", { name: /Unequip/ })).toBeTruthy());

    const equip = fetchMock.mock.calls.find(([input]) => String(input).endsWith("/equip"));
    expect(equip?.[1]?.method).toBe("POST");
    expect(new Headers(equip?.[1]?.headers).get(ANTIFORGERY_HEADER)).toBe(SIGNED_IN.csrfToken);
    expect(JSON.parse(String(equip?.[1]?.body))).toEqual({ itemId: "item-sword-1", slot: 1 });
  });

  it("frees the weapon again when it is unequipped", async () => {
    const api = stack(fakePreparation({ items: [sword()] }));
    const user = await openUnits(api);

    await user.click(screen.getByRole("button", { name: /Equip Epic Sword to Melee, first hand/ }));
    await user.click(await screen.findByRole("button", { name: /Unequip Epic Sword from first hand/ }));

    await waitFor(() => expect(screen.getAllByText("Empty").length).toBe(2));
    expect(screen.getByRole("button", { name: /Equip Epic Sword to Melee, first hand/ })).toBeTruthy();
  });

  it("lets a unit named Ranged hold a sword — the name restricts nothing", async () => {
    const api = stack(fakePreparation({ items: [sword()] }));
    const user = await openUnits(api, "/units?unit=unit-ranged");

    await user.click(screen.getByRole("button", { name: /Equip Epic Sword to Ranged, first hand/ }));

    expect(await screen.findByRole("button", { name: /Unequip Epic Sword from first hand/ })).toBeTruthy();
  });

  it("fills both hands with two distinct swords", async () => {
    const api = stack(
      fakePreparation({
        items: [sword({ id: "item-sword-1" }), sword({ id: "item-sword-2", craftsmanship: "common" })],
      }),
    );
    const user = await openUnits(api);

    await user.click(screen.getByRole("button", { name: /Equip Epic Sword to Melee, first hand/ }));
    await user.click(await screen.findByRole("button", { name: /Equip Common Sword to Melee, second hand/ }));

    await waitFor(() => expect(screen.getAllByRole("button", { name: /^Unequip/ }).length).toBe(2));
    expect(screen.queryByText("Empty")).toBeNull();
  });

  it("does not offer a hand that is already full", async () => {
    const api = stack(
      fakePreparation({ items: [sword({ id: "item-sword-1" }), sword({ id: "item-sword-2" })] }),
    );
    const user = await openUnits(api);

    // Two identical swords, so the first hand is offered twice until one of them fills it.
    await user.click(screen.getAllByRole("button", { name: /Equip Epic Sword to Melee, first hand/ })[0]!);

    const remaining = await screen.findByRole("button", { name: /Equip Epic Sword to Melee, first hand/ });
    expect(remaining.hasAttribute("disabled")).toBe(true);
    expect(
      screen.getByRole("button", { name: /Equip Epic Sword to Melee, second hand/ }).hasAttribute("disabled"),
    ).toBe(false);
  });

  it("offers one action for a weapon that takes both hands", async () => {
    const api = stack(
      fakePreparation({ items: [sword({ name: "Bow", weaponType: "Bow", slotCost: 2 })] }),
    );
    const user = await openUnits(api);

    await user.click(screen.getByRole("button", { name: /Equip Epic Bow to Melee, filling both hands/ }));

    expect(await screen.findByText("Both hands")).toBeTruthy();
    expect(screen.queryByText("First hand")).toBeNull();
    expect(screen.getAllByRole("button", { name: /^Unequip/ }).length).toBe(1);
  });

  it("reports a rejected change instead of pretending it worked", async () => {
    const preparation = fakePreparation({ items: [sword()] });
    const forge = fakeForge();

    const api: ApiStub = (url, init) => {
      if (url.endsWith("/equip")) {
        return new Response(
          JSON.stringify({ detail: "That hand is full.", code: "unit_slot_occupied" }),
          { status: 409, headers: { "Content-Type": "application/problem+json" } },
        );
      }

      return preparation.handle(url, init) ?? forge.handle(url, init);
    };

    const user = await openUnits(api);
    await user.click(screen.getByRole("button", { name: /Equip Epic Sword to Melee, first hand/ }));

    expect(await screen.findByText("That hand is full.")).toBeTruthy();
    expect(screen.getAllByText("Empty").length).toBe(2);
  });

  it("does not offer an item whose wield data has not been authored", async () => {
    const api = stack(
      fakePreparation({
        items: [sword({ name: "Chakram", weaponType: "Chakram", slotCost: null, equippable: false })],
      }),
    );
    await openUnits(api);

    expect(screen.queryByRole("list", { name: "Weapons you can equip" })).toBeNull();
    expect(screen.getByText(/Nothing is free to give this unit/)).toBeTruthy();
  });
});

describe("navigation", () => {
  it("offers the destinations that exist, in loop order", async () => {
    renderApp(SIGNED_IN, { api: stack(fakePreparation()) });

    const nav = await screen.findByRole("navigation", { name: "Primary" });

    expect(within(nav).getAllByRole("link").map((link) => link.textContent)).toEqual([
      "World",
      "Forge",
      "Inventory",
      "Units",
      "Battle",
      "Account",
    ]);
  });

  it("reaches the units screen from the roster of one unit and back", async () => {
    renderApp(SIGNED_IN, { api: stack(fakePreparation({ items: [sword()] })) });
    const user = userEvent.setup();

    const nav = await screen.findByRole("navigation", { name: "Primary" });
    await user.click(within(nav).getByRole("link", { name: "Units" }));

    expect(await screen.findByRole("heading", { level: 1, name: "Units" })).toBeTruthy();
    expect(currentPath()).toBe("/units");

    await user.click(within(nav).getByRole("link", { name: "Inventory" }));
    expect(await screen.findByRole("heading", { level: 1, name: "Inventory" })).toBeTruthy();
  });
});

describe("starter roster", () => {
  it("uses the names the server resolved from content, not names of its own", async () => {
    const renamed = STARTER_UNITS.map((unit) =>
      unit.definitionKey === "arkazia.melee" ? { ...unit, name: "Vanguard" } : unit,
    );

    await openUnits(stack(fakePreparation({ units: renamed })));

    const roster = screen.getByRole("list", { name: "Your units" });
    expect(within(roster).getByText("Vanguard")).toBeTruthy();
    expect(within(roster).queryByText("Melee")).toBeNull();
  });
});
