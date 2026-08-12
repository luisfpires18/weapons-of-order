import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useAntiforgeryTokens } from "@/auth/useSession";
import type { Unit } from "@/preparation/api";
import { fetchInventory, fetchUnits, postEquip, postUnequip } from "@/preparation/api";

export const inventoryKey = ["preparation", "inventory"] as const;
export const unitsKey = ["preparation", "units"] as const;

/** Everything the player owns, with where each item currently is. */
export function useInventory() {
  return useQuery({
    queryKey: inventoryKey,
    // The inventory is the whole screen, so a failure should be shown rather than retried
    // behind a spinner three times first. Same reasoning as the forge.
    queryFn: ({ signal }) => fetchInventory(signal),
    retry: false,
  });
}

export function useUnits() {
  return useQuery({
    queryKey: unitsKey,
    queryFn: ({ signal }) => fetchUnits(signal),
    retry: false,
  });
}

export type LoadoutChange =
  | { action: "equip"; unitId: string; itemId: string; slot?: number }
  | { action: "unequip"; unitId: string; itemId: string };

/**
 * Putting a weapon in a unit's hands, or taking it out.
 *
 * One mutation for both, because they are the same transaction from the screen's point of
 * view: the server answers with the unit's new loadout, which is written straight into the
 * cache, and the inventory is invalidated because the item's whereabouts changed with it.
 *
 * Nothing is applied optimistically. Whether a hand was free when the request landed is the
 * server's to decide, and a loadout that flickers into place and then back out is worse than
 * one that appears a moment later.
 */
export function useLoadoutChange() {
  const queryClient = useQueryClient();
  const tokens = useAntiforgeryTokens();

  return useMutation({
    mutationFn: (change: LoadoutChange) =>
      change.action === "equip"
        ? postEquip(change.unitId, { itemId: change.itemId, slot: change.slot }, tokens)
        : postUnequip(change.unitId, { itemId: change.itemId }, tokens),
    onSuccess: (unit: Unit) => {
      queryClient.setQueryData<Unit[]>(unitsKey, (units) =>
        units?.map((existing) => (existing.id === unit.id ? unit : existing)),
      );

      void queryClient.invalidateQueries({ queryKey: inventoryKey });
    },
    onError: () => {
      // Every rejection here means the same thing: this screen acted on a loadout that has
      // since moved. Re-reading is a better answer than an error the player cannot act on,
      // and the message is still shown while the fresh state arrives.
      void queryClient.invalidateQueries({ queryKey: unitsKey });
      void queryClient.invalidateQueries({ queryKey: inventoryKey });
    },
  });
}
