import { useSearchParams } from "react-router";
import { ApiProblem } from "@/api/problem";
import { FormNotice } from "@/components/auth/FormControls";
import { ScreenError, ScreenPending } from "@/preparation/ScreenStates";
import { UnitLoadout } from "@/preparation/UnitLoadout";
import { UnitRoster } from "@/preparation/UnitRoster";
import { useInventory, useLoadoutChange, useUnits } from "@/preparation/usePreparation";
import { ShellScreen } from "@/shell/ShellScreen";

/** Which unit is being prepared, kept in the URL so a reload lands back on it. */
const SELECTED_PARAM = "unit";

/**
 * Preparing an army.
 *
 * Roster on one side, the selected unit's workspace on the other, and on a phone the roster
 * becomes a row of selectors above the workspace rather than a column squeezed beside it. The
 * screen owns the width it is given: no dashboard cards, no grid of statistics, and nothing
 * that resolves to a number the game does not have.
 *
 * The selection lives in the URL. It costs one search parameter and means a reload, a shared
 * link and the browser's back button all land where the player was.
 */
export function UnitsScreen() {
  const [params, setParams] = useSearchParams();

  const units = useUnits();
  const inventory = useInventory();
  const change = useLoadoutChange();

  if (units.isPending || inventory.isPending) {
    return <ScreenPending title="Units">Calling your units</ScreenPending>;
  }

  if (units.isError) {
    return (
      <ScreenError
        title="Units"
        error={units.error}
        fallback="Your units could not be read."
        onRetry={() => void units.refetch()}
      />
    );
  }

  if (inventory.isError) {
    return (
      <ScreenError
        title="Units"
        error={inventory.error}
        fallback="Your inventory could not be read."
        onRetry={() => void inventory.refetch()}
      />
    );
  }

  if (units.data.length === 0) {
    return (
      <ShellScreen title="Units" lead="Prepare a fighter before the battle.">
        <p className="max-w-[36rem] font-body text-[1rem] leading-relaxed text-bone-dim">
          You have no units.
        </p>
      </ShellScreen>
    );
  }

  // A unit named in the URL that is not in the roster — a stale link, or somebody else's id —
  // quietly falls back to the first rather than showing an error about a unit that was never
  // theirs to see.
  const requested = params.get(SELECTED_PARAM);
  const selected = units.data.find((unit) => unit.id === requested) ?? units.data[0]!;

  const select = (unitId: string) => {
    setParams(
      (current) => {
        current.set(SELECTED_PARAM, unitId);
        return current;
      },
      { replace: true },
    );
  };

  // Everything owned, currently in nobody's hands, and with wield data authored. One physical
  // item cannot be in two places, so what one unit is holding is not offered to another.
  const available = inventory.data.filter((item) => item.equippable && item.equippedOn === null);

  return (
    <ShellScreen title="Units" lead="Prepare a fighter before the battle.">
      <div className="grid gap-8 lg:grid-cols-[15rem_minmax(0,1fr)] lg:gap-12">
        <UnitRoster units={units.data} selectedId={selected.id} onSelect={select} />

        {/* Named as a landmark because it is the half of the screen that changes when a unit
            is chosen, and the roster beside it stays put. */}
        <section
          aria-label="Selected unit"
          className="flex min-w-0 flex-col gap-6 lg:border-l-2 lg:border-slate/60 lg:pl-12"
        >
          {change.error ? (
            <FormNotice tone="error">
              {change.error instanceof ApiProblem
                ? change.error.message
                : "That change could not be made."}
            </FormNotice>
          ) : null}

          <UnitLoadout
            unit={selected}
            available={available}
            busy={change.isPending}
            onEquip={(itemId, slot) =>
              change.mutate({ action: "equip", unitId: selected.id, itemId, slot })
            }
            onUnequip={(itemId) => change.mutate({ action: "unequip", unitId: selected.id, itemId })}
          />
        </section>
      </div>
    </ShellScreen>
  );
}
