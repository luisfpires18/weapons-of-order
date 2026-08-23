import type { ReactNode } from "react";
import { Link } from "react-router";
import { INLINE_LINK_CLASSES } from "@/components/auth/FormControls";
import { CRAFTSMANSHIP_LABELS, CRAFTSMANSHIP_TEXT } from "@/forge/craftsmanship";
import type { InventoryItem, Unit, UnitWeapon } from "@/preparation/api";
import { ARMOR_LABELS, freeSlots, slotLabel, tierLabel, tierStars, weaponInSlot } from "@/preparation/labels";
import { SectionLabel } from "@/preparation/ScreenStates";
import { FORGE_PATH } from "@/shell/destinations";

const SLOT_ACTION =
  "min-h-11 cursor-pointer border-[length:var(--border-panel)] px-3 font-hud text-[0.75rem]" +
  " font-semibold uppercase tracking-[0.12em] transition-colors motion-reduce:transition-none" +
  " focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ember-bright" +
  " disabled:cursor-not-allowed disabled:border-slate/50 disabled:text-bone-dim/50" +
  " disabled:hover:bg-transparent disabled:hover:text-bone-dim/50";

/**
 * Preparing one unit.
 *
 * Identity at the top, then the two hands, then what is available to put in them. The order is
 * the order the decision is made in, and it holds on a phone because the panels stack rather
 * than being squeezed side by side.
 *
 * There is deliberately no class, specialisation, level, experience or power rating anywhere
 * on this screen. Canon derives the current class from unit and loadout, the creator has not
 * authored the names, and a placeholder would be the interface asserting something untrue.
 */
export function UnitLoadout({
  unit,
  available,
  busy,
  onEquip,
  onUnequip,
}: {
  unit: Unit;
  available: readonly InventoryItem[];
  busy: boolean;
  onEquip: (itemId: string, slot?: number) => void;
  onUnequip: (itemId: string) => void;
}) {
  const twoHanded = unit.weapons.find((weapon) => weapon.slots.length > 1);
  const free = freeSlots(unit);

  return (
    <div className="flex min-w-0 flex-col gap-10">
      <Identity unit={unit} />

      <section className="flex flex-col gap-4">
        <SectionLabel>Weapons</SectionLabel>

        {twoHanded ? (
          <SlotPanel label="Both hands" weapon={twoHanded} busy={busy} onUnequip={onUnequip} />
        ) : (
          <div className="grid gap-3 sm:grid-cols-2">
            {Array.from({ length: unit.weaponSlots }, (_, index) => index + 1).map((slot) => (
              <SlotPanel
                key={slot}
                label={slotLabel(slot)}
                weapon={weaponInSlot(unit, slot)}
                busy={busy}
                onUnequip={onUnequip}
              />
            ))}
          </div>
        )}

        {/* Armour is a real part of a unit and its limit is published above, but no armour
            has been made yet. Saying so is better than six empty slots implying otherwise. */}
        <p className="font-body text-body leading-relaxed text-bone-dim">
          Armour cannot be made yet, so there is none to fit.
        </p>
      </section>

      <section className="flex flex-col gap-4">
        <SectionLabel>Your weapons</SectionLabel>

        {available.length === 0 ? (
          <p className="max-w-[36rem] font-body text-body leading-relaxed text-bone-dim">
            Nothing is free to give this unit. Take a weapon out of another unit&rsquo;s hands, or
            make one at the{" "}
            <Link to={FORGE_PATH} className={INLINE_LINK_CLASSES}>
              forge
            </Link>
            .
          </p>
        ) : (
          <ul aria-label="Weapons you can equip" className="flex flex-col border-t border-slate/60">
            {available.map((item) => (
              <AvailableWeapon
                key={item.id}
                item={item}
                unit={unit}
                free={free}
                busy={busy}
                onEquip={onEquip}
              />
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}

/**
 * Who this unit is.
 *
 * Kingdom, fixed tier, armour limit and Mounted — the four things the creator's content
 * actually says. Mounted gets the cool accent because it is the one field here that changes
 * how the unit behaves in a battle.
 */
function Identity({ unit }: { unit: Unit }) {
  return (
    <div className="flex flex-col gap-3">
      <h2 className="font-display text-[1.75rem] font-semibold uppercase leading-tight tracking-[0.1em] text-bone lg:text-[2rem]">
        {unit.name}
      </h2>

      <dl className="flex flex-wrap items-baseline gap-x-6 gap-y-2 font-hud text-[0.8125rem] uppercase tracking-[0.12em]">
        <Fact label="Kingdom">{unit.kingdom}</Fact>
        <Fact label="Tier">
          <span aria-label={tierLabel(unit.tier)} className="text-ember">
            {tierStars(unit.tier)}
          </span>
        </Fact>
        <Fact label="Armour limit">{ARMOR_LABELS[unit.maxArmor]}</Fact>
        <Fact label="Mounted">
          <span className={unit.mounted ? "text-rune" : "text-bone-dim"}>
            {unit.mounted ? "Yes" : "No"}
          </span>
        </Fact>
      </dl>
    </div>
  );
}

function Fact({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex items-baseline gap-2">
      <dt className="text-bone-dim/80">{label}</dt>
      <dd className="text-bone">{children}</dd>
    </div>
  );
}

/**
 * One hand — or both, when a two-slot weapon has the whole loadout.
 *
 * Framed with a hairline rather than a card, and empty is a state the panel shows plainly
 * instead of hiding. The panel is what the player reads to know whether there is room.
 */
function SlotPanel({
  label,
  weapon,
  busy,
  onUnequip,
}: {
  label: string;
  weapon: UnitWeapon | undefined;
  busy: boolean;
  onUnequip: (itemId: string) => void;
}) {
  return (
    <div
      className={`flex min-h-[6.5rem] flex-col gap-3 border-[length:var(--border-panel)] p-4 ${
        weapon ? "border-slate bg-ink-raised/40" : "border-dashed border-slate/70"
      }`}
    >
      <p className="font-hud text-hud font-semibold uppercase tracking-[0.16em] text-bone-dim">{label}</p>

      {weapon ? (
        <div className="flex flex-wrap items-baseline justify-between gap-3">
          <p className="min-w-0 font-display text-[1.0625rem] uppercase tracking-[0.08em] text-bone">
            <span className={`font-semibold ${CRAFTSMANSHIP_TEXT[weapon.craftsmanship]}`}>
              {CRAFTSMANSHIP_LABELS[weapon.craftsmanship]}
            </span>{" "}
            {weapon.name}
          </p>

          <button
            type="button"
            disabled={busy}
            onClick={() => onUnequip(weapon.itemId)}
            aria-label={`Unequip ${CRAFTSMANSHIP_LABELS[weapon.craftsmanship]} ${weapon.name} from ${label.toLowerCase()}`}
            className={`${SLOT_ACTION} border-slate bg-transparent text-bone-dim hover:border-bone-dim hover:text-bone`}
          >
            Unequip
          </button>
        </div>
      ) : (
        <p className="font-body text-body text-bone-dim/80">Empty</p>
      )}
    </div>
  );
}

/**
 * A weapon the player owns and is not using.
 *
 * One button per hand, so choosing where it goes is a single press and a full hand is visibly
 * unavailable rather than an error after the fact. A two-slot weapon offers one button
 * instead, because it does not go in a hand — it takes the loadout.
 */
function AvailableWeapon({
  item,
  unit,
  free,
  busy,
  onEquip,
}: {
  item: InventoryItem;
  unit: Unit;
  free: readonly number[];
  busy: boolean;
  onEquip: (itemId: string, slot?: number) => void;
}) {
  const name = `${CRAFTSMANSHIP_LABELS[item.craftsmanship]} ${item.name}`;
  const twoHanded = item.slotCost === 2;
  const loadoutEmpty = free.length === unit.weaponSlots;

  return (
    <li className="flex flex-col gap-3 border-b border-slate/60 py-3 sm:flex-row sm:items-center sm:justify-between sm:gap-6">
      <span className="flex min-w-0 flex-col gap-1">
        <span className="font-display text-[1rem] uppercase tracking-[0.08em] text-bone">
          <span className={`font-semibold ${CRAFTSMANSHIP_TEXT[item.craftsmanship]}`}>
            {CRAFTSMANSHIP_LABELS[item.craftsmanship]}
          </span>{" "}
          {item.name}
        </span>
        <span className="font-body text-body text-bone-dim">
          {item.weaponType}
          {twoHanded ? " · takes both hands" : ""}
        </span>
      </span>

      <span className="flex shrink-0 flex-wrap gap-2">
        {twoHanded ? (
          <EquipButton
            disabled={busy || !loadoutEmpty}
            label="Equip"
            description={`Equip ${name} to ${unit.name}, filling both hands`}
            onClick={() => onEquip(item.id)}
          />
        ) : (
          Array.from({ length: unit.weaponSlots }, (_, index) => index + 1).map((slot) => (
            <EquipButton
              key={slot}
              disabled={busy || !free.includes(slot)}
              label={slotLabel(slot)}
              description={`Equip ${name} to ${unit.name}, ${slotLabel(slot).toLowerCase()}`}
              onClick={() => onEquip(item.id, slot)}
            />
          ))
        )}
      </span>
    </li>
  );
}

function EquipButton({
  disabled,
  label,
  description,
  onClick,
}: {
  disabled: boolean;
  label: string;
  description: string;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      disabled={disabled}
      onClick={onClick}
      aria-label={description}
      className={`${SLOT_ACTION} border-ember/70 bg-transparent text-ember-bright hover:bg-ember hover:text-void`}
    >
      {label}
    </button>
  );
}
