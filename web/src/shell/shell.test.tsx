// @vitest-environment jsdom
import { cleanup, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ANTIFORGERY_HEADER } from "@/auth/session";
import { renderApp, SIGNED_IN, SIGNED_OUT, TEST_ACCOUNT } from "@/testing/renderApp";

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

function currentPath() {
  return screen.getByTestId("location").textContent;
}

describe("authenticated shell", () => {
  it("gives a signed-in player the world screen inside the shell", async () => {
    renderApp(SIGNED_IN);

    expect(await screen.findByRole("heading", { level: 1, name: "World" })).toBeTruthy();
    expect(screen.getByRole("navigation", { name: "Primary" })).toBeTruthy();
    expect(screen.getByText(TEST_ACCOUNT.email, { selector: "dd" })).toBeTruthy();
  });

  it("marks the destination the player is on", async () => {
    renderApp(SIGNED_IN);

    const nav = await screen.findByRole("navigation", { name: "Primary" });

    expect(within(nav).getByRole("link", { name: "World" }).getAttribute("aria-current")).toBe("page");
    expect(within(nav).getByRole("link", { name: "Account" }).getAttribute("aria-current")).toBeNull();
  });

  it("navigates between the destinations that exist", async () => {
    renderApp(SIGNED_IN);
    const user = userEvent.setup();

    const nav = await screen.findByRole("navigation", { name: "Primary" });
    await user.click(within(nav).getByRole("link", { name: "Account" }));

    expect(await screen.findByRole("heading", { level: 1, name: "Account" })).toBeTruthy();
    expect(currentPath()).toBe("/account");
    expect(within(nav).getByRole("link", { name: "Account" }).getAttribute("aria-current")).toBe("page");
  });

  it("offers only the destinations that are implemented", async () => {
    renderApp(SIGNED_IN);

    const nav = await screen.findByRole("navigation", { name: "Primary" });

    expect(within(nav).getAllByRole("link").map((link) => link.textContent)).toEqual([
      "World",
      "Forge",
      "Account",
    ]);
  });

  it("reports the account state the server published", async () => {
    renderApp(SIGNED_IN, { at: "/account" });

    expect(await screen.findByRole("heading", { level: 1, name: "Account" })).toBeTruthy();
    expect(screen.getByText("Confirmed")).toBeTruthy();
    expect(screen.getByText(TEST_ACCOUNT.email, { selector: "dd" })).toBeTruthy();
  });
});

describe("shell access control", () => {
  it("sends an unauthenticated visitor to sign in, carrying where they were going", async () => {
    renderApp(SIGNED_OUT, { at: "/account" });

    await waitFor(() => expect(currentPath()).toBe("/login?next=%2Faccount"));
    expect(screen.queryByRole("navigation", { name: "Primary" })).toBeNull();
  });

  it("never renders shell content before the session answers", () => {
    renderApp(SIGNED_IN);

    // The very first paint, before the session request resolves.
    expect(screen.getByRole("status").textContent).toBe("Checking session");
    expect(screen.queryByRole("navigation", { name: "Primary" })).toBeNull();
    expect(screen.queryByText(TEST_ACCOUNT.email)).toBeNull();
  });

  it.each([
    "/hub",
    "/barracks",
    "/arrange",
    "/dungeon",
    "/vault",
    "/ladder",
  ])("resolves the removed route %s as not found rather than reviving it", async (path) => {
    renderApp(SIGNED_IN, { at: path });

    expect(await screen.findByRole("heading", { level: 1, name: "NOT FOUND" })).toBeTruthy();
    expect(currentPath()).toBe(path);
    expect(screen.queryByRole("navigation", { name: "Primary" })).toBeNull();
  });
});

describe("signing out", () => {
  it("ends the session on the server and returns to the public title screen", async () => {
    const { fetchMock } = renderApp(SIGNED_IN, { at: "/account" });
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Sign out" }));

    await waitFor(() => expect(currentPath()).toBe("/"));

    const logout = fetchMock.mock.calls.find(([input]) => String(input).endsWith("/api/auth/logout"));
    expect(logout).toBeDefined();
    expect(logout?.[1]?.method).toBe("POST");
    expect(new Headers(logout?.[1]?.headers).get(ANTIFORGERY_HEADER)).toBe(SIGNED_IN.csrfToken);

    expect(screen.queryByRole("navigation", { name: "Primary" })).toBeNull();
    expect(screen.queryByText(TEST_ACCOUNT.email)).toBeNull();
  });

  it("is reachable from the account menu in the top bar", async () => {
    const { fetchMock } = renderApp(SIGNED_IN);
    const user = userEvent.setup();

    const trigger = await screen.findByRole("button", { name: new RegExp(TEST_ACCOUNT.email) });
    expect(trigger.getAttribute("aria-expanded")).toBe("false");

    await user.click(trigger);
    expect(trigger.getAttribute("aria-expanded")).toBe("true");

    await user.click(screen.getByRole("button", { name: "Sign out" }));

    await waitFor(() => expect(currentPath()).toBe("/"));
    expect(
      fetchMock.mock.calls.some(([input]) => String(input).endsWith("/api/auth/logout")),
    ).toBe(true);
  });
});

describe("account menu", () => {
  it("opens onto its first item and closes back onto the trigger with Escape", async () => {
    renderApp(SIGNED_IN);
    const user = userEvent.setup();

    const trigger = await screen.findByRole("button", { name: new RegExp(TEST_ACCOUNT.email) });
    const topBar = within(screen.getByRole("banner"));
    await user.click(trigger);

    await waitFor(() =>
      expect(document.activeElement).toBe(topBar.getByRole("link", { name: "Account" })),
    );

    await user.keyboard("{Escape}");

    expect(screen.queryByRole("button", { name: "Sign out" })).toBeNull();
    expect(document.activeElement).toBe(trigger);
  });
});
