import type { ReactNode } from "react";
import { Link } from "react-router";
import type { Army, ArmyUnit } from "@/battle/api";
import { abbreviate } from "@/battle/labels";
import { deployed, deploymentBlocker, reserveBlocker, reserves, unplaced } from "@/battle/deployment";
import { CRAFTSMANSHIP_LABELS, CRAFTSMANSHIP_TEXT } from "@/forge/craftsmanship";
import { INLINE_LINK_CLASSES } from "@/components/auth/FormControls";
import { SectionLabel } from "@/preparation/ScreenStates";
import { tierLabel, tierStars } from "@/preparation/labels";
import { UNITS_PATH } from "@/shell/destinations";

const ACTION =
  "min-h-11 cursor-pointer border-[length:var(--border-panel)] px-3 font-hud text-[0.75rem]" +
  " font-semibold uppercase tracking-[0.12em] transition-colors motion-reduce:transition-none" +
  " focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ember-bright" +
  " disabled:cursor-not-allowed disabled:border-slate/50 disabled:text-bone-dim/50" +
  " disabled:hover:bg-transparent disabled:hover:text-bone-dim/50";

const QUIET_ACTION = `${ACTION} border-slate bg-transparent text-bone-dim hover:border-bone-dim hover:text-bone`;

const EMBER_ACTION = `${ACTION} border-ember/70 bg-transparent text-ember-bright hover:bg-ember hover:text-void`;

export type DeploymentActions = {
  onSelect: (unitId: string | null) => void;
  onReserve: (unitId: string) => void;
  onRemove: (unitId: string) => void;
  onMoveReserve: (unitId: string, offset: number) => void;
};

/**
 * The Units the player owns and has not put anywhere.
 *
 * Selecting one and then tapping a hex is the whole of deploying. The count beside the heading is
 * the deployment limit, because how many may stand on the battlefield at once is a rule the player
 * is planning around rather than a number to discover by being refused.
 */
export function ArmyRoster({
  army,
  selectedId,
  busy,
  actions,
}: {
  army: Army;
  selectedId: string | null;
  busy: boolean;
  actions: DeploymentActions;
}) {
  const available = unplaced(army);
  const onField = deployed(army);

  return (
    <section className="flex flex-col gap-4">
      <div className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1">
        <SectionLabel>Roster</SectionLabel>
        <p className="font-hud text-hud uppercase tracking-[0.12em] text-bone-dim tabular-nums">
          {onField.length}/{army.limits.active} deployed
        </p>
      </div>

      {army.units.length === 0 ? (
        <p className="font-body text-body leading-relaxed text-bone-dim">
          You have no units to deploy.
        </p>
      ) : available.length === 0 ? (
        <p className="font-body text-body leading-relaxed text-bone-dim">
          Every unit you own is in the army. Choose one on the battlefield or in the reserve queue to
          move it.
        </p>
      ) : (
        <ul aria-label="Units not in the army" className="flex flex-wrap gap-2">
          {available.map((unit) => (
            <li key={unit.unitId}>
              <UnitChip
                unit={unit}
                selected={unit.unitId === selectedId}
                busy={busy}
                onSelect={() => actions.onSelect(unit.unitId === selectedId ? null : unit.unitId)}
              />
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

/**
 * The reserve queue, in order.
 *
 * The order is a pre-battle decision rather than a listing preference: it decides which reserve is
 * called first when a slot opens, and which rear hex each one tries to enter through. So it is
 * editable, with two buttons rather than a drag — the same reasoning as the grid.
 */
export function ReserveQueue({
  army,
  selectedId,
  busy,
  actions,
}: {
  army: Army;
  selectedId: string | null;
  busy: boolean;
  actions: DeploymentActions;
}) {
  const queue = reserves(army);

  return (
    <section className="flex flex-col gap-4">
      <div className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1">
        <SectionLabel>Reserves</SectionLabel>
        <p className="font-hud text-hud uppercase tracking-[0.12em] text-bone-dim tabular-nums">
          {queue.length}/{army.limits.reserve} waiting
        </p>
      </div>

      {queue.length === 0 ? (
        <p className="font-body text-body leading-relaxed text-bone-dim">
          Nobody is waiting. A reserve enters through its own hex on your rear column when a slot
          opens — and waits, alive, if that hex is taken.
        </p>
      ) : (
        <ol aria-label="Reserve queue" className="flex flex-col border-t border-slate/60">
          {queue.map((unit, position) => (
            <li
              key={unit.unitId}
              className="flex flex-wrap items-center gap-x-3 gap-y-2 border-b border-slate/60 py-2"
            >
              <span
                aria-hidden
                className="font-hud text-[0.75rem] font-semibold text-ember/80 tabular-nums"
              >
                {position + 1}
              </span>

              <UnitChip
                unit={unit}
                selected={unit.unitId === selectedId}
                busy={busy}
                onSelect={() => actions.onSelect(unit.unitId === selectedId ? null : unit.unitId)}
              />

              <span className="ml-auto flex gap-2">
                <button
                  type="button"
                  disabled={busy || position === 0}
                  aria-label={`Move ${unit.name} earlier in the reserve queue`}
                  onClick={() => actions.onMoveReserve(unit.unitId, -1)}
                  className={QUIET_ACTION}
                >
                  Up
                </button>
                <button
                  type="button"
                  disabled={busy || position === queue.length - 1}
                  aria-label={`Move ${unit.name} later in the reserve queue`}
                  onClick={() => actions.onMoveReserve(unit.unitId, 1)}
                  className={QUIET_ACTION}
                >
                  Down
                </button>
              </span>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}

/**
 * What the selected Unit is, and where it can go from here.
 *
 * The stats are the server's totals for this Unit with what it is currently holding — the same
 * numbers the battle is fought with, not an estimate the browser assembled.
 */
export function SelectedUnit({
  army,
  unit,
  busy,
  actions,
}: {
  army: Army;
  unit: ArmyUnit | undefined;
  busy: boolean;
  actions: DeploymentActions;
}) {
  if (unit === undefined) {
    return (
      <section aria-label="Selected unit" className="flex flex-col gap-3">
        <SectionLabel>Selected</SectionLabel>
        <p className="font-body text-body leading-relaxed text-bone-dim">
          Choose a unit, then a hex in your half to put it there. Choosing a unit already on the
          battlefield and then another hex moves it.
        </p>
      </section>
    );
  }

  const cannotReserve = reserveBlocker(army, unit.unitId);
  const cannotDeploy = deploymentBlocker(army, unit.unitId);

  return (
    <section aria-label="Selected unit" className="flex flex-col gap-4">
      <SectionLabel>Selected</SectionLabel>

      <div className="flex flex-col gap-3 border-[length:var(--border-panel)] border-slate bg-ink-raised/40 p-4">
        <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
          <h3 className="font-display text-[1.0625rem] font-semibold uppercase tracking-[0.08em] text-bone">
            {unit.name}
          </h3>
          <span aria-label={tierLabel(unit.tier)} className="text-ember/80">
            {tierStars(unit.tier)}
          </span>
          {unit.mounted ? (
            <span className="font-hud text-hud uppercase tracking-[0.12em] text-rune">Mounted</span>
          ) : null}
        </div>

        <dl className="grid grid-cols-2 gap-x-4 gap-y-2 font-hud text-[0.75rem] uppercase tracking-[0.1em] sm:grid-cols-3">
          <Stat label="HP">{unit.stats.hp}</Stat>
          <Stat label="Power">{unit.stats.power}</Stat>
          <Stat label="Defense">{unit.stats.defense}</Stat>
          <Stat label="Interval">{unit.stats.attackIntervalSeconds.toFixed(2)}s</Stat>
          <Stat label="Crit">{Math.round(unit.stats.criticalChance * 100)}%</Stat>
          <Stat label="Range">{unit.stats.range}</Stat>
        </dl>

        {unit.weapons.length === 0 ? (
          <p className="font-body text-body leading-relaxed text-bone-dim">
            Empty-handed.{" "}
            <Link to={UNITS_PATH} className={INLINE_LINK_CLASSES}>
              Put a weapon in its hands
            </Link>{" "}
            and these numbers change.
          </p>
        ) : (
          <ul aria-label="Weapons" className="flex flex-col gap-1">
            {unit.weapons.map((weapon) => (
              <li key={weapon.itemId} className="font-body text-body text-bone">
                <span className={`font-semibold ${CRAFTSMANSHIP_TEXT[weapon.craftsmanship]}`}>
                  {CRAFTSMANSHIP_LABELS[weapon.craftsmanship]}
                </span>{" "}
                {weapon.name}
              </li>
            ))}
          </ul>
        )}

        <div className="flex flex-wrap gap-2">
          <button
            type="button"
            disabled={busy || unit.role === "reserve" || cannotReserve !== null}
            title={cannotReserve ?? undefined}
            onClick={() => actions.onReserve(unit.unitId)}
            className={EMBER_ACTION}
          >
            To reserve
          </button>

          <button
            type="button"
            disabled={busy || unit.role === "unplaced"}
            onClick={() => actions.onRemove(unit.unitId)}
            className={QUIET_ACTION}
          >
            Out of army
          </button>
        </div>

        {cannotDeploy !== null && unit.role !== "active" ? (
          <p role="status" className="font-body text-body text-ember-bright">
            {cannotDeploy}
          </p>
        ) : null}
      </div>
    </section>
  );
}

function Stat({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-1">
      <dt className="text-bone-dim/80">{label}</dt>
      <dd className="text-bone tabular-nums">{children}</dd>
    </div>
  );
}

/**
 * A Unit as a small selectable token, the same shape wherever it appears.
 *
 * `aria-pressed` rather than a link or a radio: choosing a Unit is not navigation and not a form
 * value, it is a mode the rest of the screen is now in.
 */
function UnitChip({
  unit,
  selected,
  busy,
  onSelect,
}: {
  unit: ArmyUnit;
  selected: boolean;
  busy: boolean;
  onSelect: () => void;
}) {
  return (
    <button
      type="button"
      disabled={busy}
      aria-pressed={selected}
      onClick={onSelect}
      className={[
        "flex min-h-11 cursor-pointer items-center gap-2 border-[length:var(--border-panel)] px-3",
        "font-hud text-[0.8125rem] font-semibold uppercase tracking-[0.08em] transition-colors",
        "motion-reduce:transition-none disabled:cursor-not-allowed",
        "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-selected",
        selected
          ? "border-ember bg-ember/25 text-bone"
          : "border-slate bg-transparent text-bone-dim hover:border-bone-dim hover:text-bone",
      ].join(" ")}
    >
      <span aria-hidden className={selected ? "text-ember-bright" : "text-rune/80"}>
        {abbreviate(unit.name)}
      </span>
      <span>{unit.name}</span>
    </button>
  );
}
