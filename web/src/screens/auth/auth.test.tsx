// @vitest-environment jsdom
import { cleanup, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { MINIMUM_PASSWORD_LENGTH } from "@/auth/policy";
import type { ApiStub, FetchMock } from "@/testing/renderApp";
import { renderApp, SIGNED_OUT } from "@/testing/renderApp";

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

/** The body a mutation was sent with, so a test can assert on the contract rather than on prose. */
function bodyOf(fetchMock: FetchMock, path: string): Record<string, unknown> {
  const call = fetchMock.mock.calls.find(([input]) => String(input).endsWith(path));
  expect(call).toBeDefined();
  return JSON.parse(String(call?.[1]?.body)) as Record<string, unknown>;
}

/** Answers one account mutation with a field-error problem, as the server would. */
function fieldErrors(path: string, errors: Record<string, string[]>): ApiStub {
  return (url) =>
    url.endsWith(path)
      ? new Response(JSON.stringify({ code: "validation", title: "Some details need fixing.", errors }), {
          status: 400,
          headers: { "Content-Type": "application/problem+json" },
        })
      : undefined;
}

describe("registration", () => {
  it("sends the username, the address and the password", async () => {
    const { fetchMock } = renderApp(SIGNED_OUT, { at: "/register" });
    const user = userEvent.setup();

    await user.type(await screen.findByLabelText("Username"), "Unreally");
    await user.type(screen.getByLabelText("Email"), "unreally@weaponsoforder.test");
    await user.type(screen.getByLabelText("Password"), "aaaaaa");
    await user.type(screen.getByLabelText("Confirm password"), "aaaaaa");
    await user.click(screen.getByRole("button", { name: "Create account" }));

    await waitFor(() => expect(screen.getByRole("heading", { level: 1 }).textContent).toBe("Check your email"));

    expect(bodyOf(fetchMock, "/api/auth/register")).toEqual({
      username: "Unreally",
      email: "unreally@weaponsoforder.test",
      password: "aaaaaa",
    });
  });

  it("asks for the username above the address", async () => {
    renderApp(SIGNED_OUT, { at: "/register" });

    const fields = (await screen.findAllByRole("textbox")).map((field) => field.getAttribute("id"));

    expect(fields.slice(0, 2)).toEqual(["username", "email"]);
  });

  it("shows a rejected username at the username field, not as a form-level failure", async () => {
    renderApp(SIGNED_OUT, {
      at: "/register",
      api: fieldErrors("/api/auth/register", { username: ["That username is already in use."] }),
    });
    const user = userEvent.setup();

    const username = await screen.findByLabelText("Username");
    await user.type(username, "Unreally");
    await user.type(screen.getByLabelText("Email"), "unreally@weaponsoforder.test");
    await user.type(screen.getByLabelText("Password"), "aaaaaa");
    await user.type(screen.getByLabelText("Confirm password"), "aaaaaa");
    await user.click(screen.getByRole("button", { name: "Create account" }));

    const message = await screen.findByText("That username is already in use.");

    expect(username.getAttribute("aria-invalid")).toBe("true");
    // Named by the field, so a screen reader reads the failure with the input it belongs to.
    expect(username.getAttribute("aria-describedby")).toContain(message.getAttribute("id"));
    expect(screen.queryByRole("alert")).toBeNull();
  });

  it("explains the at-sign restriction when the server refuses the name", async () => {
    renderApp(SIGNED_OUT, {
      at: "/register",
      api: fieldErrors("/api/auth/register", {
        username: ["A username cannot contain @, because sign-in reads anything with an @ as an email address."],
      }),
    });
    const user = userEvent.setup();

    await user.type(await screen.findByLabelText("Username"), "un@really");
    await user.type(screen.getByLabelText("Email"), "unreally@weaponsoforder.test");
    await user.type(screen.getByLabelText("Password"), "aaaaaa");
    await user.type(screen.getByLabelText("Confirm password"), "aaaaaa");
    await user.click(screen.getByRole("button", { name: "Create account" }));

    expect(await screen.findByText(/cannot contain @/)).toBeTruthy();
  });

  it("states the length rule and nothing that is no longer true", async () => {
    renderApp(SIGNED_OUT, { at: "/register" });

    expect(await screen.findByText(`At least ${MINIMUM_PASSWORD_LENGTH} characters.`)).toBeTruthy();
    expect(MINIMUM_PASSWORD_LENGTH).toBe(6);
    expect(screen.queryByText(/12 characters/)).toBeNull();
    expect(screen.queryByText(/unique/i)).toBeNull();
    expect(screen.queryByText(/uppercase|lowercase|symbol|digit|number/i)).toBeNull();
  });

  it("labels every control, on the same markup a phone renders", async () => {
    renderApp(SIGNED_OUT, { at: "/register" });

    // One responsive form, so this is the mobile form too: the layout differs in CSS only.
    const fields: [label: string, autoComplete: string][] = [
      ["Username", "username"],
      ["Email", "email"],
      ["Password", "new-password"],
      ["Confirm password", "new-password"],
    ];

    for (const [label, autoComplete] of fields) {
      const field = await screen.findByLabelText(label);
      expect(field.getAttribute("autocomplete")).toBe(autoComplete);
    }
  });
});

describe("signing in", () => {
  it("sends one identifier rather than an address-specific contract", async () => {
    const { fetchMock } = renderApp(SIGNED_OUT, { at: "/login" });
    const user = userEvent.setup();

    await user.type(await screen.findByLabelText("Username or email"), "Unreally");
    await user.type(screen.getByLabelText("Password"), "aaaaaa");
    await user.click(screen.getByRole("button", { name: "Sign in" }));

    const body = await waitFor(() => bodyOf(fetchMock, "/api/auth/login"));

    expect(body).toEqual({ identifier: "Unreally", password: "aaaaaa", rememberMe: false });
    expect(body).not.toHaveProperty("email");
  });

  it("sends an address through the same field", async () => {
    const { fetchMock } = renderApp(SIGNED_OUT, { at: "/login" });
    const user = userEvent.setup();

    await user.type(await screen.findByLabelText("Username or email"), "smith@weaponsoforder.test");
    await user.type(screen.getByLabelText("Password"), "aaaaaa");
    await user.click(screen.getByRole("button", { name: "Sign in" }));

    await waitFor(() =>
      expect(bodyOf(fetchMock, "/api/auth/login").identifier).toBe("smith@weaponsoforder.test"),
    );
  });

  it("keeps Remember me and sends it", async () => {
    const { fetchMock } = renderApp(SIGNED_OUT, { at: "/login" });
    const user = userEvent.setup();

    await user.type(await screen.findByLabelText("Username or email"), "Unreally");
    await user.type(screen.getByLabelText("Password"), "aaaaaa");
    await user.click(screen.getByLabelText("Keep me signed in"));
    await user.click(screen.getByRole("button", { name: "Sign in" }));

    await waitFor(() => expect(bodyOf(fetchMock, "/api/auth/login").rememberMe).toBe(true));
  });

  it("offers one identifier field, not a second screen for usernames", async () => {
    renderApp(SIGNED_OUT, { at: "/login" });

    const identifier = await screen.findByLabelText("Username or email");

    expect(identifier.getAttribute("autocomplete")).toBe("username");
    expect(screen.getAllByRole("textbox")).toHaveLength(1);
    expect(screen.queryByLabelText("Email")).toBeNull();
  });

  it("reports a rejected sign-in generically, naming neither the account nor the field", async () => {
    renderApp(SIGNED_OUT, {
      at: "/login",
      api: (url) =>
        url.endsWith("/api/auth/login")
          ? new Response(
              JSON.stringify({
                code: "invalid_credentials",
                detail: "That sign-in and password combination is not correct.",
              }),
              { status: 401, headers: { "Content-Type": "application/problem+json" } },
            )
          : undefined,
    });
    const user = userEvent.setup();

    await user.type(await screen.findByLabelText("Username or email"), "Unreally");
    await user.type(screen.getByLabelText("Password"), "wrong-one");
    await user.click(screen.getByRole("button", { name: "Sign in" }));

    const alert = await screen.findByRole("alert");

    expect(alert.textContent).toContain("not correct");
    expect(alert.textContent).not.toContain("Unreally");
  });
});

describe("password recovery", () => {
  it("still asks for the address, because recovery is what the mailbox proves", async () => {
    const { fetchMock } = renderApp(SIGNED_OUT, { at: "/forgot-password" });
    const user = userEvent.setup();

    await user.type(await screen.findByLabelText("Email"), "smith@weaponsoforder.test");
    await user.click(screen.getByRole("button", { name: "Send reset link" }));

    await waitFor(() =>
      expect(bodyOf(fetchMock, "/api/auth/forgot-password")).toEqual({
        email: "smith@weaponsoforder.test",
      }),
    );
    expect(screen.queryByLabelText("Username or email")).toBeNull();
  });

  it("states the same length rule when a new password is set", async () => {
    renderApp(SIGNED_OUT, { at: "/reset-password?userId=0199-abc&token=opaque" });

    expect(await screen.findByText(`At least ${MINIMUM_PASSWORD_LENGTH} characters.`)).toBeTruthy();
    expect(screen.queryByText(/12 characters/)).toBeNull();
  });
});
