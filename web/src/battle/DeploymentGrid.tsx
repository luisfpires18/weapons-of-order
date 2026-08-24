import type { Army, ArmyUnit } from "@/battle/api";
import type { Hex } from "@/battle/hex";
import { boardAspectRatio, hexClipPath, hexKey, hexLabel, hexRect, hexes, isPlayerHalf } from "@/battle/hex";
import { unitAt } from "@/battle/deployment";
import { abbreviate } from "@/battle/labels";

/**
 * The battlefield, as something a player can put a Unit on.
 *
 * Real buttons, not a canvas. Deploying is the one part of a battle a player actually does, and
 * doing it with ordinary controls is what gives it keyboard focus, a name a screen reader can
 * read, and a touch target the size of a thumb — none of which a drawn surface has for free.
 * PixiJS draws the battle; this is a form.
 *
 * The interaction is tap to choose, tap to place. Dragging is not the only way to do this and on a
 * phone it is the worst one, so there is no drag: selecting a Unit and then a hex works the same
 * with a mouse, a thumb and a keyboard.
 */

/**
 * How tall the board is allowed to get.
 *
 * A phone is the constraint on width and a desktop is the constraint on height: left to fill a
 * wide column the board grows taller than the viewport, and a battlefield you have to scroll to
 * see all of is not one you can plan on. The cap turns the extra width into hexes that are big
 * enough rather than hexes that are enormous.
 */
const BOARD_HEIGHT_CAP = "min(58vh, 30rem)";

/**
 * How much of its cell a hex actually fills.
 *
 * Hexes tile edge to edge, so a board of them in one colour is a solid shape rather than a grid.
 * Shrinking what is *drawn* pulls the board's dark ground through as a hairline between them.
 *
 * Only the drawing shrinks. The button keeps its full clipped size, so the gap costs the player
 * nothing to hit — the touch target stays as big as the cell.
 */
const HEX_FILL = 0.94;
export type DeploymentGridProps = {
  army: Army;
  selectedId: string | null;
  busy: boolean;
  onSelect: (unitId: string | null) => void;
  onPlace: (unitId: string, hex: Hex) => void;
};

export function DeploymentGrid({ army, selectedId, busy, onSelect, onPlace }: DeploymentGridProps) {
  const field = army.battlefield;

  // Where each reserve would come in. It is a real consequence of queue order — a reserve enters
  // through its own hex or it waits — so the board says where before the battle rather than after.
  const entries = new Map(
    army.units
      .filter((unit) => unit.role === "reserve" && unit.reserveEntryHex !== null)
      .map((unit) => [hexKey(unit.reserveEntryHex!), unit]),
  );

  return (
    <div className="flex flex-col gap-3">
      {/* Full-bleed on a phone: the board is the most spatial thing on the screen, and reclaiming
          the screen's own padding is what keeps a hex at a size a thumb can hit at 320px. */}
      <div className="flex justify-center -mx-6 w-[calc(100%+3rem)] sm:mx-0 sm:w-full">
        <div
          role="group"
          aria-label={`Battlefield, ${field.columns} columns by ${field.rows} rows. Your half is the first ${field.deploymentColumns} columns.`}
          // Its own dark ground rather than the shell's. The atmosphere behind every authenticated
          // screen is an ember wash from the lower left, and a board sitting in it reads warm all
          // the way across — which is exactly the distinction the two halves are carrying.
          className="relative bg-void/75"
          style={{
            aspectRatio: boardAspectRatio(field),
            width: `min(100%, calc(${BOARD_HEIGHT_CAP} * ${boardAspectRatio(field)}))`,
          }}
        >
          {hexes(field).map((hex) => {
            const rect = hexRect(hex, field);
            const mine = isPlayerHalf(field, hex);
            const occupant = unitAt(army, hex);

            return mine ? (
              <PlayerHex
                key={hexKey(hex)}
                hex={hex}
                rect={rect}
                occupant={occupant}
                entryFor={entries.get(hexKey(hex))}
                selectedId={selectedId}
                busy={busy}
                onSelect={onSelect}
                onPlace={onPlace}
              />
            ) : (
              <span
                key={hexKey(hex)}
                aria-hidden
                className="absolute bg-ember/18"
                style={{ ...rect, clipPath: hexClipPath(), transform: `scale(${HEX_FILL})` }}
              />
            );
          })}
        </div>
      </div>

      <p className="font-hud text-hud font-semibold uppercase tracking-[0.16em] text-bone-dim">
        <span className="text-rune">Your half</span>
        <span aria-hidden className="px-2 text-slate">
          /
        </span>
        <span className="text-ember/70">Opposition</span>
      </p>
    </div>
  );
}

type PlayerHexProps = {
  hex: Hex;
  rect: { left: string; top: string; width: string; height: string };
  occupant: ArmyUnit | undefined;
  entryFor: ArmyUnit | undefined;
  selectedId: string | null;
  busy: boolean;
  onSelect: (unitId: string | null) => void;
  onPlace: (unitId: string, hex: Hex) => void;
};

/**
 * One hex the player may use.
 *
 * A selected Unit plus an empty hex is a placement. Anything else is a selection — clicking an
 * occupied hex picks up whoever is standing there rather than displacing them, so no tap can
 * quietly undo a decision the player has already made.
 */
function PlayerHex({
  hex,
  rect,
  occupant,
  entryFor,
  selectedId,
  busy,
  onSelect,
  onPlace,
}: PlayerHexProps) {
  const selected = occupant !== undefined && occupant.unitId === selectedId;
  const placing = selectedId !== null && occupant === undefined;

  const description = [
    hexLabel(hex),
    occupant ? occupant.name : "empty",
    entryFor ? `reserve entry for ${entryFor.name}` : null,
    placing ? "place the selected unit here" : null,
  ]
    .filter(Boolean)
    .join(", ");

  return (
    <button
      type="button"
      disabled={busy}
      aria-label={description}
      aria-pressed={selected}
      onClick={() => {
        if (placing && selectedId !== null) {
          onPlace(selectedId, hex);
        } else {
          onSelect(occupant?.unitId ?? null);
        }
      }}
      style={{ ...rect, clipPath: hexClipPath(), touchAction: "manipulation" }}
      className={[
        "group absolute flex cursor-pointer items-center justify-center bg-transparent",
        "disabled:cursor-not-allowed",
        "focus-visible:outline-2 focus-visible:-outline-offset-4 focus-visible:outline-ember-bright",
        occupant ? (selected ? "text-void" : "text-bone") : "",
      ].join(" ")}
    >
      {/* What is drawn, a little smaller than what is clickable.

          Ember is the interaction accent, not a second ground colour: lighting every free hex in
          it the moment a unit is chosen would wash the player's whole half warm and lose the one
          thing the two halves are saying. A placeable hex brightens in its own colour, and ember
          is kept for the hex under the pointer and the unit that is selected. */}
      <span
        aria-hidden
        className={[
          "absolute inset-0 transition-colors motion-reduce:transition-none",
          occupant
            ? selected
              ? "bg-ember/70"
              : "bg-rune/40 group-hover:bg-rune/55"
            : placing
              ? "bg-rune/28 group-hover:bg-ember/50"
              : "bg-rune/15 group-hover:bg-rune/25",
        ].join(" ")}
        style={{ clipPath: hexClipPath(), transform: `scale(${HEX_FILL})` }}
      />

      {occupant ? (
        <span className="pointer-events-none relative select-none font-hud text-[0.6875rem] font-semibold uppercase leading-none tracking-[0.04em] sm:text-[0.8125rem]">
          {abbreviate(occupant.name)}
        </span>
      ) : entryFor ? (
        // A quiet mark rather than a label: the hex is not the reserve's yet, it is only where it
        // will try to arrive.
        <span aria-hidden className="pointer-events-none relative h-1.5 w-1.5 rounded-full bg-ember/80" />
      ) : null}
    </button>
  );
}
