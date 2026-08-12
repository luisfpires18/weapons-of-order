import type { ReactNode } from "react";
import type { ForgeRecipe, Materials } from "@/forge/api";
import { CRAFTSMANSHIP_LABELS, CRAFTSMANSHIP_TEXT } from "@/forge/craftsmanship";
import { useForgedItems } from "@/forge/useForge";

function RailSection({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="flex flex-col gap-4">
      <h2 className="font-hud text-hud font-semibold uppercase tracking-[0.16em] text-bone-dim">
        {title}
      </h2>
      {children}
    </section>
  );
}

/**
 * What the player has to work with.
 *
 * Three lines, because the material vocabulary is three materials. It is not a resource
 * dashboard and there is nothing to click: the numbers exist so the cost below them means
 * something.
 */
export function MaterialStock({ materials }: { materials: Materials }) {
  const rows: readonly [string, number][] = [
    ["Metal", materials.metal],
    ["Wood", materials.wood],
    ["Leather", materials.leather],
  ];

  return (
    <RailSection title="Materials">
      <dl className="flex flex-col">
        {rows.map(([label, amount]) => (
          <div
            key={label}
            className="flex items-baseline justify-between gap-4 border-b border-slate/60 py-2 last:border-b-0"
          >
            <dt className="font-body text-body text-bone-dim">{label}</dt>
            <dd className="font-hud text-[1.125rem] font-semibold tabular-nums text-bone">{amount}</dd>
          </div>
        ))}
      </dl>
    </RailSection>
  );
}

export function RecipeCost({ recipe }: { recipe: ForgeRecipe }) {
  const parts: string[] = [];

  if (recipe.cost.metal > 0) parts.push(`${recipe.cost.metal} Metal`);
  if (recipe.cost.wood > 0) parts.push(`${recipe.cost.wood} Wood`);
  if (recipe.cost.leather > 0) parts.push(`${recipe.cost.leather} Leather`);

  return (
    <RailSection title="Recipe">
      <div className="flex flex-col gap-1">
        <p className="font-display text-[1.125rem] font-semibold uppercase tracking-[0.1em] text-bone">
          {recipe.name}
        </p>
        <p className="font-body text-body text-bone-dim">
          {parts.length > 0 ? parts.join(" · ") : "No materials"}
        </p>
      </div>
    </RailSection>
  );
}

/**
 * The smallest honest proof that a forged sword is real and kept.
 *
 * It lists this account's own work and nothing else — no filtering, no categories, no item
 * detail, no equipping. Those belong to the inventory system, and a half-built version of it
 * here would be something Task 5 has to undo rather than build on.
 */
export function RecentWork() {
  const { data: items, isPending, isError } = useForgedItems();

  return (
    <RailSection title="Recent work">
      {isPending ? (
        <p className="font-body text-body text-bone-dim">Reading your work…</p>
      ) : isError ? (
        <p className="font-body text-body text-bone-dim">Your work could not be read just now.</p>
      ) : items.length === 0 ? (
        <p className="font-body text-body leading-relaxed text-bone-dim">
          Nothing forged yet. The first sword you finish appears here.
        </p>
      ) : (
        <ol aria-label="Forged items" className="flex flex-col">
          {items.map((item) => (
            <li
              key={item.id}
              className="flex items-baseline justify-between gap-4 border-b border-slate/60 py-3 last:border-b-0"
            >
              <span className="flex min-w-0 flex-col gap-1">
                <span className="font-body text-[0.9375rem] text-bone">
                  <span className={`font-semibold ${CRAFTSMANSHIP_TEXT[item.craftsmanship]}`}>
                    {CRAFTSMANSHIP_LABELS[item.craftsmanship]}
                  </span>{" "}
                  {item.name}
                </span>
                <time
                  dateTime={item.forgedAt}
                  className="font-hud text-[0.625rem] uppercase tracking-[0.1em] text-bone-dim/80"
                >
                  {formatForgedAt(item.forgedAt)}
                </time>
              </span>
            </li>
          ))}
        </ol>
      )}
    </RailSection>
  );
}

function formatForgedAt(value: string): string {
  const at = new Date(value);

  return Number.isNaN(at.getTime())
    ? ""
    : at.toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" });
}
