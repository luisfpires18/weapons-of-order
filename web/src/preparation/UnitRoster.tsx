import type { Unit } from "@/preparation/api";
import { tierLabel, tierStars } from "@/preparation/labels";

/**
 * The player's units, as one list in two shapes.
 *
 * A column beside the workspace from `lg`, and a row of selectors above it below that — the
 * same element and the same list either way, in the spirit of the shell's own navigation. The
 * row wraps rather than scrolls, so a longer roster or a longer authored name grows downward
 * instead of pushing the page sideways.
 *
 * Each entry is a real button. Selecting a unit is not navigation, so it is not a link, and
 * `aria-pressed` is what tells a screen reader which one is currently being prepared.
 */
export function UnitRoster({
  units,
  selectedId,
  onSelect,
}: {
  units: readonly Unit[];
  selectedId: string;
  onSelect: (unitId: string) => void;
}) {
  return (
    <section className="flex min-w-0 flex-col gap-4">
      <h2 className="font-hud text-hud font-semibold uppercase tracking-[0.16em] text-bone-dim">Roster</h2>

      <ul aria-label="Your units" className="flex flex-wrap gap-2 lg:flex-col lg:gap-0">
        {units.map((unit) => (
          <li key={unit.id} className="min-w-0 flex-1 lg:flex-none">
            <RosterEntry
              unit={unit}
              selected={unit.id === selectedId}
              onSelect={() => onSelect(unit.id)}
            />
          </li>
        ))}
      </ul>
    </section>
  );
}

function RosterEntry({
  unit,
  selected,
  onSelect,
}: {
  unit: Unit;
  selected: boolean;
  onSelect: () => void;
}) {
  const held = unit.weapons.reduce((total, weapon) => total + weapon.slots.length, 0);

  return (
    <button
      type="button"
      aria-pressed={selected}
      onClick={onSelect}
      className={[
        "group relative flex min-h-16 w-full cursor-pointer flex-col justify-center gap-1 px-2 py-2 text-left",
        "border-b-2 border-slate/60 transition-colors motion-reduce:transition-none",
        "focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-selected",
        "lg:min-h-[4.5rem] lg:border-b lg:px-4",
        selected ? "bg-ink-raised/70" : "hover:bg-ink-raised/40",
      ].join(" ")}
    >
      {/* The lit edge of the boundary, the same device the shell's navigation uses for the
          current destination: underneath on the wrapped row, down the side in the column. */}
      <span
        aria-hidden
        className="absolute -bottom-[2px] left-0 h-[2px] w-full transition-colors motion-reduce:transition-none lg:-right-[1px] lg:bottom-0 lg:left-auto lg:h-full lg:w-[2px]"
        style={
          selected
            ? {
                backgroundColor: "var(--color-ember)",
                boxShadow: "0 0 10px color-mix(in srgb, var(--color-ember) 55%, transparent)",
              }
            : undefined
        }
      />

      <span
        // Set smaller and tighter on a phone, where three selectors share the width and a
        // name the length of the longest current one would otherwise be clipped.
        className={`truncate font-display text-[0.8125rem] font-semibold uppercase tracking-[0.04em] lg:text-[1.0625rem] lg:tracking-[0.1em] ${
          selected ? "text-bone" : "text-bone-dim group-hover:text-bone"
        }`}
      >
        {unit.name}
      </span>

      <span className="flex items-baseline gap-2 font-hud text-[0.6875rem] uppercase tracking-[0.1em] text-bone-dim">
        <span aria-label={tierLabel(unit.tier)} className="text-ember/80">
          {tierStars(unit.tier)}
        </span>
        {unit.mounted ? <span className="text-rune">Mounted</span> : null}
        <span className="ml-auto hidden tabular-nums lg:inline">
          {held}/{unit.weaponSlots}
        </span>
      </span>
    </button>
  );
}
