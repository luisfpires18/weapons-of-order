import { useState } from "react";
import { ApiProblem } from "@/api/problem";
import type { Army, ArmyIntent } from "@/battle/api";
import { BattlePlayback } from "@/battle/BattlePlayback";
import { DeploymentGrid } from "@/battle/DeploymentGrid";
import { ArmyRoster, ReserveQueue, SelectedUnit } from "@/battle/DeploymentPanels";
import type { DeploymentActions } from "@/battle/DeploymentPanels";
import { empty, intentOf, moveReserve, place, reserve, unplace } from "@/battle/deployment";
import { useArmy, useSaveArmy, useSimulate } from "@/battle/useBattle";
import { FormNotice } from "@/components/auth/FormControls";
import { AnvilAction } from "@/forge/ForgeActions";
import { ScreenError, ScreenPending } from "@/preparation/ScreenStates";
import { ShellScreen } from "@/shell/ShellScreen";

/**
 * Preparing an army and watching it fight.
 *
 * One screen in two states, because they are one decision and its consequence: the deployment is
 * what the player controls, and the battle is what the server does with it. Splitting them across
 * two routes would put a page load between a choice and its outcome.
 *
 * Every edit is saved as it is made rather than gathered behind a Save button. The army is
 * server-side state — it is what a battle is fought from, and it is still there after a reload —
 * so leaving the screen mid-deployment should not quietly discard it.
 */
export function BattleScreen() {
  const army = useArmy();
  const save = useSaveArmy();
  const battle = useSimulate();

  const [selectedId, setSelectedId] = useState<string | null>(null);

  if (army.isPending) {
    return <ScreenPending title="Battle">Mustering your army</ScreenPending>;
  }

  if (army.isError) {
    return (
      <ScreenError
        title="Battle"
        error={army.error}
        fallback="Your army could not be read."
        onRetry={() => void army.refetch()}
      />
    );
  }

  if (battle.data) {
    return (
      <ShellScreen title="Battle">
        <BattlePlayback result={battle.data} onLeave={() => battle.reset()} />
      </ShellScreen>
    );
  }

  return (
    <Deployment
      army={army.data}
      selectedId={selectedId}
      onSelect={setSelectedId}
      busy={save.isPending || battle.isPending}
      error={save.error ?? battle.error}
      onSave={(intent) => save.mutate(intent)}
      onFight={() => battle.mutate()}
      fighting={battle.isPending}
    />
  );
}

function Deployment({
  army,
  selectedId,
  onSelect,
  busy,
  error,
  onSave,
  onFight,
  fighting,
}: {
  army: Army;
  selectedId: string | null;
  onSelect: (unitId: string | null) => void;
  busy: boolean;
  error: Error | null;
  onSave: (intent: ArmyIntent) => void;
  onFight: () => void;
  fighting: boolean;
}) {
  const selected = army.units.find((unit) => unit.unitId === selectedId);

  const actions: DeploymentActions = {
    onSelect,
    onReserve: (unitId) => onSave(reserve(intentOf(army), unitId)),
    onRemove: (unitId) => onSave(unplace(intentOf(army), unitId)),
    onMoveReserve: (unitId, offset) => onSave(moveReserve(intentOf(army), unitId, offset)),
  };

  return (
    <ShellScreen
      title="Battle"
      lead="Put your units on the battlefield, decide who waits behind them, and let it play out. Once it begins, nothing you do changes it."
    >
      <div className="flex flex-col gap-8">
        {error ? (
          <FormNotice tone="error">
            {error instanceof ApiProblem ? error.message : "That change could not be made."}
          </FormNotice>
        ) : null}

        <div className="grid gap-8 lg:grid-cols-[minmax(0,1.55fr)_minmax(0,1fr)] lg:gap-12">
          <DeploymentGrid
            army={army}
            selectedId={selectedId}
            busy={busy}
            onSelect={onSelect}
            onPlace={(unitId, hex) => {
              onSave(place(intentOf(army), unitId, hex));
              onSelect(unitId);
            }}
          />

          <div className="flex min-w-0 flex-col gap-8">
            <SelectedUnit army={army} unit={selected} busy={busy} actions={actions} />
            <ArmyRoster army={army} selectedId={selectedId} busy={busy} actions={actions} />
            <ReserveQueue army={army} selectedId={selectedId} busy={busy} actions={actions} />
          </div>
        </div>

        <div className="flex flex-wrap items-center gap-x-6 gap-y-3">
          <AnvilAction
            pending={fighting}
            pendingLabel="Resolving"
            disabled={!army.ready || busy}
            onClick={onFight}
          >
            Begin battle
          </AnvilAction>

          <button
            type="button"
            disabled={busy || army.units.every((unit) => unit.role === "unplaced")}
            onClick={() => {
              onSave(empty());
              onSelect(null);
            }}
            className="cursor-pointer font-hud text-hud font-semibold uppercase tracking-[0.1em] text-bone-dim transition-colors hover:text-selected focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-selected disabled:cursor-not-allowed disabled:text-bone-dim/40 disabled:hover:text-bone-dim/40 motion-reduce:transition-none"
          >
            Clear deployment
          </button>

          {!army.ready ? (
            <p className="font-body text-body text-bone-dim">
              Put at least one unit on the battlefield first.
            </p>
          ) : null}
        </div>
      </div>
    </ShellScreen>
  );
}
