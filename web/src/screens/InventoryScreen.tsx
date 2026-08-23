import { Link } from "react-router";
import { INLINE_LINK_CLASSES } from "@/components/auth/FormControls";
import { CRAFTSMANSHIP_LABELS, CRAFTSMANSHIP_TEXT } from "@/forge/craftsmanship";
import type { InventoryItem } from "@/preparation/api";
import { describeSlots, formatTimestamp } from "@/preparation/labels";
import { ScreenError, ScreenPending } from "@/preparation/ScreenStates";
import { useInventory } from "@/preparation/usePreparation";
import { FORGE_PATH, UNITS_PATH } from "@/shell/destinations";
import { ShellScreen } from "@/shell/ShellScreen";

/**
 * What the player owns.
 *
 * A rack, not a spreadsheet. Every item is one line of real information — how well it was
 * made, what it is, where it came from, and whether it is currently in somebody's hands — and
 * there is nothing else, because nothing else exists. No sale value, no item level, no gear
 * score, no durability, no rarity apart from craftsmanship. Inventing a column to fill the
 * width would be inventing a system.
 *
 * There is no search, no sorting and no filtering either. With one forgeable weapon they would
 * be controls that do nothing; they belong to the screen that has enough in it to need them.
 */
export function InventoryScreen() {
  const { data: items, isPending, isError, error, refetch } = useInventory();

  if (isPending) {
    return <ScreenPending title="Inventory">Opening your pack</ScreenPending>;
  }

  if (isError) {
    return (
      <ScreenError
        title="Inventory"
        error={error}
        fallback="Your inventory could not be read."
        onRetry={() => void refetch()}
      />
    );
  }

  if (items.length === 0) {
    return (
      <ShellScreen title="Inventory" lead="Everything you own, and where it is.">
        <p className="max-w-[36rem] font-body text-[1rem] leading-relaxed text-bone-dim">
          You own nothing yet. Work at the{" "}
          <Link to={FORGE_PATH} className={INLINE_LINK_CLASSES}>
            forge
          </Link>{" "}
          and the first weapon you finish is kept here.
        </p>
      </ShellScreen>
    );
  }

  const inHand = items.filter((item) => item.equippedOn !== null).length;

  return (
    <ShellScreen title="Inventory" lead="Everything you own, and where it is.">
      <div className="flex flex-col gap-8">
        <p className="font-hud text-hud font-semibold uppercase tracking-[0.16em] text-bone-dim">
          {items.length} {items.length === 1 ? "item" : "items"}
          <span aria-hidden className="mx-2 text-slate">
            /
          </span>
          {inHand} in hand
        </p>

        <ul aria-label="Owned items" className="flex flex-col border-t-2 border-slate/70">
          {items.map((item) => (
            <ItemRow key={item.id} item={item} />
          ))}
        </ul>

        <p className="max-w-[40rem] font-body text-body leading-relaxed text-bone-dim">
          Weapons are put into a unit&rsquo;s hands on the{" "}
          <Link to={UNITS_PATH} className={INLINE_LINK_CLASSES}>
            units
          </Link>{" "}
          screen.
        </p>
      </div>
    </ShellScreen>
  );
}

/**
 * One owned object.
 *
 * Two columns from `sm` and one below it. The state sits at the end of the row rather than in
 * a badge, and it is the only part that changes as the player prepares, so it is what the eye
 * should be able to run down.
 */
function ItemRow({ item }: { item: InventoryItem }) {
  return (
    <li className="flex flex-col gap-2 border-b border-slate/60 py-4 sm:flex-row sm:items-baseline sm:justify-between sm:gap-8">
      <span className="flex min-w-0 flex-col gap-1">
        <span className="font-display text-[1.0625rem] uppercase tracking-[0.08em] text-bone">
          <span className={`font-semibold ${CRAFTSMANSHIP_TEXT[item.craftsmanship]}`}>
            {CRAFTSMANSHIP_LABELS[item.craftsmanship]}
          </span>{" "}
          {item.name}
        </span>

        <span className="font-body text-body text-bone-dim">
          {item.weaponType}
          {item.slotCost === 2 ? " · two-handed" : ""}
          <span aria-hidden className="mx-2 text-slate">
            ·
          </span>
          {originLabel(item.origin)}
          <span aria-hidden className="mx-2 text-slate">
            ·
          </span>
          <time dateTime={item.forgedAt}>{formatTimestamp(item.forgedAt)}</time>
        </span>
      </span>

      <span className="shrink-0 font-hud text-[0.8125rem] uppercase tracking-[0.12em] sm:text-right">
        {item.equippedOn ? (
          <span className="text-ember-bright">
            {item.equippedOn.unitName}
            <span aria-hidden className="mx-2 text-slate">
              ·
            </span>
            <span className="text-bone-dim">{describeSlots(item.equippedOn.slots)}</span>
          </span>
        ) : (
          <span className="text-bone-dim">{item.equippable ? "In your pack" : "Cannot be held yet"}</span>
        )}
      </span>
    </li>
  );
}

/**
 * How an item came to exist. Only ordinary forging can make one, so only ordinary forging has
 * a name; anything else is shown as the server sent it rather than guessed at.
 */
function originLabel(origin: string): string {
  return origin === "ordinaryforge" ? "Ordinary forge" : origin;
}
