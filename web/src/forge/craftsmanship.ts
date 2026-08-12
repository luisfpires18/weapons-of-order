import type { Craftsmanship } from "@/forge/api";

/**
 * The three qualities ordinary blacksmithing can produce. There is no fourth: canon is
 * explicit that Legendary is not a craftsmanship tier, and that this describes how well the
 * object was made rather than any magic in it.
 */
export const CRAFTSMANSHIP_LABELS: Record<Craftsmanship, string> = {
  common: "Common",
  rare: "Rare",
  epic: "Epic",
};

/**
 * Craftsmanship in colour: ember for the best work an ordinary forge can do, the cool rune
 * accent for the middle, plain bone for the rest. Global interface colours, not a faction's.
 */
export const CRAFTSMANSHIP_TEXT: Record<Craftsmanship, string> = {
  common: "text-bone-dim",
  rare: "text-rune",
  epic: "text-ember-bright",
};
