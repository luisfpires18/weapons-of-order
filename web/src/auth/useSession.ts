import { useQuery, useQueryClient } from "@tanstack/react-query";
import type { QueryClient } from "@tanstack/react-query";
import { useCallback, useMemo } from "react";
import type { AntiforgeryTokens, Session } from "@/auth/session";
import { fetchSession } from "@/auth/session";

export const sessionQueryKey = ["auth", "session"] as const;

function sessionQuery() {
  return {
    queryKey: sessionQueryKey,
    queryFn: ({ signal }: { signal: AbortSignal }) => fetchSession(signal),
    // A guard waiting on three backoff rounds would leave the player staring at a spinner.
    // The session endpoint answers 200 for both states, so a failure here is a real fault.
    retry: false,
  };
}

/** The server's answer to "who is this browser", and the source of the antiforgery token. */
export function useSession() {
  return useQuery(sessionQuery());
}

export function useAntiforgeryTokens(): AntiforgeryTokens {
  const queryClient = useQueryClient();

  return useMemo(
    () => ({
      current: async () => (await queryClient.ensureQueryData(sessionQuery())).csrfToken,
      refresh: async () => (await queryClient.fetchQuery(sessionQuery())).csrfToken,
    }),
    [queryClient],
  );
}

/** Re-reads the session after an identity change, so guards and tokens both catch up. */
export function useRefreshSession(): () => Promise<Session> {
  const queryClient = useQueryClient();
  return useCallback(() => queryClient.fetchQuery(sessionQuery()), [queryClient]);
}

/**
 * Drops every cached response, not only the session.
 *
 * Anything read while signed in was read on that account's behalf. Leaving it in the cache
 * would let the next screen render another session's data before its own request lands.
 */
export function clearPrivateState(queryClient: QueryClient): void {
  queryClient.clear();
}
