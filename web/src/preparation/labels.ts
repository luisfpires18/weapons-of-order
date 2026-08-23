import type { Unit } from "@/preparation/api";

/**
 * What the two weapon slots are called.
 *
 * Hands, not "main" and "off". Canon is explicit that the second slot is a full weapon slot
 * with no off-hand penalty and no restriction on what goes in it, and calling it an off-hand
 * would import an RPG convention the game does not have.
 */
export const SLOT_LABELS: Record<number, string> = {
  1: "First hand",
  2: "Second hand",
};

export function slotLabel(slot: number): string {
  return SLOT_LABELS[slot] ?? `Slot ${slot}`;
}

/** Which hands a weapon fills, for a weapon that may fill one or both. */
export function describeSlots(slots: readonly number[]): string {
  return slots.length > 1 ? "Both hands" : slotLabel(slots[0] ?? 1);
}

export const ARMOR_LABELS: Record<Unit["maxArmor"], string> = {
  light: "Light",
  medium: "Medium",
  heavy: "Heavy",
};

/**
 * A unit's fixed tier as stars.
 *
 * Fixed classification tiers, not upgrade levels — nothing a player does moves this. It is
 * shown because the value is real content, unlike a level or a power score, which are not.
 */
export function tierStars(tier: number): string {
  return "★".repeat(Math.max(0, tier));
}

export function tierLabel(tier: number): string {
  return `${tier} star${tier === 1 ? "" : "s"}`;
}

/** A stored instant in the reader's own locale, or nothing at all if it cannot be read. */
export function formatTimestamp(value: string): string {
  const at = new Date(value);

  return Number.isNaN(at.getTime())
    ? ""
    : at.toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" });
}

/** The slot a weapon occupies on a unit, or nothing when the hand is empty. */
export function weaponInSlot(unit: Unit, slot: number): Unit["weapons"][number] | undefined {
  return unit.weapons.find((weapon) => weapon.slots.includes(slot));
}

/** The hands a unit still has free. */
export function freeSlots(unit: Unit): number[] {
  return Array.from({ length: unit.weaponSlots }, (_, index) => index + 1).filter(
    (slot) => weaponInSlot(unit, slot) === undefined,
  );
}
