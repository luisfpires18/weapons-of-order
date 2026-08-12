import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useCallback, useEffect, useRef, useState } from "react";
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

/**
 * An action the player can take at the anvil.
 *
 * `run` returns nothing on purpose. Its result is the new forge state, which arrives through
 * the query cache; what a caller needs to know locally is only whether the action is still
 * outstanding and whether it failed.
 */
export type ForgeAction<TBody> = {
  run: (body: TBody) => void;
  isPending: boolean;
  error: Error | null;
};

export type ForgeActions = {
  begin: ForgeAction<{ recipeKey: string }>;
  strike: ForgeAction<void>;
  abandon: ForgeAction<void>;
  heat: { setHeating: (heating: boolean) => void; isPending: boolean; error: Error | null };
};

/**
 * Every action at the anvil, sharing one queue.
 *
 * They are created together because they have to be ordered together. The server decides
 * what a blow was worth from the state it holds at the moment the request lands, so whether
 * the workpiece was still in the fire when the hammer fell is decided by which request
 * arrives first. Letting the browser issue a strike while the release that preceded it is
 * still in flight would make craftsmanship depend on network scheduling rather than on what
 * the player did.
 */
export function useForgeActions(active: boolean): ForgeActions {
  const enqueue = useForgeQueue();

  return {
    begin: useForgeAction<{ recipeKey: string }>(FORGE_URLS.begin, enqueue),
    strike: useForgeAction<void>(FORGE_URLS.strike, enqueue),
    abandon: useForgeAction<void>(FORGE_URLS.abandon, enqueue),
    heat: useHeatControl(active, enqueue),
  };
}

type Enqueue = <T>(run: () => Promise<T>) => Promise<T>;

/**
 * Runs what it is given one at a time, in the order it was asked.
 *
 * A promise chain rather than a scheduler: the whole mechanism is the tail below. A caller
 * claims its place in line **synchronously**, at the moment the player pressed something,
 * and its request is issued when the one in front of it has answered. That is the part that
 * matters — claiming the place later, once a request had actually started, would put a
 * release that is waiting behind an earlier request after a strike the player took second.
 *
 * A failed action does not take the queue down with it: the tail is only ever a settled
 * promise, so the next action runs whatever happened to the last one.
 */
function useForgeQueue(): Enqueue {
  const tail = useRef<Promise<unknown>>(Promise.resolve());

  return useCallback(<T,>(run: () => Promise<T>): Promise<T> => {
    const next = tail.current.then(run, run);

    tail.current = next.then(ignore, ignore);

    return next;
  }, []);
}

/**
 * One forge action, queued behind the others.
 *
 * The response is the new state, so it is written straight into the cache rather than
 * triggering a refetch: an extra round trip between striking and seeing the blow land is the
 * difference between a hammer and a form submission.
 */
function useForgeAction<TBody>(url: string, enqueue: Enqueue): ForgeAction<TBody> {
  const queryClient = useQueryClient();
  const tokens = useAntiforgeryTokens();

  const mutation = useMutation({
    mutationFn: (body: TBody) => postForgeAction(url, body ?? {}, tokens),
    onSuccess: (state: ForgeState) => {
      queryClient.setQueryData(forgeStateKey, state);

      // A finished sword is a new owned item, and the list beside the anvil should not be
      // the last thing to hear about it.
      if (state.session?.status === "completed") {
        void queryClient.invalidateQueries({ queryKey: forgedItemsKey });
      }
    },
    onError: (error: Error) => {
      // These all mean the same thing: this screen was acting on a forge that has since
      // moved. Re-reading is the correct answer, and a better one than an error the player
      // has no way to act on.
      if (error instanceof ApiProblem && STALE_VIEW_CODES.has(error.code)) {
        void queryClient.invalidateQueries({ queryKey: forgeStateKey });
      }
    },
  });

  // Held in a ref so `run` below can be stable: it is what the queue and the window
  // listeners are keyed on, and an identity that changed on re-render would tear those down
  // mid-press.
  const send = useRef(mutation.mutateAsync);

  useEffect(() => {
    send.current = mutation.mutateAsync;
  }, [mutation.mutateAsync]);

  // Counted from the moment the player acted rather than from the moment the request
  // started, so a control stays disabled while its action is still waiting its turn.
  const [outstanding, setOutstanding] = useState(0);

  const run = useCallback(
    (body: TBody) => {
      setOutstanding((count) => count + 1);

      // The place in the queue is claimed here, inside the event handler.
      void enqueue(() => send.current(body))
        .catch(ignore)
        .finally(() => setOutstanding((count) => count - 1));
    },
    [enqueue],
  );

  return { run, isPending: outstanding > 0, error: mutation.error };
}

/**
 * Holding the workpiece in the fire.
 *
 * The player's hand is continuous and the API is not, so this keeps one bit of intent and
 * sends it when it changes. Each press and each release claims its place in the shared queue
 * as it happens, which is what keeps a release ahead of the strike that followed it.
 */
function useHeatControl(active: boolean, enqueue: Enqueue): ForgeActions["heat"] {
  const action = useForgeAction<{ heating: boolean }>(FORGE_URLS.heat, enqueue);
  const { run } = action;

  const intent = useRef<boolean | null>(null);
  const anvilHasWork = useRef(active);

  useEffect(() => {
    anvilHasWork.current = active;

    // Nothing on the anvil means nothing is known about the fire either, so the next press
    // always says so out loud rather than being skipped as a repeat.
    if (!active) {
      intent.current = null;
    }
  }, [active]);

  const setHeating = useCallback(
    (heating: boolean) => {
      if (!anvilHasWork.current || intent.current === heating) {
        return;
      }

      intent.current = heating;
      run({ heating });
    },
    [run],
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

function ignore() {}
