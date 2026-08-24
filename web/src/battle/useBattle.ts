import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useAntiforgeryTokens } from "@/auth/useSession";
import type { Army, ArmyIntent, BattleResult } from "@/battle/api";
import { fetchArmy, postArmy, postSimulate } from "@/battle/api";

export const armyKey = ["battle", "army"] as const;

/** The player's army: their Units, where each one stands, and what it fights with. */
export function useArmy() {
  return useQuery({
    queryKey: armyKey,
    // The army is the whole screen, so a failure should be shown rather than retried behind a
    // spinner three times first. Same reasoning as the forge and the preparation screens.
    queryFn: ({ signal }) => fetchArmy(signal),
    retry: false,
  });
}

/**
 * Saving the army.
 *
 * Every edit is a whole army and every save answers with the army as the server then holds it,
 * which is written straight into the cache. Nothing is applied optimistically: whether a hex was
 * free when the request landed is the server's to decide, and a Unit that flickers into place and
 * then back out is worse than one that appears a moment later.
 */
export function useSaveArmy() {
  const queryClient = useQueryClient();
  const tokens = useAntiforgeryTokens();

  return useMutation({
    mutationFn: (intent: ArmyIntent) => postArmy(intent, tokens),
    onSuccess: (army: Army) => queryClient.setQueryData(armyKey, army),
    onError: () => {
      // Every rejection here means the same thing: this screen acted on an army that has since
      // moved. Re-reading is a better answer than an error the player cannot act on, and the
      // message is still shown while the fresh state arrives.
      void queryClient.invalidateQueries({ queryKey: armyKey });
    },
  });
}

/**
 * Fighting a battle.
 *
 * The result is held in the mutation rather than the query cache, because it is a thing that
 * happened once rather than a state the server holds. Battles are not persisted, and a stale one
 * in a cache would be a replay of a battle the army no longer describes.
 */
export function useSimulate() {
  const tokens = useAntiforgeryTokens();

  return useMutation<BattleResult, Error, void>({
    mutationFn: () => postSimulate(tokens),
  });
}
