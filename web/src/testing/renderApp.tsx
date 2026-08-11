import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render } from "@testing-library/react";
import type { RenderResult } from "@testing-library/react";
import { MemoryRouter, useLocation } from "react-router";
import { vi } from "vitest";
import { App } from "@/App";
import type { Session } from "@/auth/session";

/**
 * Reports the router's current URL. It sits beside the application rather than inside its
 * route table, so a test can read where a guard sent the player without the assertion
 * depending on which screen happens to be rendered there.
 */
function LocationProbe() {
  const location = useLocation();
  return <span data-testid="location">{`${location.pathname}${location.search}`}</span>;
}

export const TEST_ACCOUNT = {
  id: "0199-abc",
  email: "smith@weaponsoforder.test",
  emailConfirmed: true,
} as const;

export const SIGNED_IN: Session = {
  authenticated: true,
  account: TEST_ACCOUNT,
  csrfToken: "token-1",
};

export const SIGNED_OUT: Session = { authenticated: false, account: null, csrfToken: "token-1" };

export type FetchMock = ReturnType<typeof vi.fn<typeof fetch>>;

/**
 * Stands the real application up against a stubbed API.
 *
 * The session endpoint answers from `session`, mutations answer 204, and anything else is a
 * failure the test should hear about rather than a silent undefined. Rendering `App` rather
 * than a screen in isolation is the point: what these tests are checking is the routing, the
 * guard and the shell together, which is where a mistake would actually let someone in.
 */
export function renderApp(
  session: Session,
  { at = "/world" }: { at?: string } = {},
): { fetchMock: FetchMock } & RenderResult {
  const fetchMock = vi.fn<typeof fetch>(async (input) => {
    const url = typeof input === "string" ? input : input instanceof URL ? input.href : input.url;

    if (url.endsWith("/api/auth/session")) {
      return new Response(JSON.stringify(session), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      });
    }

    if (url.startsWith("/api/auth/")) {
      return new Response(null, { status: 204 });
    }

    throw new Error(`unexpected request: ${url}`);
  });

  vi.stubGlobal("fetch", fetchMock);

  // `retry: false` matches the application's own session query: a test should fail on the
  // first wrong answer, not after three rounds of backoff.
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return {
    fetchMock,
    ...render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={[at]}>
          <App />
          <LocationProbe />
        </MemoryRouter>
      </QueryClientProvider>,
    ),
  };
}
