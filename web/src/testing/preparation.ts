import type { InventoryItem, Unit } from "@/preparation/api";
import { PREPARATION_URLS } from "@/preparation/api";

/** The three starter definitions in `server/content/units.json`, as the API resolves them. */
export const STARTER_UNITS: readonly Omit<Unit, "weapons">[] = [
  {
    id: "unit-melee",
    definitionKey: "arkazia.melee",
    name: "Melee",
    type: "regular",
    kingdom: "Arkazia",
    tier: 1,
    maxArmor: "heavy",
    mounted: false,
    weaponSlots: 2,
  },
  {
    id: "unit-ranged",
    definitionKey: "arkazia.ranged",
    name: "Ranged",
    type: "regular",
    kingdom: "Arkazia",
    tier: 1,
    maxArmor: "heavy",
    mounted: false,
    weaponSlots: 2,
  },
  {
    id: "unit-mounted",
    definitionKey: "arkazia.mounted",
    name: "Mounted",
    type: "regular",
    kingdom: "Arkazia",
    tier: 1,
    maxArmor: "heavy",
    mounted: true,
    weaponSlots: 2,
  },
];

type OwnedItem = Omit<InventoryItem, "equippedOn">;

export function sword(overrides: Partial<OwnedItem> = {}): OwnedItem {
  return {
    id: "item-sword-1",
    name: "Sword",
    weaponType: "Sword",
    craftsmanship: "epic",
    origin: "ordinaryforge",
    forgedAt: "2026-08-12T09:00:00+00:00",
    slotCost: 1,
    equippable: true,
    ...overrides,
  };
}

type Placement = { itemId: string; unitId: string; slots: number[] };

/**
 * A stand-in for the inventory and Units API, small enough to read and faithful enough to
 * drive the screens.
 *
 * It exercises the parts that live in the browser: which control is offered when a hand is
 * full, that equipping moves an item out of the available list, that the inventory reports
 * where a weapon went. It is not the authority on any of those rules — the real ones are
 * proven against PostgreSQL in the API tests and against a running stack in the browser sweep.
 * What it does hold to is the shape of the contract, including refusing what the server
 * refuses, so a screen cannot pass here by assuming the server is permissive.
 */
export function fakePreparation(
  options: { units?: readonly Omit<Unit, "weapons">[]; items?: readonly OwnedItem[] } = {},
) {
  const roster = options.units ?? STARTER_UNITS;
  let owned: OwnedItem[] = [...(options.items ?? [])];
  let placements: Placement[] = [];
  const calls: { url: string; body: unknown }[] = [];

  const itemsById = () => new Map(owned.map((item) => [item.id, item]));

  const unitPayload = (unit: Omit<Unit, "weapons">): Unit => {
    const byId = itemsById();

    return {
      ...unit,
      weapons: placements
        .filter((placement) => placement.unitId === unit.id)
        .sort((left, right) => (left.slots[0] ?? 0) - (right.slots[0] ?? 0))
        .flatMap((placement) => {
          const item = byId.get(placement.itemId);
          if (!item) return [];

          return [
            {
              itemId: item.id,
              name: item.name,
              weaponType: item.weaponType,
              craftsmanship: item.craftsmanship,
              slots: placement.slots,
            },
          ];
        }),
    };
  };

  const inventoryPayload = (): InventoryItem[] =>
    owned.map((item) => {
      const placement = placements.find((held) => held.itemId === item.id);
      const holder = placement && roster.find((unit) => unit.id === placement.unitId);

      return {
        ...item,
        equippedOn:
          placement && holder
            ? { unitId: holder.id, unitName: holder.name, slots: placement.slots }
            : null,
      };
    });

  const equip = (unitId: string, itemId: string, slot?: number): Response => {
    const unit = roster.find((candidate) => candidate.id === unitId);
    const item = owned.find((candidate) => candidate.id === itemId);

    if (!unit) return problem(404, "unit_not_found", "That unit is not one of yours.");
    if (!item) return problem(404, "item_not_found", "That item is not one of yours.");
    if (placements.some((held) => held.itemId === itemId)) {
      return problem(409, "item_already_equipped", "That weapon is already in use.");
    }

    const held = placements.filter((placement) => placement.unitId === unitId);
    const taken = new Set(held.flatMap((placement) => placement.slots));
    const slots =
      item.slotCost === 2 ? [1, 2] : [slot ?? (taken.has(1) ? 2 : 1)];

    if (slots.some((occupied) => taken.has(occupied))) {
      return problem(409, "unit_slot_occupied", "That hand is full.");
    }

    placements = [...placements, { itemId, unitId, slots }];

    return json(unitPayload(unit));
  };

  const unequip = (unitId: string, itemId: string): Response => {
    const unit = roster.find((candidate) => candidate.id === unitId);
    if (!unit) return problem(404, "unit_not_found", "That unit is not one of yours.");

    if (!placements.some((held) => held.itemId === itemId && held.unitId === unitId)) {
      return problem(409, "item_not_equipped", "That weapon is not in this unit's hands.");
    }

    placements = placements.filter((held) => held.itemId !== itemId);

    return json(unitPayload(unit));
  };

  const handle = (url: string, init?: RequestInit): Response | undefined => {
    if (url.endsWith(PREPARATION_URLS.inventory)) return json(inventoryPayload());
    if (url.endsWith(PREPARATION_URLS.units)) return json(roster.map(unitPayload));

    const action = /\/api\/units\/([^/]+)\/(equip|unequip)$/.exec(url);
    if (!action) return undefined;

    const body = typeof init?.body === "string" ? (JSON.parse(init.body) as Record<string, unknown>) : {};
    calls.push({ url, body });

    const unitId = action[1]!;
    const itemId = String(body.itemId ?? "");

    return action[2] === "equip"
      ? equip(unitId, itemId, typeof body.slot === "number" ? body.slot : undefined)
      : unequip(unitId, itemId);
  };

  return {
    handle,
    calls,
    add(item: OwnedItem) {
      owned = [item, ...owned];
    },
    get placements() {
      return placements;
    },
  };
}

function json(body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { "Content-Type": "application/json" },
  });
}

function problem(status: number, code: string, detail: string): Response {
  return new Response(JSON.stringify({ title: detail, detail, status, code }), {
    status,
    headers: { "Content-Type": "application/problem+json" },
  });
}
