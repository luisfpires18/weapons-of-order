import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { UseMutationResult } from "@tanstack/react-query";
import { useCallback, useEffect, useRef } from "react";
import { ApiProblem } from "@/api/problem";
import { useAntiforgeryTokens } from "@/auth/useSession";
import type { ForgeState } from "@/forge/api";
import { fetchForgedItems, fetchForgeState, FORGE_URLS, postForgeAction } from "@/forge/api";

export const forgeStateKey = ["forge", "state"] as const;
export const forgedItemsKey = ["forge", "items"] as const;

/** A blow that arrived inside the cooldown is not worth telling the player about. */
export const STRIKE_COOLDOWN_CODE = "forge_strike_cooldown";

/** Problems that mean "your view of the forge is out of date", rather than "that failed". */
const STALE_VIEW_CODES = new Set([
  "forge_in_progress",
  "forge_not_active",
  "forge_conflict",
  STRIKE_COOLDOWN_CODE,
]);

/** The anvil, the stock and the recipe, as the server currently has them. */
export function useForgeState() {
  return useQuery({
    queryKey: forgeStateKey,
    queryFn: ({ signal }) => fetchForgeState(signal),
    // The forge is the screen's whole content, so a failure should be shown rather than
    // retried behind a spinner three times first.
    retry: false,
  });
}

export function useForgedItems() {
  return useQuery({
    queryKey: forgedItemsKey,
    queryFn: ({ signal }) => fetchForgedItems(signal),
    retry: false,
  });
}

type ForgeAction<TBody> = UseMutationResult<ForgeState, Error, TBody>;

/**
 * One forge action. The response is the new state, so it is written straight into the cache
 * rather than triggering a refetch: an extra round trip between striking and seeing the blow
 * land is the difference between a hammer and a form submission.
 */
function useForgeAction<TBody>(url: string): ForgeAction<TBody> {
  const queryClient = useQueryClient();
  const tokens = useAntiforgeryTokens();

  return useMutation({
    mutationFn: (body: TBody) => postForgeAction(url, body ?? {}, tokens),
    onSuccess: (state) => {
      queryClient.setQueryData(forgeStateKey, state);

      // A finished sword is a new owned item, and the list beside the anvil should not be
      // the last thing to hear about it.
      if (state.session?.status === "completed") {
        void queryClient.invalidateQueries({ queryKey: forgedItemsKey });
      }
    },
    onError: (error) => {
      // These three all mean the same thing: this screen was acting on a forge that has
      // since moved. Re-reading is the correct answer, and a better one than an error the
      // player has no way to act on.
      if (error instanceof ApiProblem && STALE_VIEW_CODES.has(error.code)) {
        void queryClient.invalidateQueries({ queryKey: forgeStateKey });
      }
    },
  });
}

export function useBeginForge(): ForgeAction<{ recipeKey: string }> {
  return useForgeAction(FORGE_URLS.begin);
}

export function useStrike(): ForgeAction<void> {
  return useForgeAction(FORGE_URLS.strike);
}

export function useAbandonForge(): ForgeAction<void> {
  return useForgeAction(FORGE_URLS.abandon);
}

/**
 * Holding the workpiece in the fire.
 *
 * The player's hand is continuous and the API is not, so this keeps one bit of intent and
 * makes sure the server ends up agreeing with it. Requests are never issued in parallel: a
 * press and the release that follows it must not be able to land out of order, or the forge
 * would be left heating after the player let go.
 */
export function useHeatControl(active: boolean): {
  setHeating: (heating: boolean) => void;
  isPending: boolean;
  error: Error | null;
} {
  const action = useForgeAction<{ heating: boolean }>(FORGE_URLS.heat);
  const desired = useRef<boolean | null>(null);
  const sent = useRef<boolean | null>(null);
  const sending = useRef(false);
  const anvilHasWork = useRef(active);

  // Held in a ref so `setHeating` below can be stable for the life of the screen. It has a
  // window listener hanging off it whose teardown releases the fire, and an identity that
  // changed on re-render would make that teardown run mid-press.
  const send = useRef(action.mutateAsync);

  useEffect(() => {
    send.current = action.mutateAsync;
  }, [action.mutateAsync]);

  useEffect(() => {
    anvilHasWork.current = active;

    // Nothing on the anvil means nothing is known about the fire either, so the next press
    // always says so out loud rather than being skipped as a repeat.
    if (!active) {
      desired.current = null;
      sent.current = null;
    }
  }, [active]);

  const drain = useCallback(async () => {
    if (sending.current) return;
    sending.current = true;

    try {
      while (desired.current !== null && desired.current !== sent.current) {
        const heating = desired.current;
        desired.current = null;

        try {
          await send.current({ heating });
          sent.current = heating;
        } catch {
          // Reported through the mutation's own error state. The loop continues so a failed
          // press cannot strand the release that was queued behind it.
          sent.current = null;
        }
      }

      desired.current = null;
    } finally {
      sending.current = false;
    }
  }, []);

  const setHeating = useCallback(
    (heating: boolean) => {
      if (!anvilHasWork.current) return;

      desired.current = heating;
      void drain();
    },
    [drain],
  );

  // Walking away with the iron still in the fire would leave it heating, because the fire is
  // server state. This is a courtesy rather than a correctness measure — a closed laptop
  // cannot send anything, and the burn rule is what actually covers that.
  useEffect(() => {
    const release = () => setHeating(false);
    window.addEventListener("pagehide", release);
    window.addEventListener("blur", release);

    return () => {
      window.removeEventListener("pagehide", release);
      window.removeEventListener("blur", release);
      release();
    };
  }, [setHeating]);

  return { setHeating, isPending: action.isPending, error: action.error };
}
