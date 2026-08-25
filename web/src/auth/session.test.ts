import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiProblem } from "@/api/problem";
import type { AntiforgeryTokens } from "@/auth/session";
import { ANTIFORGERY_HEADER, AUTH_URLS, fetchSession, postJson, SESSION_URL } from "@/auth/session";

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

function problemResponse(status: number, body: Record<string, unknown>) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/problem+json" },
  });
}

function stubFetch(...responses: Response[]) {
  const fetchMock = vi.fn<typeof fetch>();
  for (const response of responses) {
    fetchMock.mockResolvedValueOnce(response);
  }
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

function tokenSource(current: string, refreshed = current): AntiforgeryTokens & { refreshCalls: number } {
  const source = {
    refreshCalls: 0,
    current: () => Promise.resolve(current),
    refresh: () => {
      source.refreshCalls += 1;
      return Promise.resolve(refreshed);
    },
  };
  return source;
}

function headerOf(fetchMock: ReturnType<typeof stubFetch>, call: number): string | null {
  const init = fetchMock.mock.calls[call]?.[1];
  return new Headers(init?.headers).get(ANTIFORGERY_HEADER);
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("fetchSession", () => {
  it("reads the unauthenticated state as an ordinary answer", async () => {
    stubFetch(jsonResponse({ authenticated: false, account: null, csrfToken: "token-1" }));

    await expect(fetchSession()).resolves.toEqual({
      authenticated: false,
      account: null,
      csrfToken: "token-1",
    });
  });

  it("reads the signed-in account", async () => {
    stubFetch(
      jsonResponse({
        authenticated: true,
        account: {
          id: "0199-abc",
          username: "Ordersmith",
          email: "smith@weaponsoforder.test",
          emailConfirmed: true,
        },
        csrfToken: "token-2",
      }),
    );

    const session = await fetchSession();

    expect(session.authenticated).toBe(true);
    expect(session.account?.username).toBe("Ordersmith");
    expect(session.account?.email).toBe("smith@weaponsoforder.test");
  });

  it("asks the same-origin path, so the cookie is sent and no base url can be misconfigured", async () => {
    const fetchMock = stubFetch(jsonResponse({ authenticated: false, account: null, csrfToken: "t" }));

    await fetchSession();

    expect(fetchMock).toHaveBeenCalledWith(SESSION_URL, expect.anything());
    expect(SESSION_URL.startsWith("/api/")).toBe(true);
  });

  it("rejects a payload that does not match the contract", async () => {
    stubFetch(jsonResponse({ authenticated: "yes" }));

    await expect(fetchSession()).rejects.toThrow();
  });
});

describe("postJson", () => {
  it("sends the antiforgery token as a header", async () => {
    const fetchMock = stubFetch(new Response(null, { status: 204 }));

    await postJson(AUTH_URLS.login, { identifier: "a@b.test" }, tokenSource("token-1"));

    expect(headerOf(fetchMock, 0)).toBe("token-1");
    expect(fetchMock.mock.calls[0]?.[1]?.method).toBe("POST");
  });

  it("returns null for a no-content response", async () => {
    stubFetch(new Response(null, { status: 204 }));

    await expect(postJson(AUTH_URLS.logout, {}, tokenSource("token-1"))).resolves.toBeNull();
  });

  it("replays once with a refreshed token when the old one is rejected", async () => {
    const fetchMock = stubFetch(
      problemResponse(400, { code: "antiforgery", detail: "Reload the page and try again." }),
      new Response(null, { status: 204 }),
    );
    const tokens = tokenSource("stale-token", "fresh-token");

    await expect(postJson(AUTH_URLS.logout, {}, tokens)).resolves.toBeNull();

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(headerOf(fetchMock, 0)).toBe("stale-token");
    expect(headerOf(fetchMock, 1)).toBe("fresh-token");
    expect(tokens.refreshCalls).toBe(1);
  });

  it("gives up after one replay rather than looping", async () => {
    const fetchMock = stubFetch(
      problemResponse(400, { code: "antiforgery" }),
      problemResponse(400, { code: "antiforgery", detail: "Reload the page and try again." }),
    );

    await expect(postJson(AUTH_URLS.logout, {}, tokenSource("a", "b"))).rejects.toThrow(ApiProblem);
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it("does not replay a validation failure", async () => {
    const fetchMock = stubFetch(
      problemResponse(400, {
        code: "validation",
        title: "Some details need fixing.",
        errors: { email: ["Enter a valid email address."] },
      }),
    );
    const tokens = tokenSource("token-1");

    const error = await postJson(AUTH_URLS.register, {}, tokens).catch((thrown: unknown) => thrown);

    expect(error).toBeInstanceOf(ApiProblem);
    expect((error as ApiProblem).fieldError("email")).toBe("Enter a valid email address.");
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(tokens.refreshCalls).toBe(0);
  });

  it("surfaces the code and message of a rejected sign-in", async () => {
    stubFetch(
      problemResponse(401, {
        code: "invalid_credentials",
        detail: "That email and password combination is not correct.",
      }),
    );

    const error = await postJson(AUTH_URLS.login, {}, tokenSource("token-1")).catch(
      (thrown: unknown) => thrown,
    );

    expect(error).toBeInstanceOf(ApiProblem);
    expect((error as ApiProblem).code).toBe("invalid_credentials");
    expect((error as ApiProblem).status).toBe(401);
  });

  it("still produces a usable error when the body is not problem-shaped", async () => {
    stubFetch(new Response("<html>gateway timeout</html>", { status: 504 }));

    const error = await postJson(AUTH_URLS.login, {}, tokenSource("token-1")).catch(
      (thrown: unknown) => thrown,
    );

    expect(error).toBeInstanceOf(ApiProblem);
    expect((error as ApiProblem).code).toBe("unknown");
    expect((error as ApiProblem).message).not.toBe("");
  });
});
